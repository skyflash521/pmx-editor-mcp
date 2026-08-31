using System;
using System.IO;
using System.Text;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class MessageChannelTests
    {
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private static MessageChannel FromBytes(byte[] input)
        {
            return new MessageChannel(new MemoryStream(input));
        }

        private static MessageChannel FromText(string input)
        {
            return FromBytes(Utf8WithoutBom.GetBytes(input));
        }

        [Fact]
        public void ReadsLfSeparatedBody()
        {
            MessageChannel channel = FromText("いち\n");

            string message;
            Assert.Equal(MessageReadOutcome.Message, channel.Read(out message));
            Assert.Equal("いち", message);
        }

        [Fact]
        public void AcceptsCrlfSeparatedBody()
        {
            MessageChannel channel = FromText("いち\r\n");

            string message;
            Assert.Equal(MessageReadOutcome.Message, channel.Read(out message));
            Assert.Equal("いち", message);
        }

        [Fact]
        public void ReadsMultipleBodiesInOrder()
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

        [Fact]
        public void ReadsEmptyBody()
        {
            MessageChannel channel = FromText("\n");

            string message;
            Assert.Equal(MessageReadOutcome.Message, channel.Read(out message));
            Assert.Equal(string.Empty, message);
        }

        [Fact]
        public void PeerCloseReturnsEndOfStream()
        {
            MessageChannel channel = FromText(string.Empty);

            string message;
            Assert.Equal(MessageReadOutcome.EndOfStream, channel.Read(out message));
            Assert.Null(message);
        }

        [Fact]
        public void EndOfStreamWithoutSeparatorReturnsEndOfStream()
        {
            MessageChannel channel = FromText("区切りが来ない");

            string message;
            Assert.Equal(MessageReadOutcome.EndOfStream, channel.Read(out message));
        }

        [Fact]
        public void BodyAtLimitIsAccepted()
        {
            MessageChannel channel = new MessageChannel(new MemoryStream(Utf8WithoutBom.GetBytes(new string('a', 16) + "\n")), 16);

            string message;
            Assert.Equal(MessageReadOutcome.Message, channel.Read(out message));
            Assert.Equal(16, message.Length);
        }

        [Fact]
        public void BodyAtLimitFollowedByCrlfIsAccepted()
        {
            MessageChannel channel = new MessageChannel(new MemoryStream(Utf8WithoutBom.GetBytes(new string('a', 16) + "\r\n")), 16);

            string message;
            Assert.Equal(MessageReadOutcome.Message, channel.Read(out message));
            Assert.Equal(16, message.Length);
        }

        [Fact]
        public void BodyOverLimitIsReturnedAsLimitExceeded()
        {
            MessageChannel channel = new MessageChannel(new MemoryStream(Utf8WithoutBom.GetBytes(new string('a', 17) + "\n")), 16);

            string message;
            Assert.Equal(MessageReadOutcome.TooLarge, channel.Read(out message));
            Assert.Null(message);
        }

        [Fact]
        public void BodyOneByteOverLimitExceedsWithoutWaitingForSeparator()
        {
            // 余分な1バイトを無条件に保留すると、区切りが来ないまま待ち続けてしまう。
            MessageChannel channel = new MessageChannel(
                new MemoryStream(Utf8WithoutBom.GetBytes(new string('a', 17))), 16);

            string message;
            Assert.Equal(MessageReadOutcome.TooLarge, channel.Read(out message));
            Assert.Null(message);
        }

        [Fact]
        public void BodyAtLimitIsAcceptedWhenCrAndLfArriveApart()
        {
            ChunkedStream source = new ChunkedStream(
                Utf8WithoutBom.GetBytes(new string('a', 16) + "\r"),
                Utf8WithoutBom.GetBytes("\n"));
            MessageChannel channel = new MessageChannel(source, 16);

            string message;
            Assert.Equal(MessageReadOutcome.Message, channel.Read(out message));
            Assert.Equal(new string('a', 16), message);
        }

        [Fact]
        public void ReadStopsOnceLimitIsExceededWithoutSeparator()
        {
            // 全文を読んでから長さを判定する作りでは、入力の全体が読まれてしまう。
            CountingStream source = new CountingStream(Utf8WithoutBom.GetBytes(new string('a', 1000000)));
            MessageChannel channel = new MessageChannel(source, 16);

            string message;
            Assert.Equal(MessageReadOutcome.TooLarge, channel.Read(out message));
            Assert.True(source.BytesRead < 100000, "読み取ったバイト数は " + source.BytesRead);
        }

        [Fact]
        public void ReadLimitIsMeasuredInBytesNotCharacters()
        {
            // 「あ」はUTF-8で3バイトなので、6文字は18バイトで上限16を超える。
            MessageChannel channel = new MessageChannel(new MemoryStream(Utf8WithoutBom.GetBytes(new string('あ', 6) + "\n")), 16);

            string message;
            Assert.Equal(MessageReadOutcome.TooLarge, channel.Read(out message));
        }

        [Fact]
        public void BytesThatAreNotValidUtf8AreReturnedAsEncodingError()
        {
            MessageChannel channel = FromBytes(new byte[] { 0x82, 0xA0, (byte)'\n' });

            string message;
            Assert.Equal(MessageReadOutcome.InvalidEncoding, channel.Read(out message));
            Assert.Null(message);
        }

        [Fact]
        public void WriteUsesUtf8WithoutBomAndAppendsLf()
        {
            MemoryStream stream = new MemoryStream();
            MessageChannel channel = new MessageChannel(stream);

            channel.Write("いち");
            channel.Write("に");

            byte[] written = stream.ToArray();
            Assert.Equal(Utf8WithoutBom.GetBytes("いち\nに\n"), written);
        }

        [Fact]
        public void ByteCountIsMeasuredInUtf8()
        {
            Assert.Equal(0, MessageChannel.MeasureBytes(string.Empty));
            Assert.Equal(3, MessageChannel.MeasureBytes("あ"));
            Assert.Equal(4, MessageChannel.MeasureBytes("abcd"));
        }

        [Fact]
        public void LimitReturnsTheValueGivenAtConstruction()
        {
            Assert.Equal(MessageChannel.DefaultMaxMessageBytes, new MessageChannel(new MemoryStream()).MaxMessageBytes);
            Assert.Equal(16, new MessageChannel(new MemoryStream(), 16).MaxMessageBytes);
        }

        [Fact]
        public void DefaultLimitIsSixteenMebibytes()
        {
            Assert.Equal(16777216, MessageChannel.DefaultMaxMessageBytes);
        }

        [Fact]
        public void BodyOverLimitIsNotWritten()
        {
            MemoryStream stream = new MemoryStream();
            MessageChannel channel = new MessageChannel(stream, 16);

            MessageTooLargeException exception =
                Assert.Throws<MessageTooLargeException>(() => channel.Write(new string('a', 17)));

            Assert.Equal(17, exception.MessageBytes);
            Assert.Equal(16, exception.MaxMessageBytes);
            Assert.Empty(stream.ToArray());
        }

        [Fact]
        public void WriteLimitIsMeasuredInBytesNotCharacters()
        {
            MemoryStream stream = new MemoryStream();
            MessageChannel channel = new MessageChannel(stream, 16);

            // 「あ」はUTF-8で3バイトなので、6文字は18バイトで上限16を超える。
            MessageTooLargeException exception =
                Assert.Throws<MessageTooLargeException>(() => channel.Write(new string('あ', 6)));

            Assert.Equal(18, exception.MessageBytes);
            Assert.Empty(stream.ToArray());
        }

        [Fact]
        public void BodyAtLimitIsWritten()
        {
            MemoryStream stream = new MemoryStream();
            MessageChannel channel = new MessageChannel(stream, 16);

            channel.Write(new string('a', 16));

            Assert.Equal(17, stream.ToArray().Length);
        }

        /// <summary>読み取りを与えた塊の単位で返す。名前付きパイプの分割読み取りを模す。</summary>
        private sealed class ChunkedStream : Stream
        {
            private readonly byte[][] _chunks;
            private int _index;

            public ChunkedStream(params byte[][] chunks)
            {
                _chunks = chunks;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_index >= _chunks.Length)
                {
                    return 0;
                }

                byte[] chunk = _chunks[_index];
                if (chunk.Length > count)
                {
                    throw new InvalidOperationException("塊が読み取り要求より大きい。");
                }

                Array.Copy(chunk, 0, buffer, offset, chunk.Length);
                _index++;
                return chunk.Length;
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
