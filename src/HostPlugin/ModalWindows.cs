using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>ウィンドウの様子。人の応答を待つ表示かどうかの判定と、そのタイトルと本文の文づくりに要るものだけを持つ。</summary>
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

        /// <summary>ウィンドウのタイトル。</summary>
        public string Caption { get; }

        /// <summary>ウィンドウが見せている本文。持たないウィンドウでは空。</summary>
        public string Body { get; }

        /// <summary>画面に出ているかどうか。</summary>
        public bool Visible { get; }

        /// <summary>持ち主のウィンドウを使用不可にしているかどうか。</summary>
        public bool HoldsOwner { get; }
    }

    /// <summary>
    /// 人の応答を待つ表示をウィンドウの一覧から見分ける。当たりとするのは、持ち主のウィンドウを使用不可にしている
    /// 可視のウィンドウで、ウィンドウの種類は問わない。
    /// </summary>
    public static class ModalWindows
    {
        /// <summary>
        /// 当たるウィンドウのタイトルと本文。両方を持つものは両方を、片方だけを持つものはその片方を返す。
        /// 当たるウィンドウが無ければ null。2つ以上あるときは先に見つかったものを採る。
        /// </summary>
        public static string Describe(IEnumerable<WindowNote> windows)
        {
            if (windows == null)
            {
                throw new ArgumentNullException(nameof(windows));
            }

            WindowNote found = windows.FirstOrDefault(
                w => w != null && w.Visible && w.HoldsOwner && Said(w).Length != 0);

            return found == null ? null : Said(found);
        }

        private static string Said(WindowNote window)
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
