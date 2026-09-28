using System;
using System.Collections.Generic;
using System.IO;
using PEPlugin.Pmx;

namespace PmxEditorMcp.Tests
{
    internal sealed class GeneratedToolFixture : IDisposable
    {
        private const string ModulePath = @"C:\plugins\PmxEditorMcp.HostPlugin.dll";

        private const string ConnectorType = "PEPlugin.Pmx.IPXPmxConnector";

        private const string BuilderType = "PEPlugin.IPXPmxBuilder";

        private const string StateReadKey = "PEPlugin.Pmx.IPXPmxConnector.GetCurrentState()";

        private const string CommitKey = "PEPlugin.Pmx.IPXPmxConnector.Update(PEPlugin.Pmx.IPXPmx)";

        private const string StopUndoKey = "PEPlugin.Pmx.IPXPmxConnector.LockUndo()";

        private const string ResumeUndoKey = "PEPlugin.Pmx.IPXPmxConnector.UnlockUndo()";

        private readonly string _root;

        private readonly HostLog _log;

        private readonly McpMethodTable _methods = new McpMethodTable();

        private readonly FakePmxConnector _connector;

        public GeneratedToolFixture()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-generated-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _log = new HostLog(Path.Combine(_root, "host.log"));
            Handles = new HandleLedger(_log, new HandleIdIssuer());
            _connector = new FakePmxConnector(Model);

            SdkRelayTable relay = GeneratedSdkRelay.Create();
            Dictionary<string, SdkReceiver> receivers = GeneratedSdkReceivers.Create();
            receivers[ConnectorType] = connection => _connector;
            receivers[BuilderType] = connection => Builder;
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
            PmxSession bridged = new PmxSession(
                relay,
                receivers,
                resident,
                new PmxFlow(
                    StateReadKey,
                    CommitKey,
                    ConnectorType,
                    new FlowSlot[0],
                    new[] { FlowSlot.Pmx },
                    StopUndoKey,
                    ResumeUndoKey),
                GeneratedSdkFlows.Pmx,
                undo);
            ToolDispatch.AddTo(
                _methods,
                relay,
                receivers,
                GeneratedSdkLists.Create(),
                resident,
                current,
                bridged,
                new UndoRecovery(undo, current.UndoLock),
                GeneratedTools.Calls(),
                GeneratedTools.Aggregations(),
                GeneratedTools.Elements(),
                GeneratedTools.Preconditions(),
                new StillModifierKeys(),
                new EventBindingTable(GeneratedTools.Attachments(), GeneratedTools.Payloads()),
                new ScreenRefresh(() => View, () => Form),
                new ScreenTargets(() => View, () => Form),
                EditMeasure.ByRowKey());
        }

        public FakePmx Model { get; } = new FakePmx();

        public FakeBuilder Builder { get; } = new FakeBuilder();

        public FakePmxView View { get; } = new FakePmxView();

        public FakeFormConnector Form { get; } = new FakeFormConnector();

        public HandleLedger Handles { get; }

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

        public IDictionary<string, object> Call(string tool, params KeyValuePair<string, object>[] given)
        {
            McpMethod method;
            if (!_methods.TryGet(tool, out method))
            {
                throw new InvalidOperationException("登録されていないツール: " + tool);
            }

            return (IDictionary<string, object>)method(new McpMethodContext(
                ComposedEditFixture.Arguments(given),
                new InlineInvoker(),
                100000,
                Handles,
                new EventQueue(new EventSequenceIssuer()),
                new ScreenTargets(() => View, () => Form)));
        }

        private sealed class FakePmxConnector : IPXPmxConnector
        {
            private readonly FakePmx _state;

            public FakePmxConnector(FakePmx state)
            {
                _state = state;
            }

            public string CurrentPath
            {
                get { return _state.FilePath; }
                set { _state.FilePath = value; }
            }

            public IPXPmx GetCurrentState()
            {
                return FakeEditorState.Duplicate(_state);
            }

            public void Update(IPXPmx px)
            {
                FakeEditorState.Reflect(_state, px, PmxUpdateObject.All, -1);
            }

            public void Update(IPXPmx px, PmxUpdateObject obj, int index)
            {
                FakeEditorState.Reflect(_state, px, obj, index);
            }

            public void Update(IPXPmx px, PmxUpdateObject obj, int[] indices)
            {
                foreach (int index in indices)
                {
                    FakeEditorState.Reflect(_state, px, obj, index);
                }
            }

            public void LockUndo()
            {
            }

            public void UnlockUndo()
            {
            }
        }
    }
}
