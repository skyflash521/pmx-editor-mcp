using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class RowKindRuleTests
    {
        [Fact]
        public void AnAssignedSignatureTakesTheCommonContractKind()
        {
            Assert.Equal(
                ToolMapRowKind.CommonContract, RowKindRule.Of(MemberKind.Method, true, false, false, true, false));
        }

        [Theory]
        [InlineData(MemberKind.Event)]
        [InlineData(MemberKind.Property)]
        [InlineData(MemberKind.Field)]
        [InlineData(MemberKind.Constructor)]
        public void TheSpecialRuleTableComesBeforeTheMemberKind(MemberKind memberKind)
        {
            Assert.Equal(
                ToolMapRowKind.CommonContract, RowKindRule.Of(memberKind, true, false, false, true, false));
        }

        [Fact]
        public void AnEventTakesTheEventBranchKind()
        {
            Assert.Equal(ToolMapRowKind.EventBranch, RowKindRule.Of(MemberKind.Event, false, false, false, true, false));
        }

        [Theory]
        [InlineData(MemberKind.Property)]
        [InlineData(MemberKind.Field)]
        public void APropertyOrFieldTakesTheSchemaEmbeddedKind(MemberKind memberKind)
        {
            Assert.Equal(
                ToolMapRowKind.SchemaEmbedded,
                RowKindRule.Of(memberKind, false, false, false, true, false));
        }

        [Theory]
        [InlineData(MemberKind.Method)]
        [InlineData(MemberKind.Constructor)]
        public void AMethodOrConstructorTakesTheDirectDispatchKind(MemberKind memberKind)
        {
            Assert.Equal(
                ToolMapRowKind.DirectDispatch,
                RowKindRule.Of(memberKind, false, false, false, true, false));
        }

        [Fact]
        public void AConstructorOfATypeWithoutItsOwnToolTakesTheSchemaEmbeddedKind()
        {
            Assert.Equal(
                ToolMapRowKind.SchemaEmbedded,
                RowKindRule.Of(MemberKind.Constructor, false, true, false, true, false));
        }

        [Theory]
        [InlineData(MemberKind.Property)]
        [InlineData(MemberKind.Field)]
        public void APropertyOnTheWayToSuchInstancesTakesTheRoleAccessKind(
            MemberKind memberKind)
        {
            Assert.Equal(
                ToolMapRowKind.RoleAccess,
                RowKindRule.Of(memberKind, false, false, true, false, true));
        }

        [Theory]
        [InlineData(MemberKind.Property)]
        [InlineData(MemberKind.Field)]
        public void APropertyThatOnlyPointsAtSuchInstancesTakesTheSchemaEmbeddedKind(
            MemberKind memberKind)
        {
            Assert.Equal(
                ToolMapRowKind.SchemaEmbedded,
                RowKindRule.Of(memberKind, false, false, true, false, false));
        }

        [Theory]
        [InlineData(MemberKind.Property)]
        [InlineData(MemberKind.Field)]
        public void APropertyThatHoldsAnInstanceHandlesPointAtTakesTheDirectDispatchKind(
            MemberKind memberKind)
        {
            Assert.Equal(
                ToolMapRowKind.DirectDispatch, RowKindRule.Of(memberKind, false, false, true, true, false));
        }

        [Fact]
        public void APropertyThatHoldsOneOfAHandledTypeIsResolvedToTheDirectDispatchKind()
        {
            Assert.Equal(ToolMapRowKind.DirectDispatch, Resolved(Held, One));
        }

        [Fact]
        public void APropertyThatListsAHandledTypeIsResolvedToTheRoleAccessKind()
        {
            Assert.Equal(ToolMapRowKind.RoleAccess, Resolved(HeldList, Many));
        }

        [Fact]
        public void AMethodThatReachesATypeWithItsOwnToolStillTakesTheDirectDispatchKind()
        {
            Assert.Equal(
                ToolMapRowKind.DirectDispatch, RowKindRule.Of(MemberKind.Method, false, false, true, true, false));
        }

        [Fact]
        public void AMethodOfATypeWithoutItsOwnToolStillTakesTheDirectDispatchKind()
        {
            Assert.Equal(
                ToolMapRowKind.DirectDispatch, RowKindRule.Of(MemberKind.Method, false, true, false, true, false));
        }

        private const string Held = "N.IHeld";

        private const string HeldList = "System.Collections.Generic.IList<N.IHeld>";

        private const string One = "N.IOwner.Held()";

        private const string Many = "N.IOwner.Items()";

        /// <summary>行の外の材料から、その行キーが採る種別を引く。</summary>
        private static ToolMapRowKind Resolved(string valueType, string rowKey)
        {
            SignatureRecord signature = new SignatureRecord(
                rowKey,
                "N.IOwner",
                MemberKind.Property,
                rowKey == One ? "Held" : "Items",
                false,
                0,
                new ParameterRecord[0],
                valueType,
                true,
                false,
                OperationDirection.Read);

            return RowKindRule.Resolve(
                ToolMapJsonReader.Read(
                    @"{ ""rows"": [ { ""signatureKey"": """ + rowKey + @""","
                        + @" ""editKind"": ""read"", ""basis"": ""持っているものを返すだけである。"" } ] }"),
                new Dictionary<string, SignatureRecord>(StringComparer.Ordinal)
                {
                    { rowKey, signature },
                },
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(new[] { Held }, StringComparer.Ordinal),
                new HashSet<string>(new[] { Held }, StringComparer.Ordinal),
                new HashSet<string>(new[] { Many }, StringComparer.Ordinal),
                new HashSet<string>(new[] { Many }, StringComparer.Ordinal))[rowKey];
        }
    }
}
