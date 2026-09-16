using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using PmxEditorMcp.SignatureDump;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    /// <summary>
    /// 既定の設定で起こしたブリッジ。設定を変えず読むだけの件はこれを共有する——件ごとに
    /// 起こすと、確かめる事柄の数だけ起動と畳みの待ちが積み上がる。設定を変える件と、ホストへ
    /// 繋ぐ件は、自分で起こして自分で畳む。
    /// </summary>
    public sealed class SharedBridge : IAsyncLifetime
    {
        /// <summary>共有するクライアント。どの件も設定を変えずに読むだけである。</summary>
        public McpClient Client { get; private set; }

        public async Task InitializeAsync()
        {
            using (CancellationTokenSource limit =
                new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                Client = await BridgeToolsTests.StartBridgeAsync(null, null, limit.Token);
            }
        }

        public async Task DisposeAsync()
        {
            if (Client != null)
            {
                await Client.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// ブリッジをMCPサーバーとして起動し、クライアントから見える契約を確かめる。ツール定義も
    /// ツールの中継も、実行ファイルを起動して stdio 越しに見る——登録の配線まで含めて確かめたい
    /// ので、SDKの型を直接読むのでは通らない経路が残る。
    /// </summary>
    public class BridgeToolsTests : IClassFixture<SharedBridge>
    {
        private readonly SharedBridge _shared;

        public BridgeToolsTests(SharedBridge shared)
        {
            _shared = shared;
        }

        /// <summary>
        /// 接続先として読んではならない環境変数の名前。接頭辞が同じで紛らわしいので、
        /// 子プロセスへ渡す環境からは必ず消し、読まれていないことも確かめる。
        /// </summary>
        private const string IgnoredPipeEnvironmentVariableName = "PMX_EDITOR_MCP_PIPE";

        private static readonly TimeSpan TestWait = TimeSpan.FromSeconds(60);

        /// <summary>
        /// 起動したブリッジを畳むときの待ち。クライアントのSDKはここへ与えた時間をいっぱいまで
        /// 待ってから落とすので、既定の5秒だとこの組の1件ごとに5秒が乗る。ブリッジは stdin が
        /// 閉じれば0.1秒とかからず終わるので、待つのはその余裕だけでよい。
        /// </summary>
        private static readonly TimeSpan ShutdownWait = TimeSpan.FromMilliseconds(500);

        [Fact]
        public void ResultSizeDeclarationKeyMatchesContract()
        {
            Assert.Equal("anthropic/maxResultSizeChars", BridgeTools.ResultSizeMetaKey);
        }

        [Fact]
        public async Task ServerAnnouncesContractNameAndBridgeVersion()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            McpClient client = _shared.Client;

            Assert.Equal("pmx-editor-mcp", client.ServerInfo.Name);
            Assert.Equal(
                typeof(BridgeServer).Assembly.GetName().Version.ToString(),
                client.ServerInfo.Version);
        }

        [Fact]
        public async Task TheBaseRelayAndEveryGeneratedDefinitionAreRegistered()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            McpClient client = _shared.Client;

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);

            Assert.Equal(Expected(false), Sorted(tools.Select(tool => tool.Name)));
        }

        [Fact]
        public async Task AGeneratedDefinitionKeepsItsDescriptionAndInputSchema()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            McpClient client = _shared.Client;

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);

            foreach (GeneratedToolDefinition definition in GeneratedToolDefinitions.Create())
            {
                McpClientTool tool = Named(tools, definition.Name);
                Assert.Equal(definition.Description, tool.Description);
                Assert.Equal(
                    definition.InputSchema,
                    tool.ProtocolTool.InputSchema.GetRawText());
            }
        }

        [Fact]
        public async Task TheLargeTextToolAppearsOnlyWithTheDebugEntry()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient opened = await StartBridgeAsync(null, null, limit.Token, "1", null);

            IList<McpClientTool> tools = await opened.ListToolsAsync(cancellationToken: limit.Token);

            // ツールの一覧の並びは ModelContextProtocol のサーバーが決める。
            Assert.Equal(Expected(true), Sorted(tools.Select(tool => tool.Name)));
        }

        [Fact]
        public async Task TheLargeTextToolRelaysTheRequestedNumberOfCharacters()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BridgeBudget.DefaultChars))
                .Reply(request => Result(request, "\"xxx\""))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(
                host.PipeName, null, limit.Token, "1", null);

            CallToolResult result = await client.CallToolAsync(
                BridgeTools.LargeTextMethod,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    [BridgeTools.LargeTextCharsParameter] = 3,
                },
                cancellationToken: limit.Token);

            Assert.NotEqual(true, result.IsError);
            Assert.Equal(Relayed(host.PipeName, "xxx"), TextOf(result));
            Assert.Equal(
                new string[] { "handshake", BridgeTools.LargeTextMethod },
                MethodsOf(host.Requests));
            Assert.Equal(3, CharsOf(host.Requests[1]));
        }

        [Fact]
        public async Task ToolDefinitionDeclaresDefaultResponseBudget()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            McpClient client = _shared.Client;

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);

            Assert.Equal(BridgeBudget.DefaultChars, DeclaredResultSize(Named(tools, "ping")));
        }

        [Fact]
        public async Task ToolDefinitionDeclaresBudgetOverriddenByEnvironmentVariable()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(null, "250000", limit.Token);

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);

            Assert.Equal(250000, DeclaredResultSize(Named(tools, "ping")));
        }

        [Fact]
        public async Task ToolDefinitionDropsTheDeclarationWhenBothVariablesAreGiven()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(
                null, null, limit.Token, "1", "0");

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);

            Assert.Equal(Expected(true).Length, tools.Count);
            Assert.All(tools, tool => Assert.Null(tool.ProtocolTool.Meta));
        }

        [Theory]
        [InlineData(null, "0")]
        [InlineData("0", "0")]
        [InlineData("1", null)]
        [InlineData("1", "1")]
        public async Task ToolDefinitionKeepsTheDeclarationWithoutBothVariables(
            string debugHooks, string declareMeta)
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(
                null, null, limit.Token, debugHooks, declareMeta);

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);

            Assert.Equal(Expected(BridgeDebugHooks.IsEnabled(debugHooks)).Length, tools.Count);
            Assert.All(
                tools,
                tool => Assert.Equal(BridgeBudget.DefaultChars, DeclaredResultSize(tool)));
        }

        [Fact]
        public async Task ToolDefinitionIsAvailableWithoutHostConnection()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(
                "pmx-editor-mcp-test-" + Guid.NewGuid().ToString("N"), null, limit.Token);

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);

            Assert.Equal(BridgeBudget.DefaultChars, DeclaredResultSize(Named(tools, "ping")));
        }

        [Fact]
        public async Task ToolCallRelaysToHostAndReturnsResponse()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BridgeBudget.DefaultChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(host.PipeName, null, limit.Token);

            CallToolResult result = await client.CallToolAsync(
                "ping", cancellationToken: limit.Token);

            // 失敗の印は省略できる(省略は偽の意味)ので、真でないことを見る。
            Assert.NotEqual(true, result.IsError);
            Assert.Equal(Relayed(host.PipeName, "pong"), TextOf(result));

            // ホストが受け取ったのは handshake と ping で、名前を作り替えていない。
            Assert.Equal(new string[] { "handshake", "ping" }, MethodsOf(host.Requests));
        }

        /// <summary>
        /// 中継の状態もツールとして公開し、ホストの同名のメソッドへそのまま渡す。E2Eの実行器が
        /// 走らせる前後で読む先がこれで、ブリッジ越しに読めないと走らせた側から確かめられない。
        /// </summary>
        [Fact]
        public async Task TheStatusToolRelaysToTheHost()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BridgeBudget.DefaultChars))
                .Reply(request => Result(request, "{\"runningSdkVersion\":\"0.0.8.9\"}"))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(host.PipeName, null, limit.Token);

            CallToolResult result = await client.CallToolAsync(
                "sdk_status", cancellationToken: limit.Token);

            Assert.NotEqual(true, result.IsError);
            Assert.Equal(
                Relayed(host.PipeName, "{\"runningSdkVersion\":\"0.0.8.9\"}"), TextOf(result));
            Assert.Equal(
                new string[] { "handshake", "sdk_status" }, MethodsOf(host.Requests));
        }

        [Fact]
        public async Task AGeneratedToolReturnsTheValueOutOfTheEnvelope()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BridgeBudget.DefaultChars))
                .Reply(request => Result(request, "{\"ok\":true,\"value\":{\"total\":1}}"))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(host.PipeName, null, limit.Token);

            CallToolResult result = await client.CallToolAsync(
                GeneratedName(), cancellationToken: limit.Token);

            Assert.NotEqual(true, result.IsError);
            Assert.Equal(Relayed(host.PipeName, "{\"total\":1}"), TextOf(result));
        }

        [Fact]
        public async Task AGeneratedToolThatTheHostRefusesComesBackAsAnError()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BridgeBudget.DefaultChars))
                .Reply(request => Result(
                    request,
                    "{\"ok\":false,\"error\":{\"code\":\"TOOL_CONFIRM_REQUIRED\""
                        + ",\"message\":\"確認が要る。\"}}"))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(host.PipeName, null, limit.Token);

            CallToolResult result = await client.CallToolAsync(
                GeneratedName(), cancellationToken: limit.Token);

            Assert.True(result.IsError);
            Assert.Equal(
                Relayed(host.PipeName, "TOOL_CONFIRM_REQUIRED: 確認が要る。"), TextOf(result));
        }

        [Fact]
        public async Task AGeneratedToolThatDoesNotComeBackInAnEnvelopeIsAProtocolError()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BridgeBudget.DefaultChars))
                .Reply(request => Result(request, "\"包みではない\""))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(host.PipeName, null, limit.Token);

            CallToolResult result = await client.CallToolAsync(
                GeneratedName(), cancellationToken: limit.Token);

            Assert.True(result.IsError);
            Assert.StartsWith(
                BridgeErrorCodes.ProtocolError, TextOf(result), StringComparison.Ordinal);
        }

        [Fact]
        public async Task RelayFailureReturnsToolErrorWithoutCrashing()
        {
            // ホストの応答サイズ予算をブリッジと食い違わせる。待ちに入らず決まった失敗になる。
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BridgeBudget.MaximumChars))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(host.PipeName, null, limit.Token);

            CallToolResult result = await client.CallToolAsync(
                "ping", cancellationToken: limit.Token);

            Assert.True(result.IsError);
            Assert.StartsWith(BridgeErrorCodes.BudgetMismatch + ": ", TextOf(result));

            // 失敗してもプロセスは生きているので、続けて応答できる。
            Assert.NotEmpty(await client.ListToolsAsync(cancellationToken: limit.Token));
        }

        [Fact]
        public async Task TestOnlyEnvironmentVariablePinsRelayTarget()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BridgeBudget.DefaultChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeWithAsync(
                PipeTargetResolver.TestPipeEnvironmentVariableName, host.PipeName, null, limit.Token);

            CallToolResult result = await client.CallToolAsync(
                "ping", cancellationToken: limit.Token);

            Assert.NotEqual(true, result.IsError);
            Assert.Equal(Relayed(host.PipeName, "pong"), TextOf(result));

            // 応答の中身だけでは、指定を無視して別の相手を見つけた実装も通る。この待受が
            // 要求を受け取ったことまでを見て、指定が効いていることを確かめる。
            Assert.Equal(new string[] { "handshake", "ping" }, MethodsOf(host.Requests));
        }

        [Fact]
        public async Task WithoutTargetSettingListeningHostIsDiscovered()
        {
            // ホストの名乗り方どおりの名前で待ち受け、接続先の指定を与えずに起動する。実機の
            // ホストが同時に待ち受けていると候補が増えるので、その場合は候補として挙がるところ
            // までを見る。どちらの結果も、待ち受けているパイプを列挙していなければ出ない。
            using FakeHost host = new FakeHost(PipeTargetResolver.PipeNameForProcess(Environment.ProcessId))
                .Reply(HandshakeResultOf(BridgeBudget.DefaultChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeWithAsync(null, null, null, limit.Token);

            AssertFoundByEnumeration(
                await client.CallToolAsync("ping", cancellationToken: limit.Token), host);
        }

        [Fact]
        public async Task OnlyTestOnlyEnvironmentVariableNamesTheTarget()
        {
            using FakeHost host = new FakeHost(PipeTargetResolver.PipeNameForProcess(Environment.ProcessId))
                .Reply(HandshakeResultOf(BridgeBudget.DefaultChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeWithAsync(
                IgnoredPipeEnvironmentVariableName,
                "pmx-editor-mcp-test-" + Guid.NewGuid().ToString("N"),
                null,
                limit.Token);

            AssertFoundByEnumeration(
                await client.CallToolAsync("ping", cancellationToken: limit.Token), host);
        }

        [Fact]
        public async Task SuccessfulResultAnnouncesTargetOnFirstLine()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BridgeBudget.DefaultChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeWithAsync(
                PipeTargetResolver.TestPipeEnvironmentVariableName, host.PipeName, null, limit.Token);

            CallToolResult result = await client.CallToolAsync(
                "ping", cancellationToken: limit.Token);

            Assert.NotEqual(true, result.IsError);
            Assert.Equal("接続先: " + host.PipeName + "\npong", TextOf(result));
        }

        [Fact]
        public async Task LaterSuccessfulResultsAlsoAnnounceTarget()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BridgeBudget.DefaultChars))
                .Reply(request => Result(request, "\"pong\""))
                .Reply(request => Result(request, "\"pong\""))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeWithAsync(
                PipeTargetResolver.TestPipeEnvironmentVariableName, host.PipeName, null, limit.Token);

            await client.CallToolAsync("ping", cancellationToken: limit.Token);
            CallToolResult second = await client.CallToolAsync(
                "ping", cancellationToken: limit.Token);

            Assert.NotEqual(true, second.IsError);
            Assert.Equal(Relayed(host.PipeName, "pong"), TextOf(second));
        }

        [Fact]
        public async Task FailedResultReturnsOnlyCodeAndDescription()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BridgeBudget.MaximumChars))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeWithAsync(
                PipeTargetResolver.TestPipeEnvironmentVariableName, host.PipeName, null, limit.Token);

            CallToolResult result = await client.CallToolAsync(
                "ping", cancellationToken: limit.Token);

            Assert.True(result.IsError);

            // 行が増えていないことまで見る。何かを足せる余地を残すと、形が崩れても通る。
            Assert.StartsWith(BridgeErrorCodes.BudgetMismatch + ": ", TextOf(result));
            Assert.DoesNotContain("\n", TextOf(result));
        }

        /// <summary>
        /// 待受の列挙で相手を見つけたことを確かめる。実機のホストが同時に待ち受けていると
        /// 候補が増えるので、その場合は候補として挙がるところまでを見る。どちらの結果も、
        /// 待ち受けているパイプを列挙していなければ出ない。
        /// </summary>
        private static void AssertFoundByEnumeration(CallToolResult result, FakeHost host)
        {
            if (result.IsError == true)
            {
                // 候補が増えるのは、この待受のほかにも待ち受けているホストが在るからである。
                // 自分の待受だけを候補に挙げて複数と数える実装は、この検査を通らない。
                Assert.StartsWith(BridgeErrorCodes.MultipleHosts + ": ", TextOf(result));

                string[] candidates = Array.FindAll(
                    TextOf(result).Split('\n'),
                    line => line.StartsWith(PipeTargetResolver.PipeNamePrefix));

                Assert.Contains(host.PipeName, candidates);
                Assert.True(candidates.Distinct().Count() >= 2);
            }
            else
            {
                Assert.Equal(Relayed(host.PipeName, "pong"), TextOf(result));

                // 応答の中身だけでは、別の相手が同じ本文を返しても通る。この待受が要求を
                // 受け取ったことまでを見て、繋いだ先がここであることを確かめる。
                Assert.Equal(new string[] { "handshake", "ping" }, MethodsOf(host.Requests));
            }
        }

        /// <summary>
        /// ブリッジの実行ファイルをMCPサーバーとして起動する。接続先と応答サイズ予算は、
        /// このテストを走らせるプロセスの環境に左右されないよう明示して渡す。
        /// </summary>
        internal static Task<McpClient> StartBridgeAsync(
            string pipeName,
            string budgetChars,
            CancellationToken cancellationToken,
            string debugHooks = null,
            string declareMeta = null)
        {
            return StartBridgeWithAsync(
                PipeTargetResolver.TestPipeEnvironmentVariableName,
                pipeName,
                budgetChars,
                cancellationToken,
                debugHooks,
                declareMeta);
        }

        /// <summary>接続先を指定する環境変数の名前を選んでブリッジを起動する。</summary>
        private static Task<McpClient> StartBridgeWithAsync(
            string pipeEnvironmentVariableName,
            string pipeName,
            string budgetChars,
            CancellationToken cancellationToken,
            string debugHooks = null,
            string declareMeta = null)
        {
            // 接続先として読まれうる名前を親の環境から消してから、選んだものだけを与える。
            // 受け継いだ値が残ると、この起動が何を指すかが親プロセスの環境で変わる。
            Dictionary<string, string> environment = new Dictionary<string, string>
            {
                [IgnoredPipeEnvironmentVariableName] = null,
                [PipeTargetResolver.TestPipeEnvironmentVariableName] = null,
                [BridgeBudget.EnvironmentVariableName] = budgetChars,
                [BridgeDebugHooks.EnvironmentVariableName] = debugHooks,
                [BridgeDeclaration.EnvironmentVariableName] = declareMeta,
            };

            if (pipeEnvironmentVariableName != null)
            {
                environment[pipeEnvironmentVariableName] = pipeName;
            }

            StdioClientTransport transport = new StdioClientTransport(
                new StdioClientTransportOptions
                {
                    Name = "pmx-editor-mcp",
                    Command = Path.Combine(AppContext.BaseDirectory, "PmxEditorMcp.Bridge.exe"),
                    EnvironmentVariables = environment,
                    ShutdownTimeout = ShutdownWait,
                });

            return McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
        }

        private static int CharsOf(string request)
        {
            return (int)System.Text.Json.Nodes.JsonNode.Parse(request)
                .AsObject()["params"][BridgeTools.LargeTextCharsParameter];
        }

        /// <summary>組み立てた定義のうちの1つの名前。包みで返る経路を通せればどれでもよい。</summary>
        private static string GeneratedName()
        {
            return GeneratedToolDefinitions.Create()
                .Select(definition => definition.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .First();
        }

        /// <summary>接続先の行を先頭に置いた、要求元へ返る本文を組み立てる。</summary>
        private static string Relayed(string pipeName, string body)
        {
            return "接続先: " + pipeName + "\n" + body;
        }

        private static int DeclaredResultSize(McpClientTool tool)
        {
            Assert.NotNull(tool.ProtocolTool.Meta);

            Assert.Equal(
                new string[] { BridgeTools.ResultSizeMetaKey },
                tool.ProtocolTool.Meta.Select(entry => entry.Key).ToArray());

            return (int)tool.ProtocolTool.Meta[BridgeTools.ResultSizeMetaKey];
        }

        private static string TextOf(CallToolResult result)
        {
            return Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        }

        /// <summary>一覧に出るはずの名前。基盤の中継と、組み立てた定義のすべてからなる。</summary>
        private static string[] Expected(bool debugHooks)
        {
            List<string> names =
                new List<string> { FixedToolTable.PingName, FixedToolTable.SdkStatusName };
            names.AddRange(GeneratedToolDefinitions.Create().Select(d => d.Name));
            if (debugHooks)
            {
                names.Add(BridgeTools.LargeTextMethod);
            }

            return Sorted(names);
        }

        private static string[] Sorted(IEnumerable<string> names)
        {
            return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        private static McpClientTool Named(IEnumerable<McpClientTool> tools, string name)
        {
            return Assert.Single(
                tools, tool => string.Equals(tool.Name, name, StringComparison.Ordinal));
        }

        private static Func<string, string> HandshakeResultOf(int budgetChars)
        {
            return request => Result(
                request,
                "{\"protocol\":1,\"hostVersion\":\"1.0.0.0\",\"toolMapDigest\":\""
                    + GeneratedToolDefinitions.ToolMapDigest + "\",\"budgetChars\":" + budgetChars + "}");
        }

        private static string Result(string request, string result)
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":" + IdOf(request) + ",\"result\":" + result + "}";
        }

        private static int IdOf(string request)
        {
            return (int)System.Text.Json.Nodes.JsonNode.Parse(request).AsObject()["id"];
        }

        private static string[] MethodsOf(IReadOnlyList<string> requests)
        {
            string[] methods = new string[requests.Count];
            for (int index = 0; index < methods.Length; index++)
            {
                methods[index] = (string)System.Text.Json.Nodes.JsonNode
                    .Parse(requests[index]).AsObject()["method"];
            }

            return methods;
        }
    }
}
