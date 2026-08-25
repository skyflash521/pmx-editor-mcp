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
        private static readonly TimeSpan TestWait = TimeSpan.FromSeconds(60);

        [Fact]
        public void 結果の大きさを宣言する鍵は契約で定めた値である()
        {
            Assert.Equal("anthropic/maxResultSizeChars", BridgeTools.ResultSizeMetaKey);
        }

        [Fact]
        public async Task サーバーは契約で定めた名前とブリッジのバージョンを名乗る()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(null, null, limit.Token);

            Assert.Equal("pmx-editor-mcp", client.ServerInfo.Name);
            Assert.Equal(
                typeof(BridgeServer).Assembly.GetName().Version.ToString(),
                client.ServerInfo.Version);
        }

        [Fact]
        public async Task 登録するツールはホストへ中継する1件だけである()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(null, null, limit.Token);

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);

            McpClientTool only = Assert.Single(tools);
            Assert.Equal("ping", only.Name);
        }

        [Fact]
        public async Task ツール定義は応答サイズ予算の既定値を宣言する()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(null, null, limit.Token);

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);

            Assert.Equal(BridgeBudget.DefaultChars, DeclaredResultSize(Assert.Single(tools)));
        }

        [Fact]
        public async Task ツール定義は環境変数で上書きした応答サイズ予算を宣言する()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeAsync(null, "250000", limit.Token);

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);

            Assert.Equal(250000, DeclaredResultSize(Assert.Single(tools)));
        }

        [Fact]
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

        [Fact]
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

            // 失敗の印は省略できる(省略は偽の意味)ので、真でないことを見る。
            Assert.NotEqual(true, result.IsError);
            Assert.Equal("pong", TextOf(result));

            // ホストが受け取ったのは handshake と ping で、名前を作り替えていない。
            Assert.Equal(new string[] { "handshake", "ping" }, MethodsOf(host.Requests));
        }

        [Fact]
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

        [Fact(Skip = "impl pending: ブリッジの実行経路がテスト専用の環境変数で接続先を固定する")]
        public async Task テスト専用の環境変数で接続先を固定して中継できる()
        {
            // 接続先の決定は実行経路の入口にあるので、単体では組み合わせまでしか確かめられない。
            // 実行ファイルを起動して、指した相手から応答が返るところまでを見る。
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
            Assert.Equal("pong", TextOf(result));

            // 応答の中身だけでは、指定を無視して別の相手を見つけた実装も通る。この待受が
            // 要求を受け取ったことまでを見て、指定が効いていることを確かめる。
            Assert.Equal(new string[] { "handshake", "ping" }, MethodsOf(host.Requests));
        }

        [Fact(Skip = "impl pending: 接続先の指定が無ければ待ち受けているパイプを列挙して見つける")]
        public async Task 接続先の指定が無ければ待ち受けているホストを見つけて中継する()
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

        [Fact(Skip = "impl pending: 接続先を指す環境変数はテスト専用の名前だけとする")]
        public async Task 接続先を指す環境変数はテスト専用の名前だけとする()
        {
            // 名前の似た別の環境変数まで接続先として読む実装だと、利用者が起動設定で接続先を
            // 選べる余地が残る。読まないはずの名前へ待ち受けていない名前を与えても、待受の
            // 列挙で決めることを見る。読んでいれば、その名前へ繋ごうとして失敗する。
            using FakeHost host = new FakeHost(PipeTargetResolver.PipeNameForProcess(Environment.ProcessId))
                .Reply(HandshakeResultOf(BridgeBudget.DefaultChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();

            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await StartBridgeWithAsync(
                "PMX_EDITOR_MCP_PIPE",
                "pmx-editor-mcp-test-" + Guid.NewGuid().ToString("N"),
                null,
                limit.Token);

            AssertFoundByEnumeration(
                await client.CallToolAsync("ping", cancellationToken: limit.Token), host);
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
                Assert.Equal("pong", TextOf(result));

                // 応答の中身だけでは、別の相手が同じ本文を返しても通る。この待受が要求を
                // 受け取ったことまでを見て、繋いだ先がここであることを確かめる。
                Assert.Equal(new string[] { "handshake", "ping" }, MethodsOf(host.Requests));
            }
        }

        /// <summary>
        /// ブリッジの実行ファイルをMCPサーバーとして起動する。接続先と応答サイズ予算は、
        /// 呼び出し側の環境に左右されないよう明示して渡す。
        /// </summary>
        private static Task<McpClient> StartBridgeAsync(
            string pipeName, string budgetChars, CancellationToken cancellationToken)
        {
            return StartBridgeWithAsync(
                PipeTargetResolver.EnvironmentVariableName, pipeName, budgetChars, cancellationToken);
        }

        /// <summary>接続先を指定する環境変数の名前を選んでブリッジを起動する。</summary>
        private static Task<McpClient> StartBridgeWithAsync(
            string pipeEnvironmentVariableName,
            string pipeName,
            string budgetChars,
            CancellationToken cancellationToken)
        {
            // 接続先を指す環境変数はどちらも消してから、選んだほうだけを与える。親から
            // 受け継いだ指定が残ると、この起動が何を指すかが呼び出し側の環境で変わる。
            Dictionary<string, string> environment = new Dictionary<string, string>
            {
                [PipeTargetResolver.EnvironmentVariableName] = null,
                [PipeTargetResolver.TestPipeEnvironmentVariableName] = null,
                [BridgeBudget.EnvironmentVariableName] = budgetChars,
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
