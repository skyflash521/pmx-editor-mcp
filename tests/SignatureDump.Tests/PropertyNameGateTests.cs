using System;
using System.Collections.Generic;
using System.Linq;
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
        public void ATableThatMatchesTheRulePasses()
        {
            Require(
                new[]
                {
                    PropertyNameRecord.FromQuoted(Size, "大きさ"),
                    Authored(Weight, "重さ"),
                },
                new[] { Size, Weight },
                Notes("N.IThing.Size", "大きさ"));
        }

        [Fact]
        public void APropertyMissingFromTheTableStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    new[] { PropertyNameRecord.FromQuoted(Size, "大きさ") },
                    new[] { Size, Weight },
                    Notes("N.IThing.Size", "大きさ")));

            Assert.Contains("Weight", error.Message);
        }

        [Fact]
        public void APropertyThatIsNotInTheEnumerationStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    new[]
                    {
                        PropertyNameRecord.FromQuoted(Size, "大きさ"),
                        Authored(Weight, "重さ"),
                    },
                    new[] { Size },
                    Notes("N.IThing.Size", "大きさ")));

            Assert.Contains("Weight", error.Message);
        }

        [Fact]
        public void APropertyTypeThatDiffersFromTheEnumerationStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    new[] { PropertyNameRecord.FromQuoted(Size, "大きさ") },
                    new[] { new PropertyRecord("N.IThing", "Size", "System.Int64") },
                    Notes("N.IThing.Size", "大きさ")));

            Assert.Contains("Size", error.Message);
        }

        [Fact]
        public void TheSameItemListedTwiceStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    new[]
                    {
                        PropertyNameRecord.FromQuoted(Size, "大きさ"),
                        PropertyNameRecord.FromQuoted(Size, "寸法"),
                    },
                    new[] { Size },
                    Notes("N.IThing.Size", "大きさ")));

            Assert.Contains("二度", error.Message);
        }

        [Fact]
        public void AUniquelyNotedPropertyThatIsAuthoredStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    new[] { Authored(Size, "寸法") },
                    new[] { Size },
                    Notes("N.IThing.Size", "大きさ")));

            Assert.Contains("出現数", error.Message);
        }

        [Fact]
        public void APropertyWithoutANoteThatIsQuotedStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    new[] { PropertyNameRecord.FromQuoted(Size, "大きさ") },
                    new[] { Size },
                    Notes()));

            Assert.Contains("出現数", error.Message);
        }

        [Fact]
        public void OneOfTwoPropertiesSharingANoteCannotStayQuoted()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    new[]
                    {
                        PropertyNameRecord.FromQuoted(Size, "大きさ"),
                        Authored(Weight, "重さ"),
                    },
                    new[] { Size, Weight },
                    Notes("N.IThing.Size", "大きさ", "N.IThing.Weight", "大きさ")));

            Assert.Contains("出現数", error.Message);
        }

        [Fact]
        public void TheSameNoteInAnotherTypeDoesNotForceAuthoring()
        {
            PropertyRecord other = new PropertyRecord("N.IOther", "Size", "System.Int32");

            Require(
                new[]
                {
                    PropertyNameRecord.FromQuoted(other, "大きさ"),
                    PropertyNameRecord.FromQuoted(Size, "大きさ"),
                    Authored(Weight, "重さ"),
                },
                new[] { Size, Weight, other },
                Notes("N.IThing.Size", "大きさ", "N.IOther.Size", "大きさ"));
        }

        [Fact]
        public void TwoPropertiesSharingANoteMayBothBeAuthored()
        {
            Require(
                new[] { Authored(Size, "大きさ"), Authored(Weight, "重さ") },
                new[] { Size, Weight },
                Notes("N.IThing.Size", "寸法", "N.IThing.Weight", "寸法"));
        }

        [Fact]
        public void ADocumentSectionEndingOnTheLastLinePasses()
        {
            PropertyNameGate.Require(
                new[]
                {
                    PropertyNameRecord.FromQuoted(Size, "大きさ"),
                    PropertyNameRecord.FromAuthored(
                        Weight,
                        "重さ",
                        NameBasis.FromDocumentSection("doc/spec.txt", 5, 5),
                        "資料の説明を移した。"),
                },
                new[] { Size, Weight },
                Notes("N.IThing.Size", "大きさ"),
                path => 5);
        }

        [Fact]
        public void ADocumentSectionEndingOneLinePastTheFileStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => PropertyNameGate.Require(
                    new[]
                    {
                        PropertyNameRecord.FromQuoted(Size, "大きさ"),
                        PropertyNameRecord.FromAuthored(
                            Weight,
                            "重さ",
                            NameBasis.FromDocumentSection("doc/spec.txt", 5, 6),
                            "資料の説明を移した。"),
                    },
                    new[] { Size, Weight },
                    Notes("N.IThing.Size", "大きさ"),
                    path => 5));

            Assert.Contains("行数を超える", error.Message);
        }

        [Fact]
        public void AQuotedNameThatDiffersFromTheNoteStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    new[] { PropertyNameRecord.FromQuoted(Size, "寸法") },
                    new[] { Size },
                    Notes("N.IThing.Size", "大きさ")));

            Assert.Contains("記載と違う", error.Message);
        }

        [Fact]
        public void ADocumentSectionThatNamesAMissingFileStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => PropertyNameGate.Require(
                    new[]
                    {
                        PropertyNameRecord.FromQuoted(Size, "大きさ"),
                        PropertyNameRecord.FromAuthored(
                            Weight,
                            "重さ",
                            NameBasis.FromDocumentSection("doc/spec.txt", 1, 2),
                            "資料の説明を移した。"),
                    },
                    new[] { Size, Weight },
                    Notes("N.IThing.Size", "大きさ"),
                    path => -1));

            Assert.Contains("doc/spec.txt", error.Message);
        }

        [Fact]
        public void ADocumentSectionBeyondTheEndOfTheFileStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => PropertyNameGate.Require(
                    new[]
                    {
                        PropertyNameRecord.FromQuoted(Size, "大きさ"),
                        PropertyNameRecord.FromAuthored(
                            Weight,
                            "重さ",
                            NameBasis.FromDocumentSection("doc/spec.txt", 4, 9),
                            "資料の説明を移した。"),
                    },
                    new[] { Size, Weight },
                    Notes("N.IThing.Size", "大きさ"),
                    path => 5));

            Assert.Contains("行数を超える", error.Message);
        }

        [Fact]
        public void ADocumentSectionInsideTheFilePasses()
        {
            PropertyNameGate.Require(
                new[]
                {
                    PropertyNameRecord.FromQuoted(Size, "大きさ"),
                    PropertyNameRecord.FromAuthored(
                        Weight,
                        "重さ",
                        NameBasis.FromDocumentSection("doc/spec.txt", 4, 5),
                        "資料の説明を移した。"),
                },
                new[] { Size, Weight },
                Notes("N.IThing.Size", "大きさ"),
                path => 5);
        }

        [Fact]
        public void TwoItemsOfOneTypeSharingAJapaneseNameStop()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    new[]
                    {
                        PropertyNameRecord.FromQuoted(Size, "大きさ"),
                        Authored(Weight, "大きさ"),
                    },
                    new[] { Size, Weight },
                    Notes("N.IThing.Size", "大きさ")));

            Assert.Contains("重なる", error.Message);
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            IList<PropertyNameRecord> records = new[] { PropertyNameRecord.FromQuoted(Size, "大きさ") };
            IList<PropertyRecord> properties = new[] { Size };
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
            return PropertyNameRecord.FromAuthored(
                property, japaneseName, NameBasis.FromMemberShape(), "メンバー名から起こした。");
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
