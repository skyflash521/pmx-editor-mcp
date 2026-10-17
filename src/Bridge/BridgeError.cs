using System;
using System.Collections.Generic;
using System.Globalization;
using ModelContextProtocol.Protocol;

namespace PmxEditorMcp.Bridge
{
    /// <summary>ブリッジがMCPツール結果へ載せるエラーコード。</summary>
    public static class BridgeErrorCodes
    {
        /// <summary>接続先になるPMXエディタが起動していない。選んだエディタが終了したときもこれを返す。</summary>
        public const string NoEditor = "BRIDGE_NO_EDITOR";

        /// <summary>接続先になるPMXエディタは起動しているが、待ち受けているホストがない。</summary>
        public const string NoHost = "BRIDGE_NO_HOST";

        /// <summary>ホストが複数待ち受けていて接続先を1つに決められない。</summary>
        public const string MultipleHosts = "BRIDGE_MULTIPLE_HOSTS";

        /// <summary>接続の確立に失敗した。</summary>
        public const string ConnectFailed = "BRIDGE_CONNECT_FAILED";

        /// <summary>handshake が成立しなかった。</summary>
        public const string HandshakeMismatch = "BRIDGE_HANDSHAKE_MISMATCH";

        /// <summary>handshake は成立したが、ホストの応答サイズ予算がブリッジ自身の値と一致しない。</summary>
        public const string BudgetMismatch = "BRIDGE_BUDGET_MISMATCH";

        /// <summary>ホストの中継とブリッジのツール定義が別の能力対応表から作られている。</summary>
        public const string ToolDefinitionMismatch = "BRIDGE_TOOL_DEFINITION_MISMATCH";

        /// <summary>応答待ちの間に切断された。</summary>
        public const string ConnectionLost = "BRIDGE_CONNECTION_LOST";

        /// <summary>handshake 成立後のホスト応答が不正である。</summary>
        public const string ProtocolError = "BRIDGE_PROTOCOL_ERROR";

        /// <summary>待機上限を超過した。</summary>
        public const string Timeout = "BRIDGE_TIMEOUT";

        /// <summary>送信前検査で要求が上限のバイト数を超えた。</summary>
        public const string RequestTooLarge = "BRIDGE_REQUEST_TOO_LARGE";

        private const string HostErrorPrefix = "HOST_";

        public static string ForHostError(int hostErrorCode)
        {
            return HostErrorPrefix + hostErrorCode.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class BridgeException : Exception
    {
        public BridgeException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }

        public string ToResultText()
        {
            return Code + ": " + Message;
        }

        public CallToolResult ToToolResult()
        {
            return new CallToolResult
            {
                IsError = true,
                Content = new List<ContentBlock> { new TextContentBlock { Text = ToResultText() } },
            };
        }
    }
}
