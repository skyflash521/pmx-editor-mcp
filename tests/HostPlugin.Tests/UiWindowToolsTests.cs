using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using PEPlugin.View;
using Xunit;

namespace PmxEditorMcp.Tests
{
    [Collection(ModalWindowCollection.Name)]
    public sealed class UiWindowToolsTests
    {
        private const string MainForm = "PmxEditor.PmxForm";

        private const string View = "PmxViewForm.PMXView";

        private const string Transform = "PmxViewForm.TransformView";

        [Fact]
        public void AWindowOpenedFromAnOpenWindowTakesOneHop()
        {
            IList<KeyValuePair<string, IList<string>>> hops =
                UiOpenWindow.Hops(Transform, Open(MainForm, View));

            Assert.Single(hops);
            Assert.Equal(View, hops[0].Key);
            Assert.Equal(new[] { "menuStrip1", "MenuItem_View", "MenuItem_TransformView" }, hops[0].Value);
        }

        [Fact]
        public void AWindowBehindAClosedWindowOpensThatWindowFirst()
        {
            IList<KeyValuePair<string, IList<string>>> hops =
                UiOpenWindow.Hops("PmxViewForm.TransSlider", Open(MainForm, View));

            Assert.Equal(2, hops.Count);
            Assert.Equal(View, hops[0].Key);
            Assert.Equal(Transform, hops[1].Key);
        }

        [Fact]
        public void ContextMenusAndItemsThatOpenOtherWindowsTooAreNotFollowed()
        {
            IList<KeyValuePair<string, IList<string>>> hops =
                UiOpenWindow.Hops("PmxEditor.CsvElementView", Open(MainForm, View));

            Assert.Single(hops);
            Assert.Equal(
                new[] { "menuStrip1", "MenuItem_Edit", "MenuItem_CsvElement", "MenuItem_CsvElement_Show" },
                hops[0].Value);
            Assert.Null(UiOpenWindow.Hops("PmxEditor.CountProgressForm", Open(MainForm, View, Transform)));
        }

        [Fact]
        public void AWindowWithoutAnOpenerCannotBeOpened()
        {
            Assert.Null(UiOpenWindow.Hops(View, Open(MainForm)));
        }

        [Fact]
        public void TheWindowIsOpenedThroughTheMenuAndThenClosed()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    IDictionary<string, object> opened = Value(Call(screen, UiOpenWindow.ToolName, Transform));
                    Assert.Equal(false, opened[UiOpenWindow.AlreadyOpenName]);
                    Assert.Equal("TransformView", opened[UiOpenWindow.TitleName]);
                    Assert.True(screen.Transform.Visible, "開いていない。");

                    Assert.Equal(true, Value(Call(screen, UiOpenWindow.ToolName, Transform))[UiOpenWindow.AlreadyOpenName]);

