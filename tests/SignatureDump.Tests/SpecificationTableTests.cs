using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class SpecificationTableTests
    {
        private const string Document = @"## ほかの節

| 見出し | 値 |
|---|---|
| `別の表` | 1 |

## 題材の節

前置きの段落。

| 見出し | 値 |
|---|---|
| `一つ目` | 1 |
| `二つ目` | 2 |

## 次の節

| 見出し | 値 |
|---|---|
| `続き` | 3 |
";

        [Fact]
        public void TheRowsOfTheNamedSectionAreReturned()
        {
            IList<string[]> rows = SpecificationTable.Rows(Document, "## 題材の節").ToList();

            Assert.Equal(2, rows.Count);
            Assert.Equal(new[] { "`一つ目`", "1" }, rows[0]);
            Assert.Equal(new[] { "`二つ目`", "2" }, rows[1]);
        }

        [Fact]
        public void ASectionThatIsNotThereStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => SpecificationTable.Rows(Document, "## 無い節").ToList());

            Assert.Contains("節が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ARowThatIsNotTwoColumnsStops()
        {
            Assert.Throws<InvalidOperationException>(() => SpecificationTable
                .Rows("## 節\n\n| a | b | c |\n|---|---|---|\n| 1 | 2 | 3 |\n", "## 節").ToList());
        }

        [Fact]
        public void TheQuotedCellIsUnwrapped()
        {
            Assert.Equal("text", SpecificationTable.Quoted("`text`", "行"));
        }

        [Theory]
        [InlineData("text")]
        [InlineData("``")]
        [InlineData("`text")]
        public void ACellThatIsNotQuotedStops(string cell)
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => SpecificationTable.Quoted(cell, "行"));

            Assert.Contains("引用符で囲まれていない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheArgumentsAreCheckedBeforeAnythingIsCounted()
        {
            Assert.Throws<ArgumentNullException>(() => SpecificationTable.Rows(null, "## 節"));
            Assert.Throws<ArgumentNullException>(() => SpecificationTable.Rows(Document, null));
            Assert.Throws<ArgumentNullException>(() => SpecificationTable.Quoted(null, "行"));
        }
    }
}
