using System;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class BudgetDocumentTests
    {
        private const string Document = @"## ほかの節

- 未設定時の既定は **1**——この節の値は読まない。

## 応答サイズ予算の設定

ホストとブリッジは、それぞれ環境変数から読む。

- 未設定時の既定は **100,000**——上限の5分の1。
- 有効範囲は **10,000 以上 500,000 以下**。

## 次の節

- 未設定時の既定は **2**——この節の値も読まない。
";

        [Fact]
        public void TheDefaultIsReadFromItsSection()
        {
            Assert.Equal(100000, BudgetDocument.ReadDefault(Document));
        }

        [Fact]
        public void ADocumentWithoutTheSectionStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => BudgetDocument.ReadDefault("## ほかの節\n\n- 未設定時の既定は **1**。\n"));

            Assert.Contains("節が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ASectionWithoutTheDefaultStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => BudgetDocument.ReadDefault(
                    "## 応答サイズ予算の設定\n\n- 既定は無い。\n\n## 次\n\n- 未設定時の既定は **3**。\n"));

            Assert.Contains("既定の予算が読めない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheDocumentIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => BudgetDocument.ReadDefault(null));
        }
    }
}
