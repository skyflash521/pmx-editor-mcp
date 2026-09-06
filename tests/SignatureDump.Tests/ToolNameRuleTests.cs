using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolNameRuleTests
    {
        /// <summary>コネクタ型は自分1つを指すので、取得も更新も単数の名詞を採る。</summary>
        [Theory]
        [InlineData(TypeRole.Connector, ToolVerb.Get, "view_get_pmx_view")]
        [InlineData(TypeRole.Connector, ToolVerb.Update, "view_update_pmx_view")]
        [InlineData(TypeRole.OperationTarget, ToolVerb.List, "view_list_pmx_views")]
        [InlineData(TypeRole.OperationTarget, ToolVerb.Update, "view_update_pmx_views")]
        [InlineData(TypeRole.OperationTarget, ToolVerb.Add, "view_add_pmx_views")]
        [InlineData(TypeRole.HandleTarget, ToolVerb.Remove, "view_remove_pmx_views")]
        public void TheToolNameTakesTheNounThatTheVerbAndTheRoleChoose(
            TypeRole role, ToolVerb verb, string expected)
        {
            TypeRoleRecord record = new TypeRoleRecord(
                "N.IPmxView",
                role,
                "題材の根拠。",
                "pmx_view",
                role == TypeRole.Connector ? string.Empty : "pmx_views",
                CapabilityOwner.View);

            Assert.Equal(expected, ToolNameRule.OfRole(record, verb));
        }

        [Fact]
        public void TheToolNameRequiresARecord()
        {
            Assert.Throws<ArgumentNullException>(() => ToolNameRule.OfRole(null, ToolVerb.Get));
        }

        [Theory]
        [InlineData("Clear", "clear")]
        [InlineData("Normalize", "normalize")]
        [InlineData("CreateVmd", "create_vmd")]
        [InlineData("AppendPMDFile", "append_pmd_file")]
        [InlineData("SetIK", "set_ik")]
        [InlineData("op_Implicit", "op_implicit")]
        [InlineData("V3", "v3")]
        public void TheMemberNameBecomesASnakeCasedActionWord(string memberName, string expected)
        {
            Assert.Equal(expected, ToolNameRule.ActionWord(memberName));
        }

        [Fact]
        public void TheQualifierIsPlacedAfterTheActionWord()
        {
            Assert.Equal("model_clear_pmx", ToolNameRule.Compose("model", "clear", "pmx"));
        }

        [Fact]
        public void AToolWithoutAQualifierIsTheGroupAndTheActionWord()
        {
            Assert.Equal("session_run_plugin", ToolNameRule.Compose("session", "run_plugin", null));
            Assert.Equal("session_run_plugin", ToolNameRule.Compose("session", "run_plugin", string.Empty));
        }

        [Fact]
        public void AnActionWordSeenTwiceInAGroupCollides()
        {
            IDictionary<string, ISet<string>> colliding = ToolNameRule.Colliding(new[]
            {
                Word("model", "update"),
                Word("model", "update"),
                Word("model", "clear"),
            });

            Assert.Equal(new[] { "model" }, colliding.Keys);
            Assert.Equal(new[] { "update" }, colliding["model"]);
        }

        [Fact]
        public void TheSameActionWordInAnotherGroupDoesNotCollide()
        {
            IDictionary<string, ISet<string>> colliding = ToolNameRule.Colliding(new[]
            {
                Word("model", "update"),
                Word("view", "update"),
            });

            Assert.Empty(colliding);
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => ToolNameRule.ActionWord(null));
            Assert.Throws<ArgumentException>(() => ToolNameRule.ActionWord(" "));
            Assert.Throws<ArgumentNullException>(() => ToolNameRule.Compose(null, "clear", null));
            Assert.Throws<ArgumentNullException>(() => ToolNameRule.Compose("model", null, null));
            Assert.Throws<ArgumentNullException>(() => ToolNameRule.Colliding(null));
            Assert.Throws<ArgumentNullException>(
                () => ToolNameRule.Colliding(new[] { Word("model", null) }));
        }

        private static KeyValuePair<string, string> Word(string group, string actionWord)
        {
            return new KeyValuePair<string, string>(group, actionWord);
        }
    }
}
