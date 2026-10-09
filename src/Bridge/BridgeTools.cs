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
    public static class BridgeTools
    {
        /// <summary>
        /// ツール定義へ付ける、テキスト結果の文字数のしきい値を宣言する鍵。Claude Code は
        /// この宣言があるツールについて、自分の既定のしきい値を宣言値へ引き上げる。
        /// </summary>
        public const string ResultSizeMetaKey = "anthropic/maxResultSizeChars";

        public const string LargeTextMethod = FixedToolTable.LargeTextName;

        public const string LargeTextCharsParameter = "chars";

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
        /// 指定した文字数のテキストを返すツールを作る。検査からだけ使う入口が開いているときだけ登録する。
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

        /// <summary>接続先に選べるエディタを並べるツールを作る。ホストへは渡らない。</summary>
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
                    [ToolEnvelope.OkName] = true,
                    [ToolEnvelope.ValueName] = new JsonObject { ["editors"] = editors },
                },
                string.Empty,
                client.BudgetChars,
                false);
        }

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
                        [ToolEnvelope.OkName] = true,
                        [ToolEnvelope.ValueName] = new JsonObject
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

                    Meta = declared
                        ? new JsonObject { [ResultSizeMetaKey] = client.BudgetChars }
                        : null,
                });
        }

        /// <summary>
        /// 包みで返るツールを中継する。包みとして読めない応答は、接続を捨てて誤りにする。
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
