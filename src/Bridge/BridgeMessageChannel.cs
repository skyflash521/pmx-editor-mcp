using System;
using System.Globalization;
using System.IO;
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
        private BridgeMessageRead(BridgeMessageOutcome outcome, string message)
        {
            throw new NotImplementedException();
        }

        /// <summary>読み取りの区分。</summary>
        public BridgeMessageOutcome Outcome => throw new NotImplementedException();

        /// <summary>読み取れた本文。<see cref="Outcome"/> が Message のときだけ意味を持つ。</summary>
        public string Message => throw new NotImplementedException();
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

        /// <summary>上限を既定にして生成する。</summary>
        public BridgeMessageChannel(Stream stream)
            : this(stream, DefaultMaxMessageBytes)
        {
        }

        /// <summary>上限を指定して生成する。</summary>
        public BridgeMessageChannel(Stream stream, int maxMessageBytes)
        {
            throw new NotImplementedException();
        }

        /// <summary>1メッセージの本文に許すUTF-8バイト数の上限。</summary>
        public int MaxMessageBytes => throw new NotImplementedException();

        /// <summary>本文のUTF-8バイト数を数える。</summary>
        public static int MeasureBytes(string message)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// メッセージを1件書き出し、区切りのLFを付す。上限を超える要求は、呼び出し側が
        /// <see cref="MeasureBytes"/> と <see cref="MaxMessageBytes"/> で送信前に判定して
        /// 送らない。上限を超えたまま渡されたときは、判定漏れを知らせるため何も書き出さずに
        /// <see cref="MessageTooLargeException"/> を投げる。
        /// </summary>
        public Task WriteAsync(string message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>メッセージを1件読み取る。</summary>
        public Task<BridgeMessageRead> ReadAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
