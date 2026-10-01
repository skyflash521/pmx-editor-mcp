using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 見張っている間、このプロセスの見えているダイアログ(#32770)を、画面に写る姿で見えた回と、
    /// 透明なまま見えた回に分けて数える。作ったスレッドでダイアログが前面に出る瞬間と、見回りの
    /// スレッドの巡回で数える。
    /// </summary>
    internal sealed class DialogSightings : IDisposable
    {
        private readonly Thread _thread;

        private readonly ManualResetEvent _stopped = new ManualResetEvent(false);

        private readonly uint _process = (uint)Process.GetCurrentProcess().Id;

        private readonly HookProc _activated;

        private readonly IntPtr _hook;

        private int _opaque;

        private int _transparent;

        internal DialogSightings()
        {
            _activated = Activated;
            _hook = SetWindowsHookEx(CbtHook, _activated, IntPtr.Zero, GetCurrentThreadId());
            _thread = new Thread(Run) { IsBackground = true, Name = "DialogSightings" };
            _thread.Start();
        }

        internal int Opaque
        {
            get { return _opaque; }
        }

        internal int Transparent
        {
            get { return _transparent; }
        }

        public void Dispose()
        {
            _stopped.Set();
            _thread.Join();
            UnhookWindowsHookEx(_hook);
            _stopped.Dispose();
        }

        private void Run()
        {
            do
            {
                EnumWindows(Visit, IntPtr.Zero);
            }
            while (!_stopped.WaitOne(1));
        }

        private IntPtr Activated(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code == ActivateCode)
            {
                Sight(wParam, false);
            }

            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        private bool Visit(IntPtr window, IntPtr state)
        {
            Sight(window, true);

            return true;
        }

        private void Sight(IntPtr window, bool visibleOnly)
        {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            if (owner != _process || (visibleOnly && !IsWindowVisible(window)))
            {
                return;
            }

            StringBuilder name = new StringBuilder(16);
            GetClassName(window, name, name.Capacity);
            if (name.ToString() != "#32770")
            {
                return;
            }

            if (Hidden(window))
            {
                Interlocked.Increment(ref _transparent);
            }
            else
            {
                Interlocked.Increment(ref _opaque);
            }
        }

        internal static bool Hidden(IntPtr window)
        {
            if ((GetWindowLong(window, ExtendedStyleIndex) & LayeredStyle) == 0)
            {
                return false;
            }

            uint key;
            byte alpha;
            uint flags;

            return GetLayeredWindowAttributes(window, out key, out alpha, out flags)
                && (flags & AlphaFlag) != 0
                && alpha == 0;
        }

        private const int CbtHook = 5;

        private const int ActivateCode = 5;

        private const int ExtendedStyleIndex = -20;

        private const int LayeredStyle = 0x80000;

        private const uint AlphaFlag = 0x2;

        private delegate bool WindowVisitor(IntPtr window, IntPtr state);

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(WindowVisitor visitor, IntPtr state);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int kind, HookProc proc, IntPtr module, uint threadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll")]
        private static extern bool GetLayeredWindowAttributes(
            IntPtr window, out uint key, out byte alpha, out uint flags);
    }
}
