using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class OperationDirectionRuleTests
    {
        [Theory(Skip = "impl pending: 取得を表す名前で始まる戻り値つきメソッドを読み取りと判定する")]
        [InlineData("GetCount")]
        [InlineData("IsVisible")]
        [InlineData("HasChild")]
        [InlineData("CanApply")]
        [InlineData("FindBone")]
        [InlineData("SearchMorph")]
        public void 取得の名前で始まり戻り値を持てば読み取りになる(string memberName)
        {
            Assert.Equal(
                OperationDirection.Read,
                OperationDirectionRule.ForMethod(memberName, "System.Int32", false));
        }

        [Fact(Skip = "impl pending: 戻り値のないメソッドを書き込みと判定する")]
        public void 戻り値がないメソッドは書き込みになる()
        {
            Assert.Equal(
                OperationDirection.Write,
                OperationDirectionRule.ForMethod("GetCount", "System.Void", false));
        }

        [Fact(Skip = "impl pending: 出力引数を持つメソッドを書き込みと判定する")]
        public void 出力引数を持つメソッドは書き込みになる()
        {
            Assert.Equal(
                OperationDirection.Write,
                OperationDirectionRule.ForMethod("GetValue", "System.Boolean", true));
        }

        [Theory(Skip = "impl pending: 取得を表す名前で始まらないメソッドを書き込みと判定する")]
        [InlineData("SetThing")]
        [InlineData("Update")]
        [InlineData("Apply")]
        [InlineData("Remove")]
        public void 取得の名前で始まらなければ書き込みになる(string memberName)
        {
            Assert.Equal(
                OperationDirection.Write,
                OperationDirectionRule.ForMethod(memberName, "System.Int32", false));
        }

        [Theory(Skip = "impl pending: 取得を表す名前の判定を前方一致で行う")]
        [InlineData("Getter")]
        [InlineData("Issue")]
        public void 名前の判定は前方一致で行う(string memberName)
        {
            // 語の区切りを見ない前方一致であるため、取得を表さない名前も読み取りへ寄る。
            // 書き込みを読み取りと誤ると、対になる書き込み行が見つからず効果が導出されなく
            // なるだけで、誤って合格にはならない。
            Assert.Equal(
                OperationDirection.Read,
                OperationDirectionRule.ForMethod(memberName, "System.Int32", false));
        }

        [Fact(Skip = "impl pending: 取得アクセサーを持つプロパティを読み取りと判定する")]
        public void 取得アクセサーの有無でプロパティの向きが決まる()
        {
            Assert.Equal(OperationDirection.Read, OperationDirectionRule.ForProperty(true));
            Assert.Equal(OperationDirection.Write, OperationDirectionRule.ForProperty(false));
        }

        [Fact(Skip = "impl pending: プロパティとメソッド以外のメンバーを書き込みと判定する")]
        public void プロパティとメソッド以外は書き込みになる()
        {
            Assert.Equal(OperationDirection.Write, OperationDirectionRule.ForOtherMember());
        }
    }
}
