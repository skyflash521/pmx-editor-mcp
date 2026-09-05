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
                + ",{\"typeName\":\"System.String\",\"default\":\"a\",\"second\":\"b\"}]}");

            Assert.Equal(2, table.Types.Count);
            Assert.Equal("System.Int32", table.Types[0].TypeName);
            Assert.Equal(1, table.Types[0].First);
            Assert.Equal(2, table.Types[0].Second);
            Assert.Equal("b", table.Types[1].Second);
        }

        [Fact]
        public void AnEmptyTableIsRead()
        {
            Assert.Empty(SampleValueJsonReader.Read("{\"types\":[]}").Types);
        }

        [Fact]
        public void AStructuredValueIsKept()
        {
            SampleValueTable table = SampleValueJsonReader.Read(
                "{\"types\":[{\"typeName\":\"PEPlugin.SDX.V3\",\"default\":[1,2,3]"
                + ",\"second\":{\"x\":1}}]}");

            Assert.Equal(new object[] { 1, 2, 3 }, Assert.IsType<object[]>(table.Types[0].First));
            Assert.IsType<Dictionary<string, object>>(table.Types[0].Second);
        }

        [Fact]
        public void ANullSampleIsKept()
        {
            SampleValueTable table = SampleValueJsonReader.Read(
                "{\"types\":[{\"typeName\":\"System.Object\",\"default\":null,\"second\":1}]}");

            Assert.Null(table.Types[0].First);
        }

        [Theory]
        [InlineData("{")]
        [InlineData("[]")]
        [InlineData("{\"other\":[]}")]
        [InlineData("{\"types\":{}}")]
        [InlineData("{\"types\":[1]}")]
        [InlineData("{\"types\":[{\"typeName\":\"T\",\"default\":1}]}")]
        [InlineData("{\"types\":[{\"typeName\":\"T\",\"default\":1,\"second\":2,\"extra\":3}]}")]
        [InlineData("{\"types\":[{\"typeName\":\"\",\"default\":1,\"second\":2}]}")]
        [InlineData("{\"types\":[{\"typeName\":1,\"default\":1,\"second\":2}]}")]
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
                    + ",\"second\":2}]}"));

            Assert.Contains("序数の昇順", error.Message, StringComparison.Ordinal);
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
