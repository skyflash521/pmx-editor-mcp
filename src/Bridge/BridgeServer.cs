using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace PmxEditorMcp.Bridge
{
    /// <summary>stdioを使うMCPサーバーとしてのブリッジの構成。</summary>
    public static class BridgeServer
    {
        /// <summary>MCPクライアントへ名乗るサーバー名。</summary>
        public const string ServerName = "pmx-editor-mcp";

        /// <summary>初期化のときにクライアントへ渡す、このサーバーの使い方。</summary>
        public const string ServerInstructions =
            "PMXエディタを相手にするサーバー。ツールの名前は5つの系統に分かれ、" +
            "どれを引くかは何を知りたいかで決まる。" +
            "model_ はモデルの中身をどう読み書きするか——頂点と面・材質・骨と表情・物理——を" +
            "受け持つ。" +
            "motion_ はモーションをどう組み立てるか——VMDのキー・骨と表情の動き・カメラと照明——を" +
            "受け持つ。" +
            "view_ は3Dビューをどう見せて何を選ぶか——カメラ・可視・選択中の要素——を受け持つ。" +
            "session_ はエディタと何をやり取りするか——ファイル・プラグイン・元に戻す——を" +
            "受け持つ。" +
            "editor_ はPMXエディタ上でどう操作すればよいか——窓とメニューの辿り方・" +
            "ショートカット・確認の文言——を受け持つ。";

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
                .AddMcpServer(options =>
                {
                    options.ServerInfo = new Implementation
                    {
                        Name = ServerName,
                        Version = typeof(BridgeServer).Assembly.GetName().Version.ToString(),
                    };
                    options.ServerInstructions = ServerInstructions;
                })
                .WithStdioServerTransport()
                .WithTools(BridgeTools.Create(
                    client,
                    BridgeDeclaration.ReadFromEnvironment(),
                    BridgeDebugHooks.ReadFromEnvironment()));

            await builder.Build().RunAsync().ConfigureAwait(false);
        }
    }
}
