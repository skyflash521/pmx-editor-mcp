using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 行キーからSDKへの中継。解決できない行はその行だけを断り、待受も他の行も止めない。
    /// </summary>
    public sealed class SdkRelayTests : IDisposable
    {
        private const string HostVersion = "1.2.3.4";

        private const int BudgetChars = 100000;

        private const int ClientId = 4321;

        private const string GeneratedVersion = "0.0.8.9";

        private const string Digest = "8f14e45fceea167a5a36dedd4bea2543";

        private const string LiveKey = "Sdk.Type.Live()";

        private const string LostKey = "Sdk.Type.Lost()";

        private const string UnresolvedKey = "Sdk.Type.Unresolved()";

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private readonly string _directory;

        private readonly HostLog _log;

        public SdkRelayTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-relay-" + Guid.NewGuid().ToString("N"));
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
        }

        [Fact]
        public void ARowThatTheTableDoesNotCarryIsRefusedAsUnknown()
        {
            object result;
            SdkRelayRefusal refusal;

            Assert.False(Table().TryInvoke("Sdk.Type.Absent()", null, null, out result, out refusal));
            Assert.Equal(SdkRelayRefusal.Unknown, refusal);
            Assert.Null(result);
        }

        [Fact]
        public void ARowLeftUnresolvedAtGenerationIsRefusedWithoutTouchingTheOthers()
        {
            SdkRelayTable table = Table();

            object result;
            SdkRelayRefusal refusal;
            Assert.False(table.TryInvoke(UnresolvedKey, null, null, out result, out refusal));
            Assert.Equal(SdkRelayRefusal.Unresolved, refusal);
            Assert.Equal(new[] { UnresolvedKey }, table.Unresolved);

            Assert.True(table.TryInvoke(LiveKey, null, null, out result, out refusal));
            Assert.Equal(SdkRelayRefusal.None, refusal);
            Assert.Equal("live", result);
        }

        [Fact]
        public void AMemberThatTheLoadedSdkHasLostDisablesThatRowAlone()
        {
            SdkRelayTable table = Table();

            object result;
            SdkRelayRefusal refusal;
            Assert.False(table.TryInvoke(LostKey, null, null, out result, out refusal));
            Assert.Equal(SdkRelayRefusal.Disabled, refusal);
            Assert.Equal(new[] { LostKey }, table.Disabled);

            Assert.False(table.TryInvoke(LostKey, null, null, out result, out refusal));
            Assert.Equal(SdkRelayRefusal.Disabled, refusal);

            Assert.True(table.TryInvoke(LiveKey, null, null, out result, out refusal));
            Assert.Equal("live", result);
        }

        [Fact]
        public void AMemberThatTheLoadedSdkNoLongerPublishesDisablesThatRowToo()
        {
            Dictionary<string, SdkCall> calls = new Dictionary<string, SdkCall>(StringComparer.Ordinal)
            {
                { LostKey, (target, arguments) => { throw new MethodAccessException("Sdk.Type.Lost"); } },
            };
            SdkRelayTable table = new SdkRelayTable(GeneratedVersion, Digest, calls, new string[0]);

            object result;
            SdkRelayRefusal refusal;
            Assert.False(table.TryInvoke(LostKey, null, null, out result, out refusal));
            Assert.Equal(SdkRelayRefusal.Disabled, refusal);
            Assert.Equal(new[] { LostKey }, table.Disabled);
        }

        [Fact]
        public void ATypeThatTheLoadedSdkNoLongerCarriesDisablesThatRowToo()
        {
            Dictionary<string, SdkCall> calls = new Dictionary<string, SdkCall>(StringComparer.Ordinal)
            {
                { LostKey, (target, arguments) => { throw new TypeLoadException("Sdk.Type"); } },
            };
            SdkRelayTable table = new SdkRelayTable(GeneratedVersion, Digest, calls, new string[0]);

            object result;
            SdkRelayRefusal refusal;
            Assert.False(table.TryInvoke(LostKey, null, null, out result, out refusal));
            Assert.Equal(SdkRelayRefusal.Disabled, refusal);
        }

        [Fact]
        public void AFailureOfTheCallItselfIsNotTakenForAMissingMember()
        {
            Dictionary<string, SdkCall> calls = new Dictionary<string, SdkCall>(StringComparer.Ordinal)
            {
                { LiveKey, (target, arguments) => { throw new InvalidOperationException("SDKが断った。"); } },
            };
            SdkRelayTable table = new SdkRelayTable(GeneratedVersion, Digest, calls, new string[0]);

            object result;
            SdkRelayRefusal refusal;
            Assert.Throws<InvalidOperationException>(
                () => table.TryInvoke(LiveKey, null, null, out result, out refusal));
            Assert.Empty(table.Disabled);
        }

        [Fact]
        public void TheCallReceivesTheTargetAndTheArgumentsInOrder()
        {
            object seen = null;
            object[] taken = null;
            Dictionary<string, SdkCall> calls = new Dictionary<string, SdkCall>(StringComparer.Ordinal)
            {
                {
                    LiveKey,
                    (target, arguments) =>
                    {
                        seen = target;
                        taken = arguments;

                        return null;
                    }
                },
            };
            SdkRelayTable table = new SdkRelayTable(GeneratedVersion, Digest, calls, new string[0]);
            object receiver = new object();

            object result;
            SdkRelayRefusal refusal;
            Assert.True(table.TryInvoke(LiveKey, receiver, new object[] { 1, "two" }, out result, out refusal));
            Assert.Same(receiver, seen);
            Assert.Equal(new object[] { 1, "two" }, taken);
        }

        [Fact]
        public void ACallWithoutArgumentsReceivesAnEmptyList()
        {
            object[] taken = null;
            Dictionary<string, SdkCall> calls = new Dictionary<string, SdkCall>(StringComparer.Ordinal)
            {
                {
                    LiveKey,
                    (target, arguments) =>
                    {
                        taken = arguments;

                        return null;
                    }
                },
            };
            SdkRelayTable table = new SdkRelayTable(GeneratedVersion, Digest, calls, new string[0]);

            object result;
            SdkRelayRefusal refusal;
            Assert.True(table.TryInvoke(LiveKey, null, null, out result, out refusal));
            Assert.Empty(taken);
        }

        [Fact]
        public void TheStatusEntryCarriesBothVersionsAndTheRowsThatAreRefused()
        {
            SdkRelayTable table = Table();

            object result;
            SdkRelayRefusal refusal;
            Assert.False(table.TryInvoke(LostKey, null, null, out result, out refusal));

            JsonRpcConnection connection = Connection(table, "0.0.9.0");
            IDictionary<string, object> body = ResultOf(
                Exchange(connection, Handshake(), Request(2, "sdk_status"))[1]);

            Assert.Equal("0.0.9.0", body["runningSdkVersion"]);
            Assert.Equal(GeneratedVersion, body["generatedSdkVersion"]);
            Assert.Equal(new object[] { UnresolvedKey }, Assert.IsType<object[]>(body["unresolvedRows"]));
            Assert.Equal(new object[] { LostKey }, Assert.IsType<object[]>(body["disabledRows"]));
        }

        /// <summary>
        /// 中継の状態が、このホストが答えるツールの名前も並べる。ブリッジが公開する名前と
        /// 突き合わせる先がこれで、名前の食い違いは呼んでみるまで分からない。
        /// </summary>
        [Fact]
        public void TheStatusEntryNamesTheToolsThisHostAnswers()
        {
            McpMethodTable methods = new McpMethodTable();
            methods.Add("model_list_vertices", context => null);
            methods.Add("model_add_vertices", context => null);

            IDictionary<string, object> body = ResultOf(
                Exchange(
                    Connection(Table(), "0.0.8.9", methods),
                    Handshake(),
                    Request(2, "sdk_status"))[1]);

            Assert.Equal(
                new object[] { "model_add_vertices", "model_list_vertices" },
                Assert.IsType<object[]>(body["toolNames"]));
        }

        [Fact]
        public void TheMethodTableNamesWhatItCarriesInSpellingOrder()
        {
            McpMethodTable methods = new McpMethodTable();
            methods.Add("model_list_vertices", context => null);
            methods.Add("model_add_vertices", context => null);

            Assert.Equal(new[] { "model_add_vertices", "model_list_vertices" }, methods.Names);
        }

        [Fact]
        public void TheHandshakeNamesTheDigestOfTheTableTheRelayWasBuiltFrom()
        {
            JsonRpcConnection connection = Connection(Table(), "0.0.8.9");

            IDictionary<string, object> body = ResultOf(Exchange(connection, Handshake())[0]);

            Assert.Equal(Digest, body["toolMapDigest"]);
        }

        [Fact]
        public void TheStatusNameCannotBeRegisteredAsATool()
        {
            Assert.Throws<ArgumentException>(
                () => new McpMethodTable().Add("sdk_status", context => null));
        }

        /// <summary>
        /// 中継できる行・読み込まれたSDKが失った行・生成の時点で解決できなかった行を1つずつ持つ表。
        /// </summary>
        private static SdkRelayTable Table()
        {
            Dictionary<string, SdkCall> calls = new Dictionary<string, SdkCall>(StringComparer.Ordinal)
            {
                { LiveKey, (target, arguments) => "live" },
                { LostKey, (target, arguments) => { throw new MissingMethodException("Sdk.Type", "Lost"); } },
            };

            return new SdkRelayTable(GeneratedVersion, Digest, calls, new[] { UnresolvedKey });
        }

        private JsonRpcConnection Connection(
            SdkRelayTable relays, string sdkVersion, McpMethodTable methods = null)
        {
            return new JsonRpcConnection(
                _log,
                methods ?? new McpMethodTable(),
                HostVersion,
                BudgetChars,
                JsonRpcConnection.DefaultRequestTimeout,
                MessageChannel.DefaultMaxMessageBytes,
                StubClientProcess.Opener(ClientId),
                relays,
                sdkVersion);
        }

        private static string Handshake()
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"handshake\",\"params\":{\"protocol\":1}}";
        }

        private static string Request(int id, string method)
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"" + method + "\"}";
        }

        private static IList<IDictionary<string, object>> Exchange(
            JsonRpcConnection connection, params string[] requests)
        {
            using (ExchangeStream stream = new ExchangeStream(
                Utf8WithoutBom.GetBytes(string.Join("\n", requests) + "\n")))
            {
                connection.Handle(stream, new InlineInvoker());

                return stream.ReadResponses();
            }
        }

        private static IDictionary<string, object> ResultOf(IDictionary<string, object> response)
        {
            object result;
            Assert.True(response.TryGetValue("result", out result), "成功の応答ではない。");

            return Assert.IsAssignableFrom<IDictionary<string, object>>(result);
        }
    }
}
