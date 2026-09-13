using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>
    /// 実機のエディタへ投げる検査の組み立て。母集団はスキーマ正本のツールで、行から名前を導く
    /// ツールだけが行キーと編集の流れと接続の経路を持つ。
    /// </summary>
    public sealed class E2eCaseBuilderTests
    {
        private const string RowKey = "Sdk.Type.Wipe()";

        [Fact]
        public void AToolThatTakesHandlesIsCheckedWithAHandleTheLedgerDoesNotCarry()
        {
            E2eCase one = Assert.Single(Refused(Build(Tool("session_release_handle", Handles()))));

            Assert.Equal("session_release_handle", one.Tool);
            Assert.Equal(E2eExpectation.Refusal, one.Expectation);
            Assert.Equal("TOOL_INVALID_HANDLE", one.Code);
            Assert.Equal(new[] { "handles" }, one.Arguments.Keys.ToArray());
        }

        [Fact]
        public void TheHandleCheckAlsoCarriesTheOtherGroupsTheToolMustHave()
        {
            E2eCase one = Assert.Single(Refused(Build(Valued("model_update_items"))));

            Assert.Equal("TOOL_INVALID_HANDLE", one.Code);
            Assert.Equal(new[] { "handles", "value" }, one.Arguments.Keys.OrderBy(k => k).ToArray());
            Assert.Empty((IDictionary<string, object>)one.Arguments["value"]);
        }

        [Fact]
        public void AGroupThatCannotBeFilledLeavesNoHandleCheck()
        {
            Assert.Empty(Refused(Build(Shaped("model_paint_items"))));
        }

        [Fact]
        public void AGroupWithASampleValueLeavesTheHandleCheck()
        {
            ToolSchema schema = Shaped("model_paint_items");
            SchemaItem color = schema.Branches[0].Inputs.Single(i => i.Name == "color");
            E2eCase one = Assert.Single(Refused(E2eCaseBuilder.Build(
                Map(RowKey),
                new ToolSchemaTable(new[] { schema }),
                new Dictionary<string, string>(StringComparer.Ordinal),
                Paths(),
                new HashSet<string>(StringComparer.Ordinal),
                Shapes(),
                new Dictionary<SchemaItem, string> { { color, "Sdk.Paint" } },
                new SampleValueTable(new[] { new SampleValueRow("Sdk.Paint", "赤", "青") }))));

            Assert.Equal("TOOL_INVALID_HANDLE", one.Code);
            Assert.Equal("赤", one.Arguments["color"]);
        }

        [Fact]
        public void AToolThatTakesACountIsCheckedAtTheEdgeOfThePage()
        {
            E2eCase one = Assert.Single(Refused(Build(Tool("model_list_bone", Limit()))));

            Assert.Equal("TOOL_INVALID_ARGUMENT", one.Code);
            Assert.Equal(0d, Convert.ToDouble(one.Arguments["limit"]));
        }

        [Fact]
        public void ADangerousRowIsCheckedWithoutTheConfirmation()
        {
            IList<E2eCase> cases = Build(
                Tool("model_wipe", new SchemaItem[0]),
                rowKey: RowKey,
                dangerous: true);

            E2eCase one = Assert.Single(Refused(cases));
            Assert.Equal("TOOL_CONFIRM_REQUIRED", one.Code);
            Assert.Empty(one.Arguments);
        }

        [Fact]
        public void TheOtherChecksOfADangerousRowCarryTheConfirmation()
        {
            IList<E2eCase> cases = Build(
                Tool("model_wipe", Handles()), rowKey: RowKey, dangerous: true);

            E2eCase handles = Assert.Single(cases, c => c.Code == "TOOL_INVALID_HANDLE");
            Assert.Equal(true, handles.Arguments["confirm"]);
        }

        [Fact]
        public void AToolNamedByARowCarriesTheRowAndTheFlowAndThePath()
        {
            E2eCase one = Assert.Single(
                Refused(Build(Tool("model_release", Handles()), rowKey: RowKey)));

            Assert.Equal(RowKey, one.RowKey);
            Assert.Equal("read", one.EditKind);
            Assert.Equal("Host.Connector.Pmx", one.ConnectionPath);
        }

        [Fact]
        public void AToolThatNoRowNamesIsStillChecked()
        {
            E2eCase one = Assert.Single(Refused(Build(Tool("session_release_handle", Handles()))));

            Assert.Equal(string.Empty, one.RowKey);
            Assert.Equal(string.Empty, one.EditKind);
            Assert.Equal(string.Empty, one.ConnectionPath);
        }

        [Fact]
        public void AToolWithoutAnythingToAbuseGivesNoRefusalCase()
        {
            Assert.Empty(Refused(Build(Tool("model_get_name", new SchemaItem[0]))));
        }

        [Fact]
        public void EveryToolIsCheckedForHavingSomethingToCallBehindIt()
        {
            E2eCase one = Assert.Single(
                Build(Tool("model_get_name", new SchemaItem[0])),
                c => c.Expectation == E2eExpectation.Dispatched);

            Assert.Equal("model_get_name", one.Tool);
            Assert.Empty(one.Arguments);
            Assert.Null(one.Code);
        }

        [Fact]
        public void AToolThatReturnsAViewImageIsCheckedAgainstTheCapturedView()
        {
            const string Named = "view_get_client_image";
            E2eCase one = Assert.Single(
                E2eCaseBuilder.Build(
                    Map(RowKey),
                    new ToolSchemaTable(new[] { Tool(Named, new SchemaItem[0]) }),
                    new Dictionary<string, string>(StringComparer.Ordinal) { { RowKey, Named } },
                    Paths(),
                    new HashSet<string>(StringComparer.Ordinal),
                    Shapes(),
                    null,
                    null,
                    new Dictionary<string, string>(StringComparer.Ordinal) { { Named, "pmx" } }),
                c => c.Expectation == E2eExpectation.ViewImage);

            Assert.Equal(Named, one.Tool);
            Assert.Equal("pmx", one.View);
            Assert.Equal(RowKey, one.RowKey);
            Assert.Empty(one.Arguments);
        }

        [Fact]
        public void AToolThatReturnsNoViewImageIsNotCheckedAgainstAnyView()
        {
            Assert.DoesNotContain(
                Build(Tool("model_get_name", new SchemaItem[0])),
                c => c.Expectation == E2eExpectation.ViewImage);
        }

        [Fact]
        public void APositionedMemberTakesNoRelationAndThenTheHeadOfTheList()
        {
            ToolSchema writing = Positioning("model_update_bones");
            ToolSchema reading = Listed("model_list_bones");
            IList<E2eCase> written = Positions(writing, reading, reading.Tool)
                .Where(c => c.Tool == writing.Tool && c.Expectation == E2eExpectation.Success)
                .ToList();

            Assert.Equal(2, written.Count);
            Assert.Equal(true, written[0].Arguments["all"]);
            Assert.Null(((IDictionary<string, object>)written[0].Arguments["value"])["parent"]);
            Assert.Equal(0, ((IDictionary<string, object>)written[1].Arguments["value"])["parent"]);
        }

        [Fact]
        public void TheWrittenPositionIsReadBackFromTheToolThatListsTheSameType()
        {
            ToolSchema writing = Positioning("model_update_bones");
            ToolSchema reading = Listed("model_list_bones");

            E2eCase read = Assert.Single(
                Positions(writing, reading, reading.Tool),
                c => c.Expectation == E2eExpectation.Reads);
            Assert.Equal(reading.Tool, read.Tool);
            Assert.Equal(true, read.Arguments["all"]);
            Assert.Equal("parent", read.Expected.Member);
            Assert.Equal(0, read.Expected.Value);
        }

        [Fact]
        public void APositionedMemberIsAlsoCheckedWithAPositionNoListCarries()
        {
            ToolSchema writing = Positioning("model_update_bones");
            E2eCase one = Assert.Single(
                Positions(writing, Listed("model_list_bones"), null),
                c => c.Expectation == E2eExpectation.Refusal);

            Assert.Equal("TOOL_INDEX_OUT_OF_RANGE", one.Code);
            Assert.Equal(
                int.MaxValue,
                ((IDictionary<string, object>)one.Arguments["value"])["parent"]);
        }

        [Fact]
        public void AMemberTheModelDoesNotKeepIsWrittenWithoutReadingItBack()
        {
            ToolSchema writing = Positioning("model_update_bones");
            ToolSchema reading = Listed("model_list_bones");
            IList<E2eCase> cases = Positions(
                writing,
                reading,
                reading.Tool,
                new Dictionary<string, ISet<string>>(StringComparer.Ordinal)
                {
                    { writing.Tool, new HashSet<string>(new[] { "parent" }, StringComparer.Ordinal) },
                });

            Assert.Contains(
                cases, c => c.Tool == writing.Tool && c.Expectation == E2eExpectation.Success);
            Assert.DoesNotContain(cases, c => c.Expectation == E2eExpectation.Reads);
        }

        [Fact]
        public void AWriteWithNoReaderIsStillCheckedWithoutReadingItBack()
        {
            ToolSchema writing = Positioning("model_update_bones");
            IList<E2eCase> cases = Positions(writing, Listed("model_list_bones"), null);

            Assert.Contains(
                cases, c => c.Tool == writing.Tool && c.Expectation == E2eExpectation.Success);
            Assert.DoesNotContain(cases, c => c.Expectation == E2eExpectation.Reads);
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            ToolSchemaTable schemas = new ToolSchemaTable(new ToolSchema[0]);
            Dictionary<string, string> named = new Dictionary<string, string>(StringComparer.Ordinal);
            HashSet<string> dangerous = new HashSet<string>(StringComparer.Ordinal);

            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(null, schemas, named, Paths(), dangerous, Shapes()));
            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(Map(RowKey), null, named, Paths(), dangerous, Shapes()));
            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(Map(RowKey), schemas, null, Paths(), dangerous, Shapes()));
            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(Map(RowKey), schemas, named, null, dangerous, Shapes()));
            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(Map(RowKey), schemas, named, Paths(), null, Shapes()));
            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(Map(RowKey), schemas, named, Paths(), dangerous, null));
        }

        /// <summary>断りを見る検査だけ。呼び先が在ることの検査はどのツールにも付くので外す。</summary>
        private static IList<E2eCase> Refused(IEnumerable<E2eCase> cases)
        {
            return cases.Where(c => c.Expectation != E2eExpectation.Dispatched).ToList();
        }

        private static IList<E2eCase> Build(ToolSchema schema, string rowKey = null, bool dangerous = false)
        {
            Dictionary<string, string> named = new Dictionary<string, string>(StringComparer.Ordinal);
            if (rowKey != null)
            {
                named[rowKey] = schema.Tool;
            }

            return E2eCaseBuilder.Build(
                Map(rowKey ?? RowKey),
                new ToolSchemaTable(new[] { schema }),
                named,
                Paths(),
                dangerous
                    ? new HashSet<string>(new[] { rowKey }, StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal),
                Shapes());
        }

        /// <summary>SDKに由来する項目の綴り。題材では引く先を持たない。</summary>
        private static IDictionary<SchemaItem, string> Shapes()
        {
            return new Dictionary<SchemaItem, string>();
        }

        private static ToolMap Map(string rowKey)
        {
            return new ToolMap(new[]
            {
                new ToolMapRow(rowKey, ToolMapEditKind.Read, null, "読むだけ。", null, null, null),
            });
        }

        private static IDictionary<string, string> Paths()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Sdk.Type", "Host.Connector.Pmx" },
            };
        }

        private static ToolSchema Tool(string name, IList<SchemaItem> inputs)
        {
            return new ToolSchema(
                name,
                new[] { new SchemaBranch("only", null, null, inputs, new SchemaChoice[0]) },
                Output(),
                null);
        }

        /// <summary>対象をハンドルで指し、値の組も要るツール。</summary>
        private static ToolSchema Valued(string name)
        {
            SchemaItem value = new SchemaItem(
                null, new SchemaItem[0], null, "value", ItemOrigin.HostInput, null, null, false,
                null, null, null, false, null);

            return new ToolSchema(
                name,
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        Handles().Concat(new[] { value }).ToList(),
                        new[] { new SchemaChoice(new[] { "value", "values" }, true) }),
                },
                Output(),
                null);
        }

        /// <summary>対象をハンドルで指し、綴りから値を決められない項目が要るツール。</summary>
        private static ToolSchema Shaped(string name)
        {
            SchemaItem color = new SchemaItem(
                "color", null, null, "color", ItemOrigin.HostInput, null, null, false,
                null, null, null, false, null);

            return new ToolSchema(
                name,
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        Handles().Concat(new[] { color }).ToList(),
                        new[] { new SchemaChoice(new[] { "color", "brush" }, true) }),
                },
                Output(),
                null);
        }

        private static IList<SchemaItem> Handles()
        {
            return new[]
            {
                new SchemaItem(
                    null, null, Element(), "handles", ItemOrigin.HostInput, true, null, false,
                    null, null, null, false, null),
            };
        }

        private static IList<SchemaItem> Limit()
        {
            return new[]
            {
                new SchemaItem(
                    "number", null, null, "limit", ItemOrigin.HostInput, false, null, false,
                    null, null, null, false, null),
            };
        }

        /// <summary>位置で指す項目を持つツールと、それを読み返すツールの組で検査を組み立てる。</summary>
        private static IList<E2eCase> Positions(
            ToolSchema writing,
            ToolSchema reading,
            string reader,
            IDictionary<string, ISet<string>> unkept = null)
        {
            SchemaItem member = writing.Branches[0].Inputs
                .Single(i => i.Name == "value").Members.Single();

            return E2eCaseBuilder.Build(
                Map(RowKey),
                new ToolSchemaTable(new[] { writing, reading }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { RowKey, writing.Tool } },
                Paths(),
                new HashSet<string>(StringComparer.Ordinal),
                Shapes(),
                new Dictionary<SchemaItem, string> { { member, "Sdk.Bone" } },
                null,
                null,
                new HashSet<string>(new[] { "Sdk.Bone" }, StringComparer.Ordinal),
                null,
                reader == null
                    ? null
                    : new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        { writing.Tool, reader },
                    },
                unkept);
        }

        /// <summary>全件を指して値の組を書き換えるツール。値の組は位置で指す項目を1つ持つ。</summary>
        private static ToolSchema Positioning(string name)
        {
            SchemaItem parent = new SchemaItem(
                "number", null, null, "parent", ItemOrigin.HostInput, null, null, false,
                null, null, null, false, null);
            SchemaItem value = new SchemaItem(
                null, new[] { parent }, null, "value", ItemOrigin.HostInput, null, null, false,
                null, null, null, false, null);

            return new ToolSchema(
                name,
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        new[] { Whole(), value },
                        new[] { new SchemaChoice(new[] { "all", "indices" }, true) }),
                },
                Output(),
                null);
        }

        /// <summary>全件を指して並べるツール。値の組は受け取らない。</summary>
        private static ToolSchema Listed(string name)
        {
            SchemaItem items = new SchemaItem(
                null, null, Element(), "items", ItemOrigin.HostOutput, null, null, false,
                null, null, null, false, null);

            return new ToolSchema(
                name,
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        new[] { Whole() },
                        new[] { new SchemaChoice(new[] { "all", "indices" }, true) }),
                },
                new SchemaItem(
                    null, new[] { items }, null, null, ItemOrigin.HostOutput, null, null, false,
                    null, null, null, false, null),
                null);
        }

        /// <summary>対象を全件にする入力。</summary>
        private static SchemaItem Whole()
        {
            return new SchemaItem(
                "boolean", null, null, "all", ItemOrigin.HostInput, false, null, false,
                null, null, null, false, null);
        }

        private static SchemaItem Element()
        {
            return new SchemaItem(
                "number", null, null, null, ItemOrigin.HostInput, null, null, false,
                null, null, null, false, null);
        }

        private static SchemaItem Output()
        {
            return new SchemaItem(
                "number", null, null, null, ItemOrigin.HostOutput, null, null, false,
                null, null, null, false, null);
        }
    }
}
