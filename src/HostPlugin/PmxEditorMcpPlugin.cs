using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;
using PEPlugin;
using PEPlugin.View;
using PXCPlugin;

namespace PmxEditorMcp
{
    /// <summary>
    /// PMXエディタへ常駐し、MCPブリッジからの要求を受ける名前付きパイプの待受を持つプラグイン。
    /// 起動時にホストを開始し、メニュー再実行では稼働状態の表示と停止・開始の操作を受ける。
    /// </summary>
    public class PmxEditorMcpPlugin : PEPluginClass
    {
        private const string MenuText = "PMX Editor MCP";

        private const string BuilderType = "PEPlugin.IPEBuilder";

        private const string ViewType = "PEPlugin.View.IPXPmxViewConnector";

        private const string FormType = "PEPlugin.Form.IPEFormConnector";

        private const string PartsType = "PEPlugin.View.IPEPartsSelectConnector";

        private const string SubViewType = "PEPlugin.View.IPESubViewConnector";

        private const string TransformViewType = "PEPlugin.View.IPETransformViewConnector";

        private const string SettingType = "PEPlugin.View.IPEViewSettingConnector";

        private const string ExtensionEditType = "PEPlugin.View.IPEExtensionEditConnector";

        private static readonly Dictionary<string, string> WindowReceiverTypes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "PmxViewForm.PMXView", ViewType },
                { "PmxViewForm.TransformView", TransformViewType },
                { "PmxViewForm.PmxSubView", SubViewType },
                { "PmxViewForm.PmxViewSelector", PartsType },
                { "PmxViewForm.MappingEdit", "PEPlugin.View.IPEWeightEditConnector" },
                { "PmxViewForm.PmxViewEdit", "PEPlugin.View.IPEVertexEditConnector" },
                { "PmxViewForm.PmxViewEditExtension", ExtensionEditType },
                { "PmxViewForm.PmxViewGuide", "PEPlugin.View.IPEVertexGuideConnector" },
                { "PmxViewForm.PmxViewSetting", SettingType },
                { "PmxEditor.PmxViewObjectSelect", "PEPlugin.View.IPEObjectSelectConnector" },
            };

        private const int PromptTextLimitMs = 500;

        private readonly object _operationGate = new object();

        private Form _uiAnchor;
        private HostLog _log;
        private McpHost _host;
        private JsonRpcConnection _connection;
        private ResidentConnection _resident;
        private ModelUpdateWatch _modelUpdates;

        /// <summary>起動時実行とメニュー登録を有効にして生成する。</summary>
        public PmxEditorMcpPlugin()
            : base()
        {
            m_option = new PEPluginOption(true, true, MenuText);
        }

        /// <summary>プラグイン名。</summary>
        public override string Name
        {
            get { return "PmxEditorMcp"; }
        }

        /// <summary>バージョン。ホストDLLのアセンブリバージョンを用いる。</summary>
        public override string Version
        {
            get { return HostVersion; }
        }

        /// <summary>説明。</summary>
        public override string Description
        {
            get { return "MCPクライアントからPMXエディタを操作するための常駐ホスト。"; }
        }

        /// <summary>
        /// 読み込まれているSDKのアセンブリバージョン文字列。型そのものの所属を見るだけで、
        /// メンバーを名前で引かない。
        /// </summary>
        internal static string SdkVersion
        {
            get { return typeof(IPEPlugin).Assembly.GetName().Version.ToString(); }
        }

        /// <summary>ホストDLLのアセンブリバージョン文字列。</summary>
        internal static string HostVersion
        {
            get { return typeof(PmxEditorMcpPlugin).Assembly.GetName().Version.ToString(); }
        }

        /// <summary>
        /// 起動時実行ではホストを常駐させ、メニュー再実行では稼働状態を表示する。
        /// 例外はすべて捕捉し、エディタを巻き込まない。
        /// </summary>
        public override void Run(IPERunArgs args)
        {
            Run(args, StartOnBootup, ShowStatus, () => _log);
        }

        /// <summary>
        /// 実行の中身を受け取って呼ぶ入口。例外はどちらの中身から出ても外へ出さず、
        /// <paramref name="log"/> が返す記録へ書く。記録を引くのは捕らえた後である——記録は
        /// 実行の途中で作られるので、呼ぶ前に引くと、その回に作られたものへ書けない。
        /// </summary>
        internal static void Run(
            IPERunArgs args, Action<IPERunArgs> bootup, Action status, Func<HostLog> log)
        {
            try
            {
                if (args != null && args.IsBootup)
                {
                    bootup(args);
                }
                else
                {
                    status();
                }
            }
            catch (Exception exception)
            {
                HostLog written = log();
                if (written != null)
                {
                    written.WriteException("プラグインの実行で例外が起きた。", exception);
                }
            }
        }

        /// <summary>
        /// ホストの停止手順を実行し、不可視フォームを破棄してから基底の後始末へ進む。
        /// IPCサーバースレッドは背景スレッドなので、残っていてもプロセス終了で回収される。
        /// </summary>
        public override void Dispose()
        {
            try
            {
                lock (_operationGate)
                {
                    if (_host != null)
                    {
                        _host.Stop();
                    }

                    if (_modelUpdates != null)
                    {
                        _modelUpdates.Dispose();
                        _modelUpdates = null;
                    }

                    if (_resident != null)
                    {
                        _resident.Dispose();
                        _resident = null;
                    }

                    if (_uiAnchor != null)
                    {
                        _uiAnchor.Close();
                        _uiAnchor.Dispose();
                        _uiAnchor = null;
                    }
                }
            }
            catch (Exception exception)
            {
                if (_log != null)
                {
                    _log.WriteException("プラグインの後始末で例外が起きた。", exception);
                }
            }
            finally
            {
                base.Dispose();
            }
        }

        private void StartOnBootup(IPERunArgs args)
        {
            lock (_operationGate)
            {
                if (_host != null)
                {
                    return;
                }

                int editorProcessId = Process.GetCurrentProcess().Id;
                _log = new HostLog(HostLog.BuildDefaultFilePath(editorProcessId));
                _log.Write("プラグインを起動した: version=" + HostVersion);

                _uiAnchor = new Form();
                _uiAnchor.ShowInTaskbar = false;
                _uiAnchor.FormBorderStyle = FormBorderStyle.None;

                // 表示しないフォームはハンドルを持たず Invoke できないため、ここで確保する。
                _ = _uiAnchor.Handle;

                ResponseBudget budget = ResponseBudget.ReadFromEnvironment();
                HoldResidentConnection(args);

                // 基盤メソッドは接続が受け持つので、ここへはツールだけを載せる。
                McpMethodTable methods = new McpMethodTable();
                UndoSuppression undo = new UndoSuppression(_log);
                SdkRelayTable relay = GeneratedSdkRelay.Create();
                Dictionary<string, SdkReceiver> receivers = GeneratedSdkReceivers.Create();
                receivers[ToolDispatch.ReceiverKey(ToolDispatch.ViewSettingType, ToolDispatch.Views[1])] =
                    connection => connection.RunArgs.Host.Connector.View.TransformViewSetting;
                receivers[ToolDispatch.ReceiverKey(ToolDispatch.ViewSettingType, ToolDispatch.Views[2])] =
                    connection => connection.RunArgs.Host.Connector.View.PMDViewHelper.SubViewSetting;
                ScreenRefresh refresh = new ScreenRefresh(
                    () => Receiver(receivers, ViewType),
                    () => Receiver(receivers, FormType),
                    () => TransformViewSync.Refresh(Receiver(receivers, TransformViewType)));
                PmxSession current = new PmxSession(
                    relay, receivers, _resident, GeneratedSdkFlows.Current,
                    GeneratedSdkFlows.Pmx, undo);
                UndoRecovery recovery = new UndoRecovery(undo, current.UndoLock);
                ToolDispatch.AddTo(
                    methods,
                    relay,
                    receivers,
                    GeneratedSdkLists.Create(),
                    _resident,
                    current,
                    new PmxSession(
                        relay, receivers, _resident, GeneratedSdkFlows.Bridge,
                        GeneratedSdkFlows.Pmx, undo),
                    recovery,
                    GeneratedTools.Calls(),
                    GeneratedTools.Aggregations(),
                    GeneratedTools.Elements(),
                    GeneratedTools.Preconditions(),
                    new PressedModifierKeys(),
                    new EventBindingTable(
                        GeneratedTools.Attachments(), GeneratedTools.Payloads()),
                    refresh,
                    new ScreenTargets(
                        () => Receiver(receivers, ViewType), () => Receiver(receivers, FormType)),
                    VertexEditMeasure.ByRowKey());
                ComposedModelTools.AddTo(
                    methods,
                    new ComposedEdit(current, new UndoBarrier(recovery), refresh),
                    () => ((IPEBuilder)Receiver(receivers, BuilderType)).Pmx);
                ComposedScreenTools.AddTo(
                    methods,
                    new ComposedScreen(
                        current,
                        () => Receiver(receivers, ViewType),
                        () => Receiver(receivers, FormType),
                        () => Receiver(receivers, PartsType),
                        refresh,
                        () => Receiver(receivers, SettingType)),
                    () => Receiver(receivers, BuilderType),
                    () => Receiver(receivers, SubViewType),
                    OpenForms,
                    () => Receiver(receivers, TransformViewType),
                    new PressedModifierKeys());
                HandleRelease.AddTo(methods);
                EventPoll.AddTo(methods);
                UiFind.AddTo(methods);
                UiTree.AddTo(methods, OpenForms);
                Func<string, IPEBaseWindowConnector> windows = named => SdkWindow(receivers, named);
                UiOpenWindow.AddTo(methods, OpenForms, windows);
                UiCloseWindow.AddTo(methods, OpenForms, windows);
                UiPressItem.AddTo(methods, OpenForms);
                EditorPrompt.AddTo(
                    methods,
                    new DesktopModalWindowProbe(TimeSpan.FromMilliseconds(PromptTextLimitMs)));
                bool debugHooks = DebugHooks.ReadFromEnvironment();
                DebugEventInjection.AddTo(methods, debugHooks);
                DebugLargeText.AddTo(methods, debugHooks);
                DebugConnectorExpiry.AddTo(methods, debugHooks, _resident);
                _connection = new JsonRpcConnection(
                    _log, methods, HostVersion, budget.Chars, relay, SdkVersion, recovery,
                    new ScreenTargets(
                        () => Receiver(receivers, ViewType), () => Receiver(receivers, FormType)));

                _host = new McpHost(
                    McpHost.BuildPipeName(editorProcessId),
                    _log,
                    budget,
                    Following(new FormUiDispatcher(_uiAnchor), receivers),
                    (stream, generation) => _connection.Handle(stream, generation));

                string reason;
                if (!_host.TryStart(out reason))
                {
                    _log.Write("起動時に待受を開始しなかった: " + reason);
                }
            }
        }

        /// <summary>コネクタの無いウィンドウでは null を返す。</summary>
        private IPEBaseWindowConnector SdkWindow(IDictionary<string, SdkReceiver> receivers, string named)
        {
            string type;
            if (named == null || !WindowReceiverTypes.TryGetValue(named, out type))
            {
                return null;
            }

            SdkReceiver receiver;
            if (receivers.TryGetValue(type, out receiver))
            {
                return receiver(_resident) as IPEBaseWindowConnector;
            }

            IPEPMDViewHelper helper = _resident.RunArgs.Host.Connector.View.PMDViewHelper;

            return string.Equals(type, ExtensionEditType, StringComparison.Ordinal)
                ? (IPEBaseWindowConnector)helper.ExtensionEdit
                : helper.ObjectSelect;
        }

        private IEnumerable<Form> OpenForms()
        {
            List<Form> forms = new List<Form>();
            foreach (Form form in Application.OpenForms)
            {
                if (!ReferenceEquals(form, _uiAnchor))
                {
                    forms.Add(form);
                }
            }

            return forms;
        }

        private object Receiver(IDictionary<string, SdkReceiver> receivers, string type)
        {
            SdkReceiver receiver;
            if (!receivers.TryGetValue(type, out receiver))
            {
                throw new InvalidOperationException("受け手を得る道が無い: " + type);
            }

            return receiver(_resident);
        }

        /// <summary>
        /// 接続の根を常駐保持し、Cプラグイン連携のコネクタを先に得て、モデルの更新を数え始める。要求を受ける前に
        /// 済ませる。コネクタを得られなくても、数え始められなくても根は保ち、待受も続けるので、失敗は記録にとどめる。
        /// </summary>
        private void HoldResidentConnection(IPERunArgs args)
        {
            _resident = ResidentConnection.Hold(args, _log);
            IPXCPluginConnector connector;
            try
            {
                connector = _resident.Use();
            }
            catch (Exception exception)
            {
                _log.WriteException("Cプラグインコネクタを取得できなかった。", exception);

                return;
            }

            try
            {
                _modelUpdates = ModelUpdateWatch.Start(connector);
            }
            catch (Exception exception)
            {
                _log.WriteException(
                    "モデルの更新を数え始められなかった。エディタにモデルを更新させるツールのあと、TransformView を読み直させない。",
                    exception);
            }
        }

        private IUiDispatcher Following(IUiDispatcher dispatcher, IDictionary<string, SdkReceiver> receivers)
        {
            return _modelUpdates == null
                ? dispatcher
                : new TransformViewFollowing(dispatcher, _modelUpdates, () => Receiver(receivers, TransformViewType));
        }

        private void ShowStatus()
        {
            lock (_operationGate)
            {
                McpHost host = _host;
                if (host == null)
                {
                    MessageBox.Show(
                        "ホストが常駐していない。エディタを起動し直す。",
                        MenuText,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                switch (host.Status)
                {
                    case HostStatus.Running:
                        if (Confirm(host, "停止しますか?"))
                        {
                            host.Stop();
                            _log.Write("メニューから停止した。");
                        }

                        break;

                    case HostStatus.Stopped:
                        if (Confirm(host, "開始しますか?"))
                        {
                            string reason;
                            if (host.TryStart(out reason))
                            {
                                _log.Write("メニューから開始した。");
                            }
                            else
                            {
                                _log.Write("メニューからの開始を受け付けなかった: " + reason);
                            }
                        }

                        break;

                    default:
                        // 開始できない状態では問いを出さず、なぜ開始できないかを状態表示の本文に含める。
                        ShowStatusMessage(host);
                        break;
                }
            }
        }

        private bool Confirm(McpHost host, string question)
        {
            DialogResult result = MessageBox.Show(
                BuildStatusText(host) + Environment.NewLine + Environment.NewLine + question,
                MenuText,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            return result == DialogResult.Yes;
        }

        private void ShowStatusMessage(McpHost host)
        {
            MessageBox.Show(BuildStatusText(host), MenuText, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string BuildStatusText(McpHost host)
        {
            HostStatus status = host.Status;
            string text = "状態: " + DescribeStatus(status) + Environment.NewLine
                + "パイプ名: " + host.PipeName + Environment.NewLine
                + "接続: " + (host.IsClientConnected ? "接続中" : "接続なし") + Environment.NewLine
                + "応答サイズ予算: " + (host.Budget.IsValid
                    ? host.Budget.Chars.ToString(CultureInfo.InvariantCulture) + " 文字"
                    : "受理できない値") + Environment.NewLine
                + "ログ: " + host.LogFilePath;

            string blockReason = DescribeStartBlock(host, status);
            if (blockReason != null)
            {
                text += Environment.NewLine + Environment.NewLine + blockReason;
            }

            return text;
        }

        private static string DescribeStatus(HostStatus status)
        {
            switch (status)
            {
                case HostStatus.Running:
                    return "稼働中";
                case HostStatus.Stopping:
                    return "停止処理中";
                case HostStatus.Stopped:
                    return "停止済み";
                default:
                    return "開始せず(応答サイズ予算の設定が不正)";
            }
        }

        private static string DescribeStartBlock(McpHost host, HostStatus status)
        {
            switch (status)
            {
                case HostStatus.Stopping:
                    return "停止した待受の後始末が終わっていないため、いまは開始できない。"
                        + "終わるとこの表示は「停止済み」になり、開始できるようになる。";
                case HostStatus.NotStartedInvalidBudget:
                    return "待受を開始していない。" + host.Budget.InvalidReason
                        + Environment.NewLine
                        + "環境変数を直してエディタを起動し直す。";
                default:
                    return null;
            }
        }
    }
}
