using Xunit;

namespace PmxEditorMcp.Tests
{
    public class ResponseBudgetTests
    {
        private const string Pending = "impl pending: 応答サイズ予算の環境変数を厳格な10進表記として読み、範囲外・構文違反を無効として扱う";

        [Fact(Skip = Pending)]
        public void 未設定なら既定の文字数で有効になる()
        {
            ResponseBudget budget = ResponseBudget.Read(null);

            Assert.True(budget.IsValid);
            Assert.Equal(ResponseBudget.DefaultChars, budget.Chars);
        }

        [Theory(Skip = Pending)]
        [InlineData("10000", 10000)]
        [InlineData("100000", 100000)]
        [InlineData("500000", 500000)]
        public void 範囲内の10進表記はその値で有効になる(string rawValue, int expected)
        {
            ResponseBudget budget = ResponseBudget.Read(rawValue);

            Assert.True(budget.IsValid);
            Assert.Equal(expected, budget.Chars);
        }

        [Theory(Skip = Pending)]
        [InlineData("9999")]
        [InlineData("500001")]
        [InlineData("0")]
        [InlineData("99999999999999999999")]
        public void 範囲外の値は無効になり理由を持つ(string rawValue)
        {
            ResponseBudget budget = ResponseBudget.Read(rawValue);

            Assert.False(budget.IsValid);
            Assert.False(string.IsNullOrEmpty(budget.InvalidReason));
        }

        [Theory(Skip = Pending)]
        [InlineData("")]
        [InlineData("+100000")]
        [InlineData("-100000")]
        [InlineData(" 100000")]
        [InlineData("100000 ")]
        [InlineData("0100000")]
        [InlineData("１０００００")]
        [InlineData("100_000")]
        [InlineData("1e5")]
        [InlineData("100000a")]
        public void 構文に反する値は無効になり理由を持つ(string rawValue)
        {
            ResponseBudget budget = ResponseBudget.Read(rawValue);

            Assert.False(budget.IsValid);
            Assert.False(string.IsNullOrEmpty(budget.InvalidReason));
        }

        [Fact(Skip = Pending)]
        public void 無効な設定は既定の文字数へ落とさない()
        {
            ResponseBudget budget = ResponseBudget.Read("9999");

            Assert.False(budget.IsValid);
            Assert.Equal(0, budget.Chars);
        }
    }
}
