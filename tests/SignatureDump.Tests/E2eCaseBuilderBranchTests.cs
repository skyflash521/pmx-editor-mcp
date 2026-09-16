using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>呼び分けを選んだ呼び出しの組み立て。選んだ呼び分けの外の項目は渡さない。</summary>
    public sealed class E2eCaseBuilderBranchTests
    {
        private const string Tool = "model_update_things";

        private const string Factory = "model_second_kind";

        private const string Selector = "itemType";

        [Fact]
        public void TheValuesAreTheOnesTheChosenBranchTakes()
        {
            IDictionary<string, object> arguments = Called().Arguments;

            Assert.Equal(
                new[] { "second" },
                ((IDictionary<string, object>)arguments["value"]).Keys.ToArray());
        }

        /// <summary>
        /// 選ばれていない呼び分けだけが持つ項目を渡すと、呼び先はそれを知らない項目として断る。
        /// </summary>
        [Fact]
        public void NoInputOfTheBranchThatWasNotChosenIsPassed()
        {
            Assert.DoesNotContain("only", Called().Arguments.Keys);
        }

        [Fact]
        public void TheChosenBranchIsTheOneTheMakerNames()
        {
            Assert.Equal("second_kind", Called().Arguments[Selector]);
        }

        private static E2eCase Called()
        {
            return E2eCaseBuilder.Build(
                new ToolMap(new ToolMapRow[0]),
                new ToolSchemaTable(new[] { Taking(), Free(Factory) }),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<SchemaItem, string>(),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                new Dictionary<string, IList<string>>(StringComparer.Ordinal)
                {
                    { Tool, new[] { Factory } },
                })
                .Single(
                    c => c.Expectation == E2eExpectation.Called
                        && string.Equals(c.Tool, Tool, StringComparison.Ordinal));
        }

        /// <summary>種別で呼び分けるツール。呼び分けごとに受け取る値の組が違う。</summary>
        private static ToolSchema Taking()
        {
            return new ToolSchema(
                Tool,
                new[]
                {
                    Branch("first_kind", "first"),
                    Branch("second_kind", "second"),
                    Branch("third_kind", "third", Item("number", "only")),
                },
                Output(),
                null);
        }

        private static SchemaBranch Branch(string kind, string member, params SchemaItem[] extra)
        {
            return new SchemaBranch(
                kind,
                Selector,
                kind,
                new[]
                {
                    Item("text", Selector),
                    Item("number", "handles"),
                    new SchemaItem(
                        null, new[] { Item("number", member) }, null, "value",
                        ItemOrigin.HostInput, true, null, false, null, null, null, false, null),
                }.Concat(extra).ToArray(),
                new SchemaChoice[0]);
        }

        /// <summary>受け手を渡さずに呼べるツール。出たハンドルは応答の並びの中へ入る。</summary>
        private static ToolSchema Free(string tool)
        {
            return new ToolSchema(
                tool,
                new[]
                {
                    new SchemaBranch(
                        "only", null, null, new SchemaItem[0], new SchemaChoice[0]),
                },
                new SchemaItem(
                    null, null, Output(), null, ItemOrigin.HostOutput, null, null, false, null,
                    null, null, false, null),
                null);
        }

        private static SchemaItem Item(string shape, string name)
        {
            return new SchemaItem(
                shape, null, null, name, ItemOrigin.HostInput, true, null, false, null, null,
                null, false, null);
        }

        private static SchemaItem Output()
        {
            return new SchemaItem(
                "number", null, null, null, ItemOrigin.HostOutput, null, null, false, null, null,
                null, false, null);
        }
    }
}
