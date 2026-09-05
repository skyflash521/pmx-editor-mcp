using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ComposedToolDocumentTests
    {
        private const string Document =
            "### 合成ツール\n\n| ツール | 分岐 | 受け持つこと |\n|---|---|---|\n"
            + "| `session_release_handle` | 持たない | 解放する |\n"
            + "| `view_poll_events` | 持つ | 取り出す |\n";

        [Fact]
        public void TheNamesAndBranchingAreRead()
        {
            IDictionary<string, ComposedTool> tools = ComposedToolDocument.Read(Document);

            Assert.Equal(2, tools.Count);
            Assert.False(tools["session_release_handle"].Branching);
            Assert.Equal("解放する", tools["session_release_handle"].Duty);
            Assert.True(tools["view_poll_events"].Branching);
            Assert.Equal("取り出す", tools["view_poll_events"].Duty);
        }

        [Fact]
        public void ABranchingColumnThatIsNotOneOfTheTwoWordsStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ComposedToolDocument.Read(
                    "### 合成ツール\n\n| ツール | 分岐 | 受け持つこと |\n|---|---|---|\n"
                    + "| `a` | ある | 1 |\n"));

            Assert.Contains("知らない語", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnEmptyDutyStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ComposedToolDocument.Read(
                    "### 合成ツール\n\n| ツール | 分岐 | 受け持つこと |\n|---|---|---|\n"
                    + "| `a` | 持つ |  |\n"));

            Assert.Contains("受け持つことの欄が空", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheSameToolTwiceStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ComposedToolDocument.Read(
                    "### 合成ツール\n\n| ツール | 分岐 | 受け持つこと |\n|---|---|---|\n"
                    + "| `a` | 持つ | 1 |\n| `a` | 持たない | 2 |\n"));

            Assert.Contains("二度現れる", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ATableWithoutRowsStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ComposedToolDocument.Read("### 合成ツール\n\n本文だけ。\n"));

            Assert.Contains("表に行が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ASectionThatIsNotThereStops()
        {
            Assert.Throws<InvalidOperationException>(
                () => ComposedToolDocument.Read("## ほかの節\n"));
        }

        [Fact]
        public void ANameThatIsNotQuotedStops()
        {
            Assert.Throws<InvalidOperationException>(
                () => ComposedToolDocument.Read(
                    "### 合成ツール\n\n| ツール | 分岐 | 受け持つこと |\n|---|---|---|\n"
                    + "| a | 持つ | 1 |\n"));
        }
    }
}
