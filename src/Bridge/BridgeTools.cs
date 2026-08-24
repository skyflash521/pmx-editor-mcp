using System;
using System.Collections.Generic;
using ModelContextProtocol.Server;

namespace PmxEditorMcp.Bridge
{
    /// <summary>ブリッジがMCPサーバーへ登録するツールを作る。</summary>
    public static class BridgeTools
    {
        /// <summary>
        /// ツール定義へ付ける、テキスト結果の文字数のしきい値を宣言する鍵。Claude Code は
        /// この宣言があるツールについて、自分の既定のしきい値を宣言値へ引き上げる。
        /// </summary>
        public const string ResultSizeMetaKey = "anthropic/maxResultSizeChars";

        /// <summary>
        /// ブリッジが登録するツールを作る。ツール定義へ載せる応答サイズ予算は、handshake で
        /// ホストと照合するのと同じ値をクライアントから取る——別々に受け取ると、宣言した値と
        /// 照合する値を食い違わせられる。
        /// </summary>
        public static IReadOnlyList<McpServerTool> Create(HostIpcClient client)
        {
            throw new NotImplementedException();
        }
    }
}
