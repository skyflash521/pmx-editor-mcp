using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PmxEditorMcp
{
    /// <summary>メッセージを1件読み取った結果。</summary>
    public enum MessageReadOutcome
    {
        /// <summary>1件読み取れた。</summary>
        Message,

        /// <summary>相手が切断した。</summary>
        EndOfStream,

        /// <summary>
        /// 本文が上限を超えた。読み取りは打ち切られており、超過した本文の残りは読み進めていない。
        /// このまま次を読むと残りの途中から別のメッセージとして解釈するため、受けた側は切断する。
        /// </summary>
        TooLarge,

        /// <summary>
        /// 本文がUTF-8として解釈できないバイト列を含む。区切りまでは読み進めているが、
        /// 送り手の符号化が壊れている以上そのあとの本文も信用できないため、受けた側は切断する。
        /// </summary>
        InvalidEncoding,
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
    /// メッセージを1行として読み書きする入出力。本文はBOMなしUTF-8で、出力の区切りはLF、
    /// 入力はLFとCRLFの両方を受理する。読み取りは生バイトを上限付きで行い、上限を超えた時点で
    /// 全文を保持せずに打ち切る。
    /// </summary>
    public sealed class MessageChannel
    {
        /// <summary>1メッセージの本文(区切りを含まない)に許すUTF-8バイト数の上限。</summary>
        public const int DefaultMaxMessageBytes = 16 * 1024 * 1024;

        private const byte LineFeed = (byte)'\n';
        private const byte CarriageReturn = (byte)'\r';
        private const int ReadBufferBytes = 65536;

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly Stream _stream;
        private readonly byte[] _readBuffer = new byte[ReadBufferBytes];

        private int _readOffset;
        private int _readLength;

        /// <summary>上限を既定にして生成する。</summary>
        public MessageChannel(Stream stream)
            : this(stream, DefaultMaxMessageBytes)
        {
        }

        /// <summary>上限を指定して生成する。</summary>
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

        /// <summary>メッセージを1件読み取る。</summary>
        public MessageReadOutcome Read(out string message)
        {
            message = null;

            using (MemoryStream body = new MemoryStream())
            {
                while (true)
                {
                    if (_readOffset >= _readLength)
                    {
                        _readLength = _stream.Read(_readBuffer, 0, _readBuffer.Length);
                        _readOffset = 0;
                        if (_readLength <= 0)
                        {
                            return MessageReadOutcome.EndOfStream;
                        }
                    }

                    int newlineIndex = IndexOfLineFeed();
                    int available = (newlineIndex >= 0 ? newlineIndex : _readLength) - _readOffset;

                    // 上限を超えた時点で打ち切り、全文を保持しない。区切りがCRLFのときはCRの1バイトを
                    // 本文から外すため、その1バイトぶんだけ余分に受け入れてから最終判定する。
                    if (body.Length + available > (long)MaxMessageBytes + 1)
                    {
                        return MessageReadOutcome.TooLarge;
                    }

                    body.Write(_readBuffer, _readOffset, available);
                    _readOffset += available;

                    if (newlineIndex < 0 && body.Length > MaxMessageBytes
                        && body.GetBuffer()[body.Length - 1] != CarriageReturn)
                    {
                        // 余分な1バイトを保留してよいのは、それがCRLFのCRで、続くLFで本文から
                        // 外れる見込みがあるときだけ。そうでなければこの時点で上限を超えている。
                        return MessageReadOutcome.TooLarge;
                    }

                    if (newlineIndex >= 0)
                    {
                        _readOffset++;

                        // 上限いっぱいの本文を複製しないよう、内部の配列を長さ付きでそのまま渡す。
                        return Decode(body.GetBuffer(), (int)body.Length, out message);
                    }
                }
            }
        }

        /// <summary>
        /// メッセージを1件書き出し、区切りのLFを付す。上限を超える応答は、呼び出し側が
        /// <see cref="MeasureBytes"/> と <see cref="MaxMessageBytes"/> で書き出す前に判定し、
        /// 別の応答へ差し替える。本文が上限を超えたまま渡されたときは、判定漏れを知らせるため
        /// 何も書き出さずに <see cref="MessageTooLargeException"/> を投げる。
        /// </summary>
        public void Write(string message)
        {
            int messageBytes = MeasureBytes(message);
            if (messageBytes > MaxMessageBytes)
            {
                throw new MessageTooLargeException(messageBytes, MaxMessageBytes);
            }

            byte[] payload = new byte[messageBytes + 1];
            Utf8WithoutBom.GetBytes(message, 0, message.Length, payload, 0);
            payload[messageBytes] = LineFeed;

            _stream.Write(payload, 0, payload.Length);
            _stream.Flush();
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
