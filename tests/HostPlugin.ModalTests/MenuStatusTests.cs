using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Xunit;

namespace PmxEditorMcp.Tests
{
    [Collection(ModalWindowCollection.Name)]
    public sealed class MenuStatusTests : IDisposable
    {
        private const string MenuTitle = "PMX Editor MCP";
        private const int YesCommand = 6;
        private const int NoCommand = 7;
        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

        private readonly string _directory;
        private readonly McpHost _host;
        private readonly HostLog _log;

        public MenuStatusTests()
        {
            string identity = Guid.NewGuid().ToString("N");
            _directory = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-test-" + identity);
            Directory.CreateDirectory(_directory);
            _log = new HostLog(Path.Combine(_directory, "host.log"));
            _host = new McpHost(
                "pmx-editor-mcp-test-" + identity,
                _log,
                ResponseBudget.Read(null),
                new InlineDispatcher(),
                (stream, generation) => { });
        }

        public void Dispose()
        {
            _host.Stop();
            WaitUntil(() => _host.Status == HostStatus.Stopped);
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        [Fact]
        public void AnsweringYesToTheStopQuestionStopsTheRunningHost()
        {
            StartHost();

            ShowStatusAnswering(YesCommand);

            Assert.True(WaitUntil(() => _host.Status == HostStatus.Stopped), "停止しなかった。");
        }

        [Fact]
        public void AnsweringNoToTheStopQuestionLeavesTheHostRunning()
        {
            StartHost();

            ShowStatusAnswering(NoCommand);

            Assert.Equal(HostStatus.Running, _host.Status);
        }

        [Fact]
        public void AnsweringYesToTheStartQuestionStartsTheStoppedHost()
        {
            StartHost();
            _host.Stop();
            Assert.True(WaitUntil(() => _host.Status == HostStatus.Stopped), "停止しなかった。");

            ShowStatusAnswering(YesCommand);

            Assert.True(WaitUntil(() => _host.Status == HostStatus.Running), "開始しなかった。");
        }

        private void StartHost()
        {
            string reason;
            Assert.True(_host.TryStart(out reason), reason);
        }

        private void ShowStatusAnswering(int command)
        {
            bool answered = false;
            Thread answering = new Thread(() =>
            {
                Stopwatch elapsed = Stopwatch.StartNew();
                while (elapsed.Elapsed < Limit && !answered)
                {
                    foreach (IntPtr dialog in DialogsTitled(MenuTitle))
                    {
                        DialogAnswer.Command(dialog, command);
                        answered = true;
                    }

                    Thread.Sleep(10);
                }
            });
            answering.Start();

            Exception caught = null;
            Thread showing = new Thread(() =>
            {
                try
                {
                    new PmxEditorMcpPlugin().ShowStatusOf(_host, _log);
                }
                catch (Exception exception)
                {
                    caught = exception;
                }
            });
            showing.SetApartmentState(ApartmentState.STA);
            showing.Start();
            Assert.True(showing.Join(Limit + Limit), "状態表示が閉じなかった。");
            answering.Join();
            if (caught != null)
            {
                throw new InvalidOperationException("STAのスレッドで落ちた。", caught);
            }

            Assert.True(answered, "問いの表示が現れなかった。");
        }

        private static bool WaitUntil(Func<bool> condition)
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < Limit)
            {
                if (condition())
                {
                    return true;
                }

                Thread.Sleep(10);
            }

            return condition();
        }

        private static List<IntPtr> DialogsTitled(string title)
        {
            List<IntPtr> found = new List<IntPtr>();
            uint own = (uint)Process.GetCurrentProcess().Id;
            EnumWindows(
                (window, state) =>
                {
                    uint owner;
                    GetWindowThreadProcessId(window, out owner);
                    if (owner != own || !IsWindowVisible(window))
                    {
                        return true;
                    }

                    StringBuilder name = new StringBuilder(64);
                    GetClassName(window, name, name.Capacity);
                    StringBuilder caption = new StringBuilder(256);
                    GetWindowText(window, caption, caption.Capacity);
                    if (name.ToString() == "#32770" && caption.ToString() == title)
                    {
                        found.Add(window);
                    }

                    return true;
                },
                IntPtr.Zero);
            return found;
        }

        private delegate bool EnumProc(IntPtr window, IntPtr state);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumProc callback, IntPtr state);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint owner);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder text, int length);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);

        private sealed class InlineDispatcher : IUiDispatcher
        {
            public IAsyncResult Begin(Action action)
            {
                action();

                return new FinishedPending();
            }

            public bool Wait(IAsyncResult pending, TimeSpan limit)
            {
                return true;
            }

            public void End(IAsyncResult pending)
            {
            }
        }
    }
}
