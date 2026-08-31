using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class OperationDirectionRuleTests
    {
        [Theory]
        [InlineData("GetCount")]
        [InlineData("IsVisible")]
        [InlineData("HasChild")]
        [InlineData("CanApply")]
        [InlineData("FindBone")]
        [InlineData("SearchMorph")]
        public void GetterNamedMethodWithAReturnValueIsRead(string memberName)
        {
            Assert.Equal(
                OperationDirection.Read,
                OperationDirectionRule.ForMethod(memberName, "System.Int32", false));
        }

        [Fact]
        public void MethodWithoutAReturnValueIsWrite()
        {
            Assert.Equal(
                OperationDirection.Write,
                OperationDirectionRule.ForMethod("GetCount", "System.Void", false));
        }

        [Fact]
        public void MethodWithAnOutputArgumentIsWrite()
        {
            Assert.Equal(
                OperationDirection.Write,
                OperationDirectionRule.ForMethod("GetValue", "System.Boolean", true));
        }

        [Theory]
        [InlineData("SetThing")]
        [InlineData("Update")]
        [InlineData("Apply")]
        [InlineData("Remove")]
        public void MethodNotStartingWithAGetterNameIsWrite(string memberName)
        {
            Assert.Equal(
                OperationDirection.Write,
                OperationDirectionRule.ForMethod(memberName, "System.Int32", false));
        }

        [Theory]
        [InlineData("Getter")]
        [InlineData("Issue")]
        public void NameIsJudgedByPrefixMatch(string memberName)
        {
            // 語の区切りを見ない前方一致であるため、取得を表さない名前も読み取りへ寄る。
            // 書き込みを読み取りと誤ると、対になる書き込み行が見つからず効果が導出されなく
            // なるだけで、誤って合格にはならない。
            Assert.Equal(
                OperationDirection.Read,
                OperationDirectionRule.ForMethod(memberName, "System.Int32", false));
        }

        [Fact]
        public void PropertyDirectionFollowsTheGetAccessor()
        {
            Assert.Equal(OperationDirection.Read, OperationDirectionRule.ForProperty(true));
            Assert.Equal(OperationDirection.Write, OperationDirectionRule.ForProperty(false));
        }

        [Fact]
        public void MembersOtherThanPropertiesAndMethodsAreWrite()
        {
            Assert.Equal(OperationDirection.Write, OperationDirectionRule.ForOtherMember());
        }
    }
}
