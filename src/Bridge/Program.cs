using System;
using System.Threading.Tasks;

namespace PmxEditorMcp.Bridge
{
    internal static class Program
    {
        /// <summary>応答サイズ予算の設定が受理できないときにブリッジのプロセスが返す終了コード。</summary>
        internal const int InvalidBudgetExitCode = 2;

        private static async Task<int> Main(string[] args)
        {
            ResponseBudget budget = ResponseBudget.ReadFromEnvironment();
            if (!budget.IsValid)
            {
                // 診断は標準エラー出力へ出す(stdioのプロトコルストリームを汚さない)。
                Console.Error.WriteLine(budget.InvalidReason);
                return InvalidBudgetExitCode;
            }

            using HostIpcClient client = new HostIpcClient(new NamedPipeHostConnector(), budget.Chars);
            await BridgeServer.RunAsync(args, client);
            return 0;
        }
    }
}
