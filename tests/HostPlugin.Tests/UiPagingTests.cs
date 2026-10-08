using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class UiPagingTests
    {
        private const int LargeBudget = 50000000;

        private const string MainForm = "PmxEditor.PmxForm";

        [Fact]
        public void TheWindowListStartsAtTheOffset()
        {
            IDictionary<string, object> value = Value(Structure(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiTree.OffsetName, 50 },
            }));

            string[] forms = UiStructureCatalog.Windows()
                .Select(window => UiStructureCatalog.Text(window, UiStructureCatalog.FormName))
                .ToArray();
            Assert.Equal(forms.Length, value["total"]);
            Assert.Equal(forms.Skip(50).ToArray(), Forms(value["windows"]));
            Assert.False(value.ContainsKey("nextOffset"), "残りが無いのに続きを示した。");
        }

        [Fact]
        public void AnOffsetPastTheWindowsGivesNone()
        {
            IDictionary<string, object> value = Value(Structure(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiTree.OffsetName, 1000 },
            }));

            Assert.Empty((object[])value["windows"]);
            Assert.False(value.ContainsKey("nextOffset"), "残りが無いのに続きを示した。");
        }

        [Theory]
        [InlineData("1")]
        [InlineData(-1)]
        [InlineData(1.5)]
        [InlineData(3e9)]
        public void AnOffsetThatIsNotACountIsRefused(object offset)
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Code(Structure(new Dictionary<string, object>(StringComparer.Ordinal) { { UiTree.OffsetName, offset } })));
        }

        [Theory]
        [InlineData("1")]
        [InlineData(-1)]
        [InlineData(1.5)]
        [InlineData(UiTree.MaxDepth + 1)]
        public void ADepthOutsideTheRangeIsRefused(object depth)
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Code(Structure(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { UiTree.WindowName, MainForm },
                    { UiTree.DepthName, depth },
                })));
        }

        [Fact]
        public void DepthZeroGivesTheNodeWithoutChildren()
        {
            IDictionary<string, object> node = Node(Value(Structure(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiTree.WindowName, MainForm },
                { UiTree.PathName, new object[] { "menuStrip1" } },
                { UiTree.DepthName, 0 },
            })));

            Assert.Equal("menuStrip1", node[UiStructureCatalog.NameName]);
            Assert.False(node.ContainsKey(UiStructureCatalog.ChildrenName), "子を返した。");
        }

        [Fact]
        public void DepthOneGivesTheChildrenWithoutGrandchildren()
        {
            IDictionary<string, object> node = Node(Value(Structure(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiTree.WindowName, MainForm },
                { UiTree.PathName, new object[] { "menuStrip1" } },
                { UiTree.DepthName, 1 },
            })));

            object[] children = (object[])node[UiStructureCatalog.ChildrenName];
            Assert.NotEmpty(children);
            Assert.All(
                children.Cast<IDictionary<string, object>>(),
                child => Assert.False(child.ContainsKey(UiStructureCatalog.ChildrenName), "孫を返した。"));
        }

        [Fact]
        public void AWholeTreeTooLargeForTheBudgetIsRefusedWithAWayToGoDown()
        {
            IDictionary<string, object> refused = Structure(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiTree.WindowName, MainForm },
            });

            Assert.Equal(ToolEnvelope.ResponseTooLarge, Code(refused));
            Assert.Contains(UiTree.PathName, Said(refused));
        }

        [Fact]
        public void TreesOfWindowsSharingATitleTooLargeForTheBudgetAreRefusedNamingEach()
        {
            IDictionary<string, object> refused = Structure(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiTree.WindowName, "表示設定" },
            });

            Assert.Equal(ToolEnvelope.ResponseTooLarge, Code(refused));
            Assert.Contains("PmxViewForm.PmxViewSetting", Said(refused));
            Assert.Contains("VmdViewLib.VMDViewSetting", Said(refused));
        }

        [Fact]
        public void FollowingNextOffsetGivesEveryMatchOnceInOrder()
        {
            object[] texts = { "(&" };
            object[] whole = (object[])Value(Find(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { UiFind.TextsName, texts },
                    { UiFind.LimitName, UiFind.MaxLimit },
                },
                LargeBudget))["matches"];

            List<object> paged = new List<object>();
            int pages = 0;
            object offset = 0;
            while (offset != null)
            {
                IDictionary<string, object> value = Value(Find(
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { UiFind.TextsName, texts },
                        { UiFind.LimitName, UiFind.MaxLimit },
                        { UiFind.OffsetName, offset },
                    },
                    ResponseBudget.MinimumChars));
                Assert.Equal(whole.Length, value["total"]);
                Assert.True(
                    ResponseSize.Serializer.Serialize(value).Length <= ResponseSize.ValueChars(ResponseBudget.MinimumChars),
                    "頁が値の枠を超えた。");
                object[] matches = (object[])value["matches"];
                Assert.NotEmpty(matches);
                paged.AddRange(matches);
                pages++;
                offset = value.ContainsKey("nextOffset") ? value["nextOffset"] : null;
            }

            Assert.True(pages > 1, "応答の枠で区切られなかった。");
            Assert.Equal(Serialized(whole), Serialized(paged));
        }

        [Fact]
        public void TheLimitCutsThePageAndPointsToTheRest()
        {
            IDictionary<string, object> value = Value(Find(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { UiFind.TextsName, new object[] { "(&" } },
                    { UiFind.LimitName, 2 },
                    { UiFind.OffsetName, 3 },
                },
                ResponseBudget.MinimumChars));

            Assert.Equal(2, ((object[])value["matches"]).Length);
            Assert.Equal(5, value["nextOffset"]);
        }

        [Fact]
        public void AnOffsetPastTheMatchesGivesNone()
        {
            IDictionary<string, object> value = Value(Find(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { UiFind.TextsName, new object[] { "タグ編集" } },
                    { UiFind.OffsetName, 1000 },
                },
                ResponseBudget.MinimumChars));

            Assert.Empty((object[])value["matches"]);
            Assert.False(value.ContainsKey("nextOffset"), "残りが無いのに続きを示した。");
        }

        [Theory]
        [InlineData(UiFind.LimitName, 0)]
        [InlineData(UiFind.LimitName, "1")]
        [InlineData(UiFind.LimitName, 1.5)]
        [InlineData(UiFind.LimitName, 3e9)]
        [InlineData(UiFind.OffsetName, -1)]
        [InlineData(UiFind.OffsetName, "1")]
        [InlineData(UiFind.OffsetName, 1.5)]
        [InlineData(UiFind.OffsetName, 3e9)]
        public void ACountOutsideItsRangeIsRefused(string name, object count)
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Code(Find(
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { UiFind.TextsName, new object[] { "タグ編集" } },
                        { name, count },
                    },
                    ResponseBudget.MinimumChars)));
        }

        private static IDictionary<string, object> Structure(IDictionary<string, object> arguments)
        {
            McpMethodTable methods = new McpMethodTable();
            UiTree.AddTo(methods, () => new Form[0]);

            return Invoke(methods, UiTree.ToolName, arguments, ResponseBudget.MinimumChars);
        }

        private static IDictionary<string, object> Find(IDictionary<string, object> arguments, int budget)
        {
            McpMethodTable methods = new McpMethodTable();
            UiFind.AddTo(methods);

            return Invoke(methods, UiFind.ToolName, arguments, budget);
        }

        private static IDictionary<string, object> Invoke(
            McpMethodTable methods, string tool, IDictionary<string, object> arguments, int budget)
        {
            McpMethod method;
            Assert.True(methods.TryGet(tool, out method), "登録されていない。");
            McpMethodContext context = new McpMethodContext(
                arguments,
                new InlineInvoker(),
                budget,
                new HandleLedger(
                    new HostLog(System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        "pmx-editor-mcp-paging-" + Guid.NewGuid().ToString("N") + ".log")),
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

        private static IDictionary<string, object> Node(IDictionary<string, object> value)
        {
            return (IDictionary<string, object>)((IDictionary<string, object>)((object[])value["windows"])[0])["node"];
        }

        private static string[] Forms(object windows)
        {
            return ((object[])windows)
                .Select(window => (string)((IDictionary<string, object>)window)[UiStructureCatalog.FormName])
                .ToArray();
        }

        private static string[] Serialized(IEnumerable<object> matches)
        {
            return matches.Select(match => ResponseSize.Serializer.Serialize(match)).ToArray();
        }
    }
}
