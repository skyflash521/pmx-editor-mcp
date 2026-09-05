using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class AssumedLengthDocumentTests
    {
        private const string Document = @"#### 想定文字数

置いた理由の段落。

| 綴り | 想定文字数 |
|---|---|
| `boolean` | 5 |
| `text` | 256 |

#### 次の節

| 綴り | 想定文字数 |
|---|---|
| `other` | 1 |
";

        [Fact]
        public void TheTableIsReadFromItsSection()
        {
            IDictionary<string, int> lengths = AssumedLengthDocument.Read(Document);

            Assert.Equal(new[] { "boolean", "text" }, lengths.Keys);
            Assert.Equal(5, lengths["boolean"]);
            Assert.Equal(256, lengths["text"]);
        }

        [Fact]
        public void ADocumentWithoutTheSectionStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => AssumedLengthDocument.Read("#### ほかの節\n\n| 綴り | 想定文字数 |\n"));

            Assert.Contains("節が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ASectionWithoutRowsStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => AssumedLengthDocument.Read(
                    "#### 想定文字数\n\n| 綴り | 想定文字数 |\n|---|---|\n\n#### 次\n"));

            Assert.Contains("表に行が無い", error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("5文字")]
        [InlineData("5.0")]
        public void ALengthThatIsNotAPositiveIntegerStops(string length)
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => AssumedLengthDocument.Read(
                    "#### 想定文字数\n\n| 綴り | 想定文字数 |\n|---|---|\n| `boolean` | "
                        + length + " |\n"));

            Assert.Contains("1以上の整数でない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ASpellingThatIsNotQuotedStops()
        {
            Assert.Throws<InvalidOperationException>(() => AssumedLengthDocument.Read(
                "#### 想定文字数\n\n| 綴り | 想定文字数 |\n|---|---|\n| boolean | 5 |\n"));
        }

        [Fact]
        public void TheSameSpellingTwiceStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => AssumedLengthDocument.Read(
                    "#### 想定文字数\n\n| 綴り | 想定文字数 |\n|---|---|\n| `boolean` | 5 |\n"
                        + "| `boolean` | 6 |\n"));

            Assert.Contains("二度現れる", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheDocumentIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => AssumedLengthDocument.Read(null));
        }
    }
}
