using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xunit;

namespace PmxEditorMcp.Tests
{
    [Collection(ModalWindowCollection.Name)]
    public sealed class DesktopModalWindowProbeTests
    {
        [Fact]
        public void AMessageBoxIsReported()
        {
            Assert.NotNull(WhileMessageBox(owner => Probe().TryDescribe()));
        }

        [Fact]
        public void AMessageBoxTheHostIsAnsweringIsNotReported()
        {
            Assert.Null(WhileMessageBox(owner =>
            {
                AnsweringDialogs.Add(owner);
                try
                {
                    return Probe().TryDescribe();
                }
                finally
                {
                    AnsweringDialogs.Remove(owner);
                }
            }));
        }

        [Fact]
        public void TheMessageBoxIsReportedAgainOnceTheAnswerEnds()
        {
            Assert.NotNull(WhileMessageBox(owner =>
            {
                AnsweringDialogs.Add(owner);
                AnsweringDialogs.Remove(owner);

                return Probe().TryDescribe();
            }));
        }

        [Fact]
        public void AMessageBoxTheHostGaveBackIsReported()
        {
            Assert.NotNull(WhileMessageBox(owner =>
            {
                AnsweringDialogs.Add(owner);
                try
                {
                    AnsweringDialogs.GiveBack(owner, WaitForBox(owner));

                    return Probe().TryDescribe();
                }
                finally
                {
                    AnsweringDialogs.Remove(owner);
                }
            }));
        }

        [Fact]
        public void AFormShownAsModalIsReportedEvenWhileTheHostIsAnswering()
        {
            Assert.NotNull(WhileModalForm(owner =>
            {
                AnsweringDialogs.Add(owner);
                try
                {
                    return Probe().TryDescribe();
                }
                finally
                {
                    AnsweringDialogs.Remove(owner);
                }
            }));
        }

        [Fact]
        public void AModalFormThatClosesWhileItsTextIsReadIsNotReported()
        {
            Assert.Null(WhileClosingOnRead(owner => Probe().TryDescribe()));
        }

        [Fact]
        public void AModalFormWithNeitherCaptionNorBodyThatStaysIsReported()
        {
            Assert.Equal(ModalWindows.Wordless, WhileModalForm(owner => Probe().TryDescribe(), string.Empty));
        }

        private static readonly TimeSpan LookLimit = TimeSpan.FromSeconds(10);

        private static DesktopModalWindowProbe Probe()
        {
            return new DesktopModalWindowProbe(TimeSpan.FromSeconds(1));
        }

        private static string WhileMessageBox(Func<IntPtr, string> look)
        {
            return OnSta(owner =>
            {
                string seen = null;
                Task.Run(() =>
                {
                    IntPtr box = WaitForBox(owner.Handle);
                    try
                    {
                        seen = look(owner.Handle);
                    }
                    finally
                    {
                        PostMessage(box, CloseMessage, IntPtr.Zero, IntPtr.Zero);
                    }
                });
                MessageBox.Show(owner, "訊く表示", "題");

                return seen;
            });
        }

        private static string WhileModalForm(Func<IntPtr, string> look)
        {
            return WhileModalForm(look, "訊く表示");
        }

        private static string WhileModalForm(Func<IntPtr, string> look, string title)
        {
            return OnSta(owner =>
            {
                string seen = null;
                using (Form asking = Offscreen(title))
                {
                    IntPtr handle = owner.Handle;
                    asking.Shown += (sender, e) => Task.Run(() =>
                    {
                        try
                        {
                            seen = look(handle);
                        }
                        finally
                        {
                            asking.BeginInvoke(new Action(asking.Close));
                        }
                    });
                    asking.ShowDialog(owner);
                }

                return seen;
            });
        }

        private static string WhileClosingOnRead(Func<IntPtr, string> look)
        {
            return OnSta(owner =>
            {
                Task<string> looking = null;
                using (ClosingOnRead asking = new ClosingOnRead(owner.Handle))
                {
                    IntPtr handle = owner.Handle;
                    asking.Shown += (sender, e) => looking = Task.Run(() => look(handle));
                    asking.ShowDialog(owner);

                    Stopwatch waited = Stopwatch.StartNew();
                    while (looking == null || !looking.Wait(10))
                    {
                        Assert.True(waited.Elapsed < LookLimit, "表示を探す処理が終わらない。");
                        Application.DoEvents();
                    }
                }

                return looking.Result;
            });
        }

        private static string OnSta(Func<Form, string> run)
        {
            string seen = null;
            Exception caught = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    using (Form owner = Offscreen("持ち主"))
                    {
                        owner.Show();
                        seen = run(owner);
                    }
                }
                catch (Exception exception)
                {
                    caught = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (caught != null)
            {
                throw new InvalidOperationException("STAのスレッドで落ちた。", caught);
            }

            return seen;
        }

        private static IntPtr WaitForBox(IntPtr owner)
        {
            for (int tried = 0; tried < 500; tried++)
            {
                IntPtr box = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "#32770", null);
                while (box != IntPtr.Zero)
                {
                    if (GetWindow(box, GetWindowOwner) == owner && IsWindowVisible(box))
                    {
                        return box;
                    }

                    box = FindWindowEx(IntPtr.Zero, box, "#32770", null);
                }

                Thread.Sleep(10);
            }

            throw new InvalidOperationException("メッセージボックスが出なかった。");
        }

        private static Form Offscreen(string text)
        {
            return new Form
            {
                Text = text,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000),
            };
        }

        private sealed class ClosingOnRead : Form
        {
            private const int GetTextMessage = 0x000D;

            private readonly IntPtr _owner;

            private bool _closing;

            internal ClosingOnRead(IntPtr owner)
            {
                _owner = owner;
                Text = "訊く表示";
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                Location = new Point(-32000, -32000);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == GetTextMessage && Visible && !_closing && InSendMessage())
                {
                    _closing = true;
                    ShowWindow(Handle, HideWindow);
                    EnableWindow(_owner, true);
                    BeginInvoke(new Action(Close));
                    m.Result = IntPtr.Zero;

                    return;
                }

                base.WndProc(ref m);
            }
        }

        private const int HideWindow = 0;

        private const uint CloseMessage = 0x0010;

        private const uint GetWindowOwner = 4;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint relation);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool InSendMessage();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr window, bool enable);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    }
}
