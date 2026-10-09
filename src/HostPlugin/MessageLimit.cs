using System;
using System.Globalization;
using System.Text;

namespace PmxEditorMcp
{
    /// <summary>ホストとブリッジが1件のメッセージの本文に許すバイト数と、その数え方。</summary>
    public static class MessageLimit
    {
        /// <summary>1メッセージの本文(区切りを含まない)に許すUTF-8バイト数の既定の上限。</summary>
        public const int DefaultMaxMessageBytes = 16 * 1024 * 1024;

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        /// <summary>本文のUTF-8バイト数を数える。</summary>
        public static int MeasureBytes(string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return Utf8WithoutBom.GetByteCount(message);
        }

        /// <summary>本文のバイト数が上限を超えていれば <see cref="MessageTooLargeException"/> を投げる。</summary>
        public static void Require(int messageBytes, int maxMessageBytes)
        {
            if (messageBytes > maxMessageBytes)
            {
                throw new MessageTooLargeException(messageBytes, maxMessageBytes);
            }
        }
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
}
