using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class SampleValueJsonReaderTests
    {
        [Fact]
        public void TheRowsAreReadInOrder()
        {
            SampleValueTable table = SampleValueJsonReader.Read(
                "{\"types\":[{\"typeName\":\"System.Int32\",\"default\":1,\"second\":2}"
                + ",{\"typeName\":\"System.String\",\"default\":\"a\",\"second\":\"b\"}],\"rows\":[]}");

            Assert.Equal(2, table.Types.Count);
            Assert.Equal("System.Int32", table.Types[0].TypeName);
            Assert.Equal(1, table.Types[0].First);
            Assert.Equal(2, table.Types[0].Second);
            Assert.Equal("b", table.Types[1].Second);
        }

        [Fact]
        public void AnEmptyTableIsRead()
        {
            Assert.Empty(SampleValueJsonReader.Read("{\"types\":[],\"rows\":[]}").Types);
        }

        [Fact]
        public void AStructuredValueIsKept()
        {
            SampleValueTable table = SampleValueJsonReader.Read(
                "{\"types\":[{\"typeName\":\"PEPlugin.SDX.V3\",\"default\":[1,2,3]"
                + ",\"second\":{\"x\":1}}],\"rows\":[]}");

            Assert.Equal(new object[] { 1, 2, 3 }, Assert.IsType<object[]>(table.Types[0].First));
            Assert.IsType<Dictionary<string, object>>(table.Types[0].Second);
        }

        [Fact]
        public void ANullSampleIsKept()
        {
            SampleValueTable table = SampleValueJsonReader.Read(
                "{\"types\":[{\"typeName\":\"System.Object\",\"default\":null,\"second\":1}],\"rows\":[]}");

            Assert.Null(table.Types[0].First);
        }

        [Theory]
        [InlineData("{")]
        [InlineData("[]")]
        [InlineData("{\"other\":[],\"rows\":[]}")]
        [InlineData("{\"types\":{},\"rows\":[]}")]
        [InlineData("{\"types\":[1],\"rows\":[]}")]
        [InlineData("{\"types\":[{\"typeName\":\"T\",\"default\":1}],\"rows\":[]}")]
        [InlineData("{\"types\":[{\"typeName\":\"T\",\"default\":1,\"second\":2,\"extra\":3}],\"rows\":[]}")]
        [InlineData("{\"types\":[{\"typeName\":\"\",\"default\":1,\"second\":2}],\"rows\":[]}")]
        [InlineData("{\"types\":[{\"typeName\":1,\"default\":1,\"second\":2}],\"rows\":[]}")]
        public void AShapeThatIsNotTheCanonStops(string json)
        {
            Assert.Throws<FormatException>(() => SampleValueJsonReader.Read(json));
        }

        [Fact]
        public void RowsOutOfAscendingOrderStop()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => SampleValueJsonReader.Read(
                    "{\"types\":[{\"typeName\":\"System.String\",\"default\":\"a\""
                    + ",\"second\":\"b\"},{\"typeName\":\"System.Int32\",\"default\":1"
                    + ",\"second\":2}],\"rows\":[]}"));

            Assert.Contains("序数の昇順", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ARowCanCarryHowItIsPassedAsAFile()
        {
            SampleValueRow row = Assert.Single(SampleValueJsonReader.Read(
                "{\"types\":[{\"typeName\":\"A\",\"default\":1,\"second\":2,\"file\":" +
                "{\"kind\":\"image\",\"extension\":\".png\",\"purpose\":\"渡す。\"}}],\"rows\":[]}").Types);

            Assert.Equal("image", row.File.Kind);
            Assert.Equal(".png", row.File.Extension);
            Assert.Equal("渡す。", row.File.Purpose);
        }

        [Fact]
        public void ARowWithoutTheFileMemberCarriesNone()
        {
            SampleValueRow row = Assert.Single(SampleValueJsonReader.Read(
                "{\"types\":[{\"typeName\":\"A\",\"default\":1,\"second\":2}],\"rows\":[]}").Types);

            Assert.Null(row.File);
        }

        [Fact]
        public void AnExtensionWithoutItsDotStops()
        {
            Assert.Throws<FormatException>(() => SampleValueJsonReader.Read(
                "{\"types\":[{\"typeName\":\"A\",\"default\":1,\"second\":2,\"file\":" +
                "{\"kind\":\"image\",\"extension\":\"png\",\"purpose\":\"渡す。\"}}],\"rows\":[]}"));
        }

        [Fact]
        public void AFileWithoutAMemberStops()
        {
            Assert.Throws<FormatException>(() => SampleValueJsonReader.Read(
                "{\"types\":[{\"typeName\":\"A\",\"default\":1,\"second\":2,\"file\":" +
                "{\"kind\":\"image\"}}],\"rows\":[]}"));
        }

        [Fact]
        public void TheSameFileKindOnTwoTypesStops()
        {
            Assert.Throws<FormatException>(() => SampleValueJsonReader.Read(
                "{\"types\":[{\"typeName\":\"A\",\"default\":1,\"second\":2,\"file\":" +
                "{\"kind\":\"image\",\"extension\":\".png\",\"purpose\":\"渡す。\"}}," +
                "{\"typeName\":\"B\",\"default\":1,\"second\":2,\"file\":" +
                "{\"kind\":\"image\",\"extension\":\".bmp\",\"purpose\":\"渡す。\"}}],\"rows\":[]}"));
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => SampleValueJsonReader.Read(null));
            Assert.Throws<ArgumentNullException>(() => new SampleValueRow(null, 1, 2));
            Assert.Throws<ArgumentNullException>(() => new SampleValueTable(null));
        }
    }
}
