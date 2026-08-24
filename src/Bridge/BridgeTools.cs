using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace PmxEditorMcp.Bridge
{
    /// <summary>ブリッジがMCPサーバーへ登録するツールを作る。</summary>
    public static class BridgeTools
    {
        /// <summary>
        /// ツール定義へ付ける、テキスト結果の文字数のしきい値を宣言する鍵。Claude Code は
        /// この宣言があるツールについて、自分の既定のしきい値を宣言値へ引き上げる。
        /// </summary>
        public const string ResultSizeMetaKey = "anthropic/maxResultSizeChars";

        /// <summary>
        /// ブリッジが登録するツールを作る。ツール定義へ載せる応答サイズ予算は、handshake で
        /// ホストと照合するのと同じ値をクライアントから取る——別々に受け取ると、宣言した値と
        /// 照合する値を食い違わせられる。
        /// </summary>
        public static IReadOnlyList<McpServerTool> Create(HostIpcClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            return new McpServerTool[]
            {
                Relay(client, "ping", "ホストが応答することを確かめる。"),
            };
        }

        /// <summary>ホストの同名のメソッドへ中継するツールを作る。</summary>
        private static McpServerTool Relay(HostIpcClient client, string method, string description)
        {
            return McpServerTool.Create(
                (CancellationToken cancellationToken) => RelayAsync(client, method, cancellationToken),
                new McpServerToolCreateOptions
                {
                    Name = method,
                    Description = description,

                    // ツールごとに作る。使い回すと、書き換えられる同じ木を全ツールが共有する。
                    Meta = new JsonObject { [ResultSizeMetaKey] = client.BudgetChars },
                });
        }

        private static async Task<CallToolResult> RelayAsync(
            HostIpcClient client, string method, CancellationToken cancellationToken)
        {
            try
            {
                JsonNode result = await client.CallAsync(method, null, cancellationToken)
                    .ConfigureAwait(false);

                return new CallToolResult
                {
                    Content = new List<ContentBlock> { new TextContentBlock { Text = Describe(result) } },
                };
            }
            catch (BridgeException error)
            {
                // 失敗はプロセスの異常終了ではなく、要求元が読めるツール結果として返す。
                return error.ToToolResult();
            }
        }

        /// <summary>
        /// ホストの結果をテキストへ写す。文字列はそのままの中身を、ほかはJSONの表記を返す。
        /// </summary>
        private static string Describe(JsonNode result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            string text;
            JsonValue value = result as JsonValue;
            if (value != null && value.TryGetValue(out text))
            {
                return text;
            }

            return result.ToJsonString();
        }
    }
}
