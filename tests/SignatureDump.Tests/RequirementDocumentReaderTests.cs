using System;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>要求仕様書から要求の名前を読むこと。</summary>
    public sealed class RequirementDocumentReaderTests
    {
        private const string Document = @"# 要求仕様書

## 作業の要求

| 作業 | 成功の条件 |
|---|---|
| モデルの構造の把握 | 一定の呼び出し回数で読める |
| 保存と読み込み | それぞれ1回の呼び出しで済む |

## 作業に依らない要件

### 一度だけ頼んだ操作を、二度実行しない

やり直さない。

### 標準のMCPで動く製品にし、特定のクライアントに縛られない

縛られない。
";

        [Fact]
        public void TheTasksAreTheFirstCellsOfTheTableRows()
        {
            Assert.Equal(
                new[] { "モデルの構造の把握", "保存と読み込み" },
                RequirementDocumentReader.Read(Document).Tasks);
        }

        [Fact]
        public void TheConditionsAreTheHeadingsUnderTheSectionThatHoldsThem()
        {
            Assert.Equal(
                new[]
                {
                    "一度だけ頼んだ操作を、二度実行しない",
                    "標準のMCPで動く製品にし、特定のクライアントに縛られない",
                },
                RequirementDocumentReader.Read(Document).Conditions);
        }

        [Fact]
        public void ADocumentWithoutTheTableOfTasksIsRejected()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => RequirementDocumentReader.Read("# 要求仕様書\n\n## 作業に依らない要件\n\n### 速い\n"));

            Assert.Contains("作業の要求", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ADocumentWithoutAnyConditionIsRejected()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => RequirementDocumentReader.Read(
                    "# 要求仕様書\n\n## 作業の要求\n\n| 作業 | 成功の条件 |\n|---|---|\n| 把握 | 読める |\n"));

            Assert.Contains("作業に依らない要件", error.Message, StringComparison.Ordinal);
        }
    }
}
