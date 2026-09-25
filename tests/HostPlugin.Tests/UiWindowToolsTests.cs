using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
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
            Screen screen, string tool, IDictionary<string, object> arguments)
        {
            McpMethodTable methods = new McpMethodTable();
            UiOpenWindow.AddTo(methods, screen.Forms);
            UiCloseWindow.AddTo(methods, screen.Forms);
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
            thread.Start();
            thread.Join();
            if (caught != null)
            {
                throw new InvalidOperationException("STAのスレッドで落ちた。", caught);
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
                    Transform.Controls.Add(Strip(Menu("MenuItem_File", Normalize, Apply)));
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
            }

            internal Form Main { get; }

            internal Form View { get; }

            internal Form Transform { get; private set; }

            internal ToolStripMenuItem Item { get; }

            internal ToolStripMenuItem Save { get; }

            internal ToolStripMenuItem Normalize { get; }

            internal ToolStripMenuItem Apply { get; }

            internal ToolStripMenuItem Initialize { get; }

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
