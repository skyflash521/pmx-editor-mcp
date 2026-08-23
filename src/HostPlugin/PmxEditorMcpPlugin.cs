using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using PEPlugin;

namespace PmxEditorMcp
{
    /// <summary>
    /// PMXエディタへ常駐し、MCPブリッジからの要求を受ける名前付きパイプの待受を持つプラグイン。
    /// 起動時にホストを開始し、メニュー再実行では稼働状態の表示と停止・開始の操作を受ける。
    /// </summary>
    public class PmxEditorMcpPlugin : PEPluginClass
    {
        private const string MenuText = "PMX Editor MCP";

        private readonly object _operationGate = new object();

        private Form _uiAnchor;
        private HostLog _log;
        private McpHost _host;

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
            try
            {
                if (args != null && args.IsBootup)
                {
                    StartOnBootup();
                }
                else
                {
                    ShowStatus();
                }
            }
            catch (Exception exception)
            {
                if (_log != null)
                {
                    _log.WriteException("プラグインの実行で例外が起きた。", exception);
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

        /// <summary>接続を受けても要求を読み書きせず、直ちに切断する。</summary>
        private static void DisconnectWithoutExchange(Stream stream, HostGeneration generation)
        {
        }

        private void StartOnBootup()
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

                _host = new McpHost(
                    McpHost.BuildPipeName(editorProcessId),
                    _log,
                    ResponseBudget.ReadFromEnvironment(),
                    new FormUiDispatcher(_uiAnchor),
                    DisconnectWithoutExchange);

                string reason;
                if (!_host.TryStart(out reason))
                {
                    _log.Write("起動時に待受を開始しなかった: " + reason);
                }
            }
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
                            ShowResult(host);
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

                            ShowResult(host);
                        }

                        break;

                    default:
                        // 開始できない状態では問いを出さず、理由を状態表示の本文に含める。
                        ShowResult(host);
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

        private void ShowResult(McpHost host)
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
