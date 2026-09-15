using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>
    /// 届かせられない理由を、足す相手ごとに分ける判定。手立てを足す側と正本へ値を足す側とでは
    /// 動く相手が違うので、同じ数に混ぜると出力を読む側がどちらを動かせばよいか決められない。
    /// </summary>
    public sealed class RowCoverageRunnerTests
    {
        private const string Key = "PEPlugin.Pmx.IPXVertex.Wipe()";

        [Theory]
        [InlineData("この検査では受け手を作れないので呼び先まで届かせられない。", true, false)]
        [InlineData("この生成器は値を組み立てられないので呼び先まで届かせられない。", true, false)]
        [InlineData("正本が値を持たないので呼び先まで届かせられない。", false, true)]
        [InlineData("相手が居ないので呼び先まで届かせられない。", false, false)]
        public void EachReasonGoesToTheSideThatCanAddWhatItLacks(
            string basis, bool limited, bool missing)
        {
            ToolMapRow row = new ToolMapRow(
                Key, ToolMapEditKind.Read, null, basis, null, null, null);

            Assert.Equal(limited, RowCoverageRunner.Limited(row));
            Assert.Equal(missing, RowCoverageRunner.Missing(row));
        }
    }
}
