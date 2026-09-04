using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public class ValueShapeDocumentTests
    {
        private const string Document = @"## 値の表現

### 表現の綴り

| 綴り | JSONの形 |
|---|---|
| `number` | 数値 |

### 型ごとの表現

型の綴りで山括弧の中に置いた数は、型引数の個数である。

| 型 | 表現 |
|---|---|
| `System.Int32` | `number` |
| `System.Nullable<1>` | nullable_… |

## ハンドル

| 型 | 表現 |
|---|---|
| `PEPlugin.Pmx.IPXVertex` | `text` |
";

        [Fact]
        public void TheTableIsReadFromItsSection()
        {
            IList<ValueShapeRow> rows = ValueShapeDocument.Read(Document);

            Assert.Equal(2, rows.Count);
            Assert.Equal("System.Int32", rows[0].TypeName);
            Assert.Equal("number", rows[0].Shape);
        }

        [Fact]
        public void TheSpellingsAreReadFromTheirOwnSection()
        {
            Assert.Equal(new[] { "number" }, ValueShapeDocument.ReadSpellings(Document));
        }

        [Fact]
        public void ADocumentWithoutTheSpellingSectionStops()
        {
            Assert.Throws<InvalidOperationException>(
                () => ValueShapeDocument.ReadSpellings("## 値の表現\n"));
        }

        [Fact]
        public void ASpellingSectionWithoutRowsStops()
        {
            Assert.Throws<InvalidOperationException>(() => ValueShapeDocument.ReadSpellings(
                "### 表現の綴り\n\n| 綴り | JSONの形 |\n|---|---|\n\n### 次\n"));
        }

        [Fact]
        public void ASpellingThatIsNotQuotedStops()
        {
            Assert.Throws<InvalidOperationException>(() => ValueShapeDocument.ReadSpellings(
                "### 表現の綴り\n\n| 綴り | JSONの形 |\n|---|---|\n| number | 数値 |\n"));
        }

        [Fact]
        public void TheDocumentIsRequiredToReadTheSpellings()
        {
            Assert.Throws<ArgumentNullException>(() => ValueShapeDocument.ReadSpellings(null));
        }

        [Fact]
        public void AWrappingTypeHasNoSpelling()
        {
            IList<ValueShapeRow> rows = ValueShapeDocument.Read(Document);

            Assert.Equal("System.Nullable<1>", rows[1].TypeName);
            Assert.Null(rows[1].Shape);
        }

        [Fact]
        public void TheTableEndsAtTheNextHeading()
        {
            IList<ValueShapeRow> rows = ValueShapeDocument.Read(Document);

            Assert.DoesNotContain("PEPlugin.Pmx.IPXVertex", rows.Select(r => r.TypeName));
        }

        [Fact]
        public void ADocumentWithoutTheSectionStops()
        {
            Assert.Throws<InvalidOperationException>(
                () => ValueShapeDocument.Read("## 値の表現\n\n本文だけ。\n"));
        }

        [Fact]
        public void ASectionWithoutRowsStops()
        {
            Assert.Throws<InvalidOperationException>(() => ValueShapeDocument.Read(
                ValueShapeDocument.SectionHeading + "\n\n| 型 | 表現 |\n|---|---|\n\n## 次\n"));
        }

        [Fact]
        public void ARowWhoseTypeIsNotQuotedStops()
        {
            Assert.Throws<InvalidOperationException>(() => ValueShapeDocument.Read(
                ValueShapeDocument.SectionHeading
                    + "\n\n| 型 | 表現 |\n|---|---|\n| `System.Int32` | `number` |\n| System.Byte | `number` |\n"));
        }

        [Fact]
        public void ARowThatIsNotTwoColumnsStops()
        {
            Assert.Throws<InvalidOperationException>(() => ValueShapeDocument.Read(
                ValueShapeDocument.SectionHeading
                    + "\n\n| 型 | 表現 |\n|---|---|\n| `System.Int32` | `number` | 余り |\n"));
        }

        [Fact]
        public void TheDocumentIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => ValueShapeDocument.Read(null));
        }
    }
}
