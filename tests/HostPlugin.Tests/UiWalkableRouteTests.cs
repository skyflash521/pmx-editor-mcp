using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class UiWalkableRouteTests
    {
        private const string MainForm = "PmxEditor.PmxForm";

        private const string Version = "PmxEditor.VersionForm";

        private static readonly string[] VersionPath = { "menuStrip1", "MenuItem_Info", "MenuItem_Version" };

        [Fact]
        public void AnItemInAContextMenuIsNotWalkable()
        {
            Assert.False(Walkable(MainForm, "contextBody", "CMenuItem_Body_Save"));
        }

        [Fact]
        public void ADangerousItemIsNotWalkable()
        {
            Assert.False(Walkable(MainForm, "menuStrip1", "MenuItem_File", "MenuItem_New"));
        }

        [Fact]
        public void AnItemThatUsesTheClipboardIsNotWalkable()
        {
            Assert.False(Walkable(MainForm, "menuStrip1", "MenuItem_File", "MenuItem_TextInOut", "MenuItem_TextSaveClip"));
        }

        [Fact]
        public void AnItemThatOpensAModalWindowIsNotWalkable()
        {
            Assert.False(Walkable(MainForm, VersionPath));
        }

        [Fact]
        public void ACheckBoxThatOpensNothingIsWalkable()
        {
            Assert.True(Walkable(MainForm, "tabPmxData", "tabPage5", "chkBone_Local"));
        }

        [Fact]
        public void AnItemThatOpensAModelessWindowIsWalkable()
        {
            Assert.True(Walkable(MainForm, "menuStrip1", "MenuItem_Edit", "MenuItem_TagEdit"));
        }

        [Fact]
        public void TheRouteSkipsContextMenusListedFirstAndTakesTheMenuBar()
        {
            IList<IDictionary<string, object>> route = UiStructureRoute.To("PmxEditor.CsvElementView");

            Assert.NotNull(route);
            Assert.Single(route);
            Assert.Equal(MainForm, route[0][UiStructureCatalog.FormName]);
            Assert.Equal(
                new[] { "menuStrip1", "MenuItem_Edit", "MenuItem_CsvElement", "MenuItem_CsvElement_Show" },
                ((object[])route[0][UiStructureRoute.ViaName])
                    .Select(hop => (string)((IDictionary<string, object>)hop)[UiStructureCatalog.NameName])
                    .ToArray());
        }

        [Fact]
        public void AWindowOnlyAModalItemOpensHasNoRoute()
        {
            Assert.Null(UiStructureRoute.To(Version));
            Assert.Null(UiOpenWindow.Hops(Version, named => string.Equals(named, MainForm, StringComparison.Ordinal)));
        }

        [Fact]
        public void TheFoundWindowWithoutAWalkableRouteIsUnreachable()
        {
            McpMethodTable methods = new McpMethodTable();
            UiFind.AddTo(methods);
            IDictionary<string, object> value = Value(Invoke(methods, UiFind.ToolName, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiFind.TextsName, new object[] { "ver情報" } },
            }));

            IDictionary<string, object> found = ((object[])value["matches"])
                .Cast<IDictionary<string, object>>()
                .First(match => string.Equals((string)match[UiStructureCatalog.FormName], Version, StringComparison.Ordinal));
            Assert.False(found.ContainsKey("route"), "押せない道を返した。");
            Assert.Equal(true, found["unreachable"]);
        }

        [Fact]
        public void PressingAnItemThatOpensAModalWindowIsRefusedFromTheCatalogAlone()
        {
            int looked = 0;
            McpMethodTable methods = new McpMethodTable();
            UiPressItem.AddTo(methods, () =>
            {
                looked++;

                return new Form[0];
            });

            IDictionary<string, object> refused = Invoke(methods, UiPressItem.ToolName, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiPressItem.WindowName, MainForm },
                { UiPressItem.PathName, VersionPath.Cast<object>().ToArray() },
            });

            Assert.Equal(ToolEnvelope.NotApplicable, Code(refused));
            Assert.Contains(Version, Said(refused));
            Assert.Equal(0, looked);
        }

        [Fact]
        public void OpeningAModalWindowIsRefusedFromTheCatalogAlone()
        {
            int looked = 0;
            McpMethodTable methods = new McpMethodTable();
            UiOpenWindow.AddTo(methods, () =>
            {
                looked++;

                return new Form[0];
            });

            IDictionary<string, object> refused = Invoke(methods, UiOpenWindow.ToolName, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiOpenWindow.WindowName, Version },
            });

            Assert.Equal(ToolEnvelope.NotApplicable, Code(refused));
            Assert.Contains(Version, Said(refused));
            Assert.Equal(0, looked);
        }

        private static bool Walkable(string window, params string[] path)
        {
            return UiStructureCatalog.Walkable(window, path);
        }

        private static IDictionary<string, object> Invoke(
            McpMethodTable methods, string tool, IDictionary<string, object> arguments)
        {
            McpMethod method;
            Assert.True(methods.TryGet(tool, out method), "登録されていない。");
            McpMethodContext context = new McpMethodContext(
                arguments,
                new InlineInvoker(),
                100000,
                new HandleLedger(
                    new HostLog(System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        "pmx-editor-mcp-walkable-" + Guid.NewGuid().ToString("N") + ".log")),
                    new HandleIdIssuer()),
                new EventQueue(new EventSequenceIssuer()));

            return (IDictionary<string, object>)method(context);
        }

        private static IDictionary<string, object> Value(IDictionary<string, object> envelope)
        {
            Assert.True((bool)envelope["ok"], "包みが成功でない。");

            return (IDictionary<string, object>)envelope["value"];
        }

        private static string Code(IDictionary<string, object> envelope)
        {
            Assert.False((bool)envelope["ok"], "包みが失敗でない。");

            return (string)((IDictionary<string, object>)envelope["error"])["code"];
        }

        private static string Said(IDictionary<string, object> envelope)
        {
            return (string)((IDictionary<string, object>)envelope["error"])["message"];
        }
    }
}
