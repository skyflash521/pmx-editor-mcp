using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using PmxEditorMcp.SignatureDump;

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

        /// <summary>指定した文字数のテキストを返す、検査からだけ使うホストのメソッドの名前。</summary>
        public const string LargeTextMethod = FixedToolTable.LargeTextName;

        /// <summary>そのメソッドへ渡す、返すテキストの文字数の引数の名前。</summary>
        public const string LargeTextCharsParameter = "chars";

        /// <summary>
        /// ブリッジが登録するツールを作る。ツール定義へ載せる応答サイズ予算は、handshake で
        /// ホストと照合するのと同じ値をクライアントから取る——別々に受け取ると、宣言した値と
        /// 照合する値を食い違わせられる。
        /// </summary>
        public static IReadOnlyList<McpServerTool> Create(
            HostIpcClient client, bool declared, bool debugHooks)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            List<McpServerTool> tools = new List<McpServerTool>();
            IReadOnlyList<GeneratedToolDefinition> generated = GeneratedToolDefinitions.Create();
            IDictionary<string, string> own = FixedToolTable.Descriptions(debugHooks);
            foreach (KeyValuePair<string, string> fixedTool in own)
            {
                if (fixedTool.Key == FixedToolTable.LargeTextName)
                {
                    tools.Add(LargeText(client, declared, fixedTool.Value));
                }
                else if (fixedTool.Key == FixedToolTable.FindToolName)
                {
                    tools.Add(FindTool(Entries(own, generated), fixedTool.Value, client, declared));
                }
                else if (fixedTool.Key == FixedToolTable.ListEditorsName)
                {
                    tools.Add(ListEditors(client, declared, fixedTool.Value));
                }
                else if (fixedTool.Key == FixedToolTable.SelectEditorName)
                {
                    tools.Add(SelectEditor(client, declared, fixedTool.Value));
                }
                else
                {
                    tools.Add(Relay(client, declared, fixedTool.Key, fixedTool.Value));
                }
            }

            foreach (GeneratedToolDefinition definition in generated)
            {
                tools.Add(Generated(definition, client, declared));
            }

            return tools;
        }

        /// <summary>
        /// 指定した文字数のテキストを返すツールを作る。応答の大きさをMCPクライアントがどう扱うかを
        /// 確かめるために要るもので、検査からだけ使う入口が開いているときだけ登録する。
        /// </summary>
        private static McpServerTool LargeText(
            HostIpcClient client, bool declared, string description)
        {
            return McpServerTool.Create(
                (int chars, CancellationToken cancellationToken) => RelayAsync(
                    client,
                    LargeTextMethod,
                    new JsonObject { [LargeTextCharsParameter] = chars },
                    cancellationToken),
                new McpServerToolCreateOptions
                {
                    Name = LargeTextMethod,
                    Description = description,
                    Meta = declared
                        ? new JsonObject { [ResultSizeMetaKey] = client.BudgetChars }
                        : null,
                });
        }

        /// <summary>
        /// ビルド時に組み立てた定義からツールを作る。名前も説明も入力の形も本文が持つので、
        /// 委譲先の形から読み取らせない。
        /// </summary>
        private static McpServerTool Generated(
            GeneratedToolDefinition definition, HostIpcClient client, bool declared)
        {
            return McpServerTool.Create(
                new RelayFunction(definition, client),
                new McpServerToolCreateOptions
                {
                    Name = definition.Name,
                    Description = definition.Description,
                    Destructive = definition.Destructive ? true : null,
                    Meta = declared
                        ? new JsonObject { [ResultSizeMetaKey] = client.BudgetChars }
                        : null,
                });
        }

        /// <summary>語を当てる相手。ブリッジが自分で登録するツールも、組み立てた定義も入る。</summary>
        private static IList<ToolMatch.Entry> Entries(
            IDictionary<string, string> own, IReadOnlyList<GeneratedToolDefinition> generated)
        {
            List<ToolMatch.Entry> entries = new List<ToolMatch.Entry>();
            foreach (KeyValuePair<string, string> fixedTool in own)
            {
                entries.Add(new ToolMatch.Entry(fixedTool.Key, fixedTool.Value));
            }

            foreach (GeneratedToolDefinition definition in generated)
            {
                entries.Add(new ToolMatch.Entry(definition.Name, definition.Description));
            }

            return entries;
        }

        private static McpServerTool FindTool(
            IList<ToolMatch.Entry> entries,
            string description,
            HostIpcClient client,
            bool declared)
        {
            return McpServerTool.Create(
                new FindToolFunction(entries, description, client),
                new McpServerToolCreateOptions
                {
                    Name = FixedToolTable.FindToolName,
                    Description = description,
                    Meta = declared
                        ? new JsonObject { [ResultSizeMetaKey] = client.BudgetChars }
                        : null,
                });
        }

        /// <summary>
        /// 接続先に選べるエディタを並べるツールを作る。ホストへは渡らない——並べるのはブリッジが
        /// パイプとプロセスから読む。
        /// </summary>
        private static McpServerTool ListEditors(
            HostIpcClient client, bool declared, string description)
        {
            return McpServerTool.Create(
                () => ListEditorsResult(client),
                new McpServerToolCreateOptions
                {
                    Name = FixedToolTable.ListEditorsName,
                    Description = description,
                    Meta = declared
                        ? new JsonObject { [ResultSizeMetaKey] = client.BudgetChars }
                        : null,
                });
        }

        private static CallToolResult ListEditorsResult(HostIpcClient client)
        {
            IReadOnlyList<EditorSurveyEntry> surveyed;
            try
            {
                surveyed = PipeTargetResolver.SurveyRunningEditors();
            }
            catch (BridgeException error)
            {
                return error.ToToolResult();
            }

            string selected = client.SelectedPipeName;
            string connected = client.ConnectedPipeName;
            JsonArray editors = new JsonArray();
            foreach (EditorSurveyEntry entry in surveyed)
            {
                string pipeName = PipeTargetResolver.PipeNameForProcess(entry.ProcessId);
                editors.Add(new JsonObject
                {
                    ["processId"] = entry.ProcessId,
                    ["listening"] = entry.Listening,
                    ["title"] = entry.Title,
                    ["selected"] = string.Equals(pipeName, selected, StringComparison.Ordinal),
                    ["connected"] = string.Equals(pipeName, connected, StringComparison.Ordinal),
                });
            }

            return ToolEnvelopeResult.From(
                new JsonObject
                {
                    ["ok"] = true,
                    ["value"] = new JsonObject { ["editors"] = editors },
                },
                string.Empty,
                client.BudgetChars,
                false);
        }

        /// <summary>
        /// 接続先のエディタを選ぶツールを作る。選んだ接続先はこのブリッジのプロセスだけが持つ。
        /// </summary>
        private static McpServerTool SelectEditor(
            HostIpcClient client, bool declared, string description)
        {
            return McpServerTool.Create(
                (int processId, CancellationToken cancellationToken) =>
                    SelectEditorAsync(client, processId, cancellationToken),
                new McpServerToolCreateOptions
                {
                    Name = FixedToolTable.SelectEditorName,
                    Description = description,
                    Meta = declared
                        ? new JsonObject { [ResultSizeMetaKey] = client.BudgetChars }
                        : null,
                });
        }

        private static async Task<CallToolResult> SelectEditorAsync(
            HostIpcClient client, int processId, CancellationToken cancellationToken)
        {
            try
            {
                HostCallResult selected = await client
                    .SelectAsync(PipeTargetResolver.PipeNameForProcess(processId), cancellationToken)
                    .ConfigureAwait(false);
                return ToolEnvelopeResult.From(
                    new JsonObject
                    {
                        ["ok"] = true,
                        ["value"] = new JsonObject
                        {
                            [FixedToolTable.SelectEditorProcessIdParameter] = processId,
                        },
                    },
                    selected.TargetNotice,
                    client.BudgetChars,
                    false);
            }
            catch (BridgeException error)
            {
                return error.ToToolResult();
            }
        }

        /// <summary>ホストの同名のメソッドへ中継するツールを作る。</summary>
        private static McpServerTool Relay(
            HostIpcClient client, bool declared, string method, string description)
        {
            return McpServerTool.Create(
                (CancellationToken cancellationToken) =>
                    RelayAsync(client, method, null, cancellationToken),
                new McpServerToolCreateOptions
                {
                    Name = method,
                    Description = description,

                    // ツールごとに作る。使い回すと、書き換えられる同じ木を全ツールが共有する。
                    //
                    // 宣言するのはホストの本文に与えた予算そのものとする。先頭へ置く接続先の
                    // 行はブリッジの上乗せで、予算が抑えたいホストの応答の大きさではない。
                    // 予算に足すと、上限まで設定したときに宣言できる値を超えてしまう。
                    Meta = declared
                        ? new JsonObject { [ResultSizeMetaKey] = client.BudgetChars }
                        : null,
                });
        }

        /// <summary>
        /// 包みで返るツールを中継する。ホストの包みは値と誤りと警告に分かれているので、MCPの結果へ
        /// 写し直す——包みのまま返すと、呼び出し元は成否を結果の印から読めず、値も包みごと受け取る。
        /// 包みとして読めない応答は契約から外れているので、接続を捨てて誤りにする。
        /// </summary>
        internal static async Task<CallToolResult> RelayEnvelopeAsync(
            HostIpcClient client,
            string method,
            JsonObject parameters,
            bool returnsImage,
            string narrowing,
            CancellationToken cancellationToken)
        {
            try
            {
                return await client
                    .CallAsync(
                        method,
                        parameters,
                        response => ToolEnvelopeResult.From(
                            response.Result,
                            response.TargetNotice,
                            client.BudgetChars,
                            returnsImage,
                            narrowing),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (BridgeException error)
            {
                return error.ToToolResult();
            }
        }

        internal static async Task<CallToolResult> RelayAsync(
            HostIpcClient client,
            string method,
            JsonObject parameters,
            CancellationToken cancellationToken)
        {
            try
            {
                HostCallResult response = await client
                    .CallAsync(method, parameters, cancellationToken)
                    .ConfigureAwait(false);

                // 接続先は毎回名乗る。過去の知らせを覚えていることに頼ると、文脈が失われた
                // 時点で、呼び出し元はどのエディタの応答かを確かめる手立てを失う。
                return new CallToolResult
                {
                    Content = new List<ContentBlock>
                    {
                        new TextContentBlock
                        {
                            Text = response.TargetNotice + "\n" + Describe(response.Result),
                        },
                    },
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

            return ToolEnvelopeResult.Written(result);
        }
    }
}
