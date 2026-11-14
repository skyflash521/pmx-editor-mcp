using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public class ResponseBudgetTests
    {
        /// <summary>起動したブリッジのプロセスが終わるのを待つ上限。</summary>
        private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(30);

        [Fact]
        public void BudgetEnvironmentVariableNameMatchesContract()
        {
            Assert.Equal("PMX_EDITOR_MCP_BUDGET_CHARS", ResponseBudget.EnvironmentVariableName);
            Assert.Equal(100000, ResponseBudget.DefaultChars);
            Assert.Equal(10000, ResponseBudget.MinimumChars);
            Assert.Equal(500000, ResponseBudget.MaximumChars);
            Assert.Equal(2, Program.InvalidBudgetExitCode);
        }

        [Fact]
        public void InvalidReasonDoesNotEchoLineSeparatorsOutsideControlCategory()
        {
            const char LineSeparator = (char)0x2028;
            const char ParagraphSeparator = (char)0x2029;

            ResponseBudget budget = ResponseBudget.Read("12" + LineSeparator + ParagraphSeparator + "34");

            Assert.False(budget.IsValid);
            Assert.DoesNotContain(LineSeparator.ToString(), budget.InvalidReason);
            Assert.DoesNotContain(ParagraphSeparator.ToString(), budget.InvalidReason);
        }

        [Fact]
        public async Task RejectedSettingWritesSingleReasonAndExitsWithTwo()
        {
            using Process bridge = StartBridge("9999");

            // 両方の出力を直ちに読み始める。片方でも溜めたままにすると、パイプが埋まった時点で
            // ブリッジが書き込みで止まり、終了そのものを待てなくなる。
            Task<string> diagnostics = bridge.StandardError.ReadToEndAsync();
            Task<string> protocolStream = bridge.StandardOutput.ReadToEndAsync();

            using CancellationTokenSource limit = new CancellationTokenSource(ExitWait);
            try
            {
                await bridge.WaitForExitAsync(limit.Token);
            }
            catch (OperationCanceledException)
            {
                TerminateBridge(bridge);
                Assert.Fail("ブリッジが待機上限内に終了しなかった。");
            }

            Assert.Equal(Program.InvalidBudgetExitCode, bridge.ExitCode);

            string reason = await diagnostics;
            Assert.Single(reason.TrimEnd('\r', '\n').Split('\n'));
            Assert.Contains(ResponseBudget.EnvironmentVariableName, reason);

            // 診断は標準エラー出力へ出し、stdioのプロトコルストリームを汚さない。
            Assert.Equal(string.Empty, await protocolStream);
        }

        /// <summary>
        /// 待機上限を超えたブリッジを終わらせる。終了の要求は直ちに効くとは限らないので、
        /// 見届けてから戻る——見届けないと、後始末がまだ動いているプロセスと読み取りを残して進む。
        /// </summary>
        private static void TerminateBridge(Process bridge)
        {
            try
            {
                bridge.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // 打ち切りとほぼ同時に自分で終了していた。終了を待つだけでよい。
            }

            Assert.True(bridge.WaitForExit(ExitWait), "ブリッジを終了させられなかった。");
        }

        /// <summary>
        /// 応答サイズ予算を与えてブリッジの実行ファイルを起動する。接続先には存在しない
        /// パイプ名を明示し、実行環境に起動中のエディタがあっても結果が変わらないようにする。
        /// </summary>
        private static Process StartBridge(string budgetRawValue)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(AppContext.BaseDirectory, "PmxEditorMcp.Bridge.exe"),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
            };

            startInfo.Environment[ResponseBudget.EnvironmentVariableName] = budgetRawValue;
            startInfo.Environment[PipeTargetResolver.TestPipeEnvironmentVariableName] = "pmx-editor-mcp-0";

            return Process.Start(startInfo);
        }
    }
}
