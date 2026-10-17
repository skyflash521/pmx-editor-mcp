using System;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>
    /// 能力対応表の指紋。ホストの中継とブリッジのツール定義が同じ表から作られたことを、接続の
    /// 確立で照らし合わせるために使う。
    /// </summary>
    public sealed class ToolMapDigestTests
    {
        [Fact]
        public void TheSameTextGivesTheSameDigest()
        {
            Assert.Equal(
                ToolMapDigest.Of("{\"rows\":[]}"),
                ToolMapDigest.Of("{\"rows\":[]}"));
        }

        [Fact]
        public void TheSameRowKeyWithADifferentKindGivesADifferentDigest()
        {
            Assert.NotEqual(
                ToolMapDigest.Of("{\"rows\":[{\"signatureKey\":\"A\",\"editKind\":\"read\"}]}"),
                ToolMapDigest.Of("{\"rows\":[{\"signatureKey\":\"A\",\"editKind\":\"write\"}]}"));
        }

        [Theory]
        [InlineData("{\r\n  \"rows\": []\r\n}")]
        [InlineData("{\r  \"rows\": []\r}")]
        public void TheSpellingOfLineBreaksDoesNotChangeTheDigest(string spelled)
        {
            Assert.Equal(ToolMapDigest.Of("{\n  \"rows\": []\n}"), ToolMapDigest.Of(spelled));
        }

        [Fact]
        public void TheDigestIsSpelledAsHexadecimalOfAFixedLength()
        {
            string digest = ToolMapDigest.Of("{\"rows\":[]}");

            Assert.Equal(64, digest.Length);
            Assert.All(digest, letter => Assert.True("0123456789abcdef".IndexOf(letter) >= 0));
        }

        [Fact]
        public void AnEmptyTextStillGivesADigest()
        {
            Assert.Equal(64, ToolMapDigest.Of(string.Empty).Length);
        }

        [Fact]
        public void TheTextIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => ToolMapDigest.Of(null));
        }
    }
}
