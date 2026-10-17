using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class PropertyNameGateTests
    {
        private static readonly PropertyRecord Size =
            new PropertyRecord("N.IThing", "Size", "System.Int32");

        private static readonly PropertyRecord Weight =
            new PropertyRecord("N.IThing", "Weight", "System.Single");

        [Fact]
        public void ATableThatCarriesOnlyTheAuthoredItemsPasses()
        {
            Require(
                new[] { Authored(Weight, "重さ") },
                new[] { Size, Weight },
                Notes("N.IThing.Size", "大きさ"));
        }

        [Fact]
        public void AnItemWhoseNoteCanBeQuotedIsRefusedInTheTable()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    new[] { Authored(Size, "大きさ"), Authored(Weight, "重さ") },
                    new[] { Size, Weight },
                    Notes("N.IThing.Size", "大きさ")));

            Assert.Contains("記載を引ける項目は表に置かない", error.Message, StringComparison.Ordinal);
            Assert.Contains("Size", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnItemWithoutAQuotableNoteMissingFromTheTableStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    new PropertyNameRecord[0],
                    new[] { Size, Weight },
                    Notes("N.IThing.Size", "大きさ")));

            Assert.Contains("名前を起こす項目が表に無い", error.Message, StringComparison.Ordinal);
            Assert.Contains("Weight", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnItemThatIsNotInTheEnumerationStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    new[] { Authored(Weight, "重さ") },
                    new[] { Size },
                    Notes("N.IThing.Size", "大きさ")));

            Assert.Contains("列挙結果に無い項目が在る", error.Message, StringComparison.Ordinal);
            Assert.Contains("Weight", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheSameItemListedTwiceStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    new[] { Authored(Weight, "重さ"), Authored(Weight, "重量") },
                    new[] { Size, Weight },
                    Notes("N.IThing.Size", "大きさ")));

            Assert.Contains("表に同じ項目が二度在る", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TwoItemsSharingANoteAreBothAuthored()
        {
            Require(
                new[] { Authored(Size, "大きさ"), Authored(Weight, "重さ") },
                new[] { Size, Weight },
                Notes("N.IThing.Size", "同じ記載", "N.IThing.Weight", "同じ記載"));
        }

        [Fact]
        public void TheSameNoteInAnotherTypeDoesNotForceAuthoring()
        {
            PropertyRecord other = new PropertyRecord("N.IOther", "Size", "System.Int32");

            Require(
                new[] { Authored(Weight, "重さ") },
                new[] { Size, Weight, other },
                Notes("N.IThing.Size", "同じ記載", "N.IOther.Size", "同じ記載"));
        }

        [Fact]
        public void AnAuthoredNameThatRepeatsADerivedNameStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    new[] { Authored(Weight, "大きさ") },
                    new[] { Size, Weight },
                    Notes("N.IThing.Size", "大きさ")));

            Assert.Contains("同じ型の中で日本語名が重なる", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ADocumentSectionEndingOnTheLastLinePasses()
        {
            PropertyNameGate.Require(
                new[] { Document(Weight, 3, 4) },
                new[] { Size, Weight },
                Notes("N.IThing.Size", "大きさ"),
                path => 4);
        }

        [Fact]
        public void ADocumentSectionEndingOneLinePastTheFileStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => PropertyNameGate.Require(
                    new[] { Document(Weight, 3, 5) },
                    new[] { Size, Weight },
                    Notes("N.IThing.Size", "大きさ"),
                    path => 4));

            Assert.Contains("根拠の行が資料の行数を超える", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ADocumentSectionThatNamesAMissingFileStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => PropertyNameGate.Require(
                    new[] { Document(Weight, 1, 1) },
                    new[] { Size, Weight },
                    Notes("N.IThing.Size", "大きさ"),
                    path => -1));

            Assert.Contains("根拠の資料が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            IList<PropertyNameRecord> records = new[] { Authored(Weight, "重さ") };
            IList<PropertyRecord> properties = new[] { Size, Weight };
            IDictionary<string, string> notes = Notes("N.IThing.Size", "大きさ");

            Assert.Throws<ArgumentNullException>(
                () => PropertyNameGate.Require(null, properties, notes, path => 1));
            Assert.Throws<ArgumentNullException>(
                () => PropertyNameGate.Require(records, null, notes, path => 1));
            Assert.Throws<ArgumentNullException>(
                () => PropertyNameGate.Require(records, properties, null, path => 1));
            Assert.Throws<ArgumentNullException>(
                () => PropertyNameGate.Require(records, properties, notes, null));
        }

        private static void Require(
            IEnumerable<PropertyNameRecord> records,
            IEnumerable<PropertyRecord> properties,
            IDictionary<string, string> notes)
        {
            PropertyNameGate.Require(records, properties, notes, path => 1);
        }

        private static PropertyNameRecord Authored(PropertyRecord property, string japaneseName)
        {
            return new PropertyNameRecord(
                property.DeclaringType,
                property.MemberName,
                japaneseName,
                NameBasis.FromMemberShape(),
                "メンバー名から起こした。");
        }

        private static PropertyNameRecord Document(
            PropertyRecord property, int firstLine, int lastLine)
        {
            return new PropertyNameRecord(
                property.DeclaringType,
                property.MemberName,
                "重さ",
                NameBasis.FromDocumentSection("Lib/資料.txt", firstLine, lastLine),
                "資料の該当箇所を日本語へ移した。");
        }

        private static IDictionary<string, string> Notes(params string[] pairs)
        {
            Dictionary<string, string> notes = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < pairs.Length; i += 2)
            {
                notes.Add(pairs[i], pairs[i + 1]);
            }

            return notes;
        }
    }
}
