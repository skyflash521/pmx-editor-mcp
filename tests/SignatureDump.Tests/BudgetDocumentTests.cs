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
        public void TheWarningRoomIsRead()
        {
            Assert.Equal(2000, BudgetDocument.ReadWarningRoom("- **警告の枠は2,000文字**。以下略。"));
        }

        [Fact]
        public void TheRequestBudgetIsRead()
        {
            Assert.Equal(
                8000000,
                BudgetDocument.ReadRequestBytes("要求の大きさの上限は**8,000,000バイト**とする。"));
        }

        [Fact]
        public void TheTokenLimitIsRead()
        {
            Assert.Equal(
                200000,
                BudgetDocument.ReadTokenLimit("要求にはもう1つ、**構造トークンの上限 200,000** がある。"));
        }

        [Theory]
        [InlineData("警告の枠")]
        [InlineData("要求サイズ予算")]
        [InlineData("構造トークンの上限")]
        public void AValueThatTheDocumentDoesNotStateStops(string name)
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Read(name, "どの値も書かれていない本文。"));

            Assert.Contains(name + "が読めない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheDocumentIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => BudgetDocument.ReadDefault(null));
            Assert.Throws<ArgumentNullException>(() => BudgetDocument.ReadWarningRoom(null));
            Assert.Throws<ArgumentNullException>(() => BudgetDocument.ReadRequestBytes(null));
            Assert.Throws<ArgumentNullException>(() => BudgetDocument.ReadTokenLimit(null));
        }

        private static int Read(string name, string text)
        {
            switch (name)
            {
                case "警告の枠":
                    return BudgetDocument.ReadWarningRoom(text);
                case "要求サイズ予算":
                    return BudgetDocument.ReadRequestBytes(text);
                default:
                    return BudgetDocument.ReadTokenLimit(text);
            }
        }
    }
}
