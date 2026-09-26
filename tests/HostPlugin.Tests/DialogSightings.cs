using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 見張っている間、このプロセスの見えているダイアログ(#32770)を見回り、画面に写る姿で見えた回と、
    /// 透明なまま見えた回を数える。
    /// </summary>
    internal sealed class DialogSightings : IDisposable
    {
        private readonly Thread _thread;

        private readonly ManualResetEvent _stopped = new ManualResetEvent(false);

        private readonly uint _process = (uint)Process.GetCurrentProcess().Id;

        private int _opaque;

        private int _transparent;

        internal DialogSightings()
        {
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

        private bool Visit(IntPtr window, IntPtr state)
        {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            if (owner != _process || !IsWindowVisible(window))
            {
                return true;
            }

            StringBuilder name = new StringBuilder(16);
            GetClassName(window, name, name.Capacity);
            if (name.ToString() != "#32770")
            {
                return true;
            }

            if (Hidden(window))
            {
                _transparent++;
            }
            else
            {
                _opaque++;
            }

            return true;
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

        private const int ExtendedStyleIndex = -20;

        private const int LayeredStyle = 0x80000;

        private const uint AlphaFlag = 0x2;

        private delegate bool WindowVisitor(IntPtr window, IntPtr state);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(WindowVisitor visitor, IntPtr state);

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
