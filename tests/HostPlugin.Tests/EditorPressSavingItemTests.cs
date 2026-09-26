using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace PmxEditorMcp.Tests
{
    [Collection(ModalWindowCollection.Name)]
    public sealed class EditorPressSavingItemTests
    {
        private const string Window = "PmxViewForm.EffectView";

        private static readonly string[] SaveAs = { "menuStrip1", "MenuItem_File", "MenuItem_SaveAs" };

        private static readonly string[] SaveDefault = { "menuStrip1", "MenuItem_File", "MenuItem_SaveDefault" };

        private static readonly string[] Reload = { "menuStrip1", "MenuItem_File", "MenuItem_Reload" };

        private const string Asked = "エディタの文面と違う問い";

        private const string Caution = "エディタの文面と違う注意";

        [Fact]
        public void AMessageTheItemDoesNotExpectIsReturnedAsAFailureAndTheExistingFileIsKept()
        {
            using (Folder folder = new Folder())
            {
                string path = folder.Path("設定.xml");
                File.WriteAllText(path, "前の中身");
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    {
                        screen.FailWithMessage = "保存に失敗しました.";

                        IDictionary<string, object> answered = Call(fixture, SaveAs, path, true);

                        Assert.Equal(ToolEnvelope.OperationFailed, ComposedScreenFixture.Code(answered));
                        Assert.Contains("保存に失敗しました.", ComposedEditFixture.Message(answered));
                    }
                });

                Assert.Equal("前の中身", File.ReadAllText(path));
                Assert.Equal(new[] { path }, Directory.GetFiles(folder.Root));
            }
        }

        [Fact]
        public void APromptAnotherWindowShowsIsRefusedWithoutPressing()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture))
                using (Form other = Offscreen("ほかのウィンドウ"))
                using (Form asking = Offscreen("確認"))
                {
                    other.Show();
                    asking.Show(other);
                    EnableWindow(other.Handle, false);

                    Assert.Equal(
                        ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(Call(fixture, SaveDefault, null, true)));
                    Assert.Empty(screen.Answers);
                }
            });
        }

        [Fact]
        public void TheExportAnswersTheSizeFormAfterTheSaveDialog()
        {
            using (Folder folder = new Folder())
            {
                string path = folder.Path("書き出し.x");
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Form main = Offscreen("PmxForm"))
                    {
                        main.Name = "PmxForm";
                        List<DialogResult> sized = new List<DialogResult>();
                        Item(main, Export, () =>
                        {
                            string chosen = Chosen();
                            if (chosen == null)
                            {
                                return;
                            }

                            using (Form size = Offscreen("エディタの題と違う題"))
                            {
                                size.Name = "ExportForm";
                                size.Controls.Add(new Button { Name = "btnCancel", Text = "やめる", DialogResult = DialogResult.Cancel });
                                size.Controls.Add(new Button
                                {
                                    Name = "btnOK",
                                    Text = "決める",
                                    DialogResult = DialogResult.OK,
                                    Location = new Point(80, 0),
                                });
                                sized.Add(size.ShowDialog(main));
                            }

                            File.WriteAllText(chosen, Screen.Written);
                        });
                        main.Show();
                        fixture.Forms.Add(main);

                        ComposedScreenFixture.Value(Call(fixture, "PmxEditor.PmxForm", Export, path, true));

                        Assert.Equal(new[] { DialogResult.OK }, sized);
                    }
                });

                Assert.Equal(Screen.Written, File.ReadAllText(path));
            }
        }

        [Theory]
        [InlineData("書き出し.pmx", "session_save_pmx_file")]
        [InlineData("書き出し.PMD", "session_save_pmd_file")]
        public void AnExportThatTheSdkCanWriteIsRefusedAndTheToolIsNamed(string name, string tool)
        {
            using (Folder folder = new Folder())
            {
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Form main = Offscreen("PmxForm"))
                    {
                        main.Name = "PmxForm";
                        int pressed = 0;
                        Item(main, Export, () => pressed++);
                        main.Show();
                        fixture.Forms.Add(main);

                        IDictionary<string, object> refused =
                            Call(fixture, "PmxEditor.PmxForm", Export, folder.Path(name), true);

                        Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(refused));
                        Assert.Contains(tool, ComposedEditFixture.Message(refused));
                        Assert.Equal(0, pressed);
                    }
                });
            }
        }

        [Fact]
        public void ANoticeIsAgreedAndReturnedButAMotionFileLeftCutShortIsAFailureThatKeepsTheFileBefore()
        {
            using (Folder folder = new Folder())
            {
                string path = folder.Path("非物理化.vmd");
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Form view = Offscreen("VMDViewForm"))
                    {
                        view.Name = "VMDViewForm";
                        bool cut = false;
                        Item(view, SaveFixVmd, () =>
                        {
                            MessageBox.Show(Caution, "注意", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            string chosen = Chosen();
                            if (chosen == null)
                            {
                                return;
                            }

                            if (cut)
                            {
                                File.WriteAllBytes(chosen, Motion.Take(Motion.Length - 1).ToArray());
                                MessageBox.Show("保存に失敗しました.", "結果", MessageBoxButtons.OK);

                                return;
                            }

                            File.WriteAllBytes(chosen, Motion);
                            MessageBox.Show("保存完了", "結果", MessageBoxButtons.OK);
                        });
                        view.Show();
                        fixture.Forms.Add(view);

                        IDictionary<string, object> value = ComposedScreenFixture.Value(
                            Call(fixture, "VmdViewLib.VMDViewForm", SaveFixVmd, path, true));
                        Assert.Equal(
                            new[] { Caution, "保存完了" }, ((IEnumerable<object>)value["messages"]).Cast<string>());
                        Assert.Equal(Motion, File.ReadAllBytes(path));

                        cut = true;
                        IDictionary<string, object> answered =
                            Call(fixture, "VmdViewLib.VMDViewForm", SaveFixVmd, path, true);
                        Assert.Equal(ToolEnvelope.OperationFailed, ComposedScreenFixture.Code(answered));
                        Assert.Contains("VMD", ComposedEditFixture.Message(answered));
                        Assert.Contains("保存に失敗しました.", ComposedEditFixture.Message(answered));
                    }
                });

                Assert.Equal(Motion, File.ReadAllBytes(path));
            }
        }

        [Fact]
        public void ACautionBeyondWhatTheItemMayShowIsReturnedAsAFailure()
        {
            using (Folder folder = new Folder())
            {
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Form view = Offscreen("VMDViewForm"))
                    {
                        view.Name = "VMDViewForm";
                        Item(view, SaveFixVmd, () =>
                        {
                            MessageBox.Show(Caution, "注意", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            MessageBox.Show("二つ目の注意", "注意", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        });
                        view.Show();
                        fixture.Forms.Add(view);

                        IDictionary<string, object> answered =
                            Call(fixture, "VmdViewLib.VMDViewForm", SaveFixVmd, folder.Path("非物理化.vmd"), true);

                        Assert.Equal(ToolEnvelope.OperationFailed, ComposedScreenFixture.Code(answered));
                        Assert.StartsWith("エディタがダイアログを出したので閉じた", ComposedEditFixture.Message(answered));
                        Assert.Contains("二つ目の注意", ComposedEditFixture.Message(answered));
                        Assert.Contains(Caution, ComposedEditFixture.Message(answered));
                    }
                });
            }
        }

        [Fact]
        public void EveryItemTheToolPressesWritesAFileInTheCatalog()
        {
            foreach (string item in EditorPressSavingItem.Items)
            {
                string[] parts = item.Split('|');
                IDictionary<string, object> node = UiStructureCatalog.Node(
                    UiStructureCatalog.Window(parts[0]), UiStructureCatalog.RootName);
                foreach (string step in parts[1].Split('/'))
                {
                    node = node == null ? null : UiStructureCatalog.Child(node, step);
                }

                Assert.True(node != null, "台帳に無い項目: " + item);
                Assert.Equal("overwrite", UiStructureCatalog.Text(node, UiStructureCatalog.DangerName));
            }
        }

        [Fact]
        public void AnExistingFileIsReplacedByWhatTheEditorWritesToTheGivenFileItself()
        {
            using (Folder folder = new Folder())
            {
                string path = folder.Path("設定.xml");
                File.WriteAllText(path, "前の中身");
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    {
                        ComposedScreenFixture.Value(Call(fixture, SaveAs, path, true));

                        Assert.Equal(1, screen.Saves);
                        Assert.Equal(new[] { path }, screen.Chosen);
                    }
                });

                Assert.Equal(Screen.Written, File.ReadAllText(path));
                Assert.Equal(new[] { path }, Directory.GetFiles(folder.Root));
            }
        }

        [Fact]
        public void TheSaveDialogIsAnsweredWithoutShowingOnTheScreen()
        {
            using (Folder folder = new Folder())
            {
                string path = folder.Path("設定.xml");
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    using (DialogSightings sightings = new DialogSightings())
                    {
                        ComposedScreenFixture.Value(Call(fixture, SaveAs, path, true));

                        Assert.Equal(0, sightings.Opaque);
                        Assert.NotEqual(0, sightings.Transparent);
                    }
                });

                Assert.Equal(Screen.Written, File.ReadAllText(path));
            }
        }

        [Fact]
        public void AConfirmationIsAgreedWithoutShowingOnTheScreen()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture))
                using (DialogSightings sightings = new DialogSightings())
                {
                    ComposedScreenFixture.Value(Call(fixture, SaveDefault, null, true));

                    Assert.Equal(0, sightings.Opaque);
                    Assert.NotEqual(0, sightings.Transparent);
                }
            });
        }

        [Fact]
        public void AConfirmationTheItemAsksIsAgreedAndReturned()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture))
                {
                    IDictionary<string, object> value = ComposedScreenFixture.Value(Call(fixture, SaveDefault, null, true));

                    Assert.Equal(new[] { DialogResult.Yes }, screen.Answers);
                    Assert.Equal(new[] { Asked }, ((IEnumerable<object>)value["messages"]).Cast<string>());
                }
            });
        }

        [Fact]
        public void AQuestionTheItemDoesNotExpectIsAnsweredNoAndReturnedAsAFailure()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture))
                {
                    screen.Further = "ほかの設定も書き換えますか？";

                    IDictionary<string, object> answered = Call(fixture, SaveDefault, null, true);

                    Assert.Equal(ToolEnvelope.OperationFailed, ComposedScreenFixture.Code(answered));
                    Assert.Contains(Asked, ComposedEditFixture.Message(answered));
                    Assert.Contains("ほかの設定も書き換えますか？", ComposedEditFixture.Message(answered));
                    Assert.Equal(new[] { DialogResult.Yes, DialogResult.No }, screen.Answers);
                }
            });
        }

        [Fact]
        public void ACallWithoutConfirmIsRefusedWithoutPressing()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture))
                {
                    Assert.Equal(
                        ToolEnvelope.ConfirmRequired,
                        ComposedScreenFixture.Code(Call(fixture, SaveDefault, null, false)));
                    Assert.Empty(screen.Answers);
                }
            });
        }

        [Fact]
        public void AnItemThatDoesNotWriteAFileIsRefused()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                using (Screen screen = new Screen(fixture))
                {
                    Assert.Equal(
                        ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(Call(fixture, Reload, null, true)));
                    Assert.Equal(0, screen.Reloads);
                }
            });
        }

        [Fact]
        public void TheFileIsGivenExactlyWhenTheItemAsksForOne()
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
                            ComposedScreenFixture.Code(Call(fixture, SaveAs, null, true)));
                        Assert.Equal(
                            ToolEnvelope.InvalidArgument,
                            ComposedScreenFixture.Code(Call(fixture, SaveAs, "設定.xml", true)));
                        Assert.Equal(
                            ToolEnvelope.InvalidArgument,
                            ComposedScreenFixture.Code(Call(fixture, SaveAs, folder.Path("無い\\設定.xml"), true)));
                        Assert.Equal(
                            ToolEnvelope.InvalidArgument,
                            ComposedScreenFixture.Code(Call(fixture, SaveAs, folder.Root.Substring(0, 2) + "設定.xml", true)));
                        Assert.Equal(
                            ToolEnvelope.InvalidArgument,
                            ComposedScreenFixture.Code(Call(fixture, SaveAs, folder.Root.Substring(2) + "\\設定.xml", true)));
                        Assert.Equal(
                            ToolEnvelope.InvalidArgument,
                            ComposedScreenFixture.Code(Call(fixture, SaveAs, folder.Path("設定"), true)));
                        Assert.Equal(
                            ToolEnvelope.InvalidArgument,
                            ComposedScreenFixture.Code(Call(fixture, SaveDefault, folder.Path("設定.xml"), true)));
                        Assert.Equal(0, screen.Saves);
                        Assert.Empty(screen.Answers);
                    }
                });
            }
        }

        [Fact]
        public void AClosedWindowIsRefused()
        {
            OnSta(() =>
            {
                using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                {
                    Assert.Equal(
                        ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(Call(fixture, SaveDefault, null, true)));
                }
            });
        }

        [Fact]
        public void TheEditorWritingNothingIsAFailure()
        {
            using (Folder folder = new Folder())
            {
                OnSta(() =>
                {
                    using (ComposedScreenFixture fixture = new ComposedScreenFixture())
                    using (Screen screen = new Screen(fixture))
                    {
                        screen.WriteNothing = true;

                        Assert.Equal(
                            ToolEnvelope.OperationFailed,
                            ComposedScreenFixture.Code(Call(fixture, SaveAs, folder.Path("設定.xml"), true)));
                        Assert.Equal(1, screen.Saves);
                    }
                });

                Assert.Empty(Directory.GetFiles(folder.Root));
            }
        }

        private static readonly string[] Export = { "menuStrip1", "MenuItem_File", "MenuItem_Export" };

        private static readonly string[] SaveFixVmd = { "menuStrip1", "MenuItem_File", "MenuItem_SaveFixVmd" };

        private static readonly byte[] Motion =
            VmdFileTests.Bytes(visibleIkThird: false, bones: 1, morphs: 1, cameras: 0, iks: 0);

        private static IDictionary<string, object> Call(
            ComposedScreenFixture fixture, string[] path, string file, bool confirm)
        {
            return Call(fixture, Window, path, file, confirm);
        }

        private static IDictionary<string, object> Call(
            ComposedScreenFixture fixture, string window, string[] path, string file, bool confirm)
        {
            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>
            {
                ComposedScreenFixture.Given("window", window),
                ComposedScreenFixture.Given("path", path.Cast<object>().ToArray()),
                ComposedScreenFixture.Given("confirm", confirm),
            };
            if (file != null)
            {
                given.Add(ComposedScreenFixture.Given("file", file));
            }

            return fixture.Call(EditorPressSavingItem.ToolName, ComposedScreenFixture.Arguments(given.ToArray()));
        }

        /// <summary>保存のダイアログを持ち主なしで出し、選ばれた名前を返す。取り消されたら null。</summary>
        private static string Chosen()
        {
            using (SaveFileDialog dialog = new SaveFileDialog { Filter = "すべて(*.*)|*.*" })
            {
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
            }
        }

        private static void Item(Form form, string[] path, Action click)
        {
            MenuStrip strip = form.Controls.OfType<MenuStrip>().FirstOrDefault();
            if (strip == null)
            {
                strip = new MenuStrip { Name = path[0] };
                form.Controls.Add(strip);
            }

            ToolStripItemCollection items = strip.Items;
            ToolStripMenuItem at = null;
            foreach (string step in path.Skip(1))
            {
                at = items.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Name == step);
                if (at == null)
                {
                    at = new ToolStripMenuItem(step) { Name = step };
                    items.Add(at);
                }

                items = at.DropDownItems;
            }

            at.Click += (sender, e) => click();
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

        [DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr window, bool enable);

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
            thread.IsBackground = true;
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "表示が閉じられず、STAのスレッドが終わらなかった。");
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
            internal const string Written = "表示の設定";

            private readonly Form _view;

            internal Screen(ComposedScreenFixture fixture)
            {
                _view = new Form
                {
                    Name = "EffectView",
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                };
                ToolStripMenuItem saveAs = new ToolStripMenuItem("設定の保存") { Name = "MenuItem_SaveAs" };
                saveAs.Click += (sender, e) => Save();
                ToolStripMenuItem saveDefault = new ToolStripMenuItem("標準設定へ保存") { Name = "MenuItem_SaveDefault" };
                saveDefault.Click += (sender, e) => SaveToDefault();
                ToolStripMenuItem reload = new ToolStripMenuItem("設定の読み込み") { Name = "MenuItem_Reload" };
                reload.Click += (sender, e) => Reloads++;
                ToolStripMenuItem menu = new ToolStripMenuItem("ファイル") { Name = "MenuItem_File" };
                menu.DropDownItems.Add(saveAs);
                menu.DropDownItems.Add(saveDefault);
                menu.DropDownItems.Add(reload);
                MenuStrip strip = new MenuStrip { Name = "menuStrip1" };
                strip.Items.Add(menu);
                _view.Controls.Add(strip);
                _view.Show();
                fixture.Forms.Add(_view);
            }

            internal int Saves { get; private set; }

            internal int Reloads { get; private set; }

            internal List<DialogResult> Answers { get; } = new List<DialogResult>();

            internal List<string> Chosen { get; } = new List<string>();

            internal string FailWithMessage { get; set; }

            internal string Further { get; set; }

            internal bool WriteNothing { get; set; }

            public void Dispose()
            {
                _view.Dispose();
            }

            private void Save()
            {
                Saves++;
                using (SaveFileDialog dialog = new SaveFileDialog { Filter = "XML(*.xml)|*.xml" })
                {
                    if (dialog.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }

                    if (FailWithMessage != null)
                    {
                        MessageBox.Show(FailWithMessage, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                        return;
                    }

                    Chosen.Add(dialog.FileName);
                    if (!WriteNothing)
                    {
                        File.WriteAllText(dialog.FileName, Written);
                    }
                }
            }

            private void SaveToDefault()
            {
                Answers.Add(MessageBox.Show(Asked, "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question));
                if (Further != null)
                {
                    Answers.Add(MessageBox.Show(Further, "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question));
                }
            }
        }
    }
}
