using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PmxEditorMcp.Bridge
{
    /// <summary>メッセージを1件読み取った結果の区分。</summary>
    public enum BridgeMessageOutcome
    {
        /// <summary>1件読み取れた。</summary>
        Message,

        /// <summary>ホストが切断した。</summary>
        EndOfStream,

        /// <summary>
        /// 本文が上限を超えた。読み取りは打ち切っており、超過した本文の残りは読み進めていない。
        /// このまま次を読むと残りの途中から別のメッセージとして解釈するため、接続を捨てる。
        /// </summary>
        TooLarge,

        /// <summary>本文がUTF-8として解釈できないバイト列を含む。</summary>
        InvalidEncoding,
    }

    /// <summary>メッセージを1件読み取った結果。</summary>
    public sealed class BridgeMessageRead
    {
        internal BridgeMessageRead(BridgeMessageOutcome outcome, string message)
        {
            Outcome = outcome;
            Message = message;
        }

        /// <summary>読み取りの区分。</summary>
        public BridgeMessageOutcome Outcome { get; }

        /// <summary>読み取れた本文。<see cref="Outcome"/> が Message のときだけ意味を持つ。</summary>
        public string Message { get; }
    }

    /// <summary>本文が上限のバイト数を超えるため書き出せないことを表す。</summary>
    public sealed class MessageTooLargeException : Exception
    {
        /// <summary>超過した本文のバイト数と上限を示して生成する。</summary>
        public MessageTooLargeException(int messageBytes, int maxMessageBytes)
            : base("本文が " + messageBytes.ToString(CultureInfo.InvariantCulture)
                + " バイトで、上限の " + maxMessageBytes.ToString(CultureInfo.InvariantCulture)
                + " バイトを超えている。")
        {
            MessageBytes = messageBytes;
            MaxMessageBytes = maxMessageBytes;
        }

        /// <summary>書き出そうとした本文のバイト数。</summary>
        public int MessageBytes { get; }

        /// <summary>本文に許すバイト数の上限。</summary>
        public int MaxMessageBytes { get; }
    }

    /// <summary>
    /// ホストとのあいだでメッセージを1行として読み書きする入出力。本文はBOMなしUTF-8で、
    /// 出力の区切りはLF、入力はLFとCRLFの両方を受理する。読み取りは上限付きで行い、上限を
    /// 超えた時点で全文を保持せずに打ち切る。
    /// </summary>
    public sealed class BridgeMessageChannel
    {
        /// <summary>1メッセージの本文(区切りを含まない)に許すUTF-8バイト数の上限。</summary>
        public const int DefaultMaxMessageBytes = 16 * 1024 * 1024;

        private const byte LineFeed = 10;
        private const byte CarriageReturn = 13;
        private const int ReadBufferBytes = 65536;

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly Stream _stream;
        private readonly byte[] _readBuffer = new byte[ReadBufferBytes];

        private int _readOffset;
        private int _readLength;

        /// <summary>上限を既定にして生成する。</summary>
        public BridgeMessageChannel(Stream stream)
            : this(stream, DefaultMaxMessageBytes)
        {
        }

        /// <summary>上限を指定して生成する。</summary>
        public BridgeMessageChannel(Stream stream, int maxMessageBytes)
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

        /// <summary>1メッセージの本文に許すUTF-8バイト数の上限。</summary>
        public int MaxMessageBytes { get; }

        /// <summary>本文のUTF-8バイト数を数える。</summary>
        public static int MeasureBytes(string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return Utf8WithoutBom.GetByteCount(message);
        }

        /// <summary>
        /// メッセージを1件書き出し、区切りのLFを付す。上限を超える要求は、呼び出し側が
        /// <see cref="MeasureBytes"/> と <see cref="MaxMessageBytes"/> で送信前に判定して
        /// 送らない。上限を超えたまま渡されたときは、判定漏れを知らせるため何も書き出さずに
        /// <see cref="MessageTooLargeException"/> を投げる。
        /// </summary>
        public async Task WriteAsync(string message, CancellationToken cancellationToken)
        {
            int messageBytes = MeasureBytes(message);
            if (messageBytes > MaxMessageBytes)
            {
                throw new MessageTooLargeException(messageBytes, MaxMessageBytes);
            }

            byte[] payload = new byte[messageBytes + 1];
            Utf8WithoutBom.GetBytes(message, 0, message.Length, payload, 0);
            payload[messageBytes] = LineFeed;

            await _stream.WriteAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>メッセージを1件読み取る。</summary>
        public async Task<BridgeMessageRead> ReadAsync(CancellationToken cancellationToken)
        {
            using MemoryStream body = new MemoryStream();

            while (true)
            {
                if (_readOffset >= _readLength)
                {
                    _readLength = await _stream
                        .ReadAsync(_readBuffer.AsMemory(), cancellationToken)
                        .ConfigureAwait(false);
                    _readOffset = 0;
                    if (_readLength <= 0)
                    {
                        // 区切りの来ないまま切断された。読みかけの本文は捨てる。
                        return new BridgeMessageRead(BridgeMessageOutcome.EndOfStream, null);
                    }
                }

                int newlineIndex = IndexOfLineFeed();
                int available = (newlineIndex >= 0 ? newlineIndex : _readLength) - _readOffset;

                // 上限を超えた時点で打ち切り、全文を保持しない。区切りがCRLFのときはCRの1バイトを
                // 本文から外すため、その1バイトぶんだけ余分に受け入れてから最終判定する。
                if (body.Length + available > (long)MaxMessageBytes + 1)
                {
                    return new BridgeMessageRead(BridgeMessageOutcome.TooLarge, null);
                }

                body.Write(_readBuffer, _readOffset, available);
                _readOffset += available;

                if (newlineIndex < 0 && body.Length > MaxMessageBytes
                    && body.GetBuffer()[(int)body.Length - 1] != CarriageReturn)
                {
                    // 余分な1バイトを保留してよいのは、それがCRLFのCRで、続くLFで本文から
                    // 外れる見込みがあるときだけ。そうでなければこの時点で上限を超えている。
                    return new BridgeMessageRead(BridgeMessageOutcome.TooLarge, null);
                }

                if (newlineIndex >= 0)
                {
                    _readOffset++;

                    // 上限いっぱいの本文を複製しないよう、内部の配列を長さ付きでそのまま渡す。
                    return Decode(body.GetBuffer(), (int)body.Length);
                }
            }
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

        private BridgeMessageRead Decode(byte[] body, int bodyLength)
        {
            int length = bodyLength;
            if (length > 0 && body[length - 1] == CarriageReturn)
            {
                length--;
            }

            if (length > MaxMessageBytes)
            {
                return new BridgeMessageRead(BridgeMessageOutcome.TooLarge, null);
            }

            try
            {
                return new BridgeMessageRead(
                    BridgeMessageOutcome.Message, StrictUtf8.GetString(body, 0, length));
            }
            catch (DecoderFallbackException)
            {
                return new BridgeMessageRead(BridgeMessageOutcome.InvalidEncoding, null);
            }
        }
    }
}
