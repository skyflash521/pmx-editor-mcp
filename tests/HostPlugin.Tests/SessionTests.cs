using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 接続の確立でセッションを識別し、切断では手放さない振る舞い。所有者は接続元のプロセスで、
    /// 繋ぎ直しはそのプロセスからの提示だけが認められる。
    /// </summary>
    public sealed class SessionTests : IDisposable
    {
        private const string HostVersion = "1.2.3.4";

        private const int BudgetChars = 100000;

        private const int ClientId = 4321;

        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private readonly string _root;

        private readonly HostLog _log;

        public SessionTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-sessions-" + Guid.NewGuid().ToString("N"));
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
        public void AConnectionThatPresentsNothingGetsANewIdentifier()
        {
            JsonRpcConnection connection = Connection(StubClientProcess.Opener(ClientId));

            string first = SessionOf(Exchange(connection, Handshake())[0]);
            string second = SessionOf(Exchange(connection, Handshake())[0]);

            Assert.False(string.IsNullOrEmpty(first));
            Assert.NotEqual(first, second);
            Assert.Equal(2, connection.Sessions.Count);
        }

        [Fact]
        public void TheIdentifierIsAHexTextOfSixteenBytes()
        {
            string id = SessionOf(
                Exchange(Connection(StubClientProcess.Opener(ClientId)), Handshake())[0]);

            Assert.Equal(32, id.Length);
            Assert.All(id, c => Assert.True(
                (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'), "16進の文字でない: " + c));
        }

        [Fact]
        public void EachIdentifierHasItsOwnLedgerAndQueue()
        {
            List<McpMethodContext> seen = new List<McpMethodContext>();
            McpMethodTable methods = new McpMethodTable();
            methods.Add("note", context => { seen.Add(context); return "ok"; });
            JsonRpcConnection connection = Connection(StubClientProcess.Opener(ClientId), methods);

            Exchange(connection, Handshake(), Request(2, "note"));
            Exchange(connection, Handshake(), Request(2, "note"));

            Assert.Equal(2, seen.Count);
            Assert.NotSame(seen[0].Handles, seen[1].Handles);
            Assert.NotSame(seen[0].Events, seen[1].Events);
        }

        [Fact]
        public void AConnectionWhoseProcessCannotBeOpenedIsRefused()
        {
            IDictionary<string, object> response =
                Exchange(Connection(StubClientProcess.FailingOpener()), Handshake())[0];

            Assert.Equal(JsonRpcErrorCodes.SessionRefused, ErrorCodeOf(response));
        }

        [Fact]
        public void AConnectionWhoseProcessHasAlreadyExitedIsRefused()
        {
            IDictionary<string, object> response =
                Exchange(Connection(StubClientProcess.ExitedOpener(ClientId)), Handshake())[0];

            Assert.Equal(JsonRpcErrorCodes.SessionRefused, ErrorCodeOf(response));
        }

        [Fact]
        public void AnIdentifierNobodyKnowsGetsANewOneInTheResponse()
        {
            JsonRpcConnection connection = Connection(StubClientProcess.Opener(ClientId));

            string issued = SessionOf(Exchange(connection, Handshake("しらないせっしょん"))[0]);

            Assert.False(string.IsNullOrEmpty(issued));
            Assert.NotEqual("しらないせっしょん", issued);
        }

        [Fact]
        public void ThePresentedIdentifierComesBackToTheSameLedgerAndQueue()
        {
            List<McpMethodContext> seen = new List<McpMethodContext>();
            McpMethodTable methods = new McpMethodTable();
            methods.Add("note", context => { seen.Add(context); return "ok"; });
            JsonRpcConnection connection = Connection(StubClientProcess.Opener(ClientId), methods);

            string id = SessionOf(Exchange(connection, Handshake(), Request(2, "note"))[0]);
            IDictionary<string, object> again =
                Exchange(connection, Handshake(id), Request(2, "note"))[0];

            Assert.Equal(id, SessionOf(again));
            Assert.Equal(2, seen.Count);
            Assert.Same(seen[0].Handles, seen[1].Handles);
            Assert.Same(seen[0].Events, seen[1].Events);
            Assert.Equal(1, connection.Sessions.Count);
        }

        [Fact]
        public void AHandleSurvivesTheDisconnectAndIsUsableAgain()
        {
            HandleLedger ledger = null;
            McpMethodTable methods = new McpMethodTable();
            methods.Add("issue", context =>
            {
                ledger = context.Handles;
                return context.Handles.Issue("test", new object(), () => { });
            });
            JsonRpcConnection connection = Connection(StubClientProcess.Opener(ClientId), methods);

            string id = SessionOf(Exchange(connection, Handshake(), Request(2, "issue"))[0]);
            Exchange(connection, Handshake(id));

            Assert.NotNull(ledger);
            Assert.False(ledger.IsClosed);
            Assert.Equal(1, ledger.Count);
        }

        /// <summary>
        /// セッションの集まりはホストにひとつなので、別のプロセスからの提示は同じホストへ届く。
        /// 2本目の接続の接続元だけを別のプロセスにして確かめる。
        /// </summary>
        [Fact]
        public void AnIdentifierPresentedFromAnotherProcessIsRefused()
        {
            JsonRpcConnection connection = Connection(
                StubClientProcess.Opener(ClientId, ClientId + 1));

            string id = SessionOf(Exchange(connection, Handshake())[0]);
            IDictionary<string, object> response = Exchange(connection, Handshake(id))[0];

            Assert.Equal(JsonRpcErrorCodes.SessionRefused, ErrorCodeOf(response));
        }

        /// <summary>
        /// 例外で抜けた接続でも、所有者のハンドルはセッションのものなので閉じない。閉じると、
        /// 同じ識別子で繋ぎ直したときに終わったかどうかを判じられなくなる。
        /// </summary>
        [Fact]
        public void TheOwnerSurvivesAConnectionThatEndsWithAnException()
        {
            JsonRpcConnection connection = Connection(StubClientProcess.Opener(ClientId, ClientId));

            string id;
            using (ExchangeStream stream = new ExchangeStream(Lines(Handshake(), Request(2, "ping"))))
            {
                // ハンドシェイクの応答だけ通し、その次の応答は書けなくする。
                stream.FailWritesAfter(1);
                Assert.Throws<IOException>(() => connection.Handle(stream, new InlineInvoker()));
                id = SessionOf(stream.ReadResponses()[0]);
            }

            Assert.Equal(id, SessionOf(Exchange(connection, Handshake(id))[0]));
        }

        [Fact]
        public void AnIdentifierThatIsNotTextIsInvalidArguments()
        {
            IDictionary<string, object> response = Exchange(
                Connection(StubClientProcess.Opener(ClientId)),
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"handshake\","
                    + "\"params\":{\"protocol\":1,\"session\":7}}")[0];

            Assert.Equal(JsonRpcErrorCodes.InvalidParams, ErrorCodeOf(response));
        }

        [Fact]
        public void TheStoreRefusesAnIdentifierWhoseOwnerHasExited()
        {
            SessionStore store = new SessionStore(
                _log, new HandleIdIssuer(), new EventSequenceIssuer());
            ClientProcess owner = StubClientProcess.Exited(ClientId);

            Session created;
            Assert.True(store.TryResolve(null, owner, out created));

            Session reconnected;
            Assert.False(
                store.TryResolve(created.Id, StubClientProcess.Living(ClientId), out reconnected));
            Assert.Null(reconnected);
        }

        [Fact]
        public void TheStoreInputsAreRequired()
        {
            SessionStore store = new SessionStore(
                _log, new HandleIdIssuer(), new EventSequenceIssuer());
            Session session;

            Assert.Throws<ArgumentNullException>(() => store.TryResolve(null, null, out session));
            Assert.Throws<ArgumentNullException>(
                () => new SessionStore(null, new HandleIdIssuer(), new EventSequenceIssuer()));
            Assert.Throws<ArgumentNullException>(
                () => new SessionStore(_log, null, new EventSequenceIssuer()));
            Assert.Throws<ArgumentNullException>(
                () => new SessionStore(_log, new HandleIdIssuer(), null));
        }

        private JsonRpcConnection Connection(ClientProcessOpener opener)
        {
            return Connection(opener, new McpMethodTable());
        }

        private JsonRpcConnection Connection(ClientProcessOpener opener, McpMethodTable methods)
        {
            return new JsonRpcConnection(
                _log,
                methods,
                HostVersion,
                BudgetChars,
                JsonRpcConnection.DefaultRequestTimeout,
                MessageChannel.DefaultMaxMessageBytes,
                opener);
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

        private static string SessionOf(IDictionary<string, object> response)
        {
            object result;
            Assert.True(response.TryGetValue("result", out result), "成功の応答ではない。");

            IDictionary<string, object> body =
                Assert.IsAssignableFrom<IDictionary<string, object>>(result);

            return (string)body["session"];
        }

        private static int ErrorCodeOf(IDictionary<string, object> response)
        {
            object error;
            Assert.True(response.TryGetValue("error", out error), "エラーの応答ではない。");

            IDictionary<string, object> body =
                Assert.IsAssignableFrom<IDictionary<string, object>>(error);

            return Convert.ToInt32(body["code"]);
        }

        private static byte[] Lines(params string[] requests)
        {
            return Utf8WithoutBom.GetBytes(string.Join("\n", requests) + "\n");
        }

        private static IList<IDictionary<string, object>> Exchange(
            JsonRpcConnection connection, params string[] requests)
        {
            using (ExchangeStream stream = new ExchangeStream(Lines(requests)))
            {
                connection.Handle(stream, new InlineInvoker());

                return stream.ReadResponses();
            }
        }
    }
}
