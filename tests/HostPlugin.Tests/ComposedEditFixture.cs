using System;
using System.Collections.Generic;
using System.IO;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 組み立てのツールを呼ぶための題材一式。エディタが持つ現在のPMXを1つ持ち、複製の要求には
    /// その複製を返し、反映では渡された複製の中身を写す。複製を得た回数とまとめて反映した回数を
    /// 数える。Undoの記録は、本番と同じくコネクタの止める行と戻す行で止め戻しする。
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

        private readonly UndoSuppression _undo;

        private readonly PmxSession _session;

        private McpMethodTable _tools;

        private readonly System.Diagnostics.Stopwatch _editor = new System.Diagnostics.Stopwatch();

        /// <summary>題材を組む。</summary>
        public ComposedEditFixture()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-composed-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _log = new HostLog(Path.Combine(_root, "host.log"));
            Handles = new HandleLedger(_log, new HandleIdIssuer());
            _undo = new UndoSuppression(_log);
            _session = new PmxSession(
                Relay(),
                Receivers(),
                Connection(),
                new PmxFlow(
                    StateReadKey,
                    CommitKey,
                    ConnectorType,
                    new FlowSlot[0],
                    new[] { FlowSlot.Pmx },
                    StopUndoKey,
                    ResumeUndoKey,
                    PartialCommitKey),
                typeof(PEPlugin.Pmx.IPXPmx),
                _undo);
            _edit = new ComposedEdit(Session(), Barrier(), Refresh());
        }

        /// <summary>
        /// エディタが持つ現在のPMX。複製の要求にはこれの複製を返し、反映では渡された複製の中身を
        /// ここへ写す。呼び出しの結果はここを読んで確かめる。
        /// </summary>
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

        /// <summary>エディタの代役が状態の複製と反映に使った時間の累計。</summary>
        public TimeSpan EditorTime
        {
            get { return _editor.Elapsed; }
        }

        /// <summary>まとめて反映した回数。</summary>
        public int Commits { get; private set; }

        /// <summary>Undoの記録を止めている間か。</summary>
        public bool UndoLocked { get; private set; }

        /// <summary>1つの区分だけを反映した回の、区分と位置。全体を反映した回は入らない。</summary>
        public IList<KeyValuePair<PEPlugin.Pmx.PmxUpdateObject, int>> Partials { get; } =
            new List<KeyValuePair<PEPlugin.Pmx.PmxUpdateObject, int>>();

        /// <summary>最後の反映が、Undoの記録を止めている間に届いたか。</summary>
        public bool Suppressed { get; private set; }

        /// <summary>Undoの記録を戻す行を呼んだ回数。</summary>
        public int Resumes { get; private set; }

        /// <summary>この先、Undoの記録を戻す行が落ちる回数。</summary>
        public int ResumeFailures { get; set; }

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

        /// <summary>
        /// Undoの前置きの包み。本番と同じく、複製編集の流れが止め戻しする相手と、その流れと同じ
        /// Undoの抑止の枠を持つ。
        /// </summary>
        public UndoBarrier Barrier()
        {
            return new UndoBarrier(new UndoRecovery(_undo, _session.UndoLock));
        }

        /// <summary>
        /// 複製編集の流れ。本番のPMXのコネクタの流れと同じく、複製だけを取って全体を反映する行・
        /// 1つの区分だけを反映する行・Undoの記録を止める行と戻す行を持つ。
        /// </summary>
        public PmxSession Session()
        {
            return _session;
        }

        /// <summary>
        /// 反映で置き換えられて現在のPMXから外れた要素について、それが並んでいた位置に、いまの
        /// <see cref="Model"/> で並んでいる要素を返す。外れていなければそのまま返す。操作の前に
        /// 握った要素を操作の後に読むときは、これを通して現在のPMXから読む。
        /// </summary>
        public T Now<T>(T held)
            where T : class
        {
            return FakeEditorState.Now(Model, held);
        }

        /// <summary>
        /// <see cref="Model"/> そのものをハンドルで預け、そのハンドルで指す項目を返す。ハンドルで
        /// 指したPMXは複製も反映も通らないので、並びの外を指す要素のように、エディタの現在のPMX
        /// には置けない形をそのまま相手にさせられる。
        /// </summary>
        public KeyValuePair<string, object> HoldModel()
        {
            int handle = Handles.Issue(typeof(PEPlugin.Pmx.IPXPmx).FullName, Model, () => { });

            return Given(PmxSession.HandleName, (long)handle);
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
                            _editor.Start();
                            try
                            {
                                return FakeEditorState.Duplicate(Model);
                            }
                            finally
                            {
                                _editor.Stop();
                            }
                        }
                    },
                    {
                        CommitKey,
                        (target, arguments) =>
                        {
                            Commits++;
                            Suppressed = UndoLocked;
                            _editor.Start();
                            try
                            {
                                FakeEditorState.Reflect(
                                    Model,
                                    (PEPlugin.Pmx.IPXPmx)arguments[0],
                                    PEPlugin.Pmx.PmxUpdateObject.All,
                                    -1);
                            }
                            finally
                            {
                                _editor.Stop();
                            }

                            return null;
                        }
                    },
                    { StopUndoKey, (target, arguments) => { UndoLocked = true; return null; } },
                    {
                        ResumeUndoKey,
                        (target, arguments) =>
                        {
                            Resumes++;
                            if (ResumeFailures > 0)
                            {
                                ResumeFailures--;
                                throw new InvalidOperationException("Undoの記録を戻せない。");
                            }

                            UndoLocked = false;

                            return null;
                        }
                    },
                    {
                        PartialCommitKey,
                        (target, arguments) =>
                        {
                            Commits++;
                            Suppressed = UndoLocked;
                            PEPlugin.Pmx.PmxUpdateObject part = (PEPlugin.Pmx.PmxUpdateObject)arguments[1];
                            int index = (int)arguments[2];
                            Partials.Add(new KeyValuePair<PEPlugin.Pmx.PmxUpdateObject, int>(part, index));
                            _editor.Start();
                            try
                            {
                                FakeEditorState.Reflect(Model, (PEPlugin.Pmx.IPXPmx)arguments[0], part, index);
                            }
                            finally
                            {
                                _editor.Stop();
                            }

                            return null;
                        }
                    },
                };

            return new SdkRelayTable(SdkVersion, Digest, calls, new string[0]);
        }
    }
}
