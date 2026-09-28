using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 結果を返せなくなった要求を、ホストがあとから実行しないこと。別の接続の要求が直列化の錠を
    /// 持ち続けている間に、待たされた要求の時間が尽きる筋書きで確かめる。
    /// </summary>
    public sealed class JsonRpcUnreturnableRequestTests : IDisposable
    {
        private const int ClientId = 4321;

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

        private static readonly TimeSpan EndLimit = TimeSpan.FromSeconds(2);

        private readonly string _directory;

        private readonly HostLog _log;

        private int _edited;

        public JsonRpcUnreturnableRequestTests()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-unreturnable-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _log = new HostLog(Path.Combine(_directory, "host.log"));
        }

        public void Dispose()
        {
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

        /// <summary>
        /// 別の接続が錠を持ち続けている間に上限の時間を過ぎた要求は、時間切れを返し、錠が空いた
        /// あとで実行されない。時間は要求を受け取ったときから数え、錠を待つ間も数える。
        /// </summary>
        [Fact]
        public void ARequestThatWaitedPastTheTimeLimitIsAnsweredAndNeverRun()
        {
            using (ManualResetEventSlim started = new ManualResetEventSlim())
            using (ManualResetEventSlim release = new ManualResetEventSlim())
            using (ExchangeStream holder = new ExchangeStream(Lines(Handshake(), Request(2, "hold"))))
            using (ExchangeStream waiter = new ExchangeStream(Lines(Handshake(), Request(2, "edit"))))
            {
                JsonRpcConnection connection = Connection(
                    Methods(started, release), TimeSpan.FromMilliseconds(300));

                Thread holding = Serve(connection, holder);
                Thread waiting = null;
                bool answeredWhileHeld;
                try
                {
                    Assert.True(started.Wait(WaitLimit), "錠を持つ処理が始まらない。");
                    waiting = Serve(connection, waiter);

                    answeredWhileHeld = waiter.WaitForMessages(2, TimeSpan.FromSeconds(2));
                }
                finally
                {
                    release.Set();
                    Assert.True(holding.Join(WaitLimit), "錠を持つ接続が終わらない。");
                    Assert.True(waiting == null || waiting.Join(WaitLimit), "待たされた接続が終わらない。");
                }

                Assert.True(_edited == 0, "時間切れの後で、待たされた要求が実行された。");
                Assert.True(answeredWhileHeld, "錠を待つ間に上限の時間を過ぎても、時間切れが返らない。");

                IList<IDictionary<string, object>> responses = waiter.ReadResponses();
                Assert.Equal(2, responses.Count);
                IDictionary<string, object> error =
                    Assert.IsAssignableFrom<IDictionary<string, object>>(responses[1]["error"]);
                Assert.Equal(JsonRpcErrorCodes.RequestTimeout, Convert.ToInt32(error["code"]));
                Assert.Equal(2, Convert.ToInt32(responses[1]["id"]));
            }
        }

        [Fact]
        public void ARequestWhoseConnectionIsGoneWhileWaitingIsNeverRun()
        {
            string pipeName = "pmx-editor-mcp-unreturnable-" + Guid.NewGuid().ToString("N");

            using (ManualResetEventSlim started = new ManualResetEventSlim())
            using (ManualResetEventSlim release = new ManualResetEventSlim())
            using (ExchangeStream holder = new ExchangeStream(Lines(Handshake(), Request(2, "hold"))))
            using (NamedPipeServerStream server = new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0))
            {
                JsonRpcConnection connection = Connection(
                    Methods(started, release), JsonRpcConnection.DefaultRequestTimeout);

                Thread holding = Serve(connection, holder);
                Thread waiting = null;
                bool endedWhileHeld = false;
                try
                {
                    Assert.True(started.Wait(WaitLimit), "錠を持つ処理が始まらない。");

                    waiting = new Thread(() =>
                    {
                        try
                        {
                            server.WaitForConnection();
                            connection.Handle(server, new InlineInvoker());
                        }
                        catch (IOException)
                        {
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                    });
                    waiting.IsBackground = true;
                    waiting.Start();

                    using (NamedPipeClientStream client = new NamedPipeClientStream(
                        ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
                    {
                        client.Connect((int)WaitLimit.TotalMilliseconds);
                        byte[] sent = Lines(Handshake(), Request(2, "edit"));
                        // 緩衝を0にした名前付きパイプへの書き込みは、相手が読み取るまで戻らない。
                        Within(
                            () =>
                            {
                                client.Write(sent, 0, sent.Length);
                                client.Flush();
                            },
                            "ホストが要求を読み取らない。");
                        string answer = null;
                        Within(() => answer = ReadLine(client), "handshake の応答が届かない。");
                        Assert.NotNull(answer);
                    }

                    endedWhileHeld = waiting.Join(EndLimit);
                }
                finally
                {
                    release.Set();
                    Assert.True(holding.Join(WaitLimit), "錠を持つ接続が終わらない。");
                    Assert.True(waiting == null || waiting.Join(WaitLimit), "切断された接続が終わらない。");
                }

                Assert.True(_edited == 0, "相手が切断した後で、待たされた要求が実行された。");
                Assert.True(endedWhileHeld, "相手が切断した接続が、錠の空くのを待ち続けた。");
            }
        }

        [Fact]
        public void ARequestWhoseConnectionIsFoundGoneOnTakingTheLockIsNeverRun()
        {
            using (ManualResetEventSlim started = new ManualResetEventSlim())
            using (ManualResetEventSlim release = new ManualResetEventSlim())
            using (ExchangeStream holder = new ExchangeStream(Lines(Handshake(), Request(2, "hold"))))
            using (DetachingPipe waiter = new DetachingPipe(Lines(Handshake(), Request(2, "edit"))))
            {
                JsonRpcConnection connection = Connection(
                    Methods(started, release), JsonRpcConnection.DefaultRequestTimeout, null, TimeSpan.FromHours(1));

                Thread holding = Serve(connection, holder);
                Thread waiting = null;
                try
                {
                    Assert.True(started.Wait(WaitLimit), "錠を持つ処理が始まらない。");
                    waiting = ServeCatching(connection, waiter);
                    Assert.True(waiter.Consumed.Wait(WaitLimit), "待たされる接続が要求を読み取らない。");
                    waiter.Detach();
                }
                finally
                {
                    release.Set();
                    Assert.True(holding.Join(WaitLimit), "錠を持つ接続が終わらない。");
                    Assert.True(waiting == null || waiting.Join(WaitLimit), "切断された接続が終わらない。");
                }

                Assert.True(_edited == 0, "錠を取れた時点で相手が切断していたのに、待たされた要求が実行された。");
            }
        }

        [Fact]
        public void ARequestWhoseTimeRunsOutAfterTakingTheLockIsAnsweredAndNeverRun()
        {
            SteppingClock clock = new SteppingClock(TimeSpan.FromHours(1));
            using (ManualResetEventSlim started = new ManualResetEventSlim())
            using (ManualResetEventSlim release = new ManualResetEventSlim())
            using (ExchangeStream holder = new ExchangeStream(Lines(Handshake(), Request(2, "hold"))))
            using (ExchangeStream waiter = new ExchangeStream(Lines(Handshake(), Request(2, "edit"))))
            {
                JsonRpcConnection connection = Connection(
                    Methods(started, release), TimeSpan.FromMinutes(150), clock.Read, TimeSpan.FromHours(10));

                Thread holding = Serve(connection, holder);
                Thread waiting = null;
                try
                {
                    Assert.True(started.Wait(WaitLimit), "錠を持つ処理が始まらない。");
                    clock.Start();
                    waiting = ServeCatching(connection, waiter);
                    Assert.True(waiter.WaitForMessages(1, WaitLimit), "待たされる接続が handshake に答えない。");
                }
                finally
                {
                    release.Set();
                    Assert.True(holding.Join(WaitLimit), "錠を持つ接続が終わらない。");
                    Assert.True(waiting == null || waiting.Join(WaitLimit), "待たされた接続が終わらない。");
                }

                Assert.True(_edited == 0, "処理を始める前に時間が尽きたのに、待たされた要求が実行された。");

                IList<IDictionary<string, object>> responses = waiter.ReadResponses();
                Assert.Equal(2, responses.Count);
                IDictionary<string, object> error =
                    Assert.IsAssignableFrom<IDictionary<string, object>>(responses[1]["error"]);
                Assert.Equal(JsonRpcErrorCodes.RequestTimeout, Convert.ToInt32(error["code"]));
            }
        }

        private McpMethodTable Methods(ManualResetEventSlim started, ManualResetEventSlim release)
        {
            McpMethodTable methods = new McpMethodTable();
            methods.Add("hold", context =>
            {
                started.Set();
                release.Wait(WaitLimit);

                return "held";
            });
            methods.Add("edit", context =>
            {
                Interlocked.Increment(ref _edited);

                return "edited";
            });

            return methods;
        }

        private JsonRpcConnection Connection(McpMethodTable methods, TimeSpan requestTimeout)
        {
            return new JsonRpcConnection(
                _log,
                methods,
                "1.2.3.4",
                100000,
                requestTimeout,
                MessageChannel.DefaultMaxMessageBytes,
                StubClientProcess.Opener(ClientId, ClientId));
        }

        private JsonRpcConnection Connection(
            McpMethodTable methods, TimeSpan requestTimeout, Func<TimeSpan> clock, TimeSpan gatePollInterval)
        {
            return new JsonRpcConnection(
                _log,
                methods,
                "1.2.3.4",
                100000,
                requestTimeout,
                MessageChannel.DefaultMaxMessageBytes,
                StubClientProcess.Opener(ClientId, ClientId),
                new SdkRelayTable(string.Empty, string.Empty, new Dictionary<string, SdkCall>(), new string[0]),
                string.Empty,
                clock: clock,
                gatePollInterval: gatePollInterval);
        }

        private static Thread ServeCatching(JsonRpcConnection connection, Stream stream)
        {
            Thread worker = new Thread(() =>
            {
                try
                {
                    connection.Handle(stream, new InlineInvoker());
                }
                catch (Exception)
                {
                }
            });
            worker.IsBackground = true;
            worker.Start();

            return worker;
        }

        private static Thread Serve(JsonRpcConnection connection, Stream stream)
        {
            Thread worker = new Thread(() => connection.Handle(stream, new InlineInvoker()));
            worker.IsBackground = true;
            worker.Start();

            return worker;
        }

        private static string Handshake()
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"handshake\",\"params\":{\"protocol\":1}}";
        }

        private static string Request(int id, string method)
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"" + method + "\"}";
        }

        private static byte[] Lines(params string[] requests)
        {
            return Utf8WithoutBom.GetBytes(string.Join("\n", requests) + "\n");
        }

        /// <summary>上限を過ぎたら <paramref name="late"/> で、例外で終わったらその例外を添えて落ちる。</summary>
        private static void Within(Action action, string late)
        {
            Exception failed = null;
            Thread worker = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception thrown)
                {
                    failed = thrown;
                }
            });
            worker.IsBackground = true;
            worker.Start();

            Assert.True(worker.Join(WaitLimit), late);
            Assert.True(failed == null, "待った処理が例外で終わった: " + failed);
        }

        /// <summary>読み切る前に閉じられたら null。</summary>
        private static string ReadLine(Stream stream)
        {
            List<byte> line = new List<byte>();
            byte[] one = new byte[1];
            while (stream.Read(one, 0, 1) == 1)
            {
                if (one[0] == (byte)'\n')
                {
                    return Utf8WithoutBom.GetString(line.ToArray());
                }

                line.Add(one[0]);
            }

            return null;
        }

        /// <summary>読まれるたびに決まった長さだけ進む時計。進め始めるまでは止まっている。</summary>
        private sealed class SteppingClock
        {
            private readonly object _gate = new object();
            private readonly TimeSpan _step;
            private TimeSpan _now;
            private bool _stepping;

            internal SteppingClock(TimeSpan step)
            {
                _step = step;
            }

            internal void Start()
            {
                lock (_gate)
                {
                    _stepping = true;
                }
            }

            internal TimeSpan Read()
            {
                lock (_gate)
                {
                    if (_stepping)
                    {
                        _now += _step;
                    }

                    return _now;
                }
            }
        }

        /// <summary>入力を渡し切ったことを知らせ、切断をこちらから起こせるパイプ。</summary>
        private sealed class DetachingPipe : PipeStream
        {
            private readonly MemoryStream _input;
            private readonly MemoryStream _output = new MemoryStream();

            internal DetachingPipe(byte[] input)
                : base(PipeDirection.InOut, 0)
            {
                _input = new MemoryStream(input);
                IsConnected = true;
            }

            internal ManualResetEventSlim Consumed { get; } = new ManualResetEventSlim();

            internal void Detach()
            {
                IsConnected = false;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = _input.Read(buffer, offset, count);
                if (_input.Position == _input.Length)
                {
                    Consumed.Set();
                }

                return read;
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return Task.FromResult(Read(buffer, offset, count));
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                lock (_output)
                {
                    _output.Write(buffer, offset, count);
                }
            }

            public override void Flush()
            {
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Consumed.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
