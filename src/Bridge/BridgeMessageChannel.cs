using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PmxEditorMcp.Bridge
{
    public enum BridgeMessageOutcome
    {
        Message,

        EndOfStream,

        /// <summary>
        /// 本文が上限を超えた。読み取りは打ち切っており、超過した本文の残りは読み進めていない。
        /// 接続を捨てる。
        /// </summary>
        TooLarge,

        /// <summary>本文がUTF-8として解釈できないバイト列を含む。</summary>
        InvalidEncoding,
    }

    public sealed class BridgeMessageRead
    {
        internal BridgeMessageRead(BridgeMessageOutcome outcome, string message)
        {
            Outcome = outcome;
            Message = message;
        }

        public BridgeMessageOutcome Outcome { get; }

        /// <summary>読み取れた本文。<see cref="Outcome"/> が Message のときだけ意味を持つ。</summary>
        public string Message { get; }
    }

    /// <summary>
    /// ホストとのあいだでメッセージを1行として読み書きする入出力。本文はBOMなしUTF-8で、
    /// 出力の区切りはLF、入力はLFとCRLFの両方を受理する。読み取りは上限付きで行い、上限を
    /// 超えた時点で全文を保持せずに打ち切る。
    /// </summary>
    public sealed class BridgeMessageChannel
    {
        public const int DefaultMaxMessageBytes = MessageLimit.DefaultMaxMessageBytes;

        private const byte LineFeed = 10;
        private const byte CarriageReturn = 13;
        private const int ReadBufferBytes = 65536;

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly Stream _stream;
        private readonly byte[] _readBuffer = new byte[ReadBufferBytes];

        private int _readOffset;
        private int _readLength;

        public BridgeMessageChannel(Stream stream)
            : this(stream, DefaultMaxMessageBytes)
        {
        }

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

        public int MaxMessageBytes { get; }

        public static int MeasureBytes(string message)
        {
            return MessageLimit.MeasureBytes(message);
        }

        /// <summary>
        /// メッセージを1件書き出し、区切りのLFを付す。本文が上限を超えたまま渡されたときは、何も
        /// 書き出さずに <see cref="MessageTooLargeException"/> を投げる。
        /// </summary>
        public async Task WriteAsync(string message, CancellationToken cancellationToken)
        {
            int messageBytes = MeasureBytes(message);
            MessageLimit.Require(messageBytes, MaxMessageBytes);

            byte[] payload = new byte[messageBytes + 1];
            Utf8WithoutBom.GetBytes(message, 0, message.Length, payload, 0);
            payload[messageBytes] = LineFeed;

            await _stream.WriteAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

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
                        return new BridgeMessageRead(BridgeMessageOutcome.EndOfStream, null);
                    }
                }

                int newlineIndex = IndexOfLineFeed();
                int available = (newlineIndex >= 0 ? newlineIndex : _readLength) - _readOffset;

                if (body.Length + available > (long)MaxMessageBytes + 1)
                {
                    return new BridgeMessageRead(BridgeMessageOutcome.TooLarge, null);
                }

                body.Write(_readBuffer, _readOffset, available);
                _readOffset += available;

                if (newlineIndex < 0 && body.Length > MaxMessageBytes
                    && body.GetBuffer()[(int)body.Length - 1] != CarriageReturn)
                {
                    return new BridgeMessageRead(BridgeMessageOutcome.TooLarge, null);
                }

                if (newlineIndex >= 0)
                {
                    _readOffset++;

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
