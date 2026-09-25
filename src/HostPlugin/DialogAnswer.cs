using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PmxEditorMcp
{
    internal sealed class AnsweredDialog
    {
        private AnsweredDialog(
            string name, string caption, Func<IntPtr, IntPtr> field, string value, Action<IntPtr> press, Action<IntPtr> cancel)
        {
            Name = name;
            Caption = caption;
            Field = field;
            Value = value;
            Press = press;
            Cancel = cancel;
        }

        internal string Name { get; }

        /// <summary>null なら、持ち主の #32770 のうち答えを書く欄を持つものを当たりとする。</summary>
        internal string Caption { get; }

        internal Func<IntPtr, IntPtr> Field { get; }

        internal string Value { get; }

        internal Action<IntPtr> Press { get; }

        internal Action<IntPtr> Cancel { get; }

        internal static AnsweredDialog Save(string fileName)
        {
            return new AnsweredDialog(
                "保存のダイアログ",
                null,
                DialogAnswer.NameBox,
                fileName,
                dialog => DialogAnswer.Command(dialog, DialogAnswer.OkCommand),
                dialog => DialogAnswer.Command(dialog, DialogAnswer.CancelCommand));
        }

        /// <summary>エディタの InputDialog。<paramref name="caption"/> はエディタがその表示に付ける題。</summary>
        internal static AnsweredDialog Input(string caption, string value)
        {
            return new AnsweredDialog(
                "「" + caption + "」の入力ダイアログ",
                caption,
                dialog => DialogAnswer.Descendant(dialog, child => DialogAnswer.ClassHas(child, "EDIT")),
                value,
                dialog => DialogAnswer.Click(DialogAnswer.Descendant(
                    dialog, child => DialogAnswer.ClassHas(child, "BUTTON") && DialogAnswer.TextOf(child) == "OK")),
                DialogAnswer.Close);
        }
    }

    internal sealed class DialogAnswer
    {
        private static readonly TimeSpan DefaultPoll = TimeSpan.FromMilliseconds(20);

        private static readonly TimeSpan TextLimit = TimeSpan.FromSeconds(1);

        private readonly TimeSpan _poll;

        private readonly IntPtr _owner;

        private readonly uint _uiThread;

        private readonly AnsweredDialog _expected;

        private readonly HashSet<string> _quiet;

        private readonly TimeSpan _limit;

        private readonly DesktopModalWindowProbe _reader;

        private readonly ManualResetEvent _stopped = new ManualResetEvent(false);

        private readonly Thread _thread;

        private readonly Stopwatch _watch = new Stopwatch();

        private readonly Dictionary<IntPtr, TimeSpan> _unknownSince = new Dictionary<IntPtr, TimeSpan>();

        private readonly HashSet<IntPtr> _givenBack = new HashSet<IntPtr>();

        private IntPtr _answering;

        private TimeSpan _answeringSince;

        private bool _pressed;

        private bool _answered;

        private bool _settled;

        private string _failure;

        private delegate bool WindowVisitor(IntPtr window, IntPtr state);

        private DialogAnswer(
            IntPtr owner, uint uiThread, AnsweredDialog expected, IEnumerable<string> quiet, TimeSpan limit, TimeSpan poll)
        {
            _owner = owner;
            _uiThread = uiThread;
            _expected = expected;
            _quiet = new HashSet<string>(quiet, StringComparer.Ordinal);
            _limit = limit;
            _poll = poll;
            _reader = new DesktopModalWindowProbe(TextLimit);
            _thread = new Thread(Run) { IsBackground = true, Name = "DialogAnswer" };
        }

        /// <summary>
        /// UIスレッドの上で、<paramref name="owner"/> がダイアログを出していないときに呼ぶ。<see cref="Stop"/>
        /// までの間、<paramref name="owner"/> を持ち主とするダイアログ(#32770)と、題が <paramref name="expected"/>
        /// か <paramref name="quiet"/> に当たる表示はここが受け持ち、人の応答を待つ表示に数えない。
        /// <paramref name="expected"/> には答えを書いて押し、書けないまま、または押しても閉じないまま
        /// <paramref name="limit"/> が過ぎたら取り消して閉じる。<paramref name="quiet"/> の題の表示は、
        /// 自分で閉じるまで待つ。知らせの表示は文面を控えて閉じる。どれとも見分けられないダイアログは閉じず、
        /// <paramref name="limit"/> が過ぎたら人の応答を待つ表示として数えるよう戻す。<paramref name="expected"/>
        /// は null でよく、そのときは答えるダイアログを待たない。
        /// </summary>
        internal static DialogAnswer Start(
            IntPtr owner, AnsweredDialog expected, IEnumerable<string> quiet, TimeSpan limit)
        {
            return Start(owner, expected, quiet, limit, DefaultPoll);
        }

        /// <summary><paramref name="poll"/> はダイアログを見回す間隔。</summary>
        internal static DialogAnswer Start(
            IntPtr owner, AnsweredDialog expected, IEnumerable<string> quiet, TimeSpan limit, TimeSpan poll)
        {
            if (quiet == null)
            {
                throw new ArgumentNullException(nameof(quiet));
            }

            DialogAnswer answer = new DialogAnswer(owner, GetCurrentThreadId(), expected, quiet, limit, poll);
            IEnumerable<string> captions = expected != null && expected.Caption != null
                ? answer._quiet.Concat(new[] { expected.Caption })
                : answer._quiet;
            AnsweringDialogs.Add(owner, captions.ToList());
            answer._watch.Start();
            answer._thread.Start();

            return answer;
        }

        /// <summary>答えるダイアログへ答えて閉じられたか、答えるダイアログを待たなかったら null、そうでなければその事情を返す。</summary>
        internal string Stop()
        {
            _stopped.Set();
            _thread.Join();
            _stopped.Dispose();
            AnsweringDialogs.Remove(_owner);

            if (_failure != null)
            {
                return _failure;
            }

            return _expected == null || _answered ? null : _expected.Name + "が出なかった。";
        }

        private void Run()
        {
            do
            {
                foreach (KeyValuePair<IntPtr, string> dialog in Dialogs())
                {
                    if (dialog.Key == _answering)
                    {
                        continue;
                    }

                    if (_expected != null && !_settled && _answering == IntPtr.Zero && IsExpected(dialog.Key, dialog.Value))
                    {
                        _answering = dialog.Key;
                        _answeringSince = _watch.Elapsed;
                        continue;
                    }

                    if (!_quiet.Contains(dialog.Value))
                    {
                        Dismiss(dialog.Key);
                    }
                }

                if (_answering != IntPtr.Zero)
                {
                    Answer();
                }
            }
            while (!_stopped.WaitOne(_poll));

            if (_answering != IntPtr.Zero && _pressed)
            {
                Answer();
            }
        }

        private bool IsExpected(IntPtr dialog, string caption)
        {
            if (_expected.Caption != null)
            {
                return string.Equals(caption, _expected.Caption, StringComparison.Ordinal);
            }

            return GetWindow(dialog, GetWindowOwner) == _owner
                && IsClass(dialog, DialogClass)
                && _expected.Field(dialog) != IntPtr.Zero;
        }

        private void Answer()
        {
            if (!_pressed)
            {
                IntPtr field = _expected.Field(_answering);
                if (field != IntPtr.Zero && SetText(field, _expected.Value))
                {
                    _expected.Press(_answering);
                    _pressed = true;
                    _answeringSince = _watch.Elapsed;
                }
                else if (_watch.Elapsed - _answeringSince >= _limit)
                {
                    Settle(_expected.Name + "へ書き込めなかった。");
                }

                return;
            }

            if (!IsWindow(_answering) || !IsWindowVisible(_answering))
            {
                _answered = true;
                _answering = IntPtr.Zero;
                _settled = true;
            }
            else if (_watch.Elapsed - _answeringSince >= _limit)
            {
                Settle(_expected.Name + "が閉じなかった。");
            }
        }

        private void Settle(string failure)
        {
            _expected.Cancel(_answering);
            Fail(failure);
            _answering = IntPtr.Zero;
            _settled = true;
        }

        private void Dismiss(IntPtr dialog)
        {
            if (_givenBack.Contains(dialog))
            {
                return;
            }

            if (IsMessage(dialog))
            {
                string said = _reader.Said(dialog);
                Fail("エディタが表示を出したので閉じた: " + (said.Length == 0 ? "文面なし" : said));
                Close(dialog);

                return;
            }

            TimeSpan since;
            if (!_unknownSince.TryGetValue(dialog, out since))
            {
                _unknownSince[dialog] = _watch.Elapsed;

                return;
            }

            if (_watch.Elapsed - since >= _limit)
            {
                AnsweringDialogs.GiveBack(_owner, dialog);
                _givenBack.Add(dialog);
            }
        }

        private static bool IsMessage(IntPtr dialog)
        {
            if (!IsClass(dialog, DialogClass))
            {
                return false;
            }

            bool button = false;
            bool other = false;
            EnumChildWindows(
                dialog,
                (child, state) =>
                {
                    if (IsClass(child, ButtonClass))
                    {
                        button = true;
                    }
                    else if (!IsClass(child, StaticClass))
                    {
                        other = true;
                    }

                    return !other;
                },
                IntPtr.Zero);

            return button && !other;
        }

        private void Fail(string failure)
        {
            if (_failure == null)
            {
                _failure = failure;
            }
        }

        /// <summary>受け持つ表示と、その題。</summary>
        private List<KeyValuePair<IntPtr, string>> Dialogs()
        {
            List<IntPtr> visible = new List<IntPtr>();
            EnumThreadWindows(
                _uiThread,
                (window, state) =>
                {
                    if (IsWindowVisible(window))
                    {
                        visible.Add(window);
                    }

                    return true;
                },
                IntPtr.Zero);

            List<KeyValuePair<IntPtr, string>> found = new List<KeyValuePair<IntPtr, string>>();
            foreach (IntPtr window in visible)
            {
                bool owned = GetWindow(window, GetWindowOwner) == _owner && IsClass(window, DialogClass);
                string caption = _reader.Caption(window);
                bool named = _quiet.Contains(caption)
                    || (_expected != null && string.Equals(caption, _expected.Caption, StringComparison.Ordinal));
                if (owned || named)
                {
                    found.Add(new KeyValuePair<IntPtr, string>(window, caption));
                }
            }

            return found;
        }

        internal static IntPtr NameBox(IntPtr dialog)
        {
            // いまの形のダイアログの名前の欄は、FloatNotifySink に載った ComboBox の中の Edit で、番号を持たない。
            IntPtr floating = Descendant(
                dialog,
                child => IsClass(child, EditClass)
                    && IsClass(GetParent(child), ComboClass)
                    && IsClass(GetParent(GetParent(child)), FloatingClass));
            if (floating != IntPtr.Zero)
            {
                return floating;
            }

            // 古い形のダイアログの名前の欄は、cmb13 の中の Edit か、edt1 そのものである。
            IntPtr box = GetDlgItem(dialog, NameComboId);
            if (box != IntPtr.Zero)
            {
                if (IsClass(box, EditClass))
                {
                    return box;
                }

                IntPtr inner = Descendant(box, child => IsClass(child, EditClass));
                if (inner != IntPtr.Zero)
                {
                    return inner;
                }
            }

            IntPtr edit = GetDlgItem(dialog, NameEditId);

            return edit != IntPtr.Zero && IsClass(edit, EditClass) ? edit : IntPtr.Zero;
        }

        internal static IntPtr Descendant(IntPtr parent, Func<IntPtr, bool> wanted)
        {
            IntPtr found = IntPtr.Zero;
            EnumChildWindows(
                parent,
                (child, state) =>
                {
                    if (wanted(child))
                    {
                        found = child;

                        return false;
                    }

                    return true;
                },
                IntPtr.Zero);

            return found;
        }

        internal static void Command(IntPtr dialog, int command)
        {
            PostMessage(dialog, CommandMessage, new IntPtr(command), GetDlgItem(dialog, command));
        }

        internal static void Click(IntPtr button)
        {
            if (button != IntPtr.Zero)
            {
                PostMessage(button, ClickMessage, IntPtr.Zero, IntPtr.Zero);
            }
        }

        internal static void Close(IntPtr window)
        {
            PostMessage(window, CloseMessage, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>WinForms の部品のクラス名は、元の Win32 のクラス名を含む長い名前になる。</summary>
        internal static bool ClassHas(IntPtr window, string part)
        {
            return ClassOf(window).IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static string TextOf(IntPtr window)
        {
            StringBuilder text = new StringBuilder(256);
            IntPtr answered;
            SendMessageTimeout(
                window, GetTextMessage, new IntPtr(text.Capacity), text, AbortIfHung,
                (uint)TextLimit.TotalMilliseconds, out answered);

            return text.ToString();
        }

        private static bool SetText(IntPtr box, string value)
        {
            IntPtr answered;
            IntPtr sent = SendMessageTimeout(
                box, SetTextMessage, IntPtr.Zero, value, AbortIfHung, (uint)TextLimit.TotalMilliseconds, out answered);

            return sent != IntPtr.Zero && answered != IntPtr.Zero;
        }

        private static bool IsClass(IntPtr window, string name)
        {
            return window != IntPtr.Zero && string.Equals(ClassOf(window), name, StringComparison.Ordinal);
        }

        private static string ClassOf(IntPtr window)
        {
            StringBuilder found = new StringBuilder(256);
            GetClassName(window, found, found.Capacity);

            return found.ToString();
        }

        private const string DialogClass = "#32770";

        private const string EditClass = "Edit";

        private const string ButtonClass = "Button";

        private const string StaticClass = "Static";

        private const string ComboClass = "ComboBox";

        private const string FloatingClass = "FloatNotifySink";

        private const int NameComboId = 0x47C;

        private const int NameEditId = 0x480;

        internal const int OkCommand = 1;

        internal const int CancelCommand = 2;

        private const uint CommandMessage = 0x0111;

        private const uint CloseMessage = 0x0010;

        private const uint ClickMessage = 0x00F5;

        private const uint SetTextMessage = 0x000C;

        private const uint GetTextMessage = 0x000D;

        private const uint GetWindowOwner = 4;

        private const uint AbortIfHung = 0x0002;

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(uint threadId, WindowVisitor visitor, IntPtr state);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, WindowVisitor visitor, IntPtr state);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint relation);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgItem(IntPtr dialog, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr window,
            uint message,
            IntPtr wParam,
            string text,
            uint flags,
            uint limitMilliseconds,
            out IntPtr answered);

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
