using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class TypeRoleJsonReaderTests
    {
        private const string Quoted =
            "{\"declaringType\":\"N.IThing\",\"memberName\":\"Size\","
            + "\"propertyType\":\"System.Int32\",\"japaneseName\":\"大きさ\",\"decision\":\"quoted\"}";

        private const string Authored =
            "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\","
            + "\"propertyType\":\"System.Single\",\"japaneseName\":\"重さ\",\"decision\":\"authored\","
            + "\"basis\":{\"kind\":\"memberShape\"},\"origin\":\"メンバー名から起こした。\"}";

        [Fact]
        public void AQuotedRecordIsReadWithoutABasis()
        {
            PropertyNameRecord record = Assert.Single(Read(Quoted));

            Assert.Equal("N.IThing", record.Property.DeclaringType);
            Assert.Equal("Size", record.Property.MemberName);
            Assert.Equal("System.Int32", record.Property.PropertyType);
            Assert.Equal("大きさ", record.JapaneseName);
            Assert.Equal(NameDecision.Quoted, record.Decision);
            Assert.Null(record.Basis);
            Assert.Equal(string.Empty, record.Origin);
        }

        [Fact]
        public void AnAuthoredRecordCarriesItsBasisAndOrigin()
        {
            PropertyNameRecord record = Assert.Single(Read(Authored));

            Assert.Equal(NameDecision.Authored, record.Decision);
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
            IList<PropertyNameRecord> records = Read(Quoted + "," + Authored);

            Assert.Equal(new[] { "Size", "Weight" }, records.Select(r => r.Property.MemberName));
        }

        [Fact]
        public void ItemsOutOfOrdinalOrderStop()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(Authored + "," + Quoted));

            Assert.Contains("昇順", error.Message);
        }

        [Fact]
        public void TheSameItemTwiceStops()
        {
            FormatException error = Assert.Throws<FormatException>(() => Read(Quoted + "," + Quoted));

            Assert.Contains("二度", error.Message);
        }

        [Fact]
        public void TheSameMemberWithAnotherPropertyTypeStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(
                    Quoted
                    + ",{\"declaringType\":\"N.IThing\",\"memberName\":\"Size\","
                    + "\"propertyType\":\"System.Int64\",\"japaneseName\":\"寸法\","
                    + "\"decision\":\"quoted\"}"));

            Assert.Contains("二度", error.Message);
        }

        [Fact]
        public void AMemberNameThatIsAPrefixOfAnotherComesFirst()
        {
            IList<PropertyNameRecord> records = Read(Named("UV") + "," + Named("UVA1"));

            Assert.Equal(new[] { "UV", "UVA1" }, records.Select(r => r.Property.MemberName));
        }

        [Fact]
        public void AQuotedRecordWithABasisStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(
                    "{\"declaringType\":\"N.IThing\",\"memberName\":\"Size\","
                    + "\"propertyType\":\"System.Int32\",\"japaneseName\":\"大きさ\","
                    + "\"decision\":\"quoted\",\"basis\":{\"kind\":\"memberShape\"}}"));

            Assert.Contains("basis", error.Message);
        }

        [Fact]
        public void AQuotedRecordWithAnOriginStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(
                    "{\"declaringType\":\"N.IThing\",\"memberName\":\"Size\","
                    + "\"propertyType\":\"System.Int32\",\"japaneseName\":\"大きさ\","
                    + "\"decision\":\"quoted\",\"origin\":\"起こした。\"}"));

            Assert.Contains("origin", error.Message);
        }

        [Fact]
        public void AnAuthoredRecordWithoutAnOriginStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(
                    "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\","
                    + "\"propertyType\":\"System.Single\",\"japaneseName\":\"重さ\","
                    + "\"decision\":\"authored\",\"basis\":{\"kind\":\"memberShape\"}}"));

            Assert.Contains("origin", error.Message);
        }

        [Fact]
        public void AnAuthoredRecordWithoutABasisStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(
                    "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\","
                    + "\"propertyType\":\"System.Single\",\"japaneseName\":\"重さ\","
                    + "\"decision\":\"authored\",\"origin\":\"起こした。\"}"));

            Assert.Contains("basis", error.Message);
        }

        [Fact]
        public void AnUnknownDecisionStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(
                    "{\"declaringType\":\"N.IThing\",\"memberName\":\"Size\","
                    + "\"propertyType\":\"System.Int32\",\"japaneseName\":\"大きさ\","
                    + "\"decision\":\"derived\"}"));

            Assert.Contains("derived", error.Message);
        }

        [Fact]
        public void AnUnknownBasisKindStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(
                    "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\","
                    + "\"propertyType\":\"System.Single\",\"japaneseName\":\"重さ\","
                    + "\"decision\":\"authored\",\"basis\":{\"kind\":\"guess\"},"
                    + "\"origin\":\"起こした。\"}"));

            Assert.Contains("guess", error.Message);
        }

        [Fact]
        public void ADocumentSectionWhoseLastLineComesFirstStops()
        {
            Assert.Throws<FormatException>(() => Read(DocumentSection(14, 12)));
        }

        [Fact]
        public void ALineThatIsNotAWholeNumberStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(
                    "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\","
                    + "\"propertyType\":\"System.Single\",\"japaneseName\":\"重さ\","
                    + "\"decision\":\"authored\",\"basis\":{\"kind\":\"documentSection\","
                    + "\"path\":\"doc/spec.txt\",\"firstLine\":\"12\",\"lastLine\":14},"
                    + "\"origin\":\"資料の説明を移した。\"}"));

            Assert.Contains("firstLine", error.Message);
        }

        [Fact]
        public void AnUnknownMemberStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(
                    "{\"declaringType\":\"N.IThing\",\"memberName\":\"Size\","
                    + "\"propertyType\":\"System.Int32\",\"japaneseName\":\"大きさ\","
                    + "\"decision\":\"quoted\",\"note\":\"大きさ\"}"));

            Assert.Contains("note", error.Message);
        }

        [Fact]
        public void AnEmptyTableIsRead()
        {
            Assert.Empty(TypeRoleJsonReader.ReadPropertyNames("{\"propertyNames\":[]}"));
        }

        [Fact]
        public void ARootWithoutTheTableStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleJsonReader.ReadPropertyNames("{}"));

            Assert.Contains("propertyNames", error.Message);
        }

        [Fact]
        public void ARootWithAnUnknownMemberStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleJsonReader.ReadPropertyNames("{\"propertyNames\":[],\"types\":[]}"));

            Assert.Contains("types", error.Message);
        }

        [Fact]
        public void ATableThatIsNotAnArrayStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleJsonReader.ReadPropertyNames("{\"propertyNames\":{}}"));

            Assert.Contains("propertyNames", error.Message);
        }

        [Fact]
        public void AnItemThatIsNotAnObjectStops()
        {
            Assert.Throws<FormatException>(
                () => TypeRoleJsonReader.ReadPropertyNames("{\"propertyNames\":[\"大きさ\"]}"));
        }

        [Fact]
        public void TextThatIsNotJsonStops()
        {
            Assert.Throws<FormatException>(() => TypeRoleJsonReader.ReadPropertyNames("大きさ"));
        }

        [Fact]
        public void NullArgumentThrows()
        {
            Assert.Throws<ArgumentNullException>(() => TypeRoleJsonReader.ReadPropertyNames(null));
        }

        private static string DocumentSection(int firstLine, int lastLine)
        {
            return "{\"declaringType\":\"N.IThing\",\"memberName\":\"Weight\","
                + "\"propertyType\":\"System.Single\",\"japaneseName\":\"重さ\","
                + "\"decision\":\"authored\",\"basis\":{\"kind\":\"documentSection\","
                + "\"path\":\"doc/spec.txt\",\"firstLine\":" + firstLine
                + ",\"lastLine\":" + lastLine + "},\"origin\":\"資料の説明を移した。\"}";
        }

        private static string Named(string memberName)
        {
            return "{\"declaringType\":\"N.IThing\",\"memberName\":\"" + memberName + "\","
                + "\"propertyType\":\"System.Int32\",\"japaneseName\":\"" + memberName + "の値\","
                + "\"decision\":\"quoted\"}";
        }

        private static IList<PropertyNameRecord> Read(string items)
        {
            return TypeRoleJsonReader.ReadPropertyNames("{\"propertyNames\":[" + items + "]}");
        }
    }
}
