using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class McpHostTests : IDisposable
    {
        private const string Pending = "impl pending: 名前付きパイプの待受を稼働世代の単位で開始・停止し、状態区分の判定とUIディスパッチの可否を世代ごとに決める";

        private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);
        private const int ConnectTimeoutMs = 3000;
        private const int AbsentTimeoutMs = 300;

        private readonly string _directory;
        private readonly string _pipeName;
        private McpHost _host;

        public McpHostTests()
        {
            string identity = Guid.NewGuid().ToString("N");
            _directory = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-test-" + identity);
            Directory.CreateDirectory(_directory);
            _pipeName = "pmx-editor-mcp-test-" + identity;
        }

        public void Dispose()
        {
            if (_host != null)
            {
                _host.Stop();

                // 予算設定が不正なホストは待受を持たないため「停止済み」へは到達しない。
                WaitUntil(() => _host.Status == HostStatus.Stopped
                    || _host.Status == HostStatus.NotStartedInvalidBudget);
            }

            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>UIスレッドを持たないため、委譲された処理はその場で実行する。</summary>
        private sealed class InlineDispatcher : IUiDispatcher
        {
            public void Invoke(Action action)
            {
                action();
            }
        }

        private McpHost CreateHost(ConnectionHandler connectionHandler, string budgetRawValue = null)
        {
            _host = new McpHost(
                _pipeName,
                new HostLog(Path.Combine(_directory, "host.log")),
                ResponseBudget.Read(budgetRawValue),
                new InlineDispatcher(),
                connectionHandler);
            return _host;
        }

        private static bool WaitUntil(Func<bool> condition)
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < WaitLimit)
            {
                if (condition())
                {
                    return true;
                }

                Thread.Sleep(10);
            }

            return condition();
        }

        private static NamedPipeClientStream Connect(string pipeName, int timeoutMs)
        {
            NamedPipeClientStream client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            try
            {
                client.Connect(timeoutMs);
                return client;
            }
            catch (Exception)
            {
                client.Dispose();
                throw;
            }
        }

        private static bool CanConnect(string pipeName, int timeoutMs)
        {
            try
            {
                using (Connect(pipeName, timeoutMs))
                {
                    return true;
                }
            }
            catch (TimeoutException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static void Ignore(Stream stream, HostGeneration generation)
        {
        }

        [Fact(Skip = Pending)]
        public void パイプ名はエディタのプロセスIDで決まる()
        {
            Assert.Equal("pmx-editor-mcp-1234", McpHost.BuildPipeName(1234));
            Assert.NotEqual(McpHost.BuildPipeName(1234), McpHost.BuildPipeName(5678));
        }

        [Fact(Skip = Pending)]
        public void 予算設定が不正なら待受を開始せず理由を返す()
        {
            McpHost host = CreateHost(Ignore, "9999");

            string reason;
            bool started = host.TryStart(out reason);

            Assert.False(started);
            Assert.False(string.IsNullOrEmpty(reason));
            Assert.Equal(HostStatus.NotStartedInvalidBudget, host.Status);
            Assert.False(CanConnect(_pipeName, AbsentTimeoutMs));
            Assert.Contains(host.Budget.InvalidReason, File.ReadAllText(host.LogFilePath, Encoding.UTF8));

            // 環境変数を読み直しても同じ結果になるため、この状態からの開始は受け付けない。
            Assert.False(host.TryStart(out reason));
            Assert.False(string.IsNullOrEmpty(reason));
            Assert.Equal(HostStatus.NotStartedInvalidBudget, host.Status);
        }

        [Fact(Skip = Pending)]
        public void 状態表示に使う値をそのまま返す()
        {
            McpHost host = CreateHost(Ignore);

            Assert.Equal(_pipeName, host.PipeName);
            Assert.Equal(Path.Combine(_directory, "host.log"), host.LogFilePath);
            Assert.True(host.Budget.IsValid);
            Assert.Equal(ResponseBudget.DefaultChars, host.Budget.Chars);
        }

        [Fact(Skip = Pending)]
        public void 開始すると稼働中になりクライアントが接続できる()
        {
            McpHost host = CreateHost(Ignore);

            string reason;
            Assert.True(host.TryStart(out reason));
            Assert.Null(reason);
            Assert.Equal(HostStatus.Running, host.Status);
            Assert.True(CanConnect(_pipeName, ConnectTimeoutMs));
        }

        [Fact(Skip = Pending)]
        public void 切断されたら待受に戻る()
        {
            McpHost host = CreateHost(Ignore);
            string reason;
            host.TryStart(out reason);

            Assert.True(CanConnect(_pipeName, ConnectTimeoutMs));
            Assert.True(CanConnect(_pipeName, ConnectTimeoutMs));
        }

        [Fact(Skip = Pending)]
        public void 接続は同時に1本までしか受けない()
        {
            using (ManualResetEventSlim connected = new ManualResetEventSlim())
            using (ManualResetEventSlim release = new ManualResetEventSlim())
            {
                McpHost host = CreateHost((stream, generation) =>
                {
                    connected.Set();
                    release.Wait(WaitLimit);
                });
                string reason;
                host.TryStart(out reason);

                using (Connect(_pipeName, ConnectTimeoutMs))
                {
                    Assert.True(connected.Wait(WaitLimit));
                    Assert.False(CanConnect(_pipeName, AbsentTimeoutMs));
                    release.Set();
                }

                Assert.True(WaitUntil(() => CanConnect(_pipeName, ConnectTimeoutMs)));
            }
        }

        [Fact(Skip = Pending)]
        public void 接続処理が例外で終わっても待受は続く()
        {
            using (ManualResetEventSlim handled = new ManualResetEventSlim())
            {
                int attempts = 0;
                McpHost host = CreateHost((stream, generation) =>
                {
                    if (Interlocked.Increment(ref attempts) == 1)
                    {
                        throw new InvalidOperationException("接続処理の失敗");
                    }

                    handled.Set();
                });
                string reason;
                host.TryStart(out reason);

                Assert.True(CanConnect(_pipeName, ConnectTimeoutMs));
                Assert.True(CanConnect(_pipeName, ConnectTimeoutMs));

                Assert.True(handled.Wait(WaitLimit));
                Assert.Equal(HostStatus.Running, host.Status);
            }
        }

        [Fact(Skip = Pending)]
        public void 読み取りでブロックしていても停止で解ける()
        {
            using (ManualResetEventSlim connected = new ManualResetEventSlim())
            using (ManualResetEventSlim finished = new ManualResetEventSlim())
            {
                McpHost host = CreateHost((stream, generation) =>
                {
                    connected.Set();
                    try
                    {
                        // 停止でパイプが閉じられるまで戻らない読み取り。
                        stream.ReadByte();
                    }
                    catch (Exception)
                    {
                    }

                    finished.Set();
                });
                string reason;
                host.TryStart(out reason);

                using (Connect(_pipeName, ConnectTimeoutMs))
                {
                    Assert.True(connected.Wait(WaitLimit));
                    Assert.False(finished.Wait(TimeSpan.FromMilliseconds(200)));

                    host.Stop();

                    Assert.True(finished.Wait(WaitLimit));
                }

                Assert.True(WaitUntil(() => host.Status == HostStatus.Stopped));
            }
        }

        [Fact(Skip = Pending)]
        public void 未開始と停止済みでの停止は何も変えない()
        {
            McpHost host = CreateHost(Ignore);

            host.Stop();
            Assert.Equal(HostStatus.Stopped, host.Status);

            string reason;
            Assert.True(host.TryStart(out reason));
            host.Stop();
            Assert.True(WaitUntil(() => host.Status == HostStatus.Stopped));

            host.Stop();
            Assert.Equal(HostStatus.Stopped, host.Status);
            Assert.True(host.TryStart(out reason));
        }

        [Fact(Skip = Pending)]
        public void 稼働中の開始は拒否する()
        {
            McpHost host = CreateHost(Ignore);
            string reason;
            host.TryStart(out reason);

            Assert.False(host.TryStart(out reason));
            Assert.False(string.IsNullOrEmpty(reason));
            Assert.Equal(HostStatus.Running, host.Status);
        }

        [Fact(Skip = Pending)]
        public void 停止すると待受が無くなり停止済みになる()
        {
            McpHost host = CreateHost(Ignore);
            string reason;
            host.TryStart(out reason);
            Assert.True(CanConnect(_pipeName, ConnectTimeoutMs));

            host.Stop();

            Assert.True(WaitUntil(() => host.Status == HostStatus.Stopped));
            Assert.False(CanConnect(_pipeName, AbsentTimeoutMs));
        }

        [Fact(Skip = Pending)]
        public void 開始直後に停止してもパイプインスタンスが待受へ残らない()
        {
            McpHost host = CreateHost(Ignore);

            for (int attempt = 0; attempt < 20; attempt++)
            {
                string reason;
                Assert.True(host.TryStart(out reason));
                host.Stop();
                Assert.True(WaitUntil(() => host.Status == HostStatus.Stopped));
                Assert.False(CanConnect(_pipeName, AbsentTimeoutMs));
            }
        }

        [Fact(Skip = Pending)]
        public void 接続処理の最中に停止しても呼び出しスレッドをブロックしない()
        {
            using (ManualResetEventSlim connected = new ManualResetEventSlim())
            using (ManualResetEventSlim release = new ManualResetEventSlim())
            {
                McpHost host = CreateHost((stream, generation) =>
                {
                    connected.Set();
                    release.Wait(WaitLimit);
                });
                string reason;
                host.TryStart(out reason);

                using (Connect(_pipeName, ConnectTimeoutMs))
                {
                    Assert.True(connected.Wait(WaitLimit));

                    Stopwatch elapsed = Stopwatch.StartNew();
                    host.Stop();
                    elapsed.Stop();

                    Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1), "停止に " + elapsed.Elapsed + " かかった");
                    Assert.Equal(HostStatus.Stopping, host.Status);
                    release.Set();
                }

                Assert.True(WaitUntil(() => host.Status == HostStatus.Stopped));
            }
        }

        [Fact(Skip = Pending)]
        public void 停止した稼働世代ではUIディスパッチを行わない()
        {
            using (ManualResetEventSlim connected = new ManualResetEventSlim())
            using (ManualResetEventSlim release = new ManualResetEventSlim())
            using (ManualResetEventSlim finished = new ManualResetEventSlim())
            {
                bool dispatchedBeforeStop = false;
                bool dispatchedAfterStop = true;
                McpHost host = CreateHost((stream, generation) =>
                {
                    dispatchedBeforeStop = generation.TryInvokeOnUi(() => { });
                    connected.Set();
                    release.Wait(WaitLimit);
                    dispatchedAfterStop = generation.TryInvokeOnUi(() => { });
                    finished.Set();
                });
                string reason;
                host.TryStart(out reason);

                using (Connect(_pipeName, ConnectTimeoutMs))
                {
                    Assert.True(connected.Wait(WaitLimit));
                    host.Stop();
                    release.Set();
                    Assert.True(finished.Wait(WaitLimit));
                }

                Assert.True(dispatchedBeforeStop);
                Assert.False(dispatchedAfterStop);
            }
        }

        [Fact(Skip = Pending)]
        public void 待受を担うスレッドが終了する前は停止処理中と判定して開始を拒否する()
        {
            using (ManualResetEventSlim connected = new ManualResetEventSlim())
            using (ManualResetEventSlim release = new ManualResetEventSlim())
            {
                McpHost host = CreateHost((stream, generation) =>
                {
                    connected.Set();
                    release.Wait(WaitLimit);
                });
                string reason;
                host.TryStart(out reason);

                using (Connect(_pipeName, ConnectTimeoutMs))
                {
                    Assert.True(connected.Wait(WaitLimit));
                    host.Stop();

                    Assert.Equal(HostStatus.Stopping, host.Status);
                    Assert.False(host.TryStart(out reason));
                    Assert.False(string.IsNullOrEmpty(reason));

                    release.Set();
                }

                Assert.True(WaitUntil(() => host.Status == HostStatus.Stopped));
                Assert.True(host.TryStart(out reason));
            }
        }

        [Fact(Skip = Pending)]
        public void 停止から開始すると同じパイプ名の待受へ戻る()
        {
            McpHost host = CreateHost(Ignore);
            string reason;
            host.TryStart(out reason);
            host.Stop();
            Assert.True(WaitUntil(() => host.Status == HostStatus.Stopped));

            Assert.True(host.TryStart(out reason));

            Assert.Equal(_pipeName, host.PipeName);
            Assert.Equal(HostStatus.Running, host.Status);
            Assert.True(CanConnect(_pipeName, ConnectTimeoutMs));
        }

        [Fact(Skip = Pending)]
        public void 新しい稼働世代は前の稼働世代のUIディスパッチ禁止を引き継がない()
        {
            using (ManualResetEventSlim connected = new ManualResetEventSlim())
            {
                HostGeneration observed = null;
                bool dispatched = false;
                McpHost host = CreateHost((stream, generation) =>
                {
                    observed = generation;
                    dispatched = generation.TryInvokeOnUi(() => { });
                    connected.Set();
                });
                string reason;
                host.TryStart(out reason);
                using (Connect(_pipeName, ConnectTimeoutMs))
                {
                    Assert.True(connected.Wait(WaitLimit));
                }

                HostGeneration first = observed;
                host.Stop();
                Assert.True(WaitUntil(() => host.Status == HostStatus.Stopped));
                Assert.False(first.TryInvokeOnUi(() => { }));

                connected.Reset();
                Assert.True(host.TryStart(out reason));
                using (Connect(_pipeName, ConnectTimeoutMs))
                {
                    Assert.True(connected.Wait(WaitLimit));
                }

                Assert.NotSame(first, observed);
                Assert.True(dispatched);
                Assert.False(observed.IsStopRequested);
                Assert.True(first.IsStopRequested);
            }
        }

        [Fact(Skip = Pending)]
        public void 接続中は接続状態を返す()
        {
            using (ManualResetEventSlim connected = new ManualResetEventSlim())
            using (ManualResetEventSlim release = new ManualResetEventSlim())
            {
                McpHost host = CreateHost((stream, generation) =>
                {
                    connected.Set();
                    release.Wait(WaitLimit);
                });
                string reason;
                host.TryStart(out reason);

                Assert.False(host.IsClientConnected);
                using (Connect(_pipeName, ConnectTimeoutMs))
                {
                    Assert.True(connected.Wait(WaitLimit));
                    Assert.True(host.IsClientConnected);
                    release.Set();
                }

                Assert.True(WaitUntil(() => !host.IsClientConnected));
            }
        }
    }
}
