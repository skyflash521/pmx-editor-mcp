using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    /// <summary>
    /// ホストの停止と開始を、メニューを通さずに頼む入口の設定。環境変数を設定して起動したときだけ
    /// 開く。頼むのは、登録したウィンドウメッセージをホストのウィンドウへ送る形で、待受が止まって
    /// いる間も届く。
    /// </summary>
    public static class HostSwitch
    {
        /// <summary>入口を開くかどうかを与える環境変数の名前。</summary>
        public const string EnvironmentVariableName = "PMX_EDITOR_MCP_HOST_SWITCH";

        /// <summary>入口を開く値。これ以外はすべて閉じたままとする。</summary>
        public const string EnabledValue = "1";

        /// <summary>頼むメッセージを登録する名前。</summary>
        public const string MessageName = "PmxEditorMcp.HostSwitch";

        /// <summary>頼みを受けるウィンドウのタイトル。探す側がこの綴りで見つける。</summary>
        public const string WindowTitle = "PmxEditorMcp.HostSwitch";

        /// <summary>頼むメッセージの番号。WParam が0なら停止、1なら開始を頼む。</summary>
        public static readonly uint Message = RegisterWindowMessage(MessageName);

        /// <summary>環境変数の現在値から、入口を開くかどうかを読む。</summary>
        public static bool ReadFromEnvironment()
        {
            return IsEnabled(Environment.GetEnvironmentVariable(EnvironmentVariableName));
        }

        /// <summary>環境変数の値から、入口を開くかどうかを決める。開くのは値がちょうど開く値のときだけ。</summary>
        public static bool IsEnabled(string rawValue)
        {
            return string.Equals(rawValue, EnabledValue, StringComparison.Ordinal);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string name);
    }

    /// <summary>
    /// UIスレッドへ委譲するための、表示しないウィンドウ。入口が開いているときだけ、停止と開始を頼む
    /// メッセージを受けて、渡された処理を呼ぶ。
    /// </summary>
    internal sealed class HostSwitchForm : Form
    {
        private readonly bool _enabled;

        private readonly Action _stop;

        private readonly Action _start;

        internal HostSwitchForm(bool enabled, Action stop, Action start)
        {
            if (stop == null)
            {
                throw new ArgumentNullException(nameof(stop));
            }

            if (start == null)
            {
                throw new ArgumentNullException(nameof(start));
            }

            _enabled = enabled;
            _stop = stop;
            _start = start;
            Text = HostSwitch.WindowTitle;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
        }

        protected override void WndProc(ref Message m)
        {
            if (_enabled && (uint)m.Msg == HostSwitch.Message)
            {
                if (m.WParam == IntPtr.Zero)
                {
                    _stop();
                }
                else
                {
                    _start();
                }

                m.Result = (IntPtr)1;

                return;
            }

            base.WndProc(ref m);
        }
    }
}
