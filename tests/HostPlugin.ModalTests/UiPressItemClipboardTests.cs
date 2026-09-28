using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace PmxEditorMcp.Tests
{
    [Collection(ModalWindowCollection.Name)]
    public sealed class UiPressItemClipboardTests
    {
        public static readonly TheoryData<string, string> ItemsWhoseHandlersReadOrWriteTheClipboard =
            new TheoryData<string, string>
        {
            { "PmxEditor.CsvElementView", "menuStrip1/MenuItem_File/MenuItem_CopyOutput" },
            { "PmxEditor.CsvElementView", "menuStrip1/MenuItem_File/MenuItem_SaveCsv" },
            { "PmxEditor.CsvElementView", "menuStrip1/MenuItem_File/MenuItem_PasteInput" },
            { "PmxEditor.CsvElementView", "menuStrip1/MenuItem_Edit/MenuItem_InitInput" },
            { "PmxEditor.CsvElementView", "menuStrip1/MenuItem_Edit/MenuItem_Init" },
            { "PmxEditor.CsvElementView", "pnlFiletr/chkExtNonPrimary" },
            { "PmxEditor.CsvElementView", "pnlFiletr/btnFilterCheckRev" },
            { "PmxEditor.CsvElementView", "pnlFiletr/btnFilterCheckAll" },
            { "PmxEditor.CsvElementView", "pnlFiletr/chkModeType" },
            { "PmxEditor.CsvElementView", "pnlInput/btnInputDisable" },
            { "PmxEditor.CsvElementView", "pnlInput/btnInputEnable" },
            { "PmxEditor.CsvElementView", "pnlInput/chkEnableFilter" },
            { "PmxEditor.CsvElementView", "pnlInput/chkEnableHeader" },
            { "PmxEditor.PmxForm", "menuStrip1/MenuItem_File/MenuItem_TextInOut/MenuItem_TextSaveClip" },
            { "PmxEditor.PmxForm", "menuStrip1/MenuItem_File/MenuItem_TextInOut/MenuItem_TextLoadClip" },
            { "PmxEditor.PmxForm", "tabPmxData/tabPage3/grpFaceProc/btnFace_NormalVCopy" },
            { "PmxViewForm.PMXView", "menuStrip1/MenuItem_Edit/MenuItem_SelectObject/MenuItem_Object_CopyPos" },
            { "PmxViewForm.PMXView", "menuStrip1/MenuItem_Edit/MenuItem_SelectObject/MenuItem_Object_SetPosFromClip" },
            { "PmxViewForm.PMXView", "menuStrip1/MenuItem_Edit/MenuItem_SelectFace/MenuItem_SelectFace_Normal_Copy" },
            { "PmxViewForm.PMXView", "menuStrip1/MenuItem_Edit/MenuItem_SelectBone/MenuItem_Bone_DirCopy/MenuItem_Bone_DirCopy_To" },
            { "PmxViewForm.PMXView", "menuStrip1/MenuItem_Edit/MenuItem_SelectBone/MenuItem_Bone_DirCopy/MenuItem_Bone_DirCopy_Parent" },
            { "PmxViewForm.PMXView", "menuStrip1/MenuItem_Edit/MenuItem_SelectBone/MenuItem_Bone_DirCopy/MenuItem_Bone_DirCopy_Fix" },
            { "PmxViewForm.PMXView", "menuStrip1/MenuItem_Edit/MenuItem_SelectBone/MenuItem_Bone_DirCopy/MenuItem_Bone_DirCopy_LocalX" },
            { "PmxViewForm.PMXView", "menuStrip1/MenuItem_Edit/MenuItem_SelectBone/MenuItem_Bone_DirCopy/MenuItem_Bone_DirCopy_LocalY" },
            { "PmxViewForm.PMXView", "menuStrip1/MenuItem_Edit/MenuItem_SelectBone/MenuItem_Bone_DirCopy/MenuItem_Bone_DirCopy_LocalZ" },
            { "PmxViewForm.PMXView", "menuStrip1/MenuItem_Edit/MenuItem_Normal/MenuItem_Normal_Copy" },
            { "PmxViewForm.PmxSubView", "menuStrip1/MenuItem_Edit/MenuItem_SelectObject/MenuItem_Object_CopyPos" },
            { "PmxViewForm.PmxViewSelector", "menuStrip1/MenuItem_Edit/チェック要素の保存復元CToolStripMenuItem/MenuItem_CopyTextNum" },
            { "PmxViewForm.PmxViewSelector", "menuStrip1/MenuItem_Edit/チェック要素の保存復元CToolStripMenuItem/MenuItem_PasteTextNum" },
            { "PmxViewForm.PmxViewSelector", "menuStrip1/MenuItem_Edit/チェック要素の保存復元CToolStripMenuItem/MenuItem_CopyTextName" },
            { "PmxViewForm.PmxViewSelector", "menuStrip1/MenuItem_Edit/チェック要素の保存復元CToolStripMenuItem/MenuItem_PasteTextName" },
            { "PmxViewForm.TransformView", "menuStrip1/MenuItem_Edit/MenuItem_SelectObject/MenuItem_Object_CopyPos" },
            { "PmxViewForm.TransformView", "menuStrip1/MenuItem_Edit/MenuItem_SelectObject/MenuItem_Object_SetPosFromClip" },
            { "PmxViewForm.TransformView", "menuStrip1/MenuItem_Edit/MenuItem_PoseCopy" },
            { "PmxViewForm.TransformView", "menuStrip1/MenuItem_Edit/MenuItem_PosePaste" },
        };

        private const string DangerName = "danger";

        private const string ClipboardDanger = "clipboard";

        [Theory]
        [MemberData(nameof(ItemsWhoseHandlersReadOrWriteTheClipboard))]
        public void AnItemThatReadsOrWritesTheClipboardIsRefusedWithoutPressing(
            string window, string path)
        {
            string[] steps = path.Split('/');
            OnSta(() =>
            {
                using (Form form = Built(window, steps, out Func<int> pressed))
                {
                    IDictionary<string, object> answered = Call(
                        form,
                        UiPressItem.ToolName,
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            { UiPressItem.WindowName, window },
                            { UiPressItem.PathName, steps },
                        });

                    Assert.Equal(0, pressed());
                    Assert.False((bool)answered["ok"], "クリップボードを読み書きする部品を押した: " + path);
                    IDictionary<string, object> error = (IDictionary<string, object>)answered["error"];
                    Assert.Equal(ToolEnvelope.NotApplicable, (string)error["code"]);
                }
            });
        }

        [Fact]
        public void TheScreenStructureGivesTheClipboardDangerToExactlyTheItemsThatReadOrWriteTheClipboard()
        {
            HashSet<string> expected = new HashSet<string>(
                ItemsWhoseHandlersReadOrWriteTheClipboard.Select(row => (string)row[0] + "|" + (string)row[1]),
                StringComparer.Ordinal);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<string> wrong = new List<string>();
            OnSta(() =>
            {
                foreach (IDictionary<string, object> window in UiStructureCatalog.Windows())
                {
                    string named = UiStructureCatalog.Text(window, UiStructureCatalog.FormName);
                    foreach (string[] steps in PressableLeaves(
                        UiStructureCatalog.Node(window, UiStructureCatalog.RootName), new List<string>()))
                    {
                        string key = named + "|" + string.Join("/", steps);
                        seen.Add(key);
                        bool marked = Dangers(Structure(named, steps)).Contains(ClipboardDanger);
                        if (marked != expected.Contains(key))
                        {
                            wrong.Add(key + (marked ? " は読み書きしないのに印がある" : " に印が無い"));
                        }
                    }
                }
            });

            Assert.Empty(expected.Except(seen));
            Assert.True(
                wrong.Count == 0,
                "画面の構造の応答で danger の clipboard が合わない部品が " + wrong.Count + " 件ある:" + Environment.NewLine
                    + string.Join(Environment.NewLine, wrong));
        }

        private static IEnumerable<string[]> PressableLeaves(IDictionary<string, object> node, List<string> path)
        {
            foreach (IDictionary<string, object> child in UiStructureCatalog.Children(node))
            {
                if (string.Equals(UiStructureCatalog.Text(child, UiStructureCatalog.TypeName), "ContextMenuStrip", StringComparison.Ordinal))
                {
                    continue;
                }

                List<string> below = new List<string>(path) { UiStructureCatalog.Text(child, UiStructureCatalog.NameName) ?? string.Empty };
                if (UiStructureCatalog.Pressable(child) && UiStructureCatalog.Children(child).Count == 0)
                {
                    yield return below.ToArray();
                }

                foreach (string[] leaf in PressableLeaves(child, below))
                {
                    yield return leaf;
                }
            }
        }

        private static IDictionary<string, object> Structure(string window, string[] steps)
        {
            IDictionary<string, object> answered = Call(
                null,
                UiTree.ToolName,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { UiTree.WindowName, window },
                    { UiTree.PathName, steps },
                });
            Assert.True((bool)answered["ok"], "画面の構造を返さなかった: " + window + " " + string.Join("/", steps));
            IDictionary<string, object> value = (IDictionary<string, object>)answered["value"];
            object[] windows = (object[])value[UiStructureCatalog.WindowsName];

            return (IDictionary<string, object>)((IDictionary<string, object>)Assert.Single(windows))["node"];
        }

        private static ISet<string> Dangers(IDictionary<string, object> node)
        {
            HashSet<string> kinds = new HashSet<string>(StringComparer.Ordinal);
            object given;
            if (!node.TryGetValue(DangerName, out given) || given == null)
            {
                return kinds;
            }

            string one = given as string;
            if (one != null)
            {
                kinds.Add(one);

                return kinds;
            }

            foreach (object each in (System.Collections.IEnumerable)given)
            {
                kinds.Add((string)each);
            }

            return kinds;
        }

        private static Form Built(string window, string[] steps, out Func<int> pressed)
        {
            IDictionary<string, object> node =
                UiStructureCatalog.Node(UiStructureCatalog.Window(window), UiStructureCatalog.RootName);
            Form form = new Form
            {
                Name = UiStructureCatalog.Text(node, UiStructureCatalog.NameName),
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000),
            };
            object at = form;
            foreach (string step in steps)
            {
                node = UiStructureCatalog.Child(node, step);
                Assert.NotNull(node);
                at = Added(at, step, UiStructureCatalog.Text(node, UiStructureCatalog.TypeName));
            }

            int count = 0;
            ToolStripItem item = at as ToolStripItem;
            CheckBox box = at as CheckBox;
            if (item != null)
            {
                item.Click += (sender, e) => count++;
            }
            else if (box != null)
            {
                box.CheckedChanged += (sender, e) => count++;
            }
            else
            {
                ((Control)at).Click += (sender, e) => count++;
            }

            pressed = () => count;
            form.Show();

            return form;
        }

        private static object Added(object parent, string name, string type)
        {
            ToolStripDropDownItem dropping = parent as ToolStripDropDownItem;
            ToolStrip strip = parent as ToolStrip;
            if (dropping != null || strip != null)
            {
                ToolStripItem item = string.Equals(type, "ToolStripButton", StringComparison.Ordinal)
                    ? (ToolStripItem)new ToolStripButton(name)
                    : new ToolStripMenuItem(name);
                item.Name = name;
                if (dropping != null)
                {
                    dropping.DropDownItems.Add(item);
                }
                else
                {
                    strip.Items.Add(item);
                }

                return item;
            }

            Control control;
            switch (type)
            {
                case "MenuStrip":
                case "ExtMenuStrip":
                    control = new MenuStrip();
                    break;
                case "ToolStrip":
                    control = new ToolStrip();
                    break;
                case "TabControl":
                    control = new TabControl();
                    break;
                case "TabPage":
                    control = new TabPage();
                    break;
                case "Button":
                    control = new Button();
                    break;
                case "CheckBox":
                    control = new CheckBox();
                    break;
                default:
                    control = new Panel();
                    break;
            }

            control.Name = name;
            control.Text = name;
            TabPage page = control as TabPage;
            TabControl tabs = parent as TabControl;
            if (page != null && tabs != null)
            {
                tabs.TabPages.Add(page);
            }
            else
            {
                ((Control)parent).Controls.Add(control);
            }

            return control;
        }

        private static IDictionary<string, object> Call(Form form, string tool, IDictionary<string, object> arguments)
        {
            McpMethodTable methods = new McpMethodTable();
            Func<IEnumerable<Form>> forms = () => form == null ? new Form[0] : new[] { form };
            UiPressItem.AddTo(methods, forms);
            UiTree.AddTo(methods, forms);
            McpMethod method;
            Assert.True(methods.TryGet(tool, out method), "登録されていない。");

            return (IDictionary<string, object>)method(new McpMethodContext(
                arguments,
                new InlineInvoker(),
                100000,
                new HandleLedger(
                    new HostLog(System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        "pmx-editor-mcp-clipboard-" + Guid.NewGuid().ToString("N") + ".log")),
                    new HandleIdIssuer()),
                new EventQueue(new EventSequenceIssuer())));
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
            Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STAのスレッドが終わらなかった。");
            if (caught != null)
            {
                throw new InvalidOperationException("STAのスレッドで落ちた。", caught);
            }
        }
    }
}
