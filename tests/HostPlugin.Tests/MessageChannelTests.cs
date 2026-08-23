using System;
using System.IO;
using System.Text;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class MessageChannelTests
    {
        private const string Pending = "impl pending: メッセージを1行として読み書きし、上限付きの生バイト読み取りと厳格なUTF-8で扱う";

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private static MessageChannel FromBytes(byte[] input)
        {
            return new MessageChannel(new MemoryStream(input));
        }

        private static MessageChannel FromText(string input)
        {
            return FromBytes(Utf8WithoutBom.GetBytes(input));
        }

        [Fact(Skip = Pending)]
        public void LFで区切られた本文を読み取る()
        {
            MessageChannel channel = FromText("いち\n");

            string message;
            Assert.Equal(MessageReadOutcome.Message, channel.Read(out message));
            Assert.Equal("いち", message);
        }

        [Fact(Skip = Pending)]
        public void CRLFで区切られた本文も受理する()
        {
            MessageChannel channel = FromText("いち\r\n");

            string message;
            Assert.Equal(MessageReadOutcome.Message, channel.Read(out message));
            Assert.Equal("いち", message);
        }

        [Fact(Skip = Pending)]
        public void 複数の本文を順に読み取る()
        {
            MessageChannel channel = FromText("いち\nに\r\nさん\n");

            string first;
            string second;
            string third;
            Assert.Equal(MessageReadOutcome.Message, channel.Read(out first));
            Assert.Equal(MessageReadOutcome.Message, channel.Read(out second));
            Assert.Equal(MessageReadOutcome.Message, channel.Read(out third));
            Assert.Equal("いち", first);
            Assert.Equal("に", second);
            Assert.Equal("さん", third);
        }

        [Fact(Skip = Pending)]
        public void 空の本文を読み取れる()
        {
            MessageChannel channel = FromText("\n");

            string message;
            Assert.Equal(MessageReadOutcome.Message, channel.Read(out message));
            Assert.Equal(string.Empty, message);
        }

        [Fact(Skip = Pending)]
        public void 相手が切断したら終端を返す()
        {
            MessageChannel channel = FromText(string.Empty);

            string message;
            Assert.Equal(MessageReadOutcome.EndOfStream, channel.Read(out message));
            Assert.Null(message);
        }

        [Fact(Skip = Pending)]
        public void 区切りの無いまま終端に達したら終端を返す()
        {
            MessageChannel channel = FromText("区切りが来ない");

            string message;
            Assert.Equal(MessageReadOutcome.EndOfStream, channel.Read(out message));
        }

        [Fact(Skip = Pending)]
        public void 上限ちょうどの本文は受理する()
        {
            MessageChannel channel = new MessageChannel(new MemoryStream(Utf8WithoutBom.GetBytes(new string('a', 16) + "\n")), 16);

            string message;
            Assert.Equal(MessageReadOutcome.Message, channel.Read(out message));
            Assert.Equal(16, message.Length);
        }

        [Fact(Skip = Pending)]
        public void 上限ちょうどの本文にCRLFが続いても受理する()
        {
            MessageChannel channel = new MessageChannel(new MemoryStream(Utf8WithoutBom.GetBytes(new string('a', 16) + "\r\n")), 16);

            string message;
            Assert.Equal(MessageReadOutcome.Message, channel.Read(out message));
            Assert.Equal(16, message.Length);
        }

        [Fact(Skip = Pending)]
        public void 上限を超える本文は上限超過として返す()
        {
            MessageChannel channel = new MessageChannel(new MemoryStream(Utf8WithoutBom.GetBytes(new string('a', 17) + "\n")), 16);

            string message;
            Assert.Equal(MessageReadOutcome.TooLarge, channel.Read(out message));
            Assert.Null(message);
        }

        [Fact(Skip = Pending)]
        public void 区切りが来なくても上限を超えた時点で読み取りを打ち切る()
        {
            // 全文を読んでから長さを判定する作りでは、入力の全体が読まれてしまう。
            CountingStream source = new CountingStream(Utf8WithoutBom.GetBytes(new string('a', 1000000)));
            MessageChannel channel = new MessageChannel(source, 16);

            string message;
            Assert.Equal(MessageReadOutcome.TooLarge, channel.Read(out message));
            Assert.True(source.BytesRead < 100000, "読み取ったバイト数は " + source.BytesRead);
        }

        [Fact(Skip = Pending)]
        public void 上限は文字数でなくバイト数で測る()
        {
            // 「あ」はUTF-8で3バイトなので、6文字は18バイトで上限16を超える。
            MessageChannel channel = new MessageChannel(new MemoryStream(Utf8WithoutBom.GetBytes(new string('あ', 6) + "\n")), 16);

            string message;
            Assert.Equal(MessageReadOutcome.TooLarge, channel.Read(out message));
        }

        [Fact(Skip = Pending)]
        public void UTF8として解釈できないバイト列は不正な符号化として返す()
        {
            MessageChannel channel = FromBytes(new byte[] { 0x82, 0xA0, (byte)'\n' });

            string message;
            Assert.Equal(MessageReadOutcome.InvalidEncoding, channel.Read(out message));
            Assert.Null(message);
        }

        [Fact(Skip = Pending)]
        public void 書き出しはBOMなしUTF8でLFを付す()
        {
            MemoryStream stream = new MemoryStream();
            MessageChannel channel = new MessageChannel(stream);

            channel.Write("いち");
            channel.Write("に");

            byte[] written = stream.ToArray();
            Assert.Equal(Utf8WithoutBom.GetBytes("いち\nに\n"), written);
        }

        [Fact(Skip = Pending)]
        public void バイト数はUTF8で数える()
        {
            Assert.Equal(0, MessageChannel.MeasureBytes(string.Empty));
            Assert.Equal(3, MessageChannel.MeasureBytes("あ"));
            Assert.Equal(4, MessageChannel.MeasureBytes("abcd"));
        }

        [Fact(Skip = Pending)]
        public void 上限は生成時に指定した値を返す()
        {
            Assert.Equal(MessageChannel.DefaultMaxMessageBytes, new MessageChannel(new MemoryStream()).MaxMessageBytes);
            Assert.Equal(16, new MessageChannel(new MemoryStream(), 16).MaxMessageBytes);
        }

        [Fact(Skip = Pending)]
        public void 既定の上限は16MiBである()
        {
            Assert.Equal(16777216, MessageChannel.DefaultMaxMessageBytes);
        }

        [Fact(Skip = Pending)]
        public void 上限を超える本文は書き出さない()
        {
            MemoryStream stream = new MemoryStream();
            MessageChannel channel = new MessageChannel(stream, 16);

            MessageTooLargeException exception =
                Assert.Throws<MessageTooLargeException>(() => channel.Write(new string('a', 17)));

            Assert.Equal(17, exception.MessageBytes);
            Assert.Equal(16, exception.MaxMessageBytes);
            Assert.Empty(stream.ToArray());
        }

        [Fact(Skip = Pending)]
        public void 書き出しの上限も文字数でなくバイト数で測る()
        {
            MemoryStream stream = new MemoryStream();
            MessageChannel channel = new MessageChannel(stream, 16);

            // 「あ」はUTF-8で3バイトなので、6文字は18バイトで上限16を超える。
            MessageTooLargeException exception =
                Assert.Throws<MessageTooLargeException>(() => channel.Write(new string('あ', 6)));

            Assert.Equal(18, exception.MessageBytes);
            Assert.Empty(stream.ToArray());
        }

        [Fact(Skip = Pending)]
        public void 上限ちょうどの本文は書き出す()
        {
            MemoryStream stream = new MemoryStream();
            MessageChannel channel = new MessageChannel(stream, 16);

            channel.Write(new string('a', 16));

            Assert.Equal(17, stream.ToArray().Length);
        }

        /// <summary>読み取られたバイト数を数える読み取り専用のストリーム。</summary>
        private sealed class CountingStream : Stream
        {
            private readonly MemoryStream _inner;

            public CountingStream(byte[] content)
            {
                _inner = new MemoryStream(content);
            }

            public long BytesRead { get; private set; }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            // 名前付きパイプと同じく、長さと位置は扱えない。
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = _inner.Read(buffer, offset, count);
                BytesRead += read;
                return read;
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
