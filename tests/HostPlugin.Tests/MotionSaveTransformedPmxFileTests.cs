using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class MotionSaveTransformedPmxFileTests
    {
        [Fact]
        public void TheTransformedShapeIsWrittenToTheGivenPath()
        {
            using (Folder folder = new Folder())
            {
                string path = folder.Path("変形後.pmx");
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    {
                        ComposedScreenFixture.Value(Call(fixture, path, true));

                        Assert.Equal(1, screen.Saves);
                    }
                });

                Assert.Equal(Screen.Written, File.ReadAllText(path));
                Assert.Equal(new[] { path }, Directory.GetFiles(folder.Root));
            }
        }

        [Fact]
        public void AnExistingFileIsReplaced()
        {
            using (Folder folder = new Folder())
            {
                string path = folder.Path("変形後.pmx");
                File.WriteAllText(path, "前の中身");
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    {
                        ComposedScreenFixture.Value(Call(fixture, path, true));
                    }
                });

                Assert.Equal(Screen.Written, File.ReadAllText(path));
                Assert.Equal(new[] { path }, Directory.GetFiles(folder.Root));
            }
        }

        [Fact]
        public void TheNormalizationIsOffWhileSavingAndComesBack()
        {
            using (Folder folder = new Folder())
            {
                string path = folder.Path("変形後.pmx");
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    {
                        screen.Normalize.Checked = true;

                        ComposedScreenFixture.Value(Call(fixture, path, true));

                        Assert.Equal(new[] { false }, screen.NormalizedWhenSaved);
                        Assert.True(screen.Normalize.Checked, "正規化の入り切りを元へ戻していない。");
                    }
                });
            }
        }

        [Fact]
        public void ACallWithoutConfirmIsRefusedWithoutSaving()
        {
            using (Folder folder = new Folder())
            {
                string path = folder.Path("変形後.pmx");
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    {
                        Assert.Equal(
                            ToolEnvelope.ConfirmRequired, ComposedScreenFixture.Code(Call(fixture, path, false)));
                        Assert.Equal(0, screen.Saves);
                    }
                });

                Assert.False(File.Exists(path));
            }
        }

        [Fact]
        public void AClosedTransformViewIsRefused()
        {
            using (Folder folder = new Folder())
            {
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    {
                        Assert.Equal(
                            ToolEnvelope.NotApplicable,
                            ComposedScreenFixture.Code(Call(fixture, folder.Path("変形後.pmx"), true)));
                    }
                });
            }
        }

        [Fact]
        public void APathOutsideAnExistingFolderIsRefused()
        {
            using (Folder folder = new Folder())
            {
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    {
                        Assert.Equal(
                            ToolEnvelope.InvalidArgument,
                            ComposedScreenFixture.Code(Call(fixture, "変形後.pmx", true)));
                        Assert.Equal(
                            ToolEnvelope.InvalidArgument,
                            ComposedScreenFixture.Code(Call(fixture, folder.Path("無い\\変形後.pmx"), true)));
                        Assert.Equal(
                            ToolEnvelope.InvalidArgument,
                            ComposedScreenFixture.Code(Call(fixture, folder.Root.Substring(0, 2) + "変形後.pmx", true)));
                        Assert.Equal(
                            ToolEnvelope.InvalidArgument,
                            ComposedScreenFixture.Code(Call(fixture, folder.Root.Substring(2) + "\\変形後.pmx", true)));
                        Assert.Equal(0, screen.Saves);
                    }
                });
            }
        }

        [Fact]
        public void TheEditorWritingNothingIsAFailure()
        {
            using (Folder folder = new Folder())
            {
                string path = folder.Path("変形後.pmx");
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    {
                        screen.FailOnWrite = true;

                        Assert.Equal(
                            ToolEnvelope.OperationFailed, ComposedScreenFixture.Code(Call(fixture, path, true)));
                        Assert.Equal(1, screen.Saves);
                    }
                });

                Assert.Empty(Directory.GetFiles(folder.Root));
            }
        }

        [Fact]
        public void NoDialogShownIsAFailure()
        {
            using (Folder folder = new Folder())
            {
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    {
                        screen.SkipDialog = true;

                        Assert.Equal(
                            ToolEnvelope.OperationFailed,
                            ComposedScreenFixture.Code(Call(fixture, folder.Path("変形後.pmx"), true)));
                    }
                });

                Assert.Empty(Directory.GetFiles(folder.Root));
            }
        }

        [Fact]
        public void ADialogShownAfterTheAnswerLimitIsStillAnswered()
        {
            using (Folder folder = new Folder())
            {
                string path = folder.Path("変形後.pmx");
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    {
                        screen.DelayBeforeDialog = TimeSpan.FromSeconds(11);

                        ComposedScreenFixture.Value(Call(fixture, path, true));
                    }
                });

                Assert.Equal(Screen.Written, File.ReadAllText(path));
            }
        }

        [Fact]
        public void AnErrorTheEditorShowsIsClosedAndReturned()
        {
            using (Folder folder = new Folder())
            {
                string path = folder.Path("変形後.pmx");
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    {
                        screen.FailWithMessage = "失敗しました.";

                        IDictionary<string, object> answered = Call(fixture, path, true);

                        Assert.Equal(ToolEnvelope.OperationFailed, ComposedScreenFixture.Code(answered));
                        Assert.Contains("失敗しました.", ComposedEditFixture.Message(answered));
                    }
                });

                Assert.Empty(Directory.GetFiles(folder.Root));
            }
        }

        [Fact]
        public void ATransformViewAlreadyShowingADialogIsRefused()
        {
            using (Folder folder = new Folder())
            {
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    {
                        EnableWindow(screen.View.Handle, false);

                        Assert.Equal(
                            ToolEnvelope.NotApplicable,
                            ComposedScreenFixture.Code(Call(fixture, folder.Path("変形後.pmx"), true)));
                        Assert.Equal(0, screen.Saves);
                    }
                });
            }
        }

        [Fact]
        public void ADialogThatIsNeitherTheSaveDialogNorAMessageIsLeftOpenAndReportedAfterTheLimit()
        {
            using (Folder folder = new Folder())
            {
                string early = null;
                string late = null;
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    {
                        screen.ShowFolderDialog = true;
                        IntPtr owner = screen.View.Handle;
                        DesktopModalWindowProbe probe = new DesktopModalWindowProbe(TimeSpan.FromSeconds(1));
                        Task watching = Task.Run(() =>
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(3));
                            early = probe.TryDescribe();
                            Thread.Sleep(TimeSpan.FromSeconds(9));
                            late = probe.TryDescribe();
                            PostMessage(OwnedDialog(owner), CloseMessage, IntPtr.Zero, IntPtr.Zero);
                        });

                        Assert.Equal(
                            ToolEnvelope.OperationFailed,
                            ComposedScreenFixture.Code(Call(fixture, folder.Path("変形後.pmx"), true)));
                        watching.Wait();
                    }
                });

                Assert.Null(early);
                Assert.NotNull(late);
            }
        }

        private static IntPtr OwnedDialog(IntPtr owner)
        {
            IntPtr dialog = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "#32770", null);
            while (dialog != IntPtr.Zero)
            {
                if (GetWindow(dialog, GetWindowOwner) == owner && IsWindowVisible(dialog))
                {
                    return dialog;
                }

                dialog = FindWindowEx(IntPtr.Zero, dialog, "#32770", null);
            }

            throw new InvalidOperationException("持ち主のダイアログが無い。");
        }

        private const uint CloseMessage = 0x0010;

        private const uint GetWindowOwner = 4;

        [DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr window, bool enable);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint relation);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        private static IDictionary<string, object> Call(ComposedScreenFixture fixture, string path, bool confirm)
        {
            return fixture.Call(
                MotionSaveTransformedPmxFile.ToolName,
                ComposedScreenFixture.Arguments(
                    ComposedScreenFixture.Given("path", path),
                    ComposedScreenFixture.Given("confirm", confirm)));
        }

        private static void OnSta(Action action)
        {
            Exception caught = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    action();
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
        }

        private sealed class Folder : IDisposable
        {
            internal Folder()
            {
                Root = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "pmx-editor-mcp-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Root);
            }

            internal string Root { get; }

            internal string Path(string name)
            {
                return System.IO.Path.Combine(Root, name);
            }

            public void Dispose()
            {
                Directory.Delete(Root, true);
            }
        }

        private sealed class Screen : IDisposable
        {
            internal const string Written = "変形した形";

            private readonly Form _view;

            internal Screen(ComposedScreenFixture fixture)
            {
                _view = new Form
                {
                    Name = "TransformView",
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                };
                Normalize = new ToolStripMenuItem("正規化") { Name = "MenuItem_SaveNormalize", CheckOnClick = true };
                ToolStripMenuItem save = new ToolStripMenuItem("現在の形状で保存") { Name = "MenuItem_SaveModel" };
                save.Click += (sender, e) => Save();
                ToolStripMenuItem menu = new ToolStripMenuItem("ファイル") { Name = "MenuItem_File" };
                menu.DropDownItems.Add(Normalize);
                menu.DropDownItems.Add(save);
                MenuStrip strip = new MenuStrip { Name = "menuStrip1" };
                strip.Items.Add(menu);
                _view.Controls.Add(strip);
                _view.Show();
                fixture.Forms.Add(_view);
            }

            internal ToolStripMenuItem Normalize { get; }

            internal int Saves { get; private set; }

            internal List<bool> NormalizedWhenSaved { get; } = new List<bool>();

            internal bool FailOnWrite { get; set; }

            internal bool SkipDialog { get; set; }

            internal bool ShowFolderDialog { get; set; }

            internal Form View
            {
                get { return _view; }
            }

            internal TimeSpan DelayBeforeDialog { get; set; }

            internal string FailWithMessage { get; set; }

            public void Dispose()
            {
                _view.Dispose();
            }

            private void Save()
            {
                Saves++;
                if (SkipDialog)
                {
                    return;
                }

                Thread.Sleep(DelayBeforeDialog);
                if (ShowFolderDialog)
                {
                    using (FolderBrowserDialog folders = new FolderBrowserDialog())
                    {
                        folders.ShowDialog(_view);
                    }

                    return;
                }

                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Filter = "拡張モデルファイル (*.pmx)|*.pmx|すべてのファイル (*.*)|*.*";
                    dialog.AddExtension = true;
                    if (dialog.ShowDialog(_view) != DialogResult.OK)
                    {
                        return;
                    }

                    NormalizedWhenSaved.Add(Normalize.Checked);
                    if (FailWithMessage != null)
                    {
                        MessageBox.Show(_view, FailWithMessage, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                        return;
                    }

                    if (!FailOnWrite)
                    {
                        File.WriteAllText(dialog.FileName, Written);
                    }
                }
            }
        }
    }
}
