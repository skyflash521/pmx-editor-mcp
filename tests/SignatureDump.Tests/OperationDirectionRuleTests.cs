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
        public void MethodNotStartingWithAGetterNameIsWrite(string memberName)
        {
            Assert.Equal(
                OperationDirection.Write,
                OperationDirectionRule.ForMethod(memberName, "System.Int32", false));
        }

        [Theory]
        [InlineData("Issue")]
        public void NameIsJudgedByPrefixMatch(string memberName)
        {
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
    }
}
