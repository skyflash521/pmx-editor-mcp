using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class UiFindTests
    {
        [Fact]
        public void AnyOfTheTextsFindsItsParts()
        {
            IDictionary<string, object> value = Value(Call(new object[] { "タグ編集", "経過状態" }));

            Assert.Equal(3, value["total"]);
        }

        [Fact]
        public void APartHeldByMoreThanOneOfTheTextsIsFoundOnce()
        {
            IDictionary<string, object> value = Value(Call(new object[] { "タグ編集", "タグ" }));
            IDictionary<string, object> wider = Value(Call(new object[] { "タグ" }));

            Assert.Equal(wider["total"], value["total"]);
        }

        [Fact]
        public void TheOrderOfTheTextsDoesNotChangeWhatIsFound()
        {
            Assert.Equal(
                Matches(Value(Call(new object[] { "タグ編集", "経過状態" }))),
                Matches(Value(Call(new object[] { "経過状態", "タグ編集" }))));
        }

        [Fact]
        public void ASingleTextFindsThePartsThatCarryIt()
        {
            IDictionary<string, object> value = Value(Call(new object[] { "タグ編集" }));

            Assert.Equal(2, value["total"]);
        }

        [Fact]
        public void ATextThatIsNotAnArrayIsRefused()
        {
            Assert.Equal(ToolEnvelope.InvalidArgument, Code(Call("タグ編集")));
        }

        [Fact]
        public void AnEmptyArrayOfTextsIsRefused()
        {
            Assert.Equal(ToolEnvelope.InvalidArgument, Code(Call(new object[0])));
        }

        [Fact]
        public void AnEmptyTextAmongTheTextsIsRefused()
        {
            Assert.Equal(ToolEnvelope.InvalidArgument, Code(Call(new object[] { "タグ編集", string.Empty })));
        }

        [Fact]
        public void NoTextsIsRefused()
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Code(Invoke(new Dictionary<string, object>(StringComparer.Ordinal))));
        }

        private static IDictionary<string, object> Call(object texts)
        {
            return Invoke(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UiFind.TextsName, texts },
            });
        }

        private static IDictionary<string, object> Invoke(IDictionary<string, object> arguments)
        {
            McpMethodTable methods = new McpMethodTable();
            UiFind.AddTo(methods);
            McpMethod method;
            Assert.True(methods.TryGet(UiFind.ToolName, out method), "登録されていない。");
            McpMethodContext context = new McpMethodContext(
                arguments,
                new InlineInvoker(),
                100000,
                new HandleLedger(
                    new HostLog(System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        "pmx-editor-mcp-find-" + Guid.NewGuid().ToString("N") + ".log")),
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

        private static string[] Matches(IDictionary<string, object> value)
        {
            return ((object[])value["matches"])
                .Select(m => new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(m))
                .OrderBy(m => m, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
