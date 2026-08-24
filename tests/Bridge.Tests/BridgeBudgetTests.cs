using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public class BridgeBudgetTests
    {
        /// <summary>起動したブリッジのプロセスが終わるのを待つ上限。</summary>
        private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(30);

        [Fact]
        public void 未設定なら既定の文字数で有効になる()
        {
            BridgeBudget budget = BridgeBudget.Read(null);

            Assert.True(budget.IsValid);
            Assert.Equal(BridgeBudget.DefaultChars, budget.Chars);
        }

        [Fact]
        public void 応答サイズ予算の設定は契約で定めた値である()
        {
            Assert.Equal("PMX_EDITOR_MCP_BUDGET_CHARS", BridgeBudget.EnvironmentVariableName);
            Assert.Equal(100000, BridgeBudget.DefaultChars);
            Assert.Equal(10000, BridgeBudget.MinimumChars);
            Assert.Equal(500000, BridgeBudget.MaximumChars);
            Assert.Equal(2, BridgeBudget.InvalidExitCode);
        }

        [Theory]
        [InlineData("10000", 10000)]
        [InlineData("100000", 100000)]
        [InlineData("500000", 500000)]
        public void 範囲内の10進表記はその値で有効になる(string rawValue, int expected)
        {
            BridgeBudget budget = BridgeBudget.Read(rawValue);

            Assert.True(budget.IsValid);
            Assert.Equal(expected, budget.Chars);
        }

        [Theory]
        [InlineData("9999")]
        [InlineData("500001")]
        [InlineData("0")]
        [InlineData("99999999999999999999")]
        public void 範囲外の値は無効になり理由を持つ(string rawValue)
        {
            BridgeBudget budget = BridgeBudget.Read(rawValue);

            Assert.False(budget.IsValid);
            Assert.False(string.IsNullOrEmpty(budget.InvalidReason));
        }

        [Theory]
        [InlineData("")]
        [InlineData("+100000")]
        [InlineData("-100000")]
        [InlineData(" 100000")]
        [InlineData("100000 ")]
        [InlineData("0100000")]
        [InlineData("１０００００")]
        [InlineData("100_000")]
        [InlineData("1e5")]
        [InlineData("100000a")]
        public void 構文に反する値は無効になり理由を持つ(string rawValue)
        {
            BridgeBudget budget = BridgeBudget.Read(rawValue);

            Assert.False(budget.IsValid);
            Assert.False(string.IsNullOrEmpty(budget.InvalidReason));
        }

        [Fact]
        public void 無効な理由は値に含まれる制御文字をそのまま載せない()
        {
            const char CarriageReturn = (char)13;
            const char LineFeed = (char)10;

            BridgeBudget budget = BridgeBudget.Read("12" + CarriageReturn + LineFeed + "34");

            Assert.False(budget.IsValid);
            Assert.DoesNotContain(CarriageReturn.ToString(), budget.InvalidReason);
            Assert.DoesNotContain(LineFeed.ToString(), budget.InvalidReason);
        }

        [Fact]
        public void 無効な理由は制御文字に分類されない行区切りもそのまま載せない()
        {
            const char LineSeparator = (char)0x2028;
            const char ParagraphSeparator = (char)0x2029;

            BridgeBudget budget = BridgeBudget.Read("12" + LineSeparator + ParagraphSeparator + "34");

            Assert.False(budget.IsValid);
            Assert.DoesNotContain(LineSeparator.ToString(), budget.InvalidReason);
            Assert.DoesNotContain(ParagraphSeparator.ToString(), budget.InvalidReason);
        }

        [Fact]
        public void 無効な理由は長大な値をそのまま載せない()
        {
            // 長さは受理範囲の境界値と一致させない(範囲の説明文と区別できなくなるため)。
            string rawValue = new string('9', 12345);

            BridgeBudget budget = BridgeBudget.Read(rawValue);

            Assert.False(budget.IsValid);
            Assert.DoesNotContain(rawValue, budget.InvalidReason);
            Assert.Contains("全 12345 文字", budget.InvalidReason);
        }

        [Fact]
        public void 無効な設定は既定の文字数へ落とさない()
        {
            BridgeBudget budget = BridgeBudget.Read("9999");

            Assert.False(budget.IsValid);
            Assert.Equal(0, budget.Chars);
        }

        [Theory]
        [InlineData("+100000")]
        [InlineData("0100000")]
        [InlineData("9999")]
        public async Task 受理できない設定では理由を1行出して終了コード2で終わる(string rawValue)
        {
            using Process bridge = StartBridge(rawValue);

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

            Assert.Equal(BridgeBudget.InvalidExitCode, bridge.ExitCode);

            string reason = await diagnostics;
            Assert.Single(reason.TrimEnd('\r', '\n').Split('\n'));
            Assert.Contains(BridgeBudget.EnvironmentVariableName, reason);

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

            startInfo.Environment[BridgeBudget.EnvironmentVariableName] = budgetRawValue;
            startInfo.Environment[PipeTargetResolver.EnvironmentVariableName] = "pmx-editor-mcp-0";

            return Process.Start(startInfo);
        }
    }
}
