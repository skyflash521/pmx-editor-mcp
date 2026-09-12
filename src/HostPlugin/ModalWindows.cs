using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>窓の様子。人の応答を待つ表示かどうかの判定と、その文面づくりに要るものだけを持つ。</summary>
    public sealed class WindowNote
    {
        /// <summary>
        /// <paramref name="holdsOwner"/> は、その窓が持ち主の窓を使用不可にしているかどうか。
        /// </summary>
        public WindowNote(string caption, string body, bool visible, bool holdsOwner)
        {
            Caption = caption ?? string.Empty;
            Body = body ?? string.Empty;
            Visible = visible;
            HoldsOwner = holdsOwner;
        }

        /// <summary>窓の見出し。</summary>
        public string Caption { get; }

        /// <summary>窓が見せている本文。持たない窓では空。</summary>
        public string Body { get; }

        /// <summary>画面に出ているかどうか。</summary>
        public bool Visible { get; }

        /// <summary>持ち主の窓を使用不可にしているかどうか。</summary>
        public bool HoldsOwner { get; }
    }

    /// <summary>
    /// 人の応答を待つ表示を窓の一覧から見分ける。見分けるのは、持ち主の窓を使用不可にしている
    /// 可視の窓——モーダルの表示が持つ性質——であって、窓の種類ではない。種類で見ると、メッセージ
    /// ボックス以外の形で出る表示を取りこぼす。
    /// </summary>
    public static class ModalWindows
    {
        /// <summary>
        /// 当たる窓の文面。見出しと本文を持つものは両方を、本文が無いものは見出しだけを返す。
        /// 当たる窓が無ければ null。2つ以上あるときは先に見つかったものを採る——どれを答えても、
        /// 進めないことと、人の応答が要ることは変わらない。
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
