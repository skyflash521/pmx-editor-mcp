using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class VmdNamesTests
    {
        [Theory]
        [InlineData(VmdNames.SetBoneNamesKey)]
        [InlineData(VmdNames.SetMorphNamesKey)]
        public void NamesWithABlankAreRefused(string rowKey)
        {
            string code;
            string message;

            Assert.False(VmdNames.TryCall(
                rowKey,
                new object[] { new[] { "a", string.Empty, "b" } },
                out code,
                out message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Contains("1", message);
        }

        [Theory]
        [InlineData(VmdNames.SetBoneNamesKey)]
        [InlineData(VmdNames.SetMorphNamesKey)]
        public void ANullListOfNamesIsRefused(string rowKey)
        {
            string code;
            string message;

            Assert.False(VmdNames.TryCall(rowKey, new object[] { null }, out code, out message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
        }

        [Fact]
        public void ANullNameIsRefused()
        {
            string code;
            string message;

            Assert.False(VmdNames.TryCall(
                VmdNames.SetBoneNamesKey,
                new object[] { new string[] { "a", null } },
                out code,
                out message));
        }

        [Fact]
        public void NamesThatRepeatAreRefused()
        {
            string code;
            string message;

            Assert.False(VmdNames.TryCall(
                VmdNames.SetMorphNamesKey,
                new object[] { new[] { "a", "b", "a" } },
                out code,
                out message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Contains("a", message);
        }

        [Fact]
        public void DistinctNonBlankNamesPass()
        {
            string code;
            string message;

            Assert.True(VmdNames.TryCall(
                VmdNames.SetBoneNamesKey,
                new object[] { new[] { "a", "b" } },
                out code,
                out message));
            Assert.True(VmdNames.TryCall(
                VmdNames.SetBoneNamesKey,
                new object[] { new string[0] },
                out code,
                out message));
        }

        [Fact]
        public void TheBuilderThatMakesAMotionFromNamesIsChecked()
        {
            string code;
            string message;

            Assert.False(VmdNames.TryCall(
                VmdNames.CreateVmdKey,
                new object[] { new[] { "a", string.Empty }, new[] { "m" } },
                out code,
                out message));
            Assert.False(VmdNames.TryCall(
                VmdNames.CreateVmdKey,
                new object[] { new[] { "a" }, new[] { "m", "m" } },
                out code,
                out message));
            Assert.True(VmdNames.TryCall(
                VmdNames.CreateVmdKey,
                new object[] { new[] { "a" }, null },
                out code,
                out message));
        }

        [Fact]
        public void OtherCallsPass()
        {
            string code;
            string message;

            Assert.True(VmdNames.TryCall(
                "PEPlugin.Vmd.IPEVmd.ClearKeys()",
                new object[0],
                out code,
                out message));
        }
    }
}
