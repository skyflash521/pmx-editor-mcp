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

        /// <summary>名前は起動側が打つ文字列そのものなので、定数と実装を揃えて変えても気づける。</summary>
        [Fact]
        public void TheVariableNamesAreACallerContract()
        {
            Assert.Equal("PMX_EDITOR_MCP_DEBUG_HOOKS", BridgeDeclaration.DebugHooksVariableName);
            Assert.Equal("PMX_EDITOR_MCP_DECLARE_META", BridgeDeclaration.EnvironmentVariableName);
            Assert.Equal("1", BridgeDeclaration.DebugHooksEnabledValue);
            Assert.Equal("0", BridgeDeclaration.SuppressedValue);
        }
    }
}
