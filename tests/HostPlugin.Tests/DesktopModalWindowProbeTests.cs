using System;
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
            return OnSta(owner =>
            {
                string seen = null;
                using (Form asking = Offscreen("訊く表示"))
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

        private const uint CloseMessage = 0x0010;

        private const uint GetWindowOwner = 4;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint relation);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    }
}
