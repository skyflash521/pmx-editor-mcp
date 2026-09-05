using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class RowKindRuleTests
    {
        [Fact]
        public void AnAssignedSignatureTakesTheCommonContractKind()
        {
            Assert.Equal(
                ToolMapRowKind.CommonContract, RowKindRule.Of(MemberKind.Method, true));
        }

        [Theory]
        [InlineData(MemberKind.Event)]
        [InlineData(MemberKind.Property)]
        [InlineData(MemberKind.Field)]
        [InlineData(MemberKind.Constructor)]
        public void TheSpecialRuleTableComesBeforeTheMemberKind(MemberKind memberKind)
        {
            Assert.Equal(
                ToolMapRowKind.CommonContract, RowKindRule.Of(memberKind, true));
        }

        [Fact]
        public void AnEventTakesTheEventBranchKind()
        {
            Assert.Equal(ToolMapRowKind.EventBranch, RowKindRule.Of(MemberKind.Event, false));
        }

        [Theory]
        [InlineData(MemberKind.Property)]
        [InlineData(MemberKind.Field)]
        public void APropertyOrFieldTakesTheSchemaEmbeddedKind(MemberKind memberKind)
        {
            Assert.Equal(ToolMapRowKind.SchemaEmbedded, RowKindRule.Of(memberKind, false));
        }

        [Theory]
        [InlineData(MemberKind.Method)]
        [InlineData(MemberKind.Constructor)]
        public void AMethodOrConstructorTakesTheDirectDispatchKind(MemberKind memberKind)
        {
            Assert.Equal(ToolMapRowKind.DirectDispatch, RowKindRule.Of(memberKind, false));
        }
    }
}
