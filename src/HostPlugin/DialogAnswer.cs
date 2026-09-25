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
            string name, string form, Func<IntPtr, IntPtr> field, string value, Action<IntPtr> press, Action<IntPtr> cancel)
        {
            Name = name;
            Form = form;
            Field = field;
            Value = value;
            Press = press;
            Cancel = cancel;
        }

        internal string Name { get; }

        /// <summary>
        /// 当たりとするエディタの WinForms のフォームの Name。null なら、持ち主の #32770 のうち答えを書く欄を持つものを当たりとする。
        /// </summary>
        internal string Form { get; }

        /// <summary>null なら、答えを書かずに押すだけで答える。</summary>
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

        /// <summary>エディタの InputDialog。</summary>
        internal static AnsweredDialog Input(string value)
        {
            return new AnsweredDialog(
                "入力ダイアログ",
                "InputDialog",
                dialog => DialogAnswer.Named(dialog, "txtMessage"),
                value,
                dialog => DialogAnswer.Click(DialogAnswer.Named(dialog, "btnOK")),
                DialogAnswer.Close);
        }

        /// <summary>
        /// Name が <paramref name="form"/> のエディタの WinForms のフォーム。Name が <paramref name="button"/> のボタンを押して答える。
        /// </summary>
        internal static AnsweredDialog Pressed(string form, string button)
        {
            return new AnsweredDialog(
                form + " の表示",
                form,
                null,
                null,
                dialog => DialogAnswer.Click(DialogAnswer.Named(dialog, button)),
                DialogAnswer.Close);
        }
    }

    internal sealed class DialogAnswer
    {
        private static readonly TimeSpan DefaultPoll = TimeSpan.FromMilliseconds(20);

        private static readonly TimeSpan TextLimit = TimeSpan.FromSeconds(1);

        /// <summary>ツールが <see cref="Start(IntPtr, AnsweredDialog, IEnumerable{string}, TimeSpan)"/> へ渡す上限。</summary>
        internal static TimeSpan Limit { get; set; } = TimeSpan.FromSeconds(10);

        private readonly TimeSpan _poll;

        private readonly IntPtr _owner;

        private readonly uint _uiThread;

        private readonly List<AnsweredDialog> _expected;

        private int _questions;

        private int _cautions;

        private readonly List<string> _agreed = new List<string>();

        private readonly HashSet<IntPtr> _handled = new HashSet<IntPtr>();

        private readonly HashSet<IntPtr> _before = new HashSet<IntPtr>();

        private readonly bool _acknowledging;

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

        private int _next;

        private string _failure;

        private delegate bool WindowVisitor(IntPtr window, IntPtr state);

        private DialogAnswer(
            IntPtr owner,
            uint uiThread,
            IEnumerable<AnsweredDialog> expected,
            int questions,
            int cautions,
            IEnumerable<string> quiet,
            TimeSpan limit,
            TimeSpan poll,
            bool acknowledging)
        {
            _owner = owner;
            _acknowledging = acknowledging;
            _uiThread = uiThread;
            _expected = expected.ToList();
            _questions = questions;
            _cautions = cautions;
            _quiet = new HashSet<string>(quiet, StringComparer.Ordinal);
            _limit = limit;
            _poll = poll;
            _reader = new DesktopModalWindowProbe(TextLimit);
            _thread = new Thread(Run) { IsBackground = true, Name = "DialogAnswer" };
        }

        /// <summary>
        /// UIスレッドの上で、<paramref name="owner"/> がダイアログを出していないときに呼ぶ。<see cref="Stop"/>
        /// までの間、<paramref name="owner"/> を持ち主とするダイアログ(#32770)と、<paramref name="expected"/>
        /// か <paramref name="quiet"/> に当たる WinForms のフォームはここが受け持ち、人の応答を待つ表示に数えない。
        /// <paramref name="quiet"/> はフォームの Name の並び。<paramref name="expected"/> には答えを書いて押し、書けないまま、
        /// または押しても閉じないまま <paramref name="limit"/> が過ぎたら取り消して閉じる。<paramref name="quiet"/> の
        /// フォームは、自分で閉じるまで待つ。知らせの表示は文面を控えて閉じる。どれとも見分けられないダイアログは閉じず、
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

            return Begin(
                owner, expected == null ? new AnsweredDialog[0] : new[] { expected }, 0, 0, quiet, limit, poll, false);
        }

        /// <summary>
        /// UIスレッドの上で呼ぶ。<see cref="Stop"/> までの間に新しく出た表示を、持ち主を問わず受け持つ。<paramref name="expected"/>
        /// には並びの順に答える。はいといいえを持つ問いは、先に出た <paramref name="questions"/> 個まで、はいを押す。
        /// ボタンが1つだけでアイコンの付いた知らせは、先に出た <paramref name="cautions"/> 個まで、そのボタンを押す。ボタンが
        /// 1つだけでアイコンの無い知らせは、そのボタンを押す。押した表示の文面は <see cref="Agreed"/> へ控える。ほかの
        /// 表示は、いいえか取り消しで閉じる。見分けられないものは <paramref name="limit"/> が過ぎたら取り消して閉じる。
        /// </summary>
        internal static DialogAnswer StartAcknowledging(
            IEnumerable<AnsweredDialog> expected, int questions, int cautions, TimeSpan limit)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            return Begin(IntPtr.Zero, expected, questions, cautions, new string[0], limit, DefaultPoll, true);
        }

        private static DialogAnswer Begin(
            IntPtr owner,
            IEnumerable<AnsweredDialog> expected,
            int questions,
            int cautions,
            IEnumerable<string> quiet,
            TimeSpan limit,
            TimeSpan poll,
            bool acknowledging)
        {
            DialogAnswer answer = new DialogAnswer(
                owner, GetCurrentThreadId(), expected, questions, cautions, quiet, limit, poll, acknowledging);
            answer._before.UnionWith(answer.Visible());
            AnsweringDialogs.Add(owner, answer._quiet.Concat(answer.Forms()).ToList());
            answer._watch.Start();
            answer._thread.Start();

            return answer;
        }

        /// <summary>答える表示もはいで答える問いも持たない <see cref="StartAcknowledging(IEnumerable{AnsweredDialog}, int, int, TimeSpan)"/>。</summary>
        internal static DialogAnswer StartAcknowledging(TimeSpan limit)
        {
            return StartAcknowledging(new AnsweredDialog[0], 0, 0, limit);
        }

        /// <summary><see cref="Stop"/> までに、はいかOKを押して答えた知らせの文面。出た順に並ぶ。</summary>
        internal IList<string> Agreed
        {
            get { return _agreed.AsReadOnly(); }
        }

        /// <summary>答えるダイアログのすべてへ答えて閉じられたら null、そうでなければその事情を返す。</summary>
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

            return _next < _expected.Count ? _expected[_next].Name + "が出なかった。" : null;
        }

        private AnsweredDialog Current
        {
            get { return _next < _expected.Count ? _expected[_next] : null; }
        }

        private IEnumerable<string> Forms()
        {
            return _expected.Where(one => one.Form != null).Select(one => one.Form);
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

                    if (Current != null && _answering == IntPtr.Zero && IsExpected(dialog.Key, dialog.Value))
                    {
                        _answering = dialog.Key;
                        _answeringSince = _watch.Elapsed;
                        continue;
                    }

                    if (dialog.Value == null || !_quiet.Contains(dialog.Value))
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

        private bool IsExpected(IntPtr dialog, string form)
        {
            if (_handled.Contains(dialog))
            {
                return false;
            }

            if (Current.Form != null)
            {
                return string.Equals(form, Current.Form, StringComparison.Ordinal);
            }

            return IsOwned(dialog)
                && IsClass(dialog, DialogClass)
                && Current.Field != null
                && Current.Field(dialog) != IntPtr.Zero;
        }

        private static bool HoldsOwner(IntPtr window)
        {
            IntPtr owner = GetWindow(window, GetWindowOwner);

            return owner != IntPtr.Zero && !IsWindowEnabled(owner);
        }

        private bool IsOwned(IntPtr dialog)
        {
            return _owner == IntPtr.Zero || GetWindow(dialog, GetWindowOwner) == _owner;
        }

        private void Answer()
        {
            if (!_pressed)
            {
                IntPtr field = Current.Field == null ? IntPtr.Zero : Current.Field(_answering);
                if (Current.Field == null || (field != IntPtr.Zero && SetText(field, Current.Value)))
                {
                    Current.Press(_answering);
                    _pressed = true;
                    _answeringSince = _watch.Elapsed;
                }
                else if (_watch.Elapsed - _answeringSince >= _limit)
                {
                    Settle(Current.Name + "へ書き込めなかった。");
                }

                return;
            }

            if (!IsWindow(_answering) || !IsWindowVisible(_answering))
            {
                _handled.Add(_answering);
                _answering = IntPtr.Zero;
                _pressed = false;
                _next++;
            }
            else if (_watch.Elapsed - _answeringSince >= _limit)
            {
                Settle(Current.Name + "が閉じなかった。");
            }
        }

        private void Settle(string failure)
        {
            Current.Cancel(_answering);
            Fail(failure);
            _handled.Add(_answering);
            _answering = IntPtr.Zero;
            _pressed = false;
            _next = _expected.Count;
        }

        private void Dismiss(IntPtr dialog)
        {
            if (_givenBack.Contains(dialog) || _handled.Contains(dialog))
            {
                return;
            }

            if (IsMessage(dialog))
            {
                _handled.Add(dialog);
                if (Agreeable(dialog))
                {
                    _agreed.Add(_reader.Message(dialog));
                    Agree(dialog);

                    return;
                }

                string said = _reader.Said(dialog);
                Fail("エディタが表示を出したので閉じた: " + (said.Length == 0 ? "文面なし" : said));
                Refuse(dialog);

                return;
            }

            TimeSpan since;
            if (!_unknownSince.TryGetValue(dialog, out since))
            {
                _unknownSince[dialog] = _watch.Elapsed;

                return;
            }

            if (_watch.Elapsed - since < _limit)
            {
                return;
            }

            if (_owner != IntPtr.Zero)
            {
                AnsweringDialogs.GiveBack(_owner, dialog);
                _givenBack.Add(dialog);

                return;
            }

            _handled.Add(dialog);
            string shown = _reader.Said(dialog);
            Fail("エディタが表示を出したので閉じた: " + (shown.Length == 0 ? "文面なし" : shown));
            if (IsClass(dialog, DialogClass))
            {
                Refuse(dialog);
            }
            else
            {
                Close(dialog);
            }
        }

        private bool Agreeable(IntPtr dialog)
        {
            if (GetDlgItem(dialog, YesCommand) != IntPtr.Zero)
            {
                return GetDlgItem(dialog, NoCommand) != IntPtr.Zero && _questions-- > 0;
            }

            if (Buttons(dialog) != 1)
            {
                return false;
            }

            return HasIcon(dialog) ? _cautions-- > 0 : _acknowledging;
        }

        private static int Buttons(IntPtr dialog)
        {
            int count = 0;
            EnumChildWindows(
                dialog,
                (child, state) =>
                {
                    if (IsClass(child, ButtonClass))
                    {
                        count++;
                    }

                    return true;
                },
                IntPtr.Zero);

            return count;
        }

        /// <summary>知らせのアイコンは、SS_ICON の形のラベルとして置かれる。</summary>
        private static bool HasIcon(IntPtr dialog)
        {
            bool found = false;
            EnumChildWindows(
                dialog,
                (child, state) =>
                {
                    found = IsClass(child, StaticClass) && (GetWindowLong(child, StyleIndex) & StaticTypeMask) == IconStatic;

                    return !found;
                },
                IntPtr.Zero);

            return found;
        }

        /// <summary>OK だけの知らせでは、OK のボタンが取り消しの番号を持つ。</summary>
        private static void Agree(IntPtr dialog)
        {
            Press(dialog, YesCommand, OkCommand, CancelCommand);
        }

        private static void Refuse(IntPtr dialog)
        {
            Press(dialog, NoCommand, CancelCommand, OkCommand);
        }

        private static void Press(IntPtr dialog, params int[] commands)
        {
            foreach (int command in commands)
            {
                if (GetDlgItem(dialog, command) != IntPtr.Zero)
                {
                    Command(dialog, command);

                    return;
                }
            }

            Close(dialog);
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

        private List<IntPtr> Visible()
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

            return visible;
        }

        /// <summary>受け持つ表示と、それが WinForms のフォームならその Name。</summary>
        private List<KeyValuePair<IntPtr, string>> Dialogs()
        {
            List<KeyValuePair<IntPtr, string>> found = new List<KeyValuePair<IntPtr, string>>();
            foreach (IntPtr window in Visible())
            {
                bool fresh = _owner == IntPtr.Zero && !_before.Contains(window);
                bool dialog = IsClass(window, DialogClass);
                bool owned = dialog && (_owner == IntPtr.Zero ? fresh : IsOwned(window));
                bool modal = fresh && !dialog && HoldsOwner(window);
                string form = AnsweringDialogs.FormName(window);
                bool named = form != null && (_quiet.Contains(form) || Forms().Contains(form, StringComparer.Ordinal));
                if (owned || modal || named)
                {
                    found.Add(new KeyValuePair<IntPtr, string>(window, form));
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

        /// <summary><paramref name="parent"/> の中の、Name が <paramref name="name"/> の WinForms の部品。</summary>
        internal static IntPtr Named(IntPtr parent, string name)
        {
            return Descendant(
                parent, child => string.Equals(AnsweringDialogs.FormName(child), name, StringComparison.Ordinal));
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

        private const int YesCommand = 6;

        private const int NoCommand = 7;

        private const int StyleIndex = -16;

        private const int StaticTypeMask = 0x1F;

        private const int IconStatic = 0x03;

        private const uint CommandMessage = 0x0111;

        private const uint CloseMessage = 0x0010;

        private const uint ClickMessage = 0x00F5;

        private const uint SetTextMessage = 0x000C;


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
        private static extern bool IsWindowEnabled(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr window, int index);

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
        private static extern int GetClassName(IntPtr window, StringBuilder text, int count);
    }
}
