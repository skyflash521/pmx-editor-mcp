using System;
using System.Collections.Generic;
using System.IO;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 組み立てのツールを呼ぶための題材一式。現在のPMXの複製を1つだけ持ち、複製を得た回数と
    /// まとめて反映した回数を数える。
    /// </summary>
    internal sealed class ComposedEditFixture : IDisposable
    {
        public const string ModulePath = @"C:\plugins\PmxEditorMcp.HostPlugin.dll";

        private const string SdkVersion = "0.0.8.9";

        private const string Digest = "8f14e45fceea167a5a36dedd4bea2543";

        private const string ConnectorType = "PEPlugin.Pmx.IPXPmxConnector";

        private const string StateReadKey = "PEPlugin.Pmx.IPXPmxConnector.GetCurrentState()";

        private const string CommitKey = "PEPlugin.Pmx.IPXPmxConnector.Update(PEPlugin.Pmx.IPXPmx)";

        private const string PartialCommitKey =
            "PEPlugin.Pmx.IPXPmxConnector.Update(PEPlugin.Pmx.IPXPmx,PEPlugin.Pmx.PmxUpdateObject,System.Int32)";

        private readonly string _root;

        private readonly HostLog _log;

        private readonly ComposedEdit _edit;

        private const string StopUndoKey = "PEPlugin.Pmx.IPXPmxConnector.LockUndo()";

        private const string ResumeUndoKey = "PEPlugin.Pmx.IPXPmxConnector.UnlockUndo()";

        private readonly bool _lockingUndo;

        private McpMethodTable _tools;

        /// <summary>
        /// 題材を組む。<paramref name="lockingUndo"/> が真なら、Undoの記録を止め戻しする行を
        /// 持ち、反映する行は複製だけを取る流れにする。偽なら、反映する行がUndoの抑止を引数で取る。
        /// </summary>
        public ComposedEditFixture(bool lockingUndo = false)
        {
            _lockingUndo = lockingUndo;
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-composed-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _log = new HostLog(Path.Combine(_root, "host.log"));
            Handles = new HandleLedger(_log, new HandleIdIssuer());
            _edit = new ComposedEdit(Session(), Barrier(), Refresh());
        }

        /// <summary>現在のPMXとして複製を返す題材。</summary>
        public FakePmx Model { get; } = new FakePmx();

        /// <summary>3Dビューの題材。反映のあとに映し直したかをここで数える。</summary>
        public FakePmxView View { get; } = new FakePmxView();

        /// <summary>リストを持つ画面の題材。</summary>
        public FakeFormConnector Form { get; } = new FakeFormConnector();

        /// <summary>新しい要素を作る相手の題材。</summary>
        public FakeBuilder Builder { get; } = new FakeBuilder();

        /// <summary>発行したハンドルの台帳。</summary>
        public HandleLedger Handles { get; }

        /// <summary>複製を得た回数。</summary>
        public int Clones { get; private set; }

        /// <summary>まとめて反映した回数。</summary>
        public int Commits { get; private set; }

        /// <summary>Undoの記録を止めている間か。止め戻しする行を持つ流れでだけ動く。</summary>
        public bool UndoLocked { get; private set; }

        /// <summary>1つの区分だけを反映した回の、区分と位置。全体を反映した回は入らない。</summary>
        public IList<KeyValuePair<PEPlugin.Pmx.PmxUpdateObject, int>> Partials { get; } =
            new List<KeyValuePair<PEPlugin.Pmx.PmxUpdateObject, int>>();

        /// <summary>反映へ届いたUndoの抑止の頼み。</summary>
        public bool Suppressed { get; private set; }

        /// <summary>複製編集の経路へ乗せる枠。</summary>
        public ComposedEdit Edit
        {
            get { return _edit; }
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

        /// <summary>載せたツールを名前で呼ぶ。表はここで初めて組み立てる。</summary>
        public IDictionary<string, object> Call(
            string tool, IDictionary<string, object> arguments)
        {
            return Call(Method(tool), arguments);
        }

        public McpMethod Method(string tool)
        {
            if (_tools == null)
            {
                _tools = new McpMethodTable();
                ComposedModelTools.AddTo(_tools, _edit, () => Builder);
            }

            McpMethod method;
            if (!_tools.TryGet(tool, out method))
            {
                throw new InvalidOperationException("登録されていないツール: " + tool);
            }

            return method;
        }

        public IDictionary<string, object> Call(
            string tool, IDictionary<string, object> arguments, int budgetChars)
        {
            return (IDictionary<string, object>)Method(tool)(Context(arguments, budgetChars));
        }

        /// <summary>呼び出しを1つ、UIスレッドの委譲を通して呼ぶ。</summary>
        public IDictionary<string, object> Call(
            McpMethod method, IDictionary<string, object> arguments)
        {
            return (IDictionary<string, object>)method(Context(arguments));
        }

        /// <summary>その項目の組を持つ呼び出しの場。</summary>
        public McpMethodContext Context(IDictionary<string, object> arguments)
        {
            return Context(arguments, 100000);
        }

        public McpMethodContext Context(IDictionary<string, object> arguments, int budgetChars)
        {
            return new McpMethodContext(
                arguments,
                new InlineInvoker(),
                budgetChars,
                Handles,
                new EventQueue(new EventSequenceIssuer()),
                new ScreenTargets(() => View, () => Form));
        }

        /// <summary>画面へ映す段。題材の口を通して、映し直しの回数を数える。</summary>
        public ScreenRefresh Refresh()
        {
            return new ScreenRefresh(() => View, () => Form);
        }

        /// <summary>Undoの前置きの包み。止め戻しする相手を持たない流れとする。</summary>
        public UndoBarrier Barrier()
        {
            return new UndoBarrier(new UndoRecovery(new UndoSuppression(_log), null));
        }

        /// <summary>複製編集の流れ。受け手を取り、複製を1つとUndoの頼みを渡す形とする。</summary>
        public PmxSession Session()
        {
            return new PmxSession(
                Relay(),
                Receivers(),
                Connection(),
                _lockingUndo
                    ? new PmxFlow(
                        StateReadKey,
                        CommitKey,
                        ConnectorType,
                        new FlowSlot[0],
                        new[] { FlowSlot.Pmx },
                        StopUndoKey,
                        ResumeUndoKey,
                        PartialCommitKey)
                    : new PmxFlow(
                        StateReadKey,
                        CommitKey,
                        ConnectorType,
                        new FlowSlot[0],
                        new[] { FlowSlot.Pmx, FlowSlot.UndoLock },
                        partialCommit: PartialCommitKey),
                typeof(FakePmx),
                new UndoSuppression(_log));
        }

        /// <summary>項目の組を作る。</summary>
        public static IDictionary<string, object> Arguments(
            params KeyValuePair<string, object>[] given)
        {
            Dictionary<string, object> arguments =
                new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> pair in given)
            {
                arguments.Add(pair.Key, pair.Value);
            }

            return arguments;
        }

        /// <summary>項目1つ。</summary>
        public static KeyValuePair<string, object> Given(string name, object value)
        {
            return new KeyValuePair<string, object>(name, value);
        }

        /// <summary>包みが持つ誤りの符号。</summary>
        public static string Code(IDictionary<string, object> envelope)
        {
            return (string)((IDictionary<string, object>)envelope["error"])["code"];
        }

        /// <summary>包みが持つ誤りの説明。</summary>
        public static string Message(IDictionary<string, object> envelope)
        {
            return (string)((IDictionary<string, object>)envelope["error"])["message"];
        }

        /// <summary>包みが持つ中身。成功でなければ止まる。</summary>
        public static IDictionary<string, object> Value(IDictionary<string, object> envelope)
        {
            if (!Equals(envelope["ok"], true))
            {
                throw new InvalidOperationException("成功でない包み: " + Message(envelope));
            }

            return (IDictionary<string, object>)envelope["value"];
        }

        private ResidentConnection Connection()
        {
            return ResidentConnection.Hold(
                new StubRunArgs(
                    new StubPluginHost(
                        new StubConnector(
                            new StubSystemConnector(
                                new StubCPluginRunArgs(new StubCPluginConnector())))),
                    ModulePath),
                _log);
        }

        private static IDictionary<string, SdkReceiver> Receivers()
        {
            return new Dictionary<string, SdkReceiver>(StringComparer.Ordinal)
            {
                { ConnectorType, connection => new object() },
            };
        }

        private SdkRelayTable Relay()
        {
            Dictionary<string, SdkCall> calls =
                new Dictionary<string, SdkCall>(StringComparer.Ordinal)
                {
                    {
                        StateReadKey,
                        (target, arguments) =>
                        {
                            Clones++;

                            return Model;
                        }
                    },
                    {
                        CommitKey,
                        (target, arguments) =>
                        {
                            Commits++;
                            Suppressed = arguments.Length > 1 && Equals(arguments[1], true);

                            return null;
                        }
                    },
                    { StopUndoKey, (target, arguments) => { UndoLocked = true; return null; } },
                    { ResumeUndoKey, (target, arguments) => { UndoLocked = false; return null; } },
                    {
                        PartialCommitKey,
                        (target, arguments) =>
                        {
                            Commits++;
                            Partials.Add(new KeyValuePair<PEPlugin.Pmx.PmxUpdateObject, int>(
                                (PEPlugin.Pmx.PmxUpdateObject)arguments[1], (int)arguments[2]));

                            return null;
                        }
                    },
                };

            return new SdkRelayTable(SdkVersion, Digest, calls, new string[0]);
        }
    }
}
