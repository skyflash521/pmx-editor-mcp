using System;
using ModelContextProtocol.Server;

namespace PmxEditorMcp.Bridge
{
    /// <summary>stdioで話すMCPサーバーとしてのブリッジの構成。</summary>
    public static class BridgeServer
    {
        /// <summary>MCPクライアントへ名乗るサーバー名。</summary>
        public const string ServerName = "pmx-editor-mcp";

        /// <summary>
        /// stdioトランスポートのMCPサーバーを構成して動かす。標準出力はプロトコルの通り道なので、
        /// ログと診断は標準エラー出力だけへ出す。
        /// </summary>
        public static System.Threading.Tasks.Task RunAsync(string[] args, HostIpcClient client)
        {
            throw new NotImplementedException();
        }
    }
}
