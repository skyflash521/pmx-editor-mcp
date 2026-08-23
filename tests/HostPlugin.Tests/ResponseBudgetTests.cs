using Xunit;

namespace PmxEditorMcp.Tests
{
    public class ResponseBudgetTests
    {
        [Fact]
        public void 未設定なら既定の文字数で有効になる()
        {
            ResponseBudget budget = ResponseBudget.Read(null);

            Assert.True(budget.IsValid);
            Assert.Equal(ResponseBudget.DefaultChars, budget.Chars);
        }

        [Theory]
        [InlineData("10000", 10000)]
        [InlineData("100000", 100000)]
        [InlineData("500000", 500000)]
        public void 範囲内の10進表記はその値で有効になる(string rawValue, int expected)
        {
            ResponseBudget budget = ResponseBudget.Read(rawValue);

            Assert.True(budget.IsValid);
            Assert.Equal(expected, budget.Chars);
        }

        [Theory]
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

        [Theory]
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

        [Fact]
        public void 無効な理由は値に含まれる制御文字をそのまま載せない()
        {
            const char CarriageReturn = (char)13;
            const char LineFeed = (char)10;

            ResponseBudget budget = ResponseBudget.Read("12" + CarriageReturn + LineFeed + "34");

            Assert.False(budget.IsValid);
            Assert.DoesNotContain(CarriageReturn.ToString(), budget.InvalidReason);
            Assert.DoesNotContain(LineFeed.ToString(), budget.InvalidReason);
        }

        [Fact]
        public void 無効な理由は長大な値をそのまま載せない()
        {
            // 長さは受理範囲の境界値と一致させない(範囲の説明文と区別できなくなるため)。
            string rawValue = new string('9', 12345);

            ResponseBudget budget = ResponseBudget.Read(rawValue);

            Assert.False(budget.IsValid);
            Assert.DoesNotContain(rawValue, budget.InvalidReason);
            Assert.Contains("全 12345 文字", budget.InvalidReason);
        }

        [Fact]
        public void 無効な設定は既定の文字数へ落とさない()
        {
            ResponseBudget budget = ResponseBudget.Read("9999");

            Assert.False(budget.IsValid);
            Assert.Equal(0, budget.Chars);
        }
    }
}
