using System;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>
    /// 組み立てたツール定義をブリッジへ組み込むC#として綴る。綴りに現れる引用符と逆斜線は、
    /// 本文のままでは読み直せないので逃がす。
    /// </summary>
    public sealed class ToolDefinitionSourceTests
    {
        [Fact]
        public void TheDigestIsCarriedIntoTheText()
        {
            Assert.Contains(
                "internal const string ToolMapDigest = \"abc123\";",
                ToolDefinitionSource.Compose(new ToolDefinition[0], "abc123"));
        }

        [Fact]
        public void TheSchemaIsCarriedWithItsQuotesEscaped()
        {
            string text = ToolDefinitionSource.Compose(
                new[] { new ToolDefinition("one", "受け持つこと", "{\"type\":\"object\"}", false) },
                "abc123");

            Assert.Contains("\"one\",", text);
            Assert.Contains("\"受け持つこと\",", text);
            Assert.Contains("\"{\\\"type\\\":\\\"object\\\"}\"", text);
        }

        [Fact]
        public void ABackslashInTheTextIsEscaped()
        {
            Assert.Contains(
                "\"a\\\\b\"",
                ToolDefinitionSource.Compose(
                    new[] { new ToolDefinition("one", "a\\b", "{}", false) }, "abc123"));
        }

        [Fact]
        public void ALineBreakInTheTextIsEscaped()
        {
            Assert.Contains(
                "\"a\\nb\"",
                ToolDefinitionSource.Compose(
                    new[] { new ToolDefinition("one", "a\nb", "{}", false) }, "abc123"));
        }

        [Fact]
        public void WhetherTheValueIsAnImageIsCarriedIntoTheText()
        {
            string drawn = ToolDefinitionSource.Compose(
                new[] { new ToolDefinition("one", "受け持つこと", "{}", true) }, "abc123");
            string plain = ToolDefinitionSource.Compose(
                new[] { new ToolDefinition("one", "受け持つこと", "{}", false) }, "abc123");

            Assert.Contains("\"{}\",\n                    true)", drawn);
            Assert.Contains("\"{}\",\n                    false)", plain);
        }

        [Fact]
        public void NoToolsStillGivesAText()
        {
            Assert.Contains(
                "return new GeneratedToolDefinition[]",
                ToolDefinitionSource.Compose(new ToolDefinition[0], "abc123"));
        }

        [Fact]
        public void TheToolsAndTheDigestAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => ToolDefinitionSource.Compose(null, "abc123"));
            Assert.Throws<ArgumentNullException>(
                () => ToolDefinitionSource.Compose(new ToolDefinition[0], null));
        }
    }
}
