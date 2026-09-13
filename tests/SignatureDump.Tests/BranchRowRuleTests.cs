using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class BranchRowRuleTests
    {
        private const string Tool = "session_set_share_data";

        private const string TextKey = "Sdk.Form.Share(System.String,System.String)";

        private const string NumberKey = "Sdk.Form.Share(System.String,System.Int32)";

        private static readonly IDictionary<string, string> Shapes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "System.String", "text" },
                { "System.Int32", "number" },
            };

        [Fact]
        public void EachRowIsTiedToTheBranchThatSelectsItsSpelling()
        {
            IDictionary<string, SchemaBranch> byRow =
                BranchRowRule.Resolve(Schema("text", "number"), Rows(), Shapes);

            Assert.Equal("text", byRow[TextKey].SelectorValue);
            Assert.Equal("number", byRow[NumberKey].SelectorValue);
        }

        [Fact]
        public void AToolWithOneRowTiesNothing()
        {
            Assert.Empty(BranchRowRule.Resolve(
                Schema("text", "number"), new[] { Rows()[0] }, Shapes));
        }

        [Fact]
        public void AToolWhoseBranchesSelectNothingTiesNothing()
        {
            Assert.Empty(BranchRowRule.Resolve(Schema(null, null), Rows(), Shapes));
        }

        [Fact]
        public void ABranchThatSelectsASpellingNoRowCarriesIsRefused()
        {
            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
                () => BranchRowRule.Resolve(Schema("text", "base64"), Rows(), Shapes));

            Assert.Contains(NumberKey, refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RowsThatNoArgumentTellsApartAreRefused()
        {
            SignatureRecord[] same =
            {
                Signature(TextKey, "System.String"),
                Signature(NumberKey, "System.String"),
            };

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
                () => BranchRowRule.Resolve(Schema("text", "number"), same, Shapes));

            Assert.Contains("行を分ける引数が1つに決まらない", refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TwoBranchesThatSelectTheSameValueAreRefused()
        {
            Assert.Throws<InvalidOperationException>(
                () => BranchRowRule.Resolve(Schema("text", "text"), Rows(), Shapes));
        }

        private static IList<SignatureRecord> Rows()
        {
            return new[]
            {
                Signature(TextKey, "System.String"),
                Signature(NumberKey, "System.Int32"),
            };
        }

        private static SignatureRecord Signature(string key, string dataType)
        {
            return new SignatureRecord(
                key,
                "Sdk.Form",
                MemberKind.Method,
                "Share",
                false,
                0,
                new[]
                {
                    new ParameterRecord("key", "System.String", ParameterDirection.In, false, false),
                    new ParameterRecord("data", dataType, ParameterDirection.In, false, false),
                },
                "System.Void",
                true,
                false,
                OperationDirection.Read);
        }

        /// <summary>選ぶ値を2つ取る題材。null を渡すとその呼び分けは選ぶ項目を持たない。</summary>
        private static ToolSchema Schema(string first, string second)
        {
            return new ToolSchema(
                Tool,
                new[] { Branch("first", first), Branch("second", second) },
                Item(null, null),
                null);
        }

        private static SchemaBranch Branch(string name, string value)
        {
            return new SchemaBranch(
                name,
                value == null ? null : "dataShape",
                value,
                Inputs(value != null),
                new SchemaChoice[0]);
        }

        private static IList<SchemaItem> Inputs(bool selecting)
        {
            List<SchemaItem> inputs = new List<SchemaItem> { Item("key", null), Item("data", null) };
            if (selecting)
            {
                inputs.Add(Item("dataShape", "text"));
            }

            return inputs;
        }

        private static SchemaItem Item(string name, string shape)
        {
            return new SchemaItem(
                shape,
                null,
                null,
                name,
                ItemOrigin.HostInput,
                name == null ? (bool?)null : true,
                null,
                false,
                null,
                null,
                null,
                false,
                null);
        }
    }
}
