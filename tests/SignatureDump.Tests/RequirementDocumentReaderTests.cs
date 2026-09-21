using System;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>要求仕様書から要求の名前を読むこと。</summary>
    public sealed class RequirementDocumentReaderTests
    {
        private const string Document = @"# 要求仕様書

## 作業の要求

### エディタの1回でできることは、1回の呼び出しでできる

揃える。

#### 開かれていないと成り立たない

前提がある。

### 呼び出しが変えたものは読み戻せる

読み戻せる。

## 作業に依らない要件

### 一度だけ頼んだ操作を、二度実行しない

やり直さない。

### 標準のMCPで動く製品にし、特定のクライアントに縛られない

縛られない。
";

        [Fact]
        public void TheTasksAreTheHeadingsUnderTheSectionThatHoldsThem()
        {
            Assert.Equal(
                new[]
                {
                    "エディタの1回でできることは、1回の呼び出しでできる",
                    "呼び出しが変えたものは読み戻せる",
                },
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
        public void ADocumentWithoutAnyTaskIsRejected()
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
                    "# 要求仕様書\n\n## 作業の要求\n\n### 揃える\n\n揃える。\n"));

            Assert.Contains("作業に依らない要件", error.Message, StringComparison.Ordinal);
        }
    }
}
