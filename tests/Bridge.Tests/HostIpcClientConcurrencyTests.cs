using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    /// <summary>
    /// 呼び出しの直列化・待つ上限・キャンセルの扱いを確かめる。ホストへ同時に未完了の要求を
    /// 1件しか持たせないことと、打ち切ったあとに死んだ接続を引きずらないことが要点。
    /// </summary>
    public class HostIpcClientConcurrencyTests
    {
        private const int BudgetChars = 100000;

        /// <summary>打ち切りの振る舞いを確かめるための短い上限。</summary>
        private static readonly TimeSpan ShortWaitLimit = TimeSpan.FromMilliseconds(500);

        /// <summary>テストが応答を待つ上限。これを超えたら直列化か打ち切りが働いていない。</summary>
        private static readonly TimeSpan TestWait = TimeSpan.FromSeconds(30);

        [Fact]
        public void 待つ上限は契約で定めた値である()
        {
            Assert.Equal(TimeSpan.FromSeconds(125), HostIpcClient.DefaultWaitLimit);

            using HostIpcClient client = new HostIpcClient(new FakeHostConnector("pmx-editor-mcp-0"), BudgetChars);

            Assert.Equal(HostIpcClient.DefaultWaitLimit, client.WaitLimit);
        }

        [Fact]
        public async Task 応答が返らなければ上限で打ち切り接続を捨てる()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Stall()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();
            FakeHostConnector connector = new FakeHostConnector(host.PipeName);
            using HostIpcClient client = new HostIpcClient(connector, BudgetChars, ShortWaitLimit);

            BridgeException error = await ThrowsWithin<BridgeException>(
                () => client.CallAsync("slow", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.Timeout, error.Code);
            Assert.False(client.IsConnected);

            // 打ち切った接続は捨てているので、次の呼び出しは新しい接続からやり直せる。
            Assert.Equal("pong", (string)await client.CallAsync("ping", null, CancellationToken.None));
            Assert.Equal(2, connector.ConnectCount);
        }

        [Fact]
        public async Task ハンドシェイクの応答が返らなくても上限で打ち切る()
        {
            using FakeHost host = new FakeHost().Stall().Start();
            using HostIpcClient client = new HostIpcClient(
                new FakeHostConnector(host.PipeName), BudgetChars, ShortWaitLimit);

            BridgeException error = await ThrowsWithin<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.Timeout, error.Code);
            Assert.False(client.IsConnected);
        }

        [Fact]
        public async Task 接続が開き終わらなくても上限で打ち切る()
        {
            // 待つ上限は送受信だけでなく接続の確立にも掛かる。掛かっていないと、開かないパイプを
            // いつまでも待ち続ける。
            using HostIpcClient client = new HostIpcClient(
                new NeverOpeningConnector(), BudgetChars, ShortWaitLimit);

            BridgeException error = await ThrowsWithin<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.Timeout, error.Code);
            Assert.False(client.IsConnected);
        }

        [Fact]
        public async Task ハンドシェイクの途中で取り消したら接続を捨てる()
        {
            using FakeHost host = new FakeHost()
                .Stall()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();
            FakeHostConnector connector = new FakeHostConnector(host.PipeName);
            using HostIpcClient client = new HostIpcClient(connector, BudgetChars);

            using CancellationTokenSource shaking = new CancellationTokenSource();
            Task<JsonNode> opening = client.CallAsync("ping", null, shaking.Token);

            await WaitForRequestCount(host, 1);
            shaking.Cancel();

            await ThrowsWithin<OperationCanceledException>(() => opening);

            Assert.False(client.IsConnected);

            // 中途半端に開いた接続を引きずらないので、次の呼び出しは新規に開く。
            Assert.Equal("pong", (string)await client.CallAsync("ping", null, CancellationToken.None));
            Assert.Equal(2, connector.ConnectCount);
        }

        [Fact]
        public async Task 接続とハンドシェイクの最中に呼んでも接続をやり直さない()
        {
            // 排他の区間は接続の確立から始まる。要求の送受信だけを直列化する作りだと、
            // 並行した最初の呼び出しがそれぞれ接続とhandshakeを始めてしまう。
            using SemaphoreSlim holding = new SemaphoreSlim(0, 1);
            using FakeHost host = new FakeHost()
                .ReplyAsync(async (request, stopping) =>
                {
                    await holding.WaitAsync(stopping).ConfigureAwait(false);
                    return Result(
                        request,
                        "{\"protocol\":1,\"hostVersion\":\"1.0.0.0\",\"budgetChars\":" + BudgetChars + "}");
                })
                .Reply(request => Result(request, "\"pong\""))
                .Reply(request => Result(request, "\"pong\""))
                .Start();
            RecordingConnector connector = new RecordingConnector(host.PipeName);
            using HostIpcClient client = new HostIpcClient(connector, BudgetChars);

            Task<JsonNode> first = client.CallAsync("first", null, CancellationToken.None);
            await WaitForRequestCount(host, 1);

            Task<JsonNode> second = client.CallAsync("second", null, CancellationToken.None);

            // handshake の応答を保留しているあいだ、2件目は接続もhandshakeも始めない。書き出しは
            // 呼ばれた時点で数えるので、送ったかどうかは相手の読み取りを待たずに分かる。
            Assert.Equal(1, connector.WriteCount);
            Assert.Equal(1, connector.ConnectCount);

            holding.Release();
            await WhenAllWithin(first, second);

            Assert.Equal(1, connector.ConnectCount);
            Assert.Equal(
                new string[] { "handshake", "first", "second" },
                MethodsOf(host.Requests));
        }

        [Fact]
        public async Task 並行して呼んでも要求を重ねずに1件ずつ送る()
        {
            // 到着順に譲ること自体は順番待ちのテストが決定的に押さえる。ここでは、並行して
            // 呼んでもホストが見る要求が重ならず、どれも取りこぼされないことを確かめる。
            using SemaphoreSlim holding = new SemaphoreSlim(0, 1);
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .ReplyAsync(async (request, stopping) =>
                {
                    await holding.WaitAsync(stopping).ConfigureAwait(false);
                    return Result(request, "1");
                })
                .Reply(request => Result(request, "2"))
                .Reply(request => Result(request, "3"))
                .Start();
            RecordingConnector connector = new RecordingConnector(host.PipeName);
            using HostIpcClient client = new HostIpcClient(connector, BudgetChars);

            Task<JsonNode> first = client.CallAsync("first", null, CancellationToken.None);
            await WaitForRequestCount(host, 2);

            Task<JsonNode> second = client.CallAsync("second", null, CancellationToken.None);
            Task<JsonNode> third = client.CallAsync("third", null, CancellationToken.None);

            // 先行が応答を待っているあいだ、後続の要求は書き出されない。書き出しは呼ばれた時点で
            // 数えるので、相手が読み取るのを待たずに重複送信が分かる。
            Assert.Equal(2, connector.WriteCount);

            holding.Release();
            await WhenAllWithin(first, second, third);

            string[] methods = MethodsOf(host.Requests);
            Assert.Equal(4, methods.Length);
            Assert.Equal("handshake", methods[0]);
            Assert.Equal("first", methods[1]);
            Assert.Contains("second", methods);
            Assert.Contains("third", methods);
        }

        [Fact]
        public async Task 送っていない呼び出しの取り消しは先行の要求を巻き添えにしない()
        {
            using SemaphoreSlim holding = new SemaphoreSlim(0, 1);
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .ReplyAsync(async (request, stopping) =>
                {
                    await holding.WaitAsync(stopping).ConfigureAwait(false);
                    return Result(request, "\"pong\"");
                })
                .Reply(request => Result(request, "\"pong\""))
                .Start();
            using HostIpcClient client = Connect(host);

            Task<JsonNode> running = client.CallAsync("first", null, CancellationToken.None);
            await WaitForRequestCount(host, 2);

            // 先行が順番を握っているので、この呼び出しは要求を送れていない。
            using CancellationTokenSource waiting = new CancellationTokenSource();
            Task<JsonNode> queued = client.CallAsync("queued", null, waiting.Token);
            waiting.Cancel();

            await ThrowsWithin<OperationCanceledException>(() => queued);

            holding.Release();

            Assert.Equal("pong", (string)await running);
            Assert.True(client.IsConnected);

            // 送られていないので、ホストは取り消された呼び出しを見ていない。
            Assert.DoesNotContain("queued", MethodsOf(host.Requests));
        }

        [Fact]
        public async Task 要求を送ったあとの取り消しは接続を捨てて再送しない()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Stall()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();
            FakeHostConnector connector = new FakeHostConnector(host.PipeName);
            using HostIpcClient client = new HostIpcClient(connector, BudgetChars);

            using CancellationTokenSource sending = new CancellationTokenSource();
            Task<JsonNode> sent = client.CallAsync("slow", null, sending.Token);

            await WaitForRequestCount(host, 2);
            sending.Cancel();

            await ThrowsWithin<OperationCanceledException>(() => sent);

            // 実行されたか分からない要求なので、接続を捨てるだけで送り直さない。
            Assert.False(client.IsConnected);

            Assert.Equal("pong", (string)await client.CallAsync("ping", null, CancellationToken.None));
            Assert.Equal(2, connector.ConnectCount);

            // 送り直していれば、2本目の接続にも同じメソッドが現れる。
            Assert.Equal(
                new string[] { "handshake", "slow", "handshake", "ping" },
                MethodsOf(host.Requests));
        }

        [Fact]
        public async Task 接続の途中で取り消したら部分的な接続を残さない()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();

            using CancellationTokenSource connecting = new CancellationTokenSource();
            BlockingConnector connector = new BlockingConnector(host.PipeName, connecting);
            using HostIpcClient client = new HostIpcClient(connector, BudgetChars);

            Task<JsonNode> opening = client.CallAsync("ping", null, connecting.Token);

            await ThrowsWithin<OperationCanceledException>(() => opening);

            Assert.False(client.IsConnected);

            // 部分的に開いた接続を引きずらないので、次の呼び出しは新規に開く。
            Assert.Equal("pong", (string)await client.CallAsync("ping", null, CancellationToken.None));
        }

        private static HostIpcClient Connect(FakeHost host)
        {
            return new HostIpcClient(new FakeHostConnector(host.PipeName), BudgetChars);
        }

        private static Func<string, string> HandshakeResultOf(int budgetChars)
        {
            return request => Result(
                request,
                "{\"protocol\":1,\"hostVersion\":\"1.0.0.0\",\"budgetChars\":" + budgetChars + "}");
        }

        private static string Result(string request, string result)
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":" + IdOf(request) + ",\"result\":" + result + "}";
        }

        private static int IdOf(string request)
        {
            return (int)JsonNode.Parse(request).AsObject()["id"];
        }

        private static string[] MethodsOf(IReadOnlyList<string> requests)
        {
            string[] methods = new string[requests.Count];
            for (int index = 0; index < methods.Length; index++)
            {
                methods[index] = (string)JsonNode.Parse(requests[index]).AsObject()["method"];
            }

            return methods;
        }

        /// <summary>
        /// 上限付きで例外を待つ。相手が応答しない筋書きなので、製品側が待つ上限を掛け忘れると
        /// テストが無期限に止まる。テスト側にも独立した上限を置いて有限時間の失敗にする。
        /// </summary>
        private static async Task<TException> ThrowsWithin<TException>(Func<Task> action)
            where TException : Exception
        {
            Task<TException> throwing = Assert.ThrowsAnyAsync<TException>(action);
            Task finished = await Task.WhenAny(throwing, Task.Delay(TestWait)).ConfigureAwait(false);

            Assert.True(ReferenceEquals(finished, throwing), "呼び出しが待機上限内に終わらなかった。");
            return await throwing.ConfigureAwait(false);
        }

        private static async Task WhenAllWithin(params Task[] tasks)
        {
            Task all = Task.WhenAll(tasks);
            Task finished = await Task.WhenAny(all, Task.Delay(TestWait)).ConfigureAwait(false);

            Assert.True(ReferenceEquals(finished, all), "呼び出しが待機上限内に終わらなかった。");
            await all.ConfigureAwait(false);
        }

        /// <summary>ホストが指定の件数の要求を受け取るまで待つ。</summary>
        private static async Task WaitForRequestCount(FakeHost host, int count)
        {
            DateTime limit = DateTime.UtcNow + TestWait;
            while (host.Requests.Count < count)
            {
                Assert.True(DateTime.UtcNow < limit, "ホストが要求を受け取らなかった。");
                await Task.Delay(10).ConfigureAwait(false);
            }
        }

        /// <summary>書き出しが呼ばれた回数を数える接続役。</summary>
        private sealed class RecordingConnector : IHostConnector
        {
            private readonly FakeHostConnector _inner;

            private int _writes;

            public RecordingConnector(string pipeName)
            {
                _inner = new FakeHostConnector(pipeName);
            }

            public int ConnectCount => _inner.ConnectCount;

            /// <summary>本文の書き出しが呼ばれた回数。呼ばれた時点で数える。</summary>
            public int WriteCount => Volatile.Read(ref _writes);

            public async Task<Stream> ConnectAsync(CancellationToken cancellationToken)
            {
                Stream inner = await _inner.ConnectAsync(cancellationToken).ConfigureAwait(false);
                return new RecordingStream(inner, () => Interlocked.Increment(ref _writes));
            }
        }

        /// <summary>書き出しの呼び出しを数えて内側へ委ねるストリーム。</summary>
        private sealed class RecordingStream : Stream
        {
            private readonly Stream _inner;
            private readonly Action _onWrite;

            public RecordingStream(Stream inner, Action onWrite)
            {
                _inner = inner;
                _onWrite = onWrite;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override ValueTask<int> ReadAsync(
                Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return _inner.ReadAsync(buffer, cancellationToken);
            }

            public override ValueTask WriteAsync(
                ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                _onWrite();
                return _inner.WriteAsync(buffer, cancellationToken);
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return _inner.FlushAsync(cancellationToken);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _inner.Read(buffer, offset, count);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _onWrite();
                _inner.Write(buffer, offset, count);
            }

            public override void Flush()
            {
                _inner.Flush();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }

        /// <summary>いつまでも接続を開き終えない接続役。</summary>
        private sealed class NeverOpeningConnector : IHostConnector
        {
            public async Task<Stream> ConnectAsync(CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("ここへは到達しない。");
            }
        }

        /// <summary>取り消しが起きるまで接続を開き終えない接続役。</summary>
        private sealed class BlockingConnector : IHostConnector
        {
            private readonly FakeHostConnector _inner;
            private readonly CancellationTokenSource _release;

            private int _opened;

            public BlockingConnector(string pipeName, CancellationTokenSource release)
            {
                _inner = new FakeHostConnector(pipeName);
                _release = release;
            }

            public async Task<Stream> ConnectAsync(CancellationToken cancellationToken)
            {
                if (++_opened == 1)
                {
                    // 1回目は取り消しを起こしてから、開き終える前に打ち切られるようにする。
                    _release.Cancel();
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                }

                return await _inner.ConnectAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
