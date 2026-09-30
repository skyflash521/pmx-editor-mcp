using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class VmdPoseStatesTests
    {
        [Fact]
        public void ABoneIndexThatRepeatsIsRefusedWithItsPositionAndValue()
        {
            string code;
            string message;

            Assert.False(VmdPoseStates.TryCall(
                VmdPoseStates.CreateKey,
                new object[] { null, new[] { 4, 9, 4 } },
                out code,
                out message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Contains("boneIndices", message);
            Assert.Contains("2", message);
            Assert.Contains("4", message);
        }

        [Fact]
        public void DistinctBoneIndicesPass()
        {
            string code;
            string message;

            Assert.True(VmdPoseStates.TryCall(
                VmdPoseStates.CreateKey,
                new object[] { null, new[] { 4, 9, 0 } },
                out code,
                out message));
            Assert.True(VmdPoseStates.TryCall(
                VmdPoseStates.CreateKey,
                new object[] { null, new int[0] },
                out code,
                out message));
        }

        [Fact]
        public void OtherCallsPass()
        {
            string code;
            string message;

            Assert.True(VmdPoseStates.TryCall(
                VmdNames.SetBoneNamesKey,
                new object[] { null, new[] { 1, 1 } },
                out code,
                out message));
        }
    }
}
