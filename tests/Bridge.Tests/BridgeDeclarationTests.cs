using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public class BridgeDeclarationTests
    {
        [Fact]
        public void BothVariablesTogetherStopTheDeclaration()
        {
            Assert.False(BridgeDeclaration.IsDeclared("1", "0"));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(null, "0")]
        [InlineData("0", "0")]
        [InlineData("", "0")]
        [InlineData(" 1", "0")]
        [InlineData("1", null)]
        [InlineData("1", "")]
        [InlineData("1", "1")]
        [InlineData("1", " 0")]
        [InlineData("1", "00")]
        public void AnythingElseKeepsTheDeclaration(string debugHooksValue, string declareValue)
        {
            Assert.True(BridgeDeclaration.IsDeclared(debugHooksValue, declareValue));
        }

        [Fact]
        public void TheVariableNamesAreACallerContract()
        {
            Assert.Equal("PMX_EDITOR_MCP_DEBUG_HOOKS", BridgeDebugHooks.EnvironmentVariableName);
            Assert.Equal("PMX_EDITOR_MCP_DECLARE_META", BridgeDeclaration.EnvironmentVariableName);
            Assert.Equal("1", BridgeDebugHooks.EnabledValue);
            Assert.Equal("0", BridgeDeclaration.SuppressedValue);
        }
    }
}
