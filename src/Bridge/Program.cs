using System;
using System.Threading.Tasks;

namespace PmxEditorMcp.Bridge
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            BridgeBudget budget = BridgeBudget.ReadFromEnvironment();
            if (!budget.IsValid)
            {
                // 診断は標準エラー出力へ出す(stdioのプロトコルストリームを汚さない)。
                Console.Error.WriteLine(budget.InvalidReason);
                return BridgeBudget.InvalidExitCode;
            }

            using HostIpcClient client = new HostIpcClient(new NamedPipeHostConnector(), budget.Chars);
            await BridgeServer.RunAsync(args, client);
            return 0;
        }
    }
}
