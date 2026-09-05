using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class SampleValueGateTests
    {
        private const string Enum = "N.Kind";

        private const string SingleEnum = "N.Single";

        [Fact]
        public void ATableThatCoversTheTypesPasses()
        {
            Accepts(Rows());
        }

        [Fact]
        public void ATypeWithoutASampleStops()
        {
            Rejects("サンプル値を持たない型", Rows().Where(r => r.TypeName != "System.Int32").ToList());
        }

        [Fact]
        public void ASampleForATypeThatIsNotValueMappedStops()
        {
            List<SampleValueRow> rows = Rows();
            rows.Add(new SampleValueRow("N.Other", 1, 2));

            Rejects("値を写す型でないもの", rows);
        }

        [Fact]
        public void TheSameTypeTwiceStops()
        {
            List<SampleValueRow> rows = Rows();
            rows.Add(new SampleValueRow("System.Int32", 3, 4));

            Rejects("同じ型が二度現れる", rows);
        }

        [Fact]
        public void TwoSamplesThatAreTheSameStop()
        {
            Rejects("2件のサンプル値が同じ", Replaced("System.Int32", 1, 1));
        }

        [Fact]
        public void TwoSamplesThatOnlyLookAlikeAreTold()
        {
            Accepts(Replaced("System.Object", new object[] { 1 }, new object[] { 1, 2 }));
        }

        [Fact]
        public void ANumberThatIsNotANumberStops()
        {
            Rejects("数値でなければならない", Replaced("System.Int32", "1", 2));
        }

        [Fact]
        public void ATextThatIsNotAStringStops()
        {
            Rejects("文字列でなければならない", Replaced("System.String", 1, "b"));
        }

        [Fact]
        public void ABooleanThatIsNotABooleanStops()
        {
            Rejects("真偽でなければならない", Replaced("System.Boolean", "true", false));
        }

        [Fact]
        public void AnEnumNameThatTheEnumDoesNotCarryStops()
        {
            Rejects("無い列挙子", Replaced(Enum, "Gone", "Second"));
        }

        [Fact]
        public void AnEnumNameThatIsNotAStringStops()
        {
            Rejects("列挙子の名前", Replaced(Enum, 1, "Second"));
        }

        [Fact]
        public void AnEnumThatDoesNotCombineRejectsJoinedNames()
        {
            Rejects(
                "組み合わせを許さない列挙", Replaced(SingleEnum, "Alone, Apart", "Apart"));
        }

        [Fact]
        public void AnEnumThatCombinesTakesJoinedNames()
        {
            Accepts(Replaced(Enum, "First, Second", "Second"));
        }

        [Fact]
        public void AnEnumWhoseMembersCannotBeLookedUpStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => SampleValueGate.Require(
                    new SampleValueTable(Rows()),
                    Shapes(),
                    Components(),
                    new Dictionary<string, EnumMemberSet>(StringComparer.Ordinal)));

            Assert.Contains("列挙子を引けない型", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ANumberArrayOfTheWrongLengthStops()
        {
            Rejects(
                "数値 3 個の並び",
                Replaced("PEPlugin.SDX.V3", new object[] { 1, 2 }, new object[] { 4, 5, 6 }));
        }

        [Fact]
        public void ANumberArrayWithANonNumberStops()
        {
            Rejects(
                "成分が数値でない",
                Replaced("PEPlugin.SDX.V3", new object[] { 1, 2, "3" }, new object[] { 4, 5, 6 }));
        }

        [Fact]
        public void AColorOutsideTheRangeStops()
        {
            Rejects(
                "0以上1以下でない",
                Replaced("System.Drawing.Color", new object[] { 2, 0, 0, 1 },
                    new object[] { 0, 1, 0, 1 }));
        }

        [Fact]
        public void AColorOfTheWrongNumberOfComponentsStops()
        {
            Rejects(
                "数値3個か4個",
                Replaced("System.Drawing.Color", new object[] { 1, 0 }, new object[] { 0, 1, 0, 1 }));
            Rejects(
                "数値3個か4個",
                Replaced(
                    "System.Drawing.Color",
                    new object[] { 1, 0, 0, 1, 1 },
                    new object[] { 0, 1, 0, 1 }));
        }

        [Fact]
        public void AColorOfThreeComponentsIsTaken()
        {
            Accepts(Replaced("System.Drawing.Color", new object[] { 1, 0, 0 },
                new object[] { 0, 1, 0, 1 }));
        }

        [Fact]
        public void APointOfTheWrongLengthStops()
        {
            Rejects(
                "数値 2 個の並び",
                Replaced("System.Drawing.Point", new object[] { 1 }, new object[] { 3, 4 }));
        }

        [Fact]
        public void ASizeOfTheWrongLengthStops()
        {
            Rejects(
                "数値 2 個の並び",
                Replaced("System.Drawing.Size", new object[] { 1, 2, 3 }, new object[] { 3, 4 }));
        }

        [Fact]
        public void ARectangleOfTheWrongLengthStops()
        {
            Rejects(
                "数値 4 個の並び",
                Replaced(
                    "System.Drawing.Rectangle", new object[] { 0, 0, 1 }, new object[] { 1, 1, 2, 2 }));
        }

        [Fact]
        public void AJsonSampleThatIsNothingStops()
        {
            Rejects("値を持たなければならない", Replaced("System.Object", null, "sample"));
        }

        [Fact]
        public void AVersionThatIsNotTheStandardNotationStops()
        {
            Rejects("版の表記でない", Replaced("System.Version", "いち", "2.0"));
        }

        [Fact]
        public void ANumberOutsideTheRangeOfItsTypeStops()
        {
            Rejects("その型の範囲を超えている", Replaced("System.Byte", 256, 2));
        }

        [Fact]
        public void ANumberWithAFractionStops()
        {
            Rejects("整数でない", Replaced("System.Int32", 1.5, 2));
        }

        [Fact]
        public void ASinglePrecisionNumberBeyondItsRangeStops()
        {
            Rejects("その型の範囲を超えている", Replaced("System.Single", 1e300, 1.5));
        }

        [Fact]
        public void ANumberTypeThatTheRuleDoesNotCoverStops()
        {
            List<SampleValueRow> rows = Rows();
            rows.Add(new SampleValueRow("System.Int16", 1, 2));
            List<ValueShapeRow> shapes = new List<ValueShapeRow>(Shapes());
            shapes.Add(new ValueShapeRow("System.Int16", "number"));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => SampleValueGate.Require(
                    new SampleValueTable(rows), shapes, Components(), EnumMembers()));

            Assert.Contains("持てる範囲を確かめられない型", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ANumberThatIsNotFiniteStops()
        {
            Rejects("有限でない", Replaced("System.Int32", double.NaN, 2));
        }

        [Fact]
        public void AFontStyleThatTheEnumDoesNotCarryStops()
        {
            Rejects(
                "無い列挙子",
                Replaced("System.Drawing.Font", Font("MS Gothic", 9, "Gone"), Font("Meiryo", 12, "Bold")));
        }

        [Fact]
        public void AFontStyleThatCombinesMembersIsTaken()
        {
            Accepts(Replaced(
                "System.Drawing.Font",
                Font("MS Gothic", 9, "Bold, Italic"),
                Font("Meiryo", 12, "Regular")));
        }

        [Fact]
        public void AFontSizeThatIsNotPositiveStops()
        {
            Rejects(
                "0より大きくない",
                Replaced("System.Drawing.Font", Font("MS Gothic", 0, "Regular"), Font("Meiryo", 12, "Bold")));
        }

        [Fact]
        public void AFontMissingAnItemStops()
        {
            Rejects(
                "family と size と style",
                Replaced("System.Drawing.Font", Font("MS Gothic", 9, null), Font("Meiryo", 12, "Bold")));
        }

        [Fact]
        public void AFontWhoseSizeIsNotANumberStops()
        {
            Rejects(
                "size が数値でない",
                Replaced("System.Drawing.Font", Font("MS Gothic", "9", "Regular"),
                    Font("Meiryo", 12, "Bold")));
        }

        [Fact]
        public void AnImageThatIsNotBase64Stops()
        {
            Rejects("Base64として読めない", Replaced("System.Drawing.Bitmap", "!!", "aGk="));
        }

        [Fact]
        public void AnEmptyImageStops()
        {
            Rejects("が空である", Replaced("System.Drawing.Bitmap", string.Empty, "aGk="));
        }

        [Fact]
        public void AComponentCountForATypeThatIsNotAnArrayStops()
        {
            Dictionary<string, int> components = new Dictionary<string, int>(Components());
            components["System.Int32"] = 2;

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => SampleValueGate.Require(
                    new SampleValueTable(Rows()), Shapes(), components, EnumMembers()));

            Assert.Contains("成分を並べない型", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnArrayTypeWithoutAComponentCountStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => SampleValueGate.Require(
                    new SampleValueTable(Rows()),
                    Shapes(),
                    new Dictionary<string, int>(StringComparer.Ordinal),
                    EnumMembers()));

            Assert.Contains("成分の数を持たない型", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(
                () => SampleValueGate.Require(null, Shapes(), Components(), EnumMembers()));
            Assert.Throws<ArgumentNullException>(
                () => SampleValueGate.Require(
                    new SampleValueTable(Rows()), null, Components(), EnumMembers()));
            Assert.Throws<ArgumentNullException>(
                () => SampleValueGate.Require(
                    new SampleValueTable(Rows()), Shapes(), null, EnumMembers()));
            Assert.Throws<ArgumentNullException>(
                () => SampleValueGate.Require(
                    new SampleValueTable(Rows()), Shapes(), Components(), null));
        }

        private static void Accepts(IList<SampleValueRow> rows)
        {
            SampleValueGate.Require(
                new SampleValueTable(rows), Shapes(), Components(), EnumMembers());
        }

        private static void Rejects(string fragment, IList<SampleValueRow> rows)
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Accepts(rows));

            Assert.Contains(fragment, error.Message, StringComparison.Ordinal);
        }

        private static List<SampleValueRow> Replaced(string typeName, object first, object second)
        {
            List<SampleValueRow> rows = Rows();
            for (int index = 0; index < rows.Count; index++)
            {
                if (string.Equals(rows[index].TypeName, typeName, StringComparison.Ordinal))
                {
                    rows[index] = new SampleValueRow(typeName, first, second);
                    return rows;
                }
            }

            throw new InvalidOperationException("題材に無い型: " + typeName);
        }

        private static List<SampleValueRow> Rows()
        {
            return new List<SampleValueRow>
            {
                new SampleValueRow(Enum, "First", "Second"),
                new SampleValueRow(SingleEnum, "Alone", "Apart"),
                new SampleValueRow("PEPlugin.SDX.V3", new object[] { 1, 2, 3 }, new object[] { 4, 5, 6 }),
                new SampleValueRow("System.Boolean", true, false),
                new SampleValueRow("System.Byte", 1, 2),
                new SampleValueRow("System.Single", 0.5, 1.5),
                new SampleValueRow("System.Drawing.Bitmap", "aGk=", "aGk1"),
                new SampleValueRow(
                    "System.Drawing.Color", new object[] { 1, 0, 0, 1 }, new object[] { 0, 1, 0, 1 }),
                new SampleValueRow(
                    "System.Drawing.Font", Font("MS Gothic", 9, "Regular"), Font("Meiryo", 12, "Bold")),
                new SampleValueRow("System.Int32", 1, 2),
                new SampleValueRow("System.Drawing.Point", new object[] { 1, 2 }, new object[] { 3, 4 }),
                new SampleValueRow(
                    "System.Drawing.Rectangle", new object[] { 0, 0, 1, 1 }, new object[] { 1, 1, 2, 2 }),
                new SampleValueRow("System.Drawing.Size", new object[] { 1, 2 }, new object[] { 3, 4 }),
                new SampleValueRow("System.Object", new object[] { 1 }, "sample"),
                new SampleValueRow("System.String", "a", "b"),
                new SampleValueRow("System.Version", "1.0", "2.0"),
            };
        }

        private static Dictionary<string, object> Font(string family, object size, string style)
        {
            Dictionary<string, object> font =
                new Dictionary<string, object>(StringComparer.Ordinal) { { "family", family } };
            if (size != null)
            {
                font.Add("size", size);
            }

            if (style != null)
            {
                font.Add("style", style);
            }

            return font;
        }

        private static IList<ValueShapeRow> Shapes()
        {
            return new List<ValueShapeRow>
            {
                new ValueShapeRow(Enum, "enum_name"),
                new ValueShapeRow(SingleEnum, "enum_name"),
                new ValueShapeRow("PEPlugin.SDX.V3", "number_array"),
                new ValueShapeRow("System.Boolean", "boolean"),
                new ValueShapeRow("System.Byte", "number"),
                new ValueShapeRow("System.Single", "number"),
                new ValueShapeRow("System.Collections.Generic.IList<1>", null),
                new ValueShapeRow("System.Drawing.Bitmap", "image"),
                new ValueShapeRow("System.Drawing.Color", "color"),
                new ValueShapeRow("System.Drawing.Font", "font"),
                new ValueShapeRow("System.Int32", "number"),
                new ValueShapeRow("System.Drawing.Point", "point"),
                new ValueShapeRow("System.Drawing.Rectangle", "rectangle"),
                new ValueShapeRow("System.Drawing.Size", "size"),
                new ValueShapeRow("System.Object", "json"),
                new ValueShapeRow("System.String", "text"),
                new ValueShapeRow("System.Version", "text"),
                new ValueShapeRow("System.Void", "null_value"),
            };
        }

        private static IDictionary<string, int> Components()
        {
            return new Dictionary<string, int>(StringComparer.Ordinal) { { "PEPlugin.SDX.V3", 3 } };
        }

        private static IDictionary<string, EnumMemberSet> EnumMembers()
        {
            return new Dictionary<string, EnumMemberSet>(StringComparer.Ordinal)
            {
                { Enum, Members(true, "First", "Second") },
                { SingleEnum, Members(false, "Alone", "Apart") },
                { "System.Drawing.FontStyle", Members(true, "Regular", "Bold", "Italic") },
            };
        }

        private static EnumMemberSet Members(bool isCombinable, params string[] names)
        {
            return new EnumMemberSet(
                new HashSet<string>(names, StringComparer.Ordinal), isCombinable);
        }
    }
}
