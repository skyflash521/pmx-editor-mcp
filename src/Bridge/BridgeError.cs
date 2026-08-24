using System;
using ModelContextProtocol.Protocol;

namespace PmxEditorMcp.Bridge
{
    /// <summary>ブリッジがMCPツール結果へ載せるエラーコード。</summary>
    public static class BridgeErrorCodes
    {
        /// <summary>PMXエディタが起動していない。</summary>
        public const string NoEditor = "BRIDGE_NO_EDITOR";

        /// <summary>PMXエディタが複数起動していて接続先を1つに決められない。</summary>
        public const string MultipleEditors = "BRIDGE_MULTIPLE_EDITORS";

        /// <summary>接続の確立に失敗した。</summary>
        public const string ConnectFailed = "BRIDGE_CONNECT_FAILED";

        /// <summary>handshake が成立しなかった。</summary>
        public const string HandshakeMismatch = "BRIDGE_HANDSHAKE_MISMATCH";

        /// <summary>handshake は成立したが、ホストの応答サイズ予算がブリッジ自身の値と一致しない。</summary>
        public const string BudgetMismatch = "BRIDGE_BUDGET_MISMATCH";

        /// <summary>応答待ちの間に切断された。</summary>
        public const string ConnectionLost = "BRIDGE_CONNECTION_LOST";

        /// <summary>handshake 成立後のホスト応答が不正である。</summary>
        public const string ProtocolError = "BRIDGE_PROTOCOL_ERROR";

        /// <summary>待機上限を超過した。</summary>
        public const string Timeout = "BRIDGE_TIMEOUT";

        /// <summary>送信前検査で要求が上限のバイト数を超えた。</summary>
        public const string RequestTooLarge = "BRIDGE_REQUEST_TOO_LARGE";

        /// <summary>ホストが返したJSON-RPCエラーのコードを、ブリッジのエラーコードへ写す。</summary>
        public static string ForHostError(int hostErrorCode)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// MCPツール結果としてそのまま返せるエラー。ブリッジはこの例外まで到達した失敗を
    /// プロセスの終了ではなくツール結果として返す。
    /// </summary>
    public sealed class BridgeException : Exception
    {
        /// <summary>エラーコードと、要求元へ返す説明を添えて生成する。</summary>
        public BridgeException(string code, string message)
            : base(message)
        {
            throw new NotImplementedException();
        }

        /// <summary>ツール結果のテキスト本文へ載せるエラーコード。</summary>
        public string Code => throw new NotImplementedException();

        /// <summary>ツール結果のテキスト本文。</summary>
        public string ToResultText()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// MCPツール結果へ変換する。失敗はプロトコル層のエラーではなく isError=true の
        /// ツール結果として返し、要求元が失敗の内容を確認できるようにする。
        /// </summary>
        public CallToolResult ToToolResult()
        {
            throw new NotImplementedException();
        }
    }
}
