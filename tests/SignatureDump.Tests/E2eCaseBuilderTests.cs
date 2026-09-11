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

        /// <summary>確認を要する行のほかの検査は、確認を渡したうえで見る。</summary>
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

        /// <summary>行から名前を導かない共通契約のツールも母集団に入る。</summary>
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
                () => E2eCaseBuilder.Build(null, schemas, named, Paths(), dangerous));
            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(Map(RowKey), null, named, Paths(), dangerous));
            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(Map(RowKey), schemas, null, Paths(), dangerous));
            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(Map(RowKey), schemas, named, null, dangerous));
            Assert.Throws<ArgumentNullException>(
                () => E2eCaseBuilder.Build(Map(RowKey), schemas, named, Paths(), null));
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
                    : new HashSet<string>(StringComparer.Ordinal));
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
