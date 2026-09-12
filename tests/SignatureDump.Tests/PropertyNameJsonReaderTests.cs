using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class PropertyNameJsonReaderTests
    {
        private const string Shape =
            "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\",\"japaneseName\":\"重さ\","
            + "\"basis\":{\"kind\":\"memberShape\"},\"origin\":\"メンバー名から起こした。\"}";

        [Fact]
        public void ARecordCarriesItsNameBasisAndOrigin()
        {
            PropertyNameRecord record = Assert.Single(Read(Shape));

            Assert.Equal("N.IThing", record.DeclaringType);
            Assert.Equal("Weight", record.MemberName);
            Assert.Equal("N.IThing|Weight", record.Key);
            Assert.Equal("重さ", record.JapaneseName);
            Assert.Equal(NameBasisKind.MemberShape, record.Basis.Kind);
            Assert.Equal("メンバー名から起こした。", record.Origin);
        }

        [Fact]
        public void ADocumentSectionBasisCarriesItsPathAndLines()
        {
            PropertyNameRecord record = Assert.Single(Read(DocumentSection(12, 14)));

            Assert.Equal(NameBasisKind.DocumentSection, record.Basis.Kind);
            Assert.Equal("doc/spec.txt", record.Basis.Path);
            Assert.Equal(12, record.Basis.FirstLine);
            Assert.Equal(14, record.Basis.LastLine);
        }

        [Fact]
        public void ADocumentSectionOfOneLineIsRead()
        {
            PropertyNameRecord record = Assert.Single(Read(DocumentSection(12, 12)));

            Assert.Equal(12, record.Basis.FirstLine);
            Assert.Equal(12, record.Basis.LastLine);
        }

        [Fact]
        public void RecordsAreReturnedInTheWrittenOrder()
        {
            IList<PropertyNameRecord> records = Read(Named("Size") + "," + Shape);

            Assert.Equal(new[] { "Size", "Weight" }, records.Select(r => r.MemberName));
        }

        [Fact]
        public void ItemsOutOfOrdinalOrderStop()
        {
            Assert.Throws<FormatException>(() => Read(Shape + "," + Named("Size")));
        }

        [Fact]
        public void TheSameItemTwiceStops()
        {
            Assert.Throws<FormatException>(() => Read(Shape + "," + Shape));
        }

        [Fact]
        public void AMemberNameThatIsAPrefixOfAnotherComesFirst()
        {
            IList<PropertyNameRecord> records = Read(Named("UV") + "," + Named("UVA1"));

            Assert.Equal(new[] { "UV", "UVA1" }, records.Select(r => r.MemberName));
        }

        [Fact]
        public void ADeclaringTypeThatIsAPrefixOfAnotherComesFirst()
        {
            IList<PropertyNameRecord> records = Read(
                Item("N.IThing", "Zeta") + "," + Item("N.IThingMore", "Alpha"));

            Assert.Equal(
                new[] { "N.IThing", "N.IThingMore" }, records.Select(r => r.DeclaringType));
        }

        [Fact]
        public void AWrittenPropertyTypeStops()
        {
            Assert.Throws<FormatException>(() => Read(
                "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\","
                + "\"propertyType\":\"System.Single\",\"japaneseName\":\"重さ\","
                + "\"basis\":{\"kind\":\"memberShape\"},\"origin\":\"起こした。\"}"));
        }

        [Fact]
        public void AWrittenDecisionStops()
        {
            Assert.Throws<FormatException>(() => Read(
                "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\","
                + "\"japaneseName\":\"重さ\",\"decision\":\"authored\","
                + "\"basis\":{\"kind\":\"memberShape\"},\"origin\":\"起こした。\"}"));
        }

        [Fact]
        public void ARecordWithoutAnOriginStops()
        {
            Assert.Throws<FormatException>(() => Read(
                "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\","
                + "\"japaneseName\":\"重さ\",\"basis\":{\"kind\":\"memberShape\"}}"));
        }

        [Fact]
        public void ARecordWithoutABasisStops()
        {
            Assert.Throws<FormatException>(() => Read(
                "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\","
                + "\"japaneseName\":\"重さ\",\"origin\":\"起こした。\"}"));
        }

        [Fact]
        public void AnEmptyJapaneseNameStops()
        {
            Assert.Throws<FormatException>(() => Read(
                "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\",\"japaneseName\":\"\","
                + "\"basis\":{\"kind\":\"memberShape\"},\"origin\":\"起こした。\"}"));
        }

        [Fact]
        public void AnUnknownBasisKindStops()
        {
            Assert.Throws<FormatException>(() => Read(
                "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\","
                + "\"japaneseName\":\"重さ\",\"basis\":{\"kind\":\"guess\"},"
                + "\"origin\":\"起こした。\"}"));
        }

        [Fact]
        public void ADocumentSectionWhoseLastLineComesFirstStops()
        {
            Assert.Throws<FormatException>(() => Read(DocumentSection(14, 12)));
        }

        [Fact]
        public void ALineThatIsNotAWholeNumberStops()
        {
            Assert.Throws<FormatException>(() => Read(
                "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\","
                + "\"japaneseName\":\"重さ\",\"basis\":{\"kind\":\"documentSection\","
                + "\"path\":\"doc/spec.txt\",\"firstLine\":1.5,\"lastLine\":2},"
                + "\"origin\":\"起こした。\"}"));
        }

        [Fact]
        public void AnUnknownMemberStops()
        {
            Assert.Throws<FormatException>(() => Read(
                "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\","
                + "\"japaneseName\":\"重さ\",\"basis\":{\"kind\":\"memberShape\"},"
                + "\"origin\":\"起こした。\",\"note\":\"重さ\"}"));
        }

        [Fact]
        public void AnEmptyTableIsRead()
        {
            Assert.Empty(PropertyNameJsonReader.ReadPropertyNames("{\"propertyNames\":[]}"));
        }

        [Fact]
        public void ARootWithoutTheTableStops()
        {
            Assert.Throws<FormatException>(() => PropertyNameJsonReader.ReadPropertyNames("{}"));
        }

        [Fact]
        public void ARootWithAnUnknownMemberStops()
        {
            Assert.Throws<FormatException>(() => PropertyNameJsonReader.ReadPropertyNames(
                "{\"propertyNames\":[],\"note\":\"\"}"));
        }

        [Fact]
        public void ATableThatIsNotAnArrayStops()
        {
            Assert.Throws<FormatException>(
                () => PropertyNameJsonReader.ReadPropertyNames("{\"propertyNames\":{}}"));
        }

        [Fact]
        public void AnItemThatIsNotAnObjectStops()
        {
            Assert.Throws<FormatException>(() => Read("\"N.IThing\""));
        }

        [Fact]
        public void TextThatIsNotJsonStops()
        {
            Assert.Throws<FormatException>(() => PropertyNameJsonReader.ReadPropertyNames("大きさ"));
        }

        [Fact]
        public void NullArgumentThrows()
        {
            Assert.Throws<ArgumentNullException>(() => PropertyNameJsonReader.ReadPropertyNames(null));
        }

        private static string DocumentSection(int firstLine, int lastLine)
        {
            return "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\","
                + "\"japaneseName\":\"重さ\",\"basis\":{\"kind\":\"documentSection\","
                + "\"path\":\"doc/spec.txt\",\"firstLine\":" + firstLine
                + ",\"lastLine\":" + lastLine + "},\"origin\":\"資料の説明を移した。\"}";
        }

        private static string Named(string memberName)
        {
            return Item("N.IThing", memberName);
        }

        private static string Item(string declaringType, string memberName)
        {
            return "{\"declaringType\":\"" + declaringType + "\",\"memberName\":\"" + memberName
                + "\",\"japaneseName\":\"" + memberName + "の値\","
                + "\"basis\":{\"kind\":\"memberShape\"},\"origin\":\"起こした。\"}";
        }

        private static IList<PropertyNameRecord> Read(string items)
        {
            return PropertyNameJsonReader.ReadPropertyNames("{\"propertyNames\":[" + items + "]}");
        }
    }
}
