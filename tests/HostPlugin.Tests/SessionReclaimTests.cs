using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// セッションを回収する2つの経路。明示の終了と、所有者である接続元プロセスの終了である。
    /// どちらでもハンドルが失効し、待機ハンドルが解放される。
    /// </summary>
    public sealed class SessionReclaimTests : IDisposable
    {
        private const string HostVersion = "1.2.3.4";

        private const int BudgetChars = 100000;

        private const int ClientId = 4321;

        /// <summary>
        /// 回収はスレッドプールの合図で走るので、同時に走る別の検査が機を占有すると遅れる。合否が
        /// 機の混み具合で動かないよう、上限は待ちたい時間ではなく明らかに超えない値にする。
        /// </summary>
        private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(60);

        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private readonly string _root;

        private readonly HostLog _log;

        public SessionReclaimTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-reclaim-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _log = new HostLog(Path.Combine(_root, "host.log"));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void TheOwnerLeavingTakesTheSessionWithIt()
        {
            SessionStore store = Store();
            ManualResetEvent exit = new ManualResetEvent(false);

            Session session;
            Assert.True(store.TryResolve(null, new ClientProcess(ClientId, exit), out session));
            int handle = session.Handles.Issue("test", new object(), () => { });
            Assert.Equal(1, handle);

            exit.Set();

            Assert.True(WaitForCount(store, 0), "所有者が終わってもセッションが残っている。");
            Assert.Null(store.Find(session.Id));
            Assert.True(session.Handles.IsClosed);
            Assert.Equal(0, session.Handles.Count);
        }

        [Fact]
        public void AnOwnerThatHasAlreadyLeftIsReclaimedRightAfterTheSessionIsMade()
        {
            SessionStore store = Store();

            Session session;
            Assert.True(store.TryResolve(null, StubClientProcess.Exited(ClientId), out session));

            Assert.True(WaitForCount(store, 0), "終わっている所有者のセッションが残っている。");
            Assert.True(session.Handles.IsClosed);
        }

        [Fact]
        public void OnlyTheSessionWhoseOwnerLeftIsReclaimed()
        {
            SessionStore store = Store();
            ManualResetEvent leaving = new ManualResetEvent(false);

            Session first;
            Session second;
            Assert.True(store.TryResolve(null, new ClientProcess(ClientId, leaving), out first));
            Assert.True(store.TryResolve(null, StubClientProcess.Living(ClientId + 1), out second));

            leaving.Set();

            Assert.True(WaitForCount(store, 1), "片方だけが回収されていない。");
            Assert.Null(store.Find(first.Id));
            Assert.NotNull(store.Find(second.Id));
            Assert.False(second.Handles.IsClosed);
        }

        /// <summary>
        /// 所有者を保持しているあいだ、そのプロセスオブジェクトは残るのでIDは再利用されない。
        /// 回収したあとに同じIDのプロセスが現れても、識別子は既に無いので引き継げない。
        /// </summary>
        [Fact]
        public void TheIdentifierIsGoneEvenIfTheProcessIdComesBack()
        {
            SessionStore store = Store();
            ManualResetEvent exit = new ManualResetEvent(false);

            Session session;
            Assert.True(store.TryResolve(null, new ClientProcess(ClientId, exit), out session));
            exit.Set();
            Assert.True(WaitForCount(store, 0), "所有者が終わってもセッションが残っている。");

            Session again;
            Assert.True(
                store.TryResolve(session.Id, StubClientProcess.Living(ClientId), out again));
            Assert.NotEqual(session.Id, again.Id);
        }

        [Fact]
        public void EndingAndMakingSessionsDoesNotPileThemUp()
        {
            SessionStore store = Store();

            for (int i = 0; i < 50; i++)
            {
                Session session;
                Assert.True(
                    store.TryResolve(null, StubClientProcess.Living(ClientId), out session));
                Assert.True(store.End(session.Id));
            }

            Assert.Equal(0, store.Count);
        }

        [Fact]
        public void EndingWhatIsNotThereChangesNothing()
        {
            SessionStore store = Store();

            Session session;
            Assert.True(store.TryResolve(null, StubClientProcess.Living(ClientId), out session));

            Assert.True(store.End(session.Id));
            Assert.False(store.End(session.Id));
            Assert.False(store.End("しらないせっしょん"));
            Assert.Throws<ArgumentNullException>(() => store.End(null));
        }

        [Fact]
        public void TheEndSessionMethodEndsItAndClosesTheConnection()
        {
            HandleLedger ledger = null;
            McpMethodTable methods = new McpMethodTable();
            methods.Add("issue", context =>
            {
                ledger = context.Handles;
                return context.Handles.Issue("test", new object(), () => { });
            });
            JsonRpcConnection connection = Connection(methods);

            IList<IDictionary<string, object>> responses = Exchange(
                connection,
                Handshake(),
                Request(2, "issue"),
                Request(3, "end_session"),
                Request(4, "ping"));

            Assert.Equal(3, responses.Count);
            string id = SessionOf(responses[0]);
            Assert.Equal(id, ResultOf(responses[2]));
            Assert.Equal(0, connection.Sessions.Count);
            Assert.Null(connection.Sessions.Find(id));
            Assert.True(ledger.IsClosed);
        }

        /// <summary>
        /// 繋いでいる最中に別の経路がそのセッションを終わらせたら、次の要求で断られる。次の要求が
        /// handshake の受け直しであっても同じで、そこを素通りすると終わったセッションが生き返る。
        /// </summary>
        [Theory]
        [InlineData("handshake")]
        [InlineData("ping")]
        public void AConnectionIsRefusedAtTheNextRequestAfterItsSessionEnds(string next)
        {
            JsonRpcConnection connection = Connection(new McpMethodTable());
            string request = next == "handshake" ? Handshake() : Request(2, "ping");

            using (ExchangeStream stream = new ExchangeStream(
                Lines(Handshake(), request, Request(3, "ping"))))
            {
                stream.AfterWrite = written =>
                {
                    // ハンドシェイクの応答を返したところで、要求の合間に外からセッションを
                    // 終わらせる。直列化の錠を持っていない時点なので、所有者の終了による回収と
                    // 同じ形になる。
                    if (written == 1)
                    {
                        connection.Sessions.End(SessionOf(stream.ReadResponses()[0]));
                    }
                };

                connection.Handle(stream, new InlineInvoker());

                IList<IDictionary<string, object>> responses = stream.ReadResponses();
                Assert.Equal(2, responses.Count);
                Assert.Equal(JsonRpcErrorCodes.SessionRefused, ErrorCodeOf(responses[1]));
            }

            Assert.Equal(0, connection.Sessions.Count);
        }

        [Fact]
        public void EndingMarksTheSessionAndClosesItsQueue()
        {
            JsonRpcConnection connection = Connection(new McpMethodTable());
            string id = SessionOf(Exchange(connection, Handshake())[0]);
            Session session = connection.Sessions.Find(id);

            Assert.True(connection.Sessions.End(id));
            Assert.True(session.IsEnded);
            Assert.True(session.Events.IsClosed);
            Assert.True(session.Handles.IsClosed);
        }

        /// <summary>
        /// セッションを終わらせる要求も、ほかの要求と同じ直列区間の中で処理する。外で走らせると、
        /// 別の接続が使っている最中の台帳と溜め場を閉じてしまう。
        /// </summary>
        [Fact]
        public void EndingASessionWaitsForTheRequestThatIsRunning()
        {
            JsonRpcConnection[] holder = new JsonRpcConnection[1];
            ExchangeStream[] streams = new ExchangeStream[1];
            Thread other = null;
            bool closedWhileWorking = false;

            McpMethodTable methods = new McpMethodTable();
            methods.Add("work", context =>
            {
                string id = SessionOf(streams[0].ReadResponses()[0]);

                // 走っている最中に、別の接続として終了の要求を出す。直列化していれば、こちらが
                // 戻るまで終わらないので、ここでは終わるのを待たない——待てば自分が解かない錠を
                // 待つことになる。
                other = new Thread(
                    () => Exchange(holder[0], Handshake(id), Request(2, "end_session")));
                other.Start();

                Thread.Sleep(100);
                closedWhileWorking = context.Handles.IsClosed || context.Events.IsClosed;

                return "ok";
            });

            JsonRpcConnection connection = Connection(methods);
            holder[0] = connection;

            using (ExchangeStream stream = new ExchangeStream(Lines(Handshake(), Request(2, "work"))))
            {
                streams[0] = stream;
                connection.Handle(stream, new InlineInvoker());
            }

            Assert.NotNull(other);
            Assert.True(other.Join(WaitLimit), "終了の要求が終わらない。");
            Assert.False(closedWhileWorking, "処理の最中に台帳か溜め場が閉じられた。");
            Assert.Equal(0, connection.Sessions.Count);
        }

        /// <summary>
        /// 所有者の終了による回収も、明示の終了と同じ直列区間を通る。通さないと、走っている要求が
        /// 使っている最中の台帳と溜め場を閉じてしまう。
        /// </summary>
        [Fact]
        public void TheOwnerLeavingWaitsForTheRequestThatIsRunning()
        {
            object serialGate = new object();
            SessionStore store = new SessionStore(
                _log, new HandleIdIssuer(), new EventSequenceIssuer(), serialGate);
            ManualResetEvent exit = new ManualResetEvent(false);

            Session session;
            Assert.True(store.TryResolve(null, new ClientProcess(ClientId, exit), out session));

            bool closedWhileWorking;
            lock (serialGate)
            {
                exit.Set();
                Thread.Sleep(200);
                closedWhileWorking = session.Handles.IsClosed || session.Events.IsClosed;
            }

            Assert.False(closedWhileWorking, "処理の最中に台帳か溜め場が閉じられた。");

            // 持ち物から除くのと台帳を閉じるのは別の段なので、閉じたことのほうを待つ。
            Assert.True(
                WaitUntil(() => session.Handles.IsClosed && session.Events.IsClosed),
                "所有者が終わっても台帳と溜め場が閉じられない。");
            Assert.Equal(0, store.Count);
        }

        [Fact]
        public void AnEndedQueueNeitherTakesNorGivesEvents()
        {
            EventQueue queue = new EventQueue(new EventSequenceIssuer());
            queue.Enqueue("pmx_view.mouse_down", 1, null);
            queue.Close();

            Assert.True(queue.IsClosed);
            Assert.Equal(0, queue.Count);
            Assert.Throws<InvalidOperationException>(
                () => queue.Enqueue("pmx_view.mouse_down", 1, null));
            Assert.Throws<InvalidOperationException>(() => queue.Drain(1));
        }

        [Fact]
        public void TheEndedIdentifierCannotBePresentedAgain()
        {
            JsonRpcConnection connection = Connection(new McpMethodTable());

            string id = SessionOf(Exchange(connection, Handshake(), Request(2, "end_session"))[0]);
            string issued = SessionOf(Exchange(connection, Handshake(id))[0]);

            Assert.NotEqual(id, issued);
        }

        [Fact]
        public void TheEndSessionNameCannotBeRegisteredAsATool()
        {
            Assert.Throws<ArgumentException>(
                () => new McpMethodTable().Add("end_session", context => null));
        }

        private SessionStore Store()
        {
            return new SessionStore(
                _log, new HandleIdIssuer(), new EventSequenceIssuer(), new object());
        }

        private JsonRpcConnection Connection(McpMethodTable methods)
        {
            return new JsonRpcConnection(
                _log,
                methods,
                HostVersion,
                BudgetChars,
                JsonRpcConnection.DefaultRequestTimeout,
                MessageChannel.DefaultMaxMessageBytes,
                StubClientProcess.Opener(ClientId, ClientId));
        }

        /// <summary>回収はスレッドプールの合図で走るので、数が落ち着くまで待つ。</summary>
        private static bool WaitForCount(SessionStore store, int count)
        {
            return WaitUntil(() => store.Count == count);
        }

        private static bool WaitUntil(Func<bool> condition)
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            while (!condition())
            {
                if (elapsed.Elapsed > WaitLimit)
                {
                    return false;
                }

                Thread.Sleep(10);
            }

            return true;
        }

        private static string Handshake()
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"handshake\",\"params\":{\"protocol\":1}}";
        }

        private static string Handshake(string session)
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"handshake\","
                + "\"params\":{\"protocol\":1,\"session\":\"" + session + "\"}}";
        }

        private static string Request(int id, string method)
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"" + method + "\"}";
        }

        private static byte[] Lines(params string[] requests)
        {
            return Utf8WithoutBom.GetBytes(string.Join("\n", requests) + "\n");
        }

        private static int ErrorCodeOf(IDictionary<string, object> response)
        {
            object error;
            Assert.True(response.TryGetValue("error", out error), "エラーの応答ではない。");

            IDictionary<string, object> body =
                Assert.IsAssignableFrom<IDictionary<string, object>>(error);

            return Convert.ToInt32(body["code"]);
        }

        private static object ResultOf(IDictionary<string, object> response)
        {
            object result;
            Assert.True(response.TryGetValue("result", out result), "成功の応答ではない。");

            return result;
        }

        private static string SessionOf(IDictionary<string, object> response)
        {
            IDictionary<string, object> body =
                Assert.IsAssignableFrom<IDictionary<string, object>>(ResultOf(response));

            return (string)body["session"];
        }

        private static IList<IDictionary<string, object>> Exchange(
            JsonRpcConnection connection, params string[] requests)
        {
            byte[] input = Utf8WithoutBom.GetBytes(string.Join("\n", requests) + "\n");
            using (ExchangeStream stream = new ExchangeStream(input))
            {
                connection.Handle(stream, new InlineInvoker());

                return stream.ReadResponses();
            }
        }
    }
}
