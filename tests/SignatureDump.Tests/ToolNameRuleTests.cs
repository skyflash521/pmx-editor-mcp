using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolNameRuleTests
    {
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