                    Assert.Equal(false, Value(Call(screen, UiCloseWindow.ToolName, Transform))[UiCloseWindow.AlreadyClosedName]);
                    Assert.False(screen.Transform.Visible, "閉じていない。");
                    Assert.Equal(true, Value(Call(screen, UiCloseWindow.ToolName, Transform))[UiCloseWindow.AlreadyClosedName]);
                }
            });
        }

        [Fact]
        public void AWindowTheSdkCanShowIsOpenedAndClosedThroughItWithoutTheMenu()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    int pressed = 0;
                    screen.Item.Click += (sender, e) => pressed++;
                    FakeWindowConnector connector = new FakeWindowConnector();
                    screen.Add(connector);
                    Func<string, IPEBaseWindowConnector> windows =
                        named => string.Equals(named, Transform, StringComparison.Ordinal) ? connector : null;

                    IDictionary<string, object> opened = Value(Call(screen, UiOpenWindow.ToolName, Transform, windows));
                    Assert.True(connector.Visible, "開いていない。");
                    IDictionary<string, object> closed = Value(Call(screen, UiCloseWindow.ToolName, Transform, windows));

                    Assert.Equal(false, opened[UiOpenWindow.AlreadyOpenName]);
                    Assert.Equal("TransformView", opened[UiOpenWindow.TitleName]);
                    Assert.Equal(false, closed[UiCloseWindow.AlreadyClosedName]);
                    Assert.False(connector.Visible, "閉じていない。");
                    Assert.False(connector.IsDisposed, "隠すのでなく破棄した。");
                    Assert.Equal(new[] { true, false }, connector.Written);
                    Assert.Equal(0, pressed);
                }
            });
        }

        [Fact]
        public void AWindowShownAsAnotherOfTheSameTypeIsClosedByItself()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));
                    FakeWindowConnector connector = new FakeWindowConnector();
                    screen.Add(connector);

                    IDictionary<string, object> closed = Value(Call(
                        screen, UiCloseWindow.ToolName, Transform, named => connector));

                    Assert.Equal(false, closed[UiCloseWindow.AlreadyClosedName]);
                    Assert.False(screen.Transform.Visible, "開いていた方が閉じていない。");
                    Assert.Empty(connector.Written);
                }
            });
        }

        [Fact]
        public void ADisabledMenuItemIsNotPressed()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    screen.Item.Enabled = false;

                    Assert.Equal(ToolEnvelope.NotApplicable, Code(Call(screen, UiOpenWindow.ToolName, Transform)));
                    Assert.Null(screen.Transform);
                }
            });
        }

        [Fact]
        public void TheMainFormAndWindowsThatCannotBeReopenedAreNotClosed()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    IDictionary<string, object> main = Call(screen, UiCloseWindow.ToolName, MainForm);
                    Assert.Equal(ToolEnvelope.NotApplicable, Code(main));
                    string said = (string)((IDictionary<string, object>)main["error"])["message"];
                    Assert.Contains("終了", said);
                    Assert.Contains(UiCloseWindow.ShutdownToolName, said);
                    Assert.Equal(ToolEnvelope.NotApplicable, Code(Call(screen, UiCloseWindow.ToolName, View)));
                    Assert.True(screen.Main.Visible && screen.View.Visible, "閉じた。");
                }
            });
        }

        [Fact]
        public void AnUnknownWindowIsRefused()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    Assert.Equal(ToolEnvelope.InvalidArgument, Code(Call(screen, UiOpenWindow.ToolName, "TransformView")));
                    Assert.Equal(ToolEnvelope.InvalidArgument, Code(Call(screen, UiCloseWindow.ToolName, "TransformView")));
                }
            });
        }

        [Fact]
        public void AMenuItemInAnOpenWindowIsPressed()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    int pressed = 0;
                    screen.Normalize.Click += (sender, e) => pressed++;
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));

                    Value(Press(screen, Transform, "menuStrip1", "MenuItem_File", "MenuItem_SaveNormalize"));

                    Assert.Equal(1, pressed);
                }
            });
        }

        [Fact]
        public void AMenuItemTellsWhetherItIsCheckedAfterItIsPressed()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    screen.Normalize.CheckOnClick = true;
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));

                    IDictionary<string, object> value =
                        Value(Press(screen, Transform, "menuStrip1", "MenuItem_File", "MenuItem_SaveNormalize"));

                    Assert.Equal(true, value[UiPressItem.CheckedName]);
                }
            });
        }

        [Fact]
        public void AMenuItemAlreadyInTheAskedStateIsNotPressed()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    int pressed = 0;
                    screen.Normalize.CheckOnClick = true;
                    screen.Normalize.Checked = true;
                    screen.Normalize.Click += (sender, e) => pressed++;
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));

                    IDictionary<string, object> kept = Value(Checked(screen, true));
                    IDictionary<string, object> turned = Value(Checked(screen, false));

                    Assert.Equal(true, kept[UiPressItem.CheckedName]);
                    Assert.Equal(false, turned[UiPressItem.CheckedName]);
                    Assert.Equal(1, pressed);
                }
            });
        }

        [Fact]
        public void AnItemThatDoesNotTurnToTheAskedStateIsAFailure()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));

                    IDictionary<string, object> failed = Checked(screen, true);

                    Assert.Equal(ToolEnvelope.OperationFailed, Code(failed));
                    Assert.Contains(UiPressItem.CheckedName, Said(failed));
                }
            });
        }

        [Fact]
        public void AToolbarButtonTellsAndTakesItsCheckedStateToo()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));
                    ToolStripButton realtime = new ToolStripButton("tbRealtime") { Name = "tbRealtime", CheckOnClick = true };
                    ToolStrip strip = new ToolStrip { Name = "tsOperate" };
                    strip.Items.Add(realtime);
                    screen.Transform.Controls.Add(strip);

                    IDictionary<string, object> pressed = Value(Press(screen, Transform, "tsOperate", "tbRealtime"));
                    IDictionary<string, object> kept = Value(CheckedAt(screen, true, "tsOperate", "tbRealtime"));

                    Assert.Equal(true, pressed[UiPressItem.CheckedName]);
                    Assert.Equal(true, kept[UiPressItem.CheckedName]);
                    Assert.True(realtime.Checked);
                }
            });
        }

        [Fact]
        public void AMenuItemIsReadAfterItsMenuDecidesItsState()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    int pressed = 0;
                    screen.Normalize.Click += (sender, e) => pressed++;
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));
                    ToolStripMenuItem file = (ToolStripMenuItem)screen.Normalize.OwnerItem;
                    file.DropDownOpening += (sender, e) => screen.Normalize.Checked = true;

                    IDictionary<string, object> kept = Value(Checked(screen, true));

                    Assert.Equal(true, kept[UiPressItem.CheckedName]);
                    Assert.Equal(0, pressed);
                }
            });
        }

        [Fact]
        public void AnItemMissingFromTheScreenIsNotCalledOneWithoutAState()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));

                    IDictionary<string, object> refused = CheckedAt(screen, true, "tsOperate", "tbRealtime");

                    Assert.Equal(ToolEnvelope.NotApplicable, Code(refused));
                    Assert.Contains("見つからない", Said(refused));
                }
            });
        }

        [Fact]
        public void AStateIsNotAskedOfAButton()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    List<string> happened = new List<string>();
                    Form form = new Form
                    {
                        Name = "PmxViewSelector",
                        ShowInTaskbar = false,
                        StartPosition = FormStartPosition.Manual,
                        Location = new Point(-32000, -32000),
                    };
                    screen.Add(form);
                    Button all = new Button { Name = "btnBoneAll" };
                    all.Click += (sender, e) => happened.Add("press");
                    TabPage page = new TabPage { Name = "tabPage2" };
                    page.Controls.Add(all);
                    TabControl tabs = new TabControl { Name = "tabPage" };
                    tabs.TabPages.Add(page);
                    form.Controls.Add(tabs);
                    form.Show();

                    IDictionary<string, object> refused = Call(
                        screen,
                        UiPressItem.ToolName,
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            { UiPressItem.WindowName, "PmxViewForm.PmxViewSelector" },
                            { UiPressItem.PathName, new[] { "tabPage", "tabPage2", "btnBoneAll" } },
                            { UiPressItem.CheckedName, true },
                        });

                    Assert.Equal(ToolEnvelope.InvalidArgument, Code(refused));
                    Assert.Empty(happened);
                }
            });
        }

        [Fact]
        public void AnItemThatHasItsOwnToolIsRefusedAndTheToolIsNamed()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    int pressed = 0;
                    screen.Apply.Click += (sender, e) => pressed++;
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));

                    IDictionary<string, object> refused =
                        Press(screen, Transform, "menuStrip1", "MenuItem_File", "MenuItem_SetupCurrentPose");

                    Assert.Equal(ToolEnvelope.NotApplicable, Code(refused));
                    Assert.Contains(MotionApplyCurrentPose.ToolName, Said(refused));
                    Assert.Equal(0, pressed);
                }
            });
        }

        [Theory]
        [InlineData("PmxEditor.PmxForm", "menuStrip1/MenuItem_File/MenuItem_Save", "session_save_pmx_file")]
        [InlineData("PmxEditor.PmxForm", "menuStrip1/MenuItem_File/MenuItem_SaveAs", "session_save_pmx_file")]
        [InlineData("PmxViewForm.EffectView", "menuStrip1/MenuItem_File/MenuItem_SaveAs", EditorPressSavingItem.ToolName)]
        [InlineData("VmdViewLib.VMDViewForm", "menuStrip1/MenuItem_File/MenuItem_SaveFixVmd", EditorPressSavingItem.ToolName)]
        [InlineData("PmxViewForm.PmxViewSetting", "menuStrip1/MenuItem_File/MenuItem_SaveAs", "view_save_view_setting")]
        [InlineData("PmxViewForm.PmxViewSetting", "menuStrip1/MenuItem_File/MenuItem_Save", "view_save_view_setting")]
        public void AnItemThatWritesAFileIsRefusedAndTheToolThatWritesItIsNamed(string window, string path, string tool)
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    IDictionary<string, object> refused = Press(screen, window, path.Split('/'));

                    Assert.Equal(ToolEnvelope.NotApplicable, Code(refused));
                    Assert.Contains(tool, Said(refused));
                }
            });
        }

        [Theory]
        [InlineData("VmdViewLib.RecForm", "btnStart")]
        [InlineData("PmxEditor.ExportForm", "btnOK")]
        public void AnItemThatWritesAFileWithoutAToolNamesNoTool(string window, string path)
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    IDictionary<string, object> refused = Press(screen, window, path.Split('/'));

                    Assert.Equal(ToolEnvelope.NotApplicable, Code(refused));
                    Assert.DoesNotContain("session_save_pmx_file", Said(refused));
                    Assert.DoesNotContain(EditorPressSavingItem.ToolName, Said(refused));
                }
            });
        }

        [Fact]
        public void ANoticeTheItemShowsIsClosedWithOkAndReturned()
        {
            OnSta(() => WithLimit(() =>
            {
                using (Screen screen = new Screen())
                {
                    List<DialogResult> answers = new List<DialogResult>();
                    screen.Archive.Click += (sender, e) =>
                        answers.Add(MessageBox.Show("追加しました.", "確認", MessageBoxButtons.OK));
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));

                    IDictionary<string, object> value = Value(Press(screen, Transform, ArchivePath));

                    Assert.Equal(new[] { DialogResult.OK }, answers);
                    Assert.Equal(new object[] { "追加しました." }, (object[])value[UiPressItem.MessagesName]);
                }
            }));
        }

        [Fact]
        public void AQuestionTheItemAsksIsAnsweredNoAndReturnedAsAFailure()
        {
            OnSta(() => WithLimit(() =>
            {
                using (Screen screen = new Screen())
                {
                    List<DialogResult> answers = new List<DialogResult>();
                    screen.Archive.Click += (sender, e) => answers.Add(
                        MessageBox.Show("追加しますか？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question));
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));

                    IDictionary<string, object> refused = Press(screen, Transform, ArchivePath);

                    Assert.Equal(ToolEnvelope.OperationFailed, Code(refused));
                    Assert.Contains("追加しますか？", Said(refused));
                    Assert.Equal(new[] { DialogResult.No }, answers);
                }
            }));
        }

        [Fact]
        public void AnErrorNoticeTheItemShowsIsClosedAndReturnedAsAFailure()
        {
            OnSta(() => WithLimit(() =>
            {
                using (Screen screen = new Screen())
                {
                    List<DialogResult> answers = new List<DialogResult>();
                    screen.Archive.Click += (sender, e) => answers.Add(MessageBox.Show(
                        "失敗しました.", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Exclamation));
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));

                    IDictionary<string, object> refused = Press(screen, Transform, ArchivePath);

                    Assert.Equal(ToolEnvelope.OperationFailed, Code(refused));
                    Assert.Contains("失敗しました.", Said(refused));
                    Assert.Single(answers);
                }
            }));
        }

        [Fact]
        public void ANoticeClosedBeforeAFailureIsReturnedWithIt()
        {
            OnSta(() => WithLimit(() =>
            {
                using (Screen screen = new Screen())
                {
                    screen.Archive.Click += (sender, e) =>
                    {
                        MessageBox.Show("変更しました.", "結果", MessageBoxButtons.OK);
                        MessageBox.Show("続けますか？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    };
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));

                    IDictionary<string, object> refused = Press(screen, Transform, ArchivePath);

                    Assert.Equal(ToolEnvelope.OperationFailed, Code(refused));
                    Assert.Contains("変更しました.", Said(refused));
                    Assert.Contains("続けますか？", Said(refused));
                }
            }));
        }

        [Fact]
        public void AQuestionShownWhileClosingIsAnsweredNoAndReturnedAsAFailure()
        {
            OnSta(() => WithLimit(() =>
            {
                using (Screen screen = new Screen())
                {
                    List<DialogResult> answers = new List<DialogResult>();
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));
                    screen.Transform.FormClosing += (sender, e) =>
                    {
                        DialogResult answer = MessageBox.Show(
                            "閉じますか？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        answers.Add(answer);
                        e.Cancel = answer != DialogResult.Yes;
                    };

                    IDictionary<string, object> refused = Call(screen, UiCloseWindow.ToolName, Transform);

                    Assert.Equal(ToolEnvelope.OperationFailed, Code(refused));
                    Assert.Contains("閉じますか？", Said(refused));
                    Assert.Equal(new[] { DialogResult.No }, answers);
                    Assert.True(screen.Transform.Visible, "閉じた。");
                }
            }));
        }

        [Fact]
        public void ANoticeShownWhileOpeningIsClosedWithOkAndReturned()
        {
            OnSta(() => WithLimit(() =>
            {
                using (Screen screen = new Screen())
                {
                    List<DialogResult> answers = new List<DialogResult>();
                    screen.Item.Click += (sender, e) =>
                        answers.Add(MessageBox.Show("開きました.", "結果", MessageBoxButtons.OK));

                    IDictionary<string, object> opened = Value(Call(screen, UiOpenWindow.ToolName, Transform));

                    Assert.Equal(new[] { DialogResult.OK }, answers);
                    Assert.Equal(new object[] { "開きました." }, (object[])opened[UiOpenWindow.MessagesName]);
                }
            }));
        }

        [Fact]
        public void AWindowIsNotOpenedWhileTheEditorWaitsForAnAnswer()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                using (Form asking = Asking())
                {
                    asking.Show(screen.Main);
                    EnableWindow(screen.Main.Handle, false);

                    Assert.Equal(ToolEnvelope.NotApplicable, Code(Call(screen, UiOpenWindow.ToolName, Transform)));
                    Assert.Null(screen.Transform);
                }
            });
        }

        [Fact]
        public void AWindowIsNotClosedWhileTheEditorWaitsForAnAnswer()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                using (Form asking = Asking())
                {
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));
                    asking.Show(screen.Main);
                    EnableWindow(screen.Main.Handle, false);

                    Assert.Equal(ToolEnvelope.NotApplicable, Code(Call(screen, UiCloseWindow.ToolName, Transform)));
                    Assert.True(screen.Transform.Visible, "閉じた。");
                }
            });
        }

        [Fact]
        public void AFormTheItemOpensModallyIsClosedAndReturnedAsAFailure()
        {
            OnSta(() => WithLimit(() =>
            {
                using (Screen screen = new Screen())
                {
                    List<DialogResult> answers = new List<DialogResult>();
                    screen.Archive.Click += (sender, e) =>
                    {
                        using (Form asking = new Form
                        {
                            Text = "頂点モーフ再計算用閾値",
                            ShowInTaskbar = false,
                            StartPosition = FormStartPosition.Manual,
                            Location = new Point(-32000, -32000),
                        })
                        {
                            answers.Add(asking.ShowDialog(screen.Transform));
                        }
                    };
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));

                    IDictionary<string, object> refused = Press(screen, Transform, ArchivePath);

                    Assert.Equal(ToolEnvelope.OperationFailed, Code(refused));
                    Assert.Contains("頂点モーフ再計算用閾値", Said(refused));
                    Assert.Equal(new[] { DialogResult.Cancel }, answers);
                    Assert.True(screen.Transform.Visible, "押した先のウィンドウまで閉じた。");
                }
            }));
        }

        [Fact]
        public void AnItemIsNotPressedWhileTheEditorWaitsForAnAnswer()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                using (Form asking = new Form
                {
                    Text = "確認",
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                })
                {
                    int pressed = 0;
                    screen.Archive.Click += (sender, e) => pressed++;
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));
                    asking.Show(screen.Main);
                    EnableWindow(screen.Main.Handle, false);

                    Assert.Equal(ToolEnvelope.NotApplicable, Code(Press(screen, Transform, ArchivePath)));
                    Assert.Equal(0, pressed);
                }
            });
        }

        [Fact]
        public void AnItemInsideAContainerMissingFromTheCatalogIsPressed()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    int pressed = 0;
                    screen.Initialize.Click += (sender, e) => pressed++;
                    Value(Call(screen, UiOpenWindow.ToolName, Transform));

                    Value(Press(screen, Transform, "extMenuStrip1", "MenuItem_Init", "MenuItem_Initialize"));

                    Assert.Equal(1, pressed);
                }
            });
        }

        [Fact]
        public void AnItemInAClosedWindowIsNotPressed()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    IDictionary<string, object> refused =
                        Press(screen, Transform, "menuStrip1", "MenuItem_File", "MenuItem_SaveNormalize");

                    Assert.Equal(ToolEnvelope.NotApplicable, Code(refused));
                    Assert.Contains(UiOpenWindow.ToolName, Said(refused));
                }
            });
        }

        [Fact]
        public void ADangerousItemIsRefusedWithoutPressing()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    int pressed = 0;
                    screen.Save.Click += (sender, e) => pressed++;

                    IDictionary<string, object> refused =
                        Press(screen, MainForm, "menuStrip1", "MenuItem_File", "MenuItem_Save");

                    Assert.Equal(ToolEnvelope.NotApplicable, Code(refused));
                    Assert.Equal(0, pressed);
                }
            });
        }

        [Fact]
        public void ADisabledItemIsNotPressedEither()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    int pressed = 0;
                    screen.Item.Click += (sender, e) => pressed++;
                    screen.Item.Enabled = false;

                    Assert.Equal(
                        ToolEnvelope.NotApplicable,
                        Code(Press(screen, View, "menuStrip1", "MenuItem_View", "MenuItem_TransformView")));
                    Assert.Equal(0, pressed);
                }
            });
        }

        [Fact]
        public void AButtonOnADialogWaitingForAnAnswerIsNotPressed()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                using (Form dialog = new Form { Name = "InputDialog", ShowInTaskbar = false })
                {
                    int pressed = 0;
                    Button ok = new Button { Name = "btnOK" };
                    ok.Click += (sender, e) => pressed++;
                    dialog.Controls.Add(ok);
                    screen.Add(dialog);
                    IDictionary<string, object> refused = null;
                    dialog.Shown += (sender, e) =>
                    {
                        refused = Press(screen, "PmxEditor.InputDialog", "btnOK");
                        dialog.Close();
                    };

                    dialog.ShowDialog();

                    Assert.Equal(ToolEnvelope.NotApplicable, Code(refused));
                    Assert.Equal(0, pressed);
                }
            });
        }

        [Fact]
        public void OnlyAPressableLeafIsAccepted()
        {
            OnSta(() =>
            {
                using (Screen screen = new Screen())
                {
                    Assert.Equal(ToolEnvelope.InvalidArgument, Code(Press(screen, View, "menuStrip1", "MenuItem_View")));
                    Assert.Equal(ToolEnvelope.InvalidArgument, Code(Press(screen, View, "menuStrip1", "MenuItem_Nothing")));
                    Assert.Equal(ToolEnvelope.InvalidArgument, Code(Press(screen, "TransformView", "menuStrip1")));
                    Assert.Equal(ToolEnvelope.InvalidArgument, Code(Press(screen, View)));
                }
            });
        }

        private static Form Asking()
        {
            return new Form
            {
                Text = "確認",
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000),
            };
        }

        private static readonly string[] ArchivePath = { "menuStrip1", "MenuItem_File", "MenuItem_PushArchive" };

        private static void WithLimit(Action action)
        {
            TimeSpan held = DialogAnswer.Limit;
            DialogAnswer.Limit = TimeSpan.FromSeconds(0.5);
            try
            {
                action();
            }
            finally
            {
                DialogAnswer.Limit = held;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr window, bool enable);

        private static IDictionary<string, object> Checked(Screen screen, bool state)
        {
            return CheckedAt(screen, state, "menuStrip1", "MenuItem_File", "MenuItem_SaveNormalize");
        }

        private static IDictionary<string, object> CheckedAt(Screen screen, bool state, params string[] path)
        {
            return Call(
                screen,
                UiPressItem.ToolName,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { UiPressItem.WindowName, Transform },
                    { UiPressItem.PathName, path },
                    { UiPressItem.CheckedName, state },
                });
        }

        private static IDictionary<string, object> Press(Screen screen, string window, params string[] path)
        {
            return Call(
                screen,
                UiPressItem.ToolName,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { UiPressItem.WindowName, window },
                    { UiPressItem.PathName, path },
                });
        }

        private static string Said(IDictionary<string, object> envelope)
        {
            return (string)((IDictionary<string, object>)envelope["error"])["message"];
        }

        private static Func<string, bool> Open(params string[] open)
        {
            return named => Array.IndexOf(open, named) >= 0;
        }

        private static IDictionary<string, object> Call(Screen screen, string tool, string window)
        {
            return Call(
                screen, tool, new Dictionary<string, object>(StringComparer.Ordinal) { { "window", window } });
        }

        private static IDictionary<string, object> Call(
            Screen screen, string tool, string window, Func<string, IPEBaseWindowConnector> windows)
        {
            return Call(
                screen, tool, new Dictionary<string, object>(StringComparer.Ordinal) { { "window", window } }, windows);
        }

        private static IDictionary<string, object> Call(
            Screen screen, string tool, IDictionary<string, object> arguments)
        {
            return Call(screen, tool, arguments, named => null);
        }

        private static IDictionary<string, object> Call(
            Screen screen,
            string tool,
            IDictionary<string, object> arguments,
            Func<string, IPEBaseWindowConnector> windows)
        {
            McpMethodTable methods = new McpMethodTable();
            UiOpenWindow.AddTo(methods, screen.Forms, windows);
            UiCloseWindow.AddTo(methods, screen.Forms, windows);
            UiPressItem.AddTo(methods, screen.Forms);
            McpMethod method;
            Assert.True(methods.TryGet(tool, out method), "登録されていない。");

            return (IDictionary<string, object>)method(new McpMethodContext(
                arguments,
                new InlineInvoker(),
                100000,
                new HandleLedger(
                    new HostLog(System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        "pmx-editor-mcp-window-" + Guid.NewGuid().ToString("N") + ".log")),
                    new HandleIdIssuer()),
                new EventQueue(new EventSequenceIssuer())));
        }

        private static IDictionary<string, object> Value(IDictionary<string, object> envelope)
        {
            Assert.True((bool)envelope["ok"], "包みが成功でない。");

            return (IDictionary<string, object>)envelope["value"];
        }

        private static string Code(IDictionary<string, object> envelope)
        {
            Assert.False((bool)envelope["ok"], "包みが成功した。");

            return (string)((IDictionary<string, object>)envelope["error"])["code"];
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
            thread.IsBackground = true;
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "表示が閉じられず、STAのスレッドが終わらなかった。");
            if (caught != null)
            {
                throw new InvalidOperationException("STAのスレッドで落ちた。", caught);
            }
        }

        /// <summary>エディタのウィンドウと同じく、フォームそのものが SDK のコネクタを実装する。</summary>
        private sealed class FakeWindowConnector : Form, IPEBaseWindowConnector
        {
            internal FakeWindowConnector()
            {
                Name = "TransformView";
                Text = "TransformView";
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                Location = new Point(-32000, -32000);
            }

            internal List<bool> Written { get; } = new List<bool>();

            bool IPEBaseWindowConnector.Visible
            {
                get { return Visible; }

                set
                {
                    Written.Add(value);
                    Visible = value;
                }
            }

            Point IPEBaseWindowConnector.Location
            {
                get { return Location; }
                set { Location = value; }
            }

            bool IPEBaseWindowConnector.Focus()
            {
                return Focus();
            }
        }

        private sealed class Screen : IDisposable
        {
            private readonly List<Form> _forms = new List<Form>();

            internal Screen()
            {
                Main = Shown("PmxForm", "Pmx編集");
                Save = new ToolStripMenuItem("上書き保存(&U)") { Name = "MenuItem_Save" };
                Main.Controls.Add(Strip(Menu("MenuItem_File", Save)));
                View = Shown("PMXView", "PmxView");
                Item = new ToolStripMenuItem("TransformView(&T)") { Name = "MenuItem_TransformView" };
                Item.Click += (sender, e) =>
                {
                    Transform = Shown("TransformView", "TransformView");
                    Transform.Controls.Add(Strip(Menu("MenuItem_File", Normalize, Apply, Archive)));
                    SplitContainer split = new SplitContainer { Name = "splitContainer1" };
                    MenuStrip ext = new MenuStrip { Name = "extMenuStrip1" };
                    ext.Items.Add(Menu("MenuItem_Init", Initialize));
                    split.Panel2.Controls.Add(ext);
                    Transform.Controls.Add(split);
                };
                View.Controls.Add(Strip(Menu("MenuItem_View", Item)));
                Normalize = new ToolStripMenuItem("保存／更新時などの頂点モーフ正規化(&N)") { Name = "MenuItem_SaveNormalize" };
                Apply = new ToolStripMenuItem("現在の変形状態でモデル形状を更新(&U)") { Name = "MenuItem_SetupCurrentPose" };
                Initialize = new ToolStripMenuItem("全て初期化(&Q)") { Name = "MenuItem_Initialize" };
                Archive = new ToolStripMenuItem("現在の形状をアーカイブ追加(&A)") { Name = "MenuItem_PushArchive" };
            }

            internal Form Main { get; }

            internal Form View { get; }

            internal Form Transform { get; private set; }

            internal ToolStripMenuItem Item { get; }

            internal ToolStripMenuItem Save { get; }

            internal ToolStripMenuItem Normalize { get; }

            internal ToolStripMenuItem Apply { get; }

            internal ToolStripMenuItem Initialize { get; }

            internal ToolStripMenuItem Archive { get; }

            internal void Add(Form form)
            {
                _forms.Add(form);
            }

            internal IEnumerable<Form> Forms()
            {
                return new List<Form>(_forms);
            }

            public void Dispose()
            {
                foreach (Form form in _forms)
                {
                    form.Dispose();
                }
            }

            private static MenuStrip Strip(ToolStripMenuItem menu)
            {
                MenuStrip strip = new MenuStrip { Name = "menuStrip1" };
                strip.Items.Add(menu);

                return strip;
            }

            private static ToolStripMenuItem Menu(string name, params ToolStripMenuItem[] items)
            {
                ToolStripMenuItem menu = new ToolStripMenuItem(name) { Name = name };
                menu.DropDownItems.AddRange(items);

                return menu;
            }

            private Form Shown(string name, string title)
            {
                Form form = new Form
                {
                    Name = name,
                    Text = title,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                };
                _forms.Add(form);
                form.Show();

                return form;
            }
        }
    }
}
