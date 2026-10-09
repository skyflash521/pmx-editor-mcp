using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace PmxEditorMcp
{
    public enum MessageReadOutcome
    {
        Message,

        EndOfStream,

        /// <summary>
        /// 本文が上限を超えた。読み取りは打ち切られており、超過した本文の残りは読み進めていない。
        /// 受けた側は切断する。
        /// </summary>
        TooLarge,

        /// <summary>本文がUTF-8として解釈できないバイト列を含む。区切りまでは読み進めている。受けた側は切断する。</summary>
        InvalidEncoding,
    }

    /// <summary>
    /// メッセージを1行として読み書きする入出力。本文はBOMなしUTF-8で、出力の区切りはLF、
    /// 入力はLFとCRLFの両方を受理する。読み取りは生バイトを上限付きで行い、上限を超えた時点で
    /// 全文を保持せずに打ち切る。
    /// </summary>
    public sealed class MessageChannel
    {
        public const int DefaultMaxMessageBytes = MessageLimit.DefaultMaxMessageBytes;

        private const byte LineFeed = (byte)'\n';
        private const byte CarriageReturn = (byte)'\r';
        private const int ReadBufferBytes = 65536;

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly Stream _stream;
        private readonly byte[] _readBuffer = new byte[ReadBufferBytes];

        private int _readOffset;
        private int _readLength;
        private Task<int> _pendingRead;

        public MessageChannel(Stream stream)
            : this(stream, DefaultMaxMessageBytes)
        {
        }

        public MessageChannel(Stream stream, int maxMessageBytes)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (maxMessageBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMessageBytes));
            }

            _stream = stream;
            MaxMessageBytes = maxMessageBytes;
        }

        public int MaxMessageBytes { get; }

        /// <summary>
        /// 応答を書き出す先の相手が切断したと分かっているときは真。切断は、読み取りが相手の切断を
        /// 受け取った時点で分かる。<see cref="ReadAhead"/> で読み取りを先に始めておくと、次の
        /// <see cref="Read"/> を呼ぶ前でも分かる。書き出す先が名前付きパイプのときだけ判定できる。
        /// 先に始めた読み取りが切断で0バイトを返しても、<see cref="PipeStream.IsConnected"/> は真のまま残る。
        /// </summary>
        public bool IsPeerGone
        {
            get
            {
                PipeStream pipe = _stream as PipeStream;
                if (pipe == null)
                {
                    return false;
                }

                if (!pipe.IsConnected)
                {
                    return true;
                }

                Task<int> pending = _pendingRead;

                return pending != null
                    && pending.IsCompleted
                    && (pending.Status != TaskStatus.RanToCompletion || pending.Result == 0);
            }
        }

        /// <summary>
        /// 次の <see cref="Read"/> が受け取る分の読み取りを始めて、戻りを待たずに返る。
        /// 未読の分が残っているとき・始めた読み取りが受け取られていないときは何もしない。
        /// </summary>
        public void ReadAhead()
        {
            if (_pendingRead == null && _readOffset >= _readLength)
            {
                _pendingRead = _stream.ReadAsync(_readBuffer, 0, _readBuffer.Length);
            }
        }

        public static int MeasureBytes(string message)
        {
            return MessageLimit.MeasureBytes(message);
        }

        public MessageReadOutcome Read(out string message)
        {
            message = null;

            using (MemoryStream body = new MemoryStream())
            {
                while (true)
                {
                    if (_readOffset >= _readLength)
                    {
                        _readLength = FillBuffer();
                        _readOffset = 0;
                        if (_readLength <= 0)
                        {
                            return MessageReadOutcome.EndOfStream;
                        }
                    }

                    int newlineIndex = IndexOfLineFeed();
                    int available = (newlineIndex >= 0 ? newlineIndex : _readLength) - _readOffset;

                    if (body.Length + available > (long)MaxMessageBytes + 1)
                    {
                        return MessageReadOutcome.TooLarge;
                    }

                    body.Write(_readBuffer, _readOffset, available);
                    _readOffset += available;

                    if (newlineIndex < 0 && body.Length > MaxMessageBytes
                        && body.GetBuffer()[body.Length - 1] != CarriageReturn)
                    {
                        return MessageReadOutcome.TooLarge;
                    }

                    if (newlineIndex >= 0)
                    {
                        _readOffset++;

                        return Decode(body.GetBuffer(), (int)body.Length, out message);
                    }
                }
            }
        }

        /// <summary>
        /// メッセージを1件書き出し、区切りのLFを付す。本文が上限を超えたまま渡されたときは、何も
        /// 書き出さずに <see cref="MessageTooLargeException"/> を投げる。
        /// </summary>
        public void Write(string message)
        {
            int messageBytes = MeasureBytes(message);
            MessageLimit.Require(messageBytes, MaxMessageBytes);

            byte[] payload = new byte[messageBytes + 1];
            Utf8WithoutBom.GetBytes(message, 0, message.Length, payload, 0);
            payload[messageBytes] = LineFeed;

            _stream.Write(payload, 0, payload.Length);
            _stream.Flush();
        }

        private int FillBuffer()
        {
            Task<int> pending = _pendingRead;
            if (pending == null)
            {
                return _stream.Read(_readBuffer, 0, _readBuffer.Length);
            }

            _pendingRead = null;

            return pending.GetAwaiter().GetResult();
        }

        private int IndexOfLineFeed()
        {
            for (int index = _readOffset; index < _readLength; index++)
            {
                if (_readBuffer[index] == LineFeed)
                {
                    return index;
                }
            }

            return -1;
        }

        private MessageReadOutcome Decode(byte[] body, int bodyLength, out string message)
        {
            message = null;

            int length = bodyLength;
            if (length > 0 && body[length - 1] == CarriageReturn)
            {
                length--;
            }

            if (length > MaxMessageBytes)
            {
                return MessageReadOutcome.TooLarge;
            }

            try
            {
                message = StrictUtf8.GetString(body, 0, length);
            }
            catch (DecoderFallbackException)
            {
                message = null;
                return MessageReadOutcome.InvalidEncoding;
            }

            return MessageReadOutcome.Message;
        }
    }
}
