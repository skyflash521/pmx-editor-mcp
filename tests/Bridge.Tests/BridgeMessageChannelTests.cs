using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public class BridgeMessageChannelTests
    {
        private const byte LineFeed = 10;

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        [Fact]
        public void BodySizeIsCountedInUtf8Bytes()
        {
            Assert.Equal(3, BridgeMessageChannel.MeasureBytes("あ"));
            Assert.Equal(0, BridgeMessageChannel.MeasureBytes(string.Empty));
        }

        [Fact]
        public void IoLimitMatchesContract()
        {
            Assert.Equal(16 * 1024 * 1024, BridgeMessageChannel.DefaultMaxMessageBytes);
        }

        [Fact]
        public async Task WrittenBodiesAreUtf8WithoutBomAndEndWithLf()
        {
            using MemoryStream stream = new MemoryStream();
            BridgeMessageChannel channel = new BridgeMessageChannel(stream);

            await channel.WriteAsync("あ", CancellationToken.None);
            await channel.WriteAsync("b", CancellationToken.None);

            Assert.Equal(
                new byte[] { 0xE3, 0x81, 0x82, LineFeed, 0x62, LineFeed },
                stream.ToArray());
        }

        [Fact]
        public async Task OversizedBodyIsReportedInsteadOfWritten()
        {
            using MemoryStream stream = new MemoryStream();
            BridgeMessageChannel channel = new BridgeMessageChannel(stream, 4);

            MessageTooLargeException error = await Assert.ThrowsAsync<MessageTooLargeException>(
                () => channel.WriteAsync("12345", CancellationToken.None));

            Assert.Equal(5, error.MessageBytes);
            Assert.Equal(4, error.MaxMessageBytes);
            Assert.Empty(stream.ToArray());
        }

        [Fact]
        public async Task BodyAtLimitIsWritten()
        {
            using MemoryStream stream = new MemoryStream();
            BridgeMessageChannel channel = new BridgeMessageChannel(stream, 4);

            await channel.WriteAsync("1234", CancellationToken.None);

            Assert.Equal(5, stream.ToArray().Length);
        }

        [Fact]
        public async Task WriteLimitIsMeasuredInBytesNotCharacters()
        {
            // 「あ」はUTF-8で3バイトなので、2文字は6バイトで上限4を超える。
            using MemoryStream stream = new MemoryStream();
            BridgeMessageChannel channel = new BridgeMessageChannel(stream, 4);

            await Assert.ThrowsAsync<MessageTooLargeException>(
                () => channel.WriteAsync("ああ", CancellationToken.None));

            Assert.Empty(stream.ToArray());
        }

        [Fact]
        public async Task ReadsLfSeparatedBodiesInOrder()
        {
            BridgeMessageChannel channel = ReadingChannel("first\nsecond\n");

            Assert.Equal("first", await ReadMessage(channel));
            Assert.Equal("second", await ReadMessage(channel));
        }

        [Fact]
        public async Task CrlfSeparatedBodyExcludesTrailingCr()
        {
            BridgeMessageChannel channel = ReadingChannel("first\r\nsecond\r\n");

            Assert.Equal("first", await ReadMessage(channel));
            Assert.Equal("second", await ReadMessage(channel));
        }

        [Fact]
        public async Task SeparatorCrAndLfArrivingApartAreNotMixedIntoBody()
        {
            // パイプは境界を保証しないので、CRが1回の読み取りの末尾に、LFが次の先頭に来うる。
            BridgeMessageChannel channel = new BridgeMessageChannel(
                new ChunkedStream(
                    Utf8WithoutBom.GetBytes("first\r"),
                    new byte[] { LineFeed },
                    Utf8WithoutBom.GetBytes("second\n")),
                BridgeMessageChannel.DefaultMaxMessageBytes);

            Assert.Equal("first", await ReadMessage(channel));
            Assert.Equal("second", await ReadMessage(channel));
        }

        [Fact]
        public async Task CrInsideBodyIsKept()
        {
            // 区切りを決めるのはLFなので、単独のCRは本文の一部である。
            BridgeMessageChannel channel = ReadingChannel("a\rb\n");

            Assert.Equal("a\rb", await ReadMessage(channel));
        }

        [Fact]
        public async Task EmptyBodyIsReadAsOneMessage()
        {
            BridgeMessageChannel channel = ReadingChannel("\n");

            Assert.Equal(string.Empty, await ReadMessage(channel));
        }

        [Fact]
        public async Task HostCloseIsReturnedAsDisconnect()
        {
            BridgeMessageChannel channel = ReadingChannel(string.Empty);

            BridgeMessageRead read = await channel.ReadAsync(CancellationToken.None);

            Assert.Equal(BridgeMessageOutcome.EndOfStream, read.Outcome);
            Assert.Null(read.Message);
        }

        [Fact]
        public async Task DisconnectWithoutSeparatorDropsPartialBody()
        {
            BridgeMessageChannel channel = ReadingChannel("区切りが来ない");

            BridgeMessageRead read = await channel.ReadAsync(CancellationToken.None);

            Assert.Equal(BridgeMessageOutcome.EndOfStream, read.Outcome);
            Assert.Null(read.Message);
        }

        [Fact]
        public async Task ReadStopsOnceLimitIsExceededWithoutSeparator()
        {
            // 全文を読んでから長さを判定する作りでは、入力の全体が読まれてしまう。
            CountingStream source = new CountingStream(Utf8WithoutBom.GetBytes(new string('a', 1000000)));
            BridgeMessageChannel channel = new BridgeMessageChannel(source, 16);

            BridgeMessageRead read = await channel.ReadAsync(CancellationToken.None);

            Assert.Equal(BridgeMessageOutcome.TooLarge, read.Outcome);
            Assert.True(source.BytesRead < 100000, "読み取ったバイト数は " + source.BytesRead);
        }

        [Fact]
        public async Task BodyAtLimitIsRead()
        {
            BridgeMessageChannel channel = ReadingChannel("1234\n", 4);

            Assert.Equal("1234", await ReadMessage(channel));
        }

        [Fact]
        public async Task BodyOneByteOverLimitIsCut()
        {
            BridgeMessageChannel channel = ReadingChannel("12345\n", 4);

            BridgeMessageRead read = await channel.ReadAsync(CancellationToken.None);

            Assert.Equal(BridgeMessageOutcome.TooLarge, read.Outcome);
        }

        [Fact]
        public async Task ReadLimitIsMeasuredInBytesNotCharacters()
        {
            // 「あ」はUTF-8で3バイトなので、6文字は18バイトで上限16を超える。
            BridgeMessageChannel channel = ReadingChannel(new string('あ', 6) + "\n", 16);

            BridgeMessageRead read = await channel.ReadAsync(CancellationToken.None);

            Assert.Equal(BridgeMessageOutcome.TooLarge, read.Outcome);
        }

        [Fact]
        public async Task BodyAtLimitIsReadWithCrlfSeparator()
        {
            // 区切りのCRは本文から外れるので、上限の判定には数えない。
            BridgeMessageChannel channel = ReadingChannel("1234\r\n", 4);

            Assert.Equal("1234", await ReadMessage(channel));
        }

        [Fact]
        public async Task BodyAtLimitIsReadWhenCrAndLfArriveApart()
        {
            // 上限を1バイト超えた時点で打ち切る作りだと、続くLFで本文から外れるCRを待てずに
            // 上限超過にしてしまう。
            BridgeMessageChannel channel = new BridgeMessageChannel(
                new ChunkedStream(
                    Utf8WithoutBom.GetBytes("1234\r"),
                    new byte[] { LineFeed }),
                4);

            Assert.Equal("1234", await ReadMessage(channel));
        }

        [Fact]
        public async Task OverLimitBodyNotEndingWithCrIsCutWithoutWaitingForSeparator()
        {
            // 余分な1バイトを無条件に保留すると、区切りが来ないまま待ち続けてしまう。
            BridgeMessageChannel channel = ReadingChannel("12345", 4);

            BridgeMessageRead read = await channel.ReadAsync(CancellationToken.None);

            Assert.Equal(BridgeMessageOutcome.TooLarge, read.Outcome);
        }

        [Fact]
        public async Task BodyThatIsNotValidUtf8IsReturnedAsEncodingError()
        {
            BridgeMessageChannel channel = new BridgeMessageChannel(
                new MemoryStream(new byte[] { 0x82, 0xA0, LineFeed }),
                BridgeMessageChannel.DefaultMaxMessageBytes);

            BridgeMessageRead read = await channel.ReadAsync(CancellationToken.None);

            Assert.Equal(BridgeMessageOutcome.InvalidEncoding, read.Outcome);
            Assert.Null(read.Message);
        }

        [Fact]
        public async Task BodiesWithMultiByteCharactersAreReadPerSeparator()
        {
            BridgeMessageChannel channel = ReadingChannel("あい\nうえ\n");

            Assert.Equal("あい", await ReadMessage(channel));
            Assert.Equal("うえ", await ReadMessage(channel));
        }

        private static BridgeMessageChannel ReadingChannel(string content)
        {
            return ReadingChannel(content, BridgeMessageChannel.DefaultMaxMessageBytes);
        }

        private static BridgeMessageChannel ReadingChannel(string content, int maxMessageBytes)
        {
            return new BridgeMessageChannel(
                new MemoryStream(Utf8WithoutBom.GetBytes(content)), maxMessageBytes);
        }

        private static async Task<string> ReadMessage(BridgeMessageChannel channel)
        {
            BridgeMessageRead read = await channel.ReadAsync(CancellationToken.None);

            Assert.Equal(BridgeMessageOutcome.Message, read.Outcome);
            return read.Message;
        }

        /// <summary>指定した塊の単位でしか渡さない読み取り専用のストリーム。</summary>
        private sealed class ChunkedStream : Stream
        {
            private readonly byte[][] _chunks;
            private int _index;
            private int _offset;

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

                // 塊をまたいで渡さないことだけを保証し、1回に渡す量は呼び出し側の求めに合わせる
                // (読み取りバッファの大きさをテストが決めてしまわないようにする)。
                byte[] chunk = _chunks[_index];
                int taken = Math.Min(chunk.Length - _offset, count);
                Array.Copy(chunk, _offset, buffer, offset, taken);

                _offset += taken;
                if (_offset >= chunk.Length)
                {
                    _index++;
                    _offset = 0;
                }

                return taken;
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
