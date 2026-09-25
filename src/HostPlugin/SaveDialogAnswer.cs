using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PmxEditorMcp
{
    internal sealed class SaveDialogAnswer
    {
        private static readonly TimeSpan DefaultPoll = TimeSpan.FromMilliseconds(20);

        private static readonly TimeSpan TextLimit = TimeSpan.FromSeconds(1);

        private readonly TimeSpan _poll;

        private readonly IntPtr _owner;

        private readonly uint _uiThread;

        private readonly string _fileName;

        private readonly TimeSpan _limit;

        private readonly DesktopModalWindowProbe _reader;

        private readonly ManualResetEvent _stopped = new ManualResetEvent(false);

        private readonly Thread _thread;

        private readonly Stopwatch _watch = new Stopwatch();

        private readonly Dictionary<IntPtr, TimeSpan> _unknownSince = new Dictionary<IntPtr, TimeSpan>();

        private readonly HashSet<IntPtr> _givenBack = new HashSet<IntPtr>();

        private IntPtr _saving;

        private TimeSpan _savingSince;

        private bool _pressed;

        private bool _saved;

        private bool _settled;

        private string _failure;

        private delegate bool WindowVisitor(IntPtr window, IntPtr state);

        private SaveDialogAnswer(IntPtr owner, uint uiThread, string fileName, TimeSpan limit, TimeSpan poll)
        {
            _owner = owner;
            _uiThread = uiThread;
            _fileName = fileName;
            _limit = limit;
            _poll = poll;
            _reader = new DesktopModalWindowProbe(TextLimit);
            _thread = new Thread(Run) { IsBackground = true, Name = "SaveDialogAnswer" };
        }

        /// <summary>
        /// UIスレッドの上で、<paramref name="owner"/> がダイアログを出していないときに呼ぶ。<see cref="Stop"/>
        /// までの間、<paramref name="owner"/> を持ち主とするダイアログはここが受け持ち、人の応答を待つ表示に
        /// 数えない。保存のダイアログには <paramref name="fileName"/> を入れて保存を押し、名前の欄へ書けない
        /// まま、または押しても閉じないまま <paramref name="limit"/> が過ぎたら取り消して閉じる。知らせの
        /// 表示は文面を控えて閉じる。どちらとも見分けられないダイアログは閉じず、<paramref name="limit"/> が
        /// 過ぎたら人の応答を待つ表示として数えるよう戻す。
        /// </summary>
        internal static SaveDialogAnswer Start(IntPtr owner, string fileName, TimeSpan limit)
        {
            return Start(owner, fileName, limit, DefaultPoll);
        }

        /// <summary><paramref name="poll"/> はダイアログを見回す間隔。</summary>
        internal static SaveDialogAnswer Start(IntPtr owner, string fileName, TimeSpan limit, TimeSpan poll)
        {
            SaveDialogAnswer answer = new SaveDialogAnswer(owner, GetCurrentThreadId(), fileName, limit, poll);
            AnsweringDialogs.Add(owner);
            answer._watch.Start();
            answer._thread.Start();

            return answer;
        }

        /// <summary>保存のダイアログへ答えて閉じられたら null、そうでなければその事情を返す。</summary>
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

            return _saved ? null : "保存のダイアログが出なかった。";
        }

        private void Run()
        {
            do
            {
                foreach (IntPtr dialog in Dialogs())
                {
                    if (dialog == _saving)
                    {
                        continue;
                    }

                    if (!_settled && _saving == IntPtr.Zero && IsSaveDialog(dialog))
                    {
                        _saving = dialog;
                        _savingSince = _watch.Elapsed;
                        continue;
                    }

                    Dismiss(dialog);
                }

                if (_saving != IntPtr.Zero)
                {
                    Answer();
                }
            }
            while (!_stopped.WaitOne(_poll));

            if (_saving != IntPtr.Zero && _pressed)
            {
                Answer();
            }
        }

        private void Answer()
        {
            if (!_pressed)
            {
                IntPtr name = NameBox(_saving);
                if (name != IntPtr.Zero && SetText(name))
                {
                    PostMessage(_saving, CommandMessage, new IntPtr(OkCommand), GetDlgItem(_saving, OkCommand));
                    _pressed = true;
                    _savingSince = _watch.Elapsed;
                }
                else if (_watch.Elapsed - _savingSince >= _limit)
                {
                    Settle("保存のダイアログの名前の欄へ書き込めなかった。");
                }

                return;
            }

            if (!IsWindow(_saving) || !IsWindowVisible(_saving))
            {
                _saved = true;
                _saving = IntPtr.Zero;
                _settled = true;
            }
            else if (_watch.Elapsed - _savingSince >= _limit)
            {
                Settle("保存のダイアログが「保存」で閉じなかった。");
            }
        }

        private void Settle(string failure)
        {
            PostMessage(_saving, CommandMessage, new IntPtr(CancelCommand), GetDlgItem(_saving, CancelCommand));
            Fail(failure);
            _saving = IntPtr.Zero;
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
                PostMessage(dialog, CloseMessage, IntPtr.Zero, IntPtr.Zero);

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

        private List<IntPtr> Dialogs()
        {
            List<IntPtr> found = new List<IntPtr>();
            EnumThreadWindows(
                _uiThread,
                (window, state) =>
                {
                    if (IsWindowVisible(window)
                        && GetWindow(window, GetWindowOwner) == _owner
                        && IsClass(window, DialogClass))
                    {
                        found.Add(window);
                    }

                    return true;
                },
                IntPtr.Zero);

            return found;
        }

        private static bool IsSaveDialog(IntPtr dialog)
        {
            return NameBox(dialog) != IntPtr.Zero;
        }

        private static IntPtr NameBox(IntPtr dialog)
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

        private static IntPtr Descendant(IntPtr parent, Func<IntPtr, bool> wanted)
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

        private bool SetText(IntPtr box)
        {
            IntPtr answered;
            IntPtr sent = SendMessageTimeout(
                box, SetTextMessage, IntPtr.Zero, _fileName, AbortIfHung, (uint)TextLimit.TotalMilliseconds, out answered);

            return sent != IntPtr.Zero && answered != IntPtr.Zero;
        }

        private static bool IsClass(IntPtr window, string name)
        {
            if (window == IntPtr.Zero)
            {
                return false;
            }

            StringBuilder found = new StringBuilder(64);
            GetClassName(window, found, found.Capacity);

            return string.Equals(found.ToString(), name, StringComparison.Ordinal);
        }

        private const string DialogClass = "#32770";

        private const string EditClass = "Edit";

        private const string ButtonClass = "Button";

        private const string StaticClass = "Static";

        private const string ComboClass = "ComboBox";

        private const string FloatingClass = "FloatNotifySink";

        private const int NameComboId = 0x47C;

        private const int NameEditId = 0x480;

        private const int OkCommand = 1;

        private const int CancelCommand = 2;

        private const uint CommandMessage = 0x0111;

        private const uint CloseMessage = 0x0010;

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
