using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class DebugHooksTests
    {
        [Fact]
        public void TheExactValueOpensTheEntry()
        {
            Assert.True(DebugHooks.IsEnabled("1"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" 1")]
        [InlineData("1 ")]
        [InlineData("01")]
        [InlineData("true")]
        [InlineData("0")]
        [InlineData("2")]
        public void AnyOtherValueLeavesItClosed(string rawValue)
        {
            Assert.False(DebugHooks.IsEnabled(rawValue));
        }

        /// <summary>名前は起動側が打つ文字列そのものなので、定数と実装を揃えて変えても気づける。</summary>
        [Fact]
        public void TheVariableNameIsACallerContract()
        {
            Assert.Equal("PMX_EDITOR_MCP_DEBUG_HOOKS", DebugHooks.EnvironmentVariableName);
            Assert.Equal("1", DebugHooks.EnabledValue);
        }
    }
}
