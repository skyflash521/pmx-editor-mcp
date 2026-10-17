using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class RequestParameterTests
    {
        [Fact]
        public void TextComesBackAsGiven()
        {
            Assert.Equal("ui.model.mouse", RequestParameter.Text(Parameters("ui.model.mouse"), "a"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TextThatIsEmptyStops(string value)
        {
            InvalidParamsException error = Assert.Throws<InvalidParamsException>(
                () => RequestParameter.Text(Parameters(value), "a"));

            Assert.Contains("a", error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(true)]
        public void TextThatIsNotAStringStops(object value)
        {
            Assert.Throws<InvalidParamsException>(() => RequestParameter.Text(Parameters(value), "a"));
        }

        [Fact]
        public void TextThatIsMissingStops()
        {
            Assert.Throws<InvalidParamsException>(() => RequestParameter.Text(Empty(), "a"));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(42)]
        [InlineData(int.MaxValue)]
        public void APositiveIntegerComesBackAsGiven(int value)
        {
            Assert.Equal(value, RequestParameter.PositiveInteger(Parameters(value), "a"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1.5)]
        [InlineData(double.NaN)]
        [InlineData("7")]
        [InlineData(true)]
        public void AValueThatIsNotAPositiveIntegerStops(object value)
        {
            InvalidParamsException error = Assert.Throws<InvalidParamsException>(
                () => RequestParameter.PositiveInteger(Parameters(value), "a"));

            Assert.Contains("a", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ANumberBeyondTheIntegerRangeStops()
        {
            Assert.Throws<InvalidParamsException>(
                () => RequestParameter.PositiveInteger(Parameters((double)int.MaxValue + 1), "a"));
        }

        [Fact]
        public void ANumberThatIsMissingStops()
        {
            Assert.Throws<InvalidParamsException>(() => RequestParameter.PositiveInteger(Empty(), "a"));
        }

        [Fact]
        public void TheArgumentsAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => RequestParameter.Text(null, "a"));
            Assert.Throws<ArgumentNullException>(() => RequestParameter.Text(Empty(), null));
            Assert.Throws<ArgumentNullException>(() => RequestParameter.PositiveInteger(null, "a"));
            Assert.Throws<ArgumentNullException>(() => RequestParameter.PositiveInteger(Empty(), null));
        }

        private static IDictionary<string, object> Empty()
        {
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private static IDictionary<string, object> Parameters(object value)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal) { { "a", value } };
        }
    }
}
