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
            E2eCase one = Assert.Single(Build(Tool("session_release_handle", Handles())));

            Assert.Equal("session_release_handle", one.Tool);
            Assert.Equal(E2eExpectation.Refusal, one.Expectation);
            Assert.Equal("TOOL_INVALID_HANDLE", one.Code);
            Assert.Equal(new[] { "handles" }, one.Arguments.Keys.ToArray());
        }

        [Fact]
        public void TheHandleCheckAlsoCarriesTheOtherGroupsTheToolMustHave()
        {
            E2eCase one = Assert.Single(Build(Valued("model_update_items")));

            Assert.Equal("TOOL_INVALID_HANDLE", one.Code);
            Assert.Equal(new[] { "handles", "value" }, one.Arguments.Keys.OrderBy(k => k).ToArray());
            Assert.Empty((IDictionary<string, object>)one.Arguments["value"]);
        }

        [Fact]
        public void AGroupThatCannotBeFilledLeavesNoHandleCheck()
        {
            Assert.Empty(Build(Shaped("model_paint_items")));
        }

        [Fact]
        public void AToolThatTakesACountIsCheckedAtTheEdgeOfThePage()
        {
            E2eCase one = Assert.Single(Build(Tool("model_list_bone", Limit())));

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

            E2eCase one = Assert.Single(cases);
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
                Build(Tool("model_release", Handles()), rowKey: RowKey));

            Assert.Equal(RowKey, one.RowKey);
            Assert.Equal("read", one.EditKind);
            Assert.Equal("Host.Connector.Pmx", one.ConnectionPath);
        }

        [Fact]
        public void AToolThatNoRowNamesIsStillChecked()
        {
            E2eCase one = Assert.Single(Build(Tool("session_release_handle", Handles())));

            Assert.Equal(string.Empty, one.RowKey);
            Assert.Equal(string.Empty, one.EditKind);
            Assert.Equal(string.Empty, one.ConnectionPath);
        }

        [Fact]
        public void AToolWithoutAnythingToCheckGivesNoCase()
        {
            Assert.Empty(Build(Tool("model_get_name", new SchemaItem[0])));
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
