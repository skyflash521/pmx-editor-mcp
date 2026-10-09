using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    public sealed class WindowNote
    {
        /// <summary>
        /// <paramref name="holdsOwner"/> は、そのウィンドウが持ち主のウィンドウを使用不可にしているかどうか。
        /// </summary>
        public WindowNote(string caption, string body, bool visible, bool holdsOwner)
        {
            Caption = caption ?? string.Empty;
            Body = body ?? string.Empty;
            Visible = visible;
            HoldsOwner = holdsOwner;
        }

        public string Caption { get; }

        /// <summary>ウィンドウが見せている本文。持たないウィンドウでは空。</summary>
        public string Body { get; }

        public bool Visible { get; }

        public bool HoldsOwner { get; }
    }

    /// <summary>
    /// 人の応答を待つ表示をウィンドウの一覧から見分ける。当たりとするのは、持ち主のウィンドウを使用不可にしている
    /// 可視のウィンドウで、ウィンドウの種類は問わない。
    /// </summary>
    public static class ModalWindows
    {
        /// <summary>当たるウィンドウがどれもタイトルも本文も持たないときの <see cref="Describe"/> の値。</summary>
        public const string Wordless = "タイトルも本文も持たない表示";

        /// <summary>
        /// 当たるウィンドウのタイトルと本文。両方を持つものは両方を、片方だけを持つものはその片方を返す。
        /// 2つ以上あるときは、タイトルか本文を持つもののうち先に見つかったものを採る。当たるウィンドウが
        /// どれもタイトルも本文も持たなければ <see cref="Wordless"/>、当たるウィンドウが無ければ null。
        /// </summary>
        public static string Describe(IEnumerable<WindowNote> windows)
        {
            if (windows == null)
            {
                throw new ArgumentNullException(nameof(windows));
            }

            List<WindowNote> found = windows.Where(w => w != null && w.Visible && w.HoldsOwner).ToList();
            if (found.Count == 0)
            {
                return null;
            }

            WindowNote worded = found.FirstOrDefault(w => Said(w).Length != 0);

            return worded == null ? Wordless : Said(worded);
        }

        /// <summary>ウィンドウのタイトルと本文。どちらも持たなければ空。</summary>
        internal static string Said(WindowNote window)
        {
            string caption = window.Caption.Trim();
            string body = window.Body.Trim();
            if (caption.Length == 0)
            {
                return body;
            }

            return body.Length == 0 ? caption : caption + ": " + body;
        }
    }
}
