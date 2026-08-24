using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

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
        public static async Task RunAsync(string[] args, HostIpcClient client)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

            builder.Services
                .AddMcpServer(options => options.ServerInfo = new Implementation
                {
                    Name = ServerName,
                    Version = typeof(BridgeServer).Assembly.GetName().Version.ToString(),
                })
                .WithStdioServerTransport()
                .WithTools(BridgeTools.Create(client));

            await builder.Build().RunAsync().ConfigureAwait(false);
        }
    }
}
