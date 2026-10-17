using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class UnresolvedToolsTests : IDisposable
    {
        private const string ModulePath = @"C:\plugins\PmxEditorMcp.HostPlugin.dll";

        private const string ReadTool = "view_get_selected_body_indices_pmd_view_connector";

        private const string ReadingTool = "view_get_selected_current_vertex";

        private readonly string _directory;

        private readonly HostLog _log;

        public UnresolvedToolsTests()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-unresolved-" + Guid.NewGuid().ToString("N"));
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
        public void ARowThatCannotReachTheSdkIsLeftOutAndNamed()
        {
            Dictionary<string, int> rows = new Dictionary<string, int>(StringComparer.Ordinal);
            List<string> unresolved = new List<string>();

            GeneratedRows.Add(rows, unresolved, "lost", () => { throw new TypeLoadException(); });
            GeneratedRows.Add(rows, unresolved, "kept", () => 1);

            Assert.Equal(new[] { "kept" }, rows.Keys.ToArray());
            Assert.Equal(new[] { "lost" }, unresolved);
        }

        [Fact]
        public void AFailureOtherThanReachingTheSdkIsThrown()
        {
            Assert.Throws<InvalidOperationException>(() => GeneratedRows.Add(
                new Dictionary<string, int>(StringComparer.Ordinal),
                new List<string>(),
                "broken",
                () => { throw new InvalidOperationException(); }));
        }

        [Fact]
        public void AnUnresolvedToolAndTheToolsReadingItAreRefused()
        {
            Dictionary<string, IList<ToolCall>> calls = GeneratedTools.Calls(new List<string>());
            Assert.True(calls.Remove(ReadTool), "題材のツールが表に無い。");

            SdkRelayTable relay;
            McpMethodTable methods = Register(
                calls, GeneratedTools.Aggregations(new List<string>()), ReadTool, out relay);

            AssertRefused(methods, ReadTool);
            AssertRefused(methods, ReadingTool);
            Assert.Equal(new[] { ReadTool, ReadingTool }.OrderBy(t => t, StringComparer.Ordinal), relay.RefusedTools);
        }

        [Fact]
        public void AToolIssuingIntoFieldsOfAnUnresolvedToolIsRefused()
        {
            Dictionary<string, IList<ToolCall>> calls = GeneratedTools.Calls(new List<string>());
            Dictionary<string, ToolFields> aggregations = GeneratedTools.Aggregations(new List<string>());
            KeyValuePair<string, string> pair = IssuingToolAndItsAggregation(calls, aggregations);
            aggregations.Remove(pair.Value);

            SdkRelayTable relay;
            McpMethodTable methods = Register(calls, aggregations, pair.Value, out relay);

            AssertRefused(methods, pair.Value);
            AssertRefused(methods, pair.Key);
            Assert.Contains(pair.Key, relay.RefusedTools);
            Assert.Contains(pair.Value, relay.RefusedTools);
        }

        private static KeyValuePair<string, string> IssuingToolAndItsAggregation(
            IDictionary<string, IList<ToolCall>> calls, IDictionary<string, ToolFields> aggregations)
        {
            foreach (KeyValuePair<string, IList<ToolCall>> named in calls)
            {
                foreach (ToolCall call in named.Value.Where(c => c.Issues != null))
                {
                    foreach (ToolArgument argument in call.Arguments.Where(a => !a.Injected && a.Referenced != null))
                    {
                        foreach (KeyValuePair<string, ToolFields> aggregation in aggregations.Where(a => a.Value.Writes))
                        {
                            if (aggregation.Value.Sets.SelectMany(s => s.Fields).Any(
                                f => string.Equals(f.Name, argument.Name, StringComparison.Ordinal)
                                    && f.RowKey.StartsWith(call.Issues.FullName + ".", StringComparison.Ordinal)))
                            {
                                return new KeyValuePair<string, string>(named.Key, aggregation.Key);
                            }
                        }
                    }
                }
            }

            throw new InvalidOperationException("預ける引数を持つツールが表に無い。");
        }

        private McpMethodTable Register(
            Dictionary<string, IList<ToolCall>> calls,
            Dictionary<string, ToolFields> aggregations,
            string unresolved,
            out SdkRelayTable relay)
        {
            relay = GeneratedSdkRelay.Create();
            Dictionary<string, SdkReceiver> receivers = GeneratedSdkReceivers.Create();
            ResidentConnection resident = ResidentConnection.Hold(
                new StubRunArgs(
                    new StubPluginHost(
                        new StubConnector(
                            new StubSystemConnector(
                                new StubCPluginRunArgs(new StubCPluginConnector())))),
                    ModulePath),
                _log);
            UndoSuppression undo = new UndoSuppression(_log);
            PmxSession current = new PmxSession(
                relay, receivers, resident, GeneratedSdkFlows.Current, GeneratedSdkFlows.Pmx, undo);
            McpMethodTable methods = new McpMethodTable();
            ToolDispatch.AddTo(
                methods,
                relay,
                receivers,
                GeneratedSdkLists.Create(),
                resident,
                current,
                new PmxSession(
                    relay, receivers, resident, GeneratedSdkFlows.Bridge, GeneratedSdkFlows.Pmx, undo),
                new UndoRecovery(undo, current.UndoLock),
                calls,
                aggregations,
                GeneratedTools.Elements(new List<string>()),
                GeneratedTools.Preconditions(),
                new StillModifierKeys(),
                new EventBindingTable(GeneratedTools.Attachments(), GeneratedTools.Payloads()),
                _log,
                new ScreenRefresh(() => null, () => null),
                new ScreenTargets(() => null, () => null),
                EditMeasure.ByRowKey(),
                new[] { unresolved });

            return methods;
        }

        private void AssertRefused(McpMethodTable methods, string tool)
        {
            McpMethod method;
            Assert.True(methods.TryGet(tool, out method), "登録されていないツール: " + tool);

            IDictionary<string, object> envelope = (IDictionary<string, object>)method(new McpMethodContext(
                new Dictionary<string, object>(StringComparer.Ordinal),
                new InlineInvoker(),
                100000,
                new HandleLedger(_log, new HandleIdIssuer()),
                new EventQueue(new EventSequenceIssuer())));

            Assert.False((bool)envelope["ok"], "断っていない: " + tool);
            IDictionary<string, object> error = (IDictionary<string, object>)envelope["error"];
            Assert.Equal(ToolEnvelope.NotApplicable, error["code"]);
            Assert.Contains(tool, (string)error["message"]);
        }
    }
}
