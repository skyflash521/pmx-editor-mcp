using System;
using System.Globalization;
using System.Text;

namespace PmxEditorMcp
{
    public static class MessageLimit
    {
        public const int DefaultMaxMessageBytes = 16 * 1024 * 1024;

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

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

    public sealed class MessageTooLargeException : Exception
    {
        public MessageTooLargeException(int messageBytes, int maxMessageBytes)
            : base("本文が " + messageBytes.ToString(CultureInfo.InvariantCulture)
                + " バイトで、上限の " + maxMessageBytes.ToString(CultureInfo.InvariantCulture)
                + " バイトを超えている。")
        {
            MessageBytes = messageBytes;
            MaxMessageBytes = maxMessageBytes;
        }

        public int MessageBytes { get; }

        public int MaxMessageBytes { get; }
    }
}
