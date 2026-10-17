using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class VmeResultsTests
    {
        private static bool Accepts(
            FakeVme vme, FakeVmeResult result, out string code, out string message)
        {
            return VmeResults.TryCall(
                VmeResults.SetKey, vme, new object[] { result }, out code, out message);
        }

        private static FakeVmeResult Recorded()
        {
            return new FakeVmeResult
            {
                Frames = 3,
                BoneEnabled = true,
                MorphEnabled = true,
                Bones = 1,
                Morphs = 1,
            };
        }

        [Fact]
        public void ABoneNameHeldTwiceIsRefusedByName()
        {
            string code;
            string message;

            Assert.False(Accepts(
                new FakeVme().Named(new[] { "a", "x", "x" }, null), Recorded(), out code, out message));
            Assert.Equal(ToolEnvelope.NotApplicable, code);
            Assert.Contains("ボーン", message);
            Assert.Contains("x", message);
        }

        [Fact]
        public void AMorphNameHeldTwiceIsRefusedByName()
        {
            string code;
            string message;

            Assert.False(Accepts(
                new FakeVme().Named(null, new[] { "m", "m" }), Recorded(), out code, out message));
            Assert.Contains("モーフ", message);
            Assert.Contains("m", message);
        }

        [Fact]
        public void ABoneWithoutANameIsRefused()
        {
            string code;
            string message;

            Assert.False(Accepts(
                new FakeVme().Named(new string[] { null }, null), Recorded(), out code, out message));
            Assert.Contains("名前を持たない", message);
        }

        [Fact]
        public void DistinctNamesPass()
        {
            string code;
            string message;

            Assert.True(Accepts(
                new FakeVme().Named(new[] { "a", "b" }, new[] { "m" }),
                Recorded(),
                out code,
                out message));
            Assert.Null(message);
        }

        [Fact]
        public void NamesThatDifferOnlyInCaseAreDistinct()
        {
            string code;
            string message;

            Assert.True(Accepts(
                new FakeVme().Named(new[] { "a", "A" }, null), Recorded(), out code, out message));
        }

        [Fact]
        public void ARepeatedBoneNameIsIgnoredWhenTheResultHoldsNoBones()
        {
            string code;
            string message;
            FakeVmeResult result = Recorded();
            result.BoneEnabled = false;

            Assert.True(Accepts(
                new FakeVme().Named(new[] { "x", "x" }, null), result, out code, out message));

            result = Recorded();
            result.Bones = 0;

            Assert.True(Accepts(
                new FakeVme().Named(new[] { "x", "x" }, null), result, out code, out message));
        }

        [Fact]
        public void ARepeatedMorphNameIsIgnoredWhenTheResultHoldsNoMorphs()
        {
            string code;
            string message;
            FakeVmeResult result = Recorded();
            result.MorphEnabled = false;

            Assert.True(Accepts(
                new FakeVme().Named(null, new[] { "m", "m" }), result, out code, out message));

            result = Recorded();
            result.Morphs = 0;

            Assert.True(Accepts(
                new FakeVme().Named(null, new[] { "m", "m" }), result, out code, out message));
        }

        [Fact]
        public void ARepeatedNameIsIgnoredWhenTheResultHasNoFrames()
        {
            string code;
            string message;
            FakeVmeResult result = Recorded();
            result.Frames = 0;

            Assert.True(Accepts(
                new FakeVme().Named(new[] { "x", "x" }, new[] { "m", "m" }),
                result,
                out code,
                out message));
        }

        [Fact]
        public void AnotherCallIsNotChecked()
        {
            string code;
            string message;

            Assert.True(VmeResults.TryCall(
                "PEPlugin.Vme.IPEVme.Run()",
                new FakeVme().Named(new[] { "x", "x" }, null),
                new object[] { Recorded() },
                out code,
                out message));
        }
    }
}
