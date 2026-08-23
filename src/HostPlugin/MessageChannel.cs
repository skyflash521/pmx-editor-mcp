using System;
using System.Globalization;
using System.IO;

namespace PmxEditorMcp
{
    /// <summary>メッセージを1件読み取った結果。</summary>
    public enum MessageReadOutcome
    {
        /// <summary>1件読み取れた。</summary>
        Message,

        /// <summary>相手が切断した。</summary>
        EndOfStream,

        /// <summary>本文が上限を超えた。読み取りは打ち切られている。</summary>
        TooLarge,

        /// <summary>本文がUTF-8として解釈できないバイト列を含む。</summary>
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

        /// <summary>上限を既定にして生成する。</summary>
        public MessageChannel(Stream stream)
            : this(stream, DefaultMaxMessageBytes)
        {
        }

        /// <summary>上限を指定して生成する。</summary>
        public MessageChannel(Stream stream, int maxMessageBytes)
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

        /// <summary>メッセージを1件読み取る。</summary>
        public MessageReadOutcome Read(out string message)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// メッセージを1件書き出し、区切りのLFを付す。上限を超える応答は、呼び出し側が
        /// <see cref="MeasureBytes"/> と <see cref="MaxMessageBytes"/> で書き出す前に判定し、
        /// 別の応答へ差し替える。本文が上限を超えたまま渡されたときは、判定漏れを知らせるため
        /// 何も書き出さずに <see cref="MessageTooLargeException"/> を投げる。
        /// </summary>
        public void Write(string message)
        {
            throw new NotImplementedException();
        }
    }
}
