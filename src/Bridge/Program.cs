using System;

namespace PmxEditorMcp.Bridge
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            BridgeBudget budget = BridgeBudget.ReadFromEnvironment();
            if (!budget.IsValid)
            {
                // 診断は標準エラー出力へ出す(stdioのプロトコルストリームを汚さない)。
                Console.Error.WriteLine(budget.InvalidReason);
                return BridgeBudget.InvalidExitCode;
            }

            return 0;
        }
    }
}
