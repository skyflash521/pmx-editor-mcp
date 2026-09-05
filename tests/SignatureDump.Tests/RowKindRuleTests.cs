using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class RowKindRuleTests
    {
        [Fact]
        public void AnAssignedSignatureTakesTheCommonContractKind()
        {
            Assert.Equal(
                ToolMapRowKind.CommonContract, RowKindRule.Of(MemberKind.Method, true, false));
        }

        [Theory]
        [InlineData(MemberKind.Event)]
        [InlineData(MemberKind.Property)]
        [InlineData(MemberKind.Field)]
        [InlineData(MemberKind.Constructor)]
        public void TheSpecialRuleTableComesBeforeTheMemberKind(MemberKind memberKind)
        {
            Assert.Equal(
                ToolMapRowKind.CommonContract, RowKindRule.Of(memberKind, true, false));
        }

        [Fact]
        public void AnEventTakesTheEventBranchKind()
        {
            Assert.Equal(ToolMapRowKind.EventBranch, RowKindRule.Of(MemberKind.Event, false, false));
        }

        [Theory]
        [InlineData(MemberKind.Property)]
        [InlineData(MemberKind.Field)]
        public void APropertyOrFieldTakesTheSchemaEmbeddedKind(MemberKind memberKind)
        {
            Assert.Equal(ToolMapRowKind.SchemaEmbedded, RowKindRule.Of(memberKind, false, false));
        }

        [Theory]
        [InlineData(MemberKind.Method)]
        [InlineData(MemberKind.Constructor)]
        public void AMethodOrConstructorTakesTheDirectDispatchKind(MemberKind memberKind)
        {
            Assert.Equal(ToolMapRowKind.DirectDispatch, RowKindRule.Of(memberKind, false, false));
        }

        [Fact]
        public void AConstructorOfATypeWithoutItsOwnToolTakesTheSchemaEmbeddedKind()
        {
            Assert.Equal(
                ToolMapRowKind.SchemaEmbedded,
                RowKindRule.Of(MemberKind.Constructor, false, true));
        }

        [Fact]
        public void AMethodOfATypeWithoutItsOwnToolStillTakesTheDirectDispatchKind()
        {
            Assert.Equal(
                ToolMapRowKind.DirectDispatch, RowKindRule.Of(MemberKind.Method, false, true));
        }
    }
}
