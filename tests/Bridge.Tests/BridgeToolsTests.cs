using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    /// <summary>
    /// ブリッジをMCPサーバーとして起動し、クライアントから見える契約を確かめる。ツール定義も
    /// ツールの中継も、実行ファイルを起動して stdio 越しに見る——登録の配線まで含めて確かめたい
    /// ので、SDKの型を直接読むのでは通らない経路が残る。
    /// </summary>
    public class BridgeToolsTests
    {
        private const string Pending = "impl pending: 応答サイズ予算を載せたツール定義を作り、ホストへ中継する";

        private static readonly TimeSpan TestWait = TimeSpan.FromSeconds(60);

        [Fact(Skip = Pending)]
        public void 結果の大きさを宣言する鍵は契約で定めた値である()
        {
            Assert.Equal("anthropic/maxResultSizeChars", BridgeTools.ResultSizeMetaKey);
        }

        [Fact(Skip = Pending)]
        public async Task サーバーは契約で定めた名前とブリッジのバージョンを名乗る()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(null, null, limit.Token);

            Assert.Equal("pmx-editor-mcp", client.ServerInfo.Name);
            Assert.Equal(
                typeof(BridgeServer).Assembly.GetName().Version.ToString(),
                client.ServerInfo.Version);
        }

        [Fact(Skip = Pending)]
        public async Task 登録するツールはホストへ中継する1件だけである()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(null, null, limit.Token);

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);

            McpClientTool only = Assert.Single(tools);
            Assert.Equal("ping", only.Name);
        }

        [Fact(Skip = Pending)]
        public async Task ツール定義は応答サイズ予算の既定値を宣言する()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(null, null, limit.Token);

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);

            Assert.Equal(BridgeBudget.DefaultChars, DeclaredResultSize(Assert.Single(tools)));
        }

        [Fact(Skip = Pending)]
        public async Task ツール定義は環境変数で上書きした応答サイズ予算を宣言する()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(null, "250000", limit.Token);

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);

            Assert.Equal(250000, DeclaredResultSize(Assert.Single(tools)));
        }

        [Fact(Skip = Pending)]
        public async Task ツール定義はホストへ接続していなくても得られる()
        {
            // 接続は最初のツール呼び出しまで行わないので、待ち受けていないパイプを指していても
            // 一覧はブリッジ自身の設定値から答えられる。
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(
                "pmx-editor-mcp-test-" + Guid.NewGuid().ToString("N"), null, limit.Token);

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);

            Assert.Equal(BridgeBudget.DefaultChars, DeclaredResultSize(Assert.Single(tools)));
        }

        [Fact(Skip = Pending)]
        public async Task ツールの呼び出しはホストへ中継して応答を返す()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BridgeBudget.DefaultChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(host.PipeName, null, limit.Token);

            CallToolResult result = await client.CallToolAsync(
                "ping", cancellationToken: limit.Token);

            Assert.False(result.IsError);
            Assert.Equal("pong", TextOf(result));

            // ホストが受け取ったのは handshake と ping で、名前を作り替えていない。
            Assert.Equal(new string[] { "handshake", "ping" }, MethodsOf(host.Requests));
        }

        [Fact(Skip = Pending)]
        public async Task 中継に失敗してもプロセスは落ちずエラーとしてツール結果で返す()
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
            Assert.Single(await client.ListToolsAsync(cancellationToken: limit.Token));
        }

        /// <summary>
        /// ブリッジの実行ファイルをMCPサーバーとして起動する。接続先と応答サイズ予算は、
        /// 呼び出し側の環境に左右されないよう明示して渡す。
        /// </summary>
        private static Task<McpClient> StartBridgeAsync(
            string pipeName, string budgetChars, CancellationToken cancellationToken)
        {
            Dictionary<string, string> environment = new Dictionary<string, string>
            {
                [PipeTargetResolver.EnvironmentVariableName] = pipeName,
                [BridgeBudget.EnvironmentVariableName] = budgetChars,
            };

            StdioClientTransport transport = new StdioClientTransport(
                new StdioClientTransportOptions
                {
                    Name = "pmx-editor-mcp",
                    Command = Path.Combine(AppContext.BaseDirectory, "PmxEditorMcp.Bridge.exe"),
                    EnvironmentVariables = environment,
                });

            return McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
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
