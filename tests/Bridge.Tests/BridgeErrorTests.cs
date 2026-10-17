using System.Linq;
using ModelContextProtocol.Protocol;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public class BridgeErrorTests
    {
        [Fact]
        public void TextJoinsErrorCodeAndMessageWithColon()
        {
            BridgeException error = new BridgeException(BridgeErrorCodes.Timeout, "待機上限を超えた。");

            Assert.Equal(BridgeErrorCodes.Timeout, error.Code);
            Assert.Equal("待機上限を超えた。", error.Message);
            Assert.Equal("BRIDGE_TIMEOUT: 待機上限を超えた。", error.ToResultText());
        }

        [Theory]
        [InlineData(-32601, "HOST_-32601")]
        [InlineData(-32002, "HOST_-32002")]
        [InlineData(0, "HOST_0")]
        public void HostErrorCodeKeepsItsSign(int hostErrorCode, string expected)
        {
            Assert.Equal(expected, BridgeErrorCodes.ForHostError(hostErrorCode));
        }

        [Fact]
        public void HostErrorTextCarriesMessageVerbatim()
        {
            BridgeException error = new BridgeException(
                BridgeErrorCodes.ForHostError(-32601), "未知のメソッド");

            Assert.Equal("HOST_-32601: 未知のメソッド", error.ToResultText());
        }

        [Fact]
        public void ToolResultMarksErrorAndCarriesSingleText()
        {
            BridgeException error = new BridgeException(BridgeErrorCodes.NoEditor, "起動していない。");

            CallToolResult result = error.ToToolResult();

            Assert.True(result.IsError);
            Assert.Single(result.Content);

            TextContentBlock text = Assert.IsType<TextContentBlock>(result.Content.Single());
            Assert.Equal("BRIDGE_NO_EDITOR: 起動していない。", text.Text);
        }

        [Fact]
        public void ErrorCodesMatchContract()
        {
            Assert.Equal("BRIDGE_NO_EDITOR", BridgeErrorCodes.NoEditor);
            Assert.Equal("BRIDGE_NO_HOST", BridgeErrorCodes.NoHost);
            Assert.Equal("BRIDGE_MULTIPLE_HOSTS", BridgeErrorCodes.MultipleHosts);
            Assert.Equal("BRIDGE_CONNECT_FAILED", BridgeErrorCodes.ConnectFailed);
            Assert.Equal("BRIDGE_HANDSHAKE_MISMATCH", BridgeErrorCodes.HandshakeMismatch);
            Assert.Equal("BRIDGE_BUDGET_MISMATCH", BridgeErrorCodes.BudgetMismatch);
            Assert.Equal("BRIDGE_CONNECTION_LOST", BridgeErrorCodes.ConnectionLost);
            Assert.Equal("BRIDGE_PROTOCOL_ERROR", BridgeErrorCodes.ProtocolError);
            Assert.Equal("BRIDGE_TIMEOUT", BridgeErrorCodes.Timeout);
            Assert.Equal("BRIDGE_REQUEST_TOO_LARGE", BridgeErrorCodes.RequestTooLarge);
        }
    }
}
