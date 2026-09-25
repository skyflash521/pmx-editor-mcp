using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PmxEditorMcp
{
    /// <summary>
    /// このプロセスが出している窓を数え上げて、人の応答を待つ表示を探す。呼び先はビルド時に
    /// 決まるので、名前で引く経路は持たない。窓の文字列を得るのはその窓のスレッドへの要求なので、
    /// 待つ長さに上限を置く——上限が無いと、UIスレッドが応答しないときにこの見張り自身が戻らなくなり、
    /// 何が起きているかを伝えるという役目を果たせない。
    /// </summary>
    public sealed class DesktopModalWindowProbe : IModalWindowProbe
    {
        private const int MaxTextLength = 512;

        private readonly TimeSpan _textLimit;

        private delegate bool WindowVisitor(IntPtr window, IntPtr state);

        /// <summary>窓の文字列を得るのに待つ長さの上限を与えて生成する。</summary>
        public DesktopModalWindowProbe(TimeSpan textLimit)
        {
            if (textLimit <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(textLimit), "待つ長さの上限は正の長さで与える。");
            }

            _textLimit = textLimit;
        }

        /// <summary>出ている表示の文面。出ていなければ null。</summary>
        public string TryDescribe()
        {
            return ModalWindows.Describe(Windows());
        }

        internal string Said(IntPtr window)
        {
            return ModalWindows.Describe(new[] { new WindowNote(Text(window), Body(window), true, true) })
                ?? string.Empty;
        }

        private IEnumerable<WindowNote> Windows()
        {
            List<WindowNote> notes = new List<WindowNote>();
            using (Process self = Process.GetCurrentProcess())
            {
                foreach (ProcessThread thread in self.Threads)
                {
                    List<IntPtr> windows = new List<IntPtr>();
                    EnumThreadWindows(
                        (uint)thread.Id,
                        (window, state) =>
                        {
                            windows.Add(window);

                            return true;
                        },
                        IntPtr.Zero);
                    foreach (IntPtr window in windows)
                    {
                        // 文字列の取得は同じプロセスの窓へ要求を送るので、送る先を、持ち主を
                        // 使用不可にしている可視の窓だけに絞る。
                        IntPtr owner = GetWindow(window, GetWindowOwner);
                        bool holdsOwner = owner != IntPtr.Zero
                            && !IsWindowEnabled(owner)
                            && !(AnsweringDialogs.Hides(owner, window) && IsDialog(window));
                        bool visible = IsWindowVisible(window);
                        notes.Add(visible && holdsOwner
                            ? new WindowNote(Text(window), Body(window), true, true)
                            : new WindowNote(string.Empty, string.Empty, visible, holdsOwner));
                    }
                }
            }

            return notes;
        }

        /// <summary>
        /// 窓が見せている本文。子の文字列を順につないだもので、持たない窓では空になる。押す先の
        /// 文言も混ざるが、何を訊かれているかは本文の側に出る。
        /// </summary>
        private string Body(IntPtr window)
        {
            List<IntPtr> children = new List<IntPtr>();
            EnumChildWindows(
                window,
                (child, state) =>
                {
                    children.Add(child);

                    return true;
                },
                IntPtr.Zero);
            StringBuilder body = new StringBuilder();
            foreach (IntPtr child in children)
            {
                if (!IsStatic(child))
                {
                    continue;
                }

                string text = Text(child);
                if (text.Length == 0)
                {
                    continue;
                }

                if (body.Length != 0)
                {
                    body.Append(' ');
                }

                body.Append(text);
            }

            return body.ToString();
        }

        private static bool IsStatic(IntPtr window)
        {
            return IsClass(window, "Static");
        }

        private static bool IsDialog(IntPtr window)
        {
            return IsClass(window, "#32770");
        }

        private static bool IsClass(IntPtr window, string wanted)
        {
            StringBuilder name = new StringBuilder(MaxTextLength);
            GetClassName(window, name, name.Capacity);

            return string.Equals(name.ToString(), wanted, StringComparison.Ordinal);
        }

        /// <summary>
        /// 窓の文字列。上限までに応答が無ければ空とする——応答しない窓から文面は得られないが、
        /// 得られないことを理由に見張りが止まってはならない。
        /// </summary>
        private string Text(IntPtr window)
        {
            StringBuilder text = new StringBuilder(MaxTextLength);
            IntPtr answered;
            IntPtr got = SendMessageTimeout(
                window,
                GetTextMessage,
                new IntPtr(text.Capacity),
                text,
                AbortIfHung,
                (uint)_textLimit.TotalMilliseconds,
                out answered);

            return got == IntPtr.Zero ? string.Empty : text.ToString();
        }

        private const uint GetWindowOwner = 4;

        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(
            uint threadId, WindowVisitor visitor, IntPtr state);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(
            IntPtr parent, WindowVisitor visitor, IntPtr state);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint relation);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr window);

        /// <summary>文字列を得る要求。</summary>
        private const uint GetTextMessage = 0x000D;

        /// <summary>応答しない窓では待たずに戻る。</summary>
        private const uint AbortIfHung = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr window,
            uint message,
            IntPtr count,
            StringBuilder text,
            uint flags,
            uint limitMilliseconds,
            out IntPtr answered);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder text, int count);
    }
}
