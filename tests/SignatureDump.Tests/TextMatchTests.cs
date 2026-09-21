using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>探す語を文へ当てる規則。</summary>
    public sealed class TextMatchTests
    {
        [Fact]
        public void TheWordIsFoundWhereverItSitsInTheText()
        {
            Assert.True(TextMatch.Contains("材質を分割して取り出す", "分割"));
            Assert.False(TextMatch.Contains("材質を分割して取り出す", "分離"));
        }

        [Fact]
        public void TheCaseAndTheWidthAndTheKanaTypeAreNotTold()
        {
            Assert.True(TextMatch.Contains("Material", "material"));
            Assert.True(TextMatch.Contains("ＵＶ", "uv"));
            Assert.True(TextMatch.Contains("ボーン", "ﾎﾞｰﾝ"));
        }

        [Fact]
        public void AnEmptyWordIsFoundEverywhereAndNothingIsFoundInNothing()
        {
            Assert.True(TextMatch.Contains("材質", string.Empty));
            Assert.False(TextMatch.Contains(null, "材質"));
            Assert.False(TextMatch.Contains("材質", null));
        }
    }
}
