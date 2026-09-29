using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class VmdSavingTests
    {
        [Fact]
        public void AVmdWithoutKeysTheReaderWouldMisplaceIsSaved()
        {
            FakeVmd vmd = new FakeVmd();
            vmd.Bone.Add(null);
            vmd.Morph.Add(null);
            string code;
            string message;

            Assert.True(VmdSaving.TryCall(VmdSaving.ToFileKey, vmd, out code, out message));
        }

        [Fact]
        public void AVmdHoldingCameraKeysIsRefusedAsNotApplicable()
        {
            FakeVmd vmd = new FakeVmd();
            vmd.Camera.Add(null);
            string code;
            string message;

            Assert.False(VmdSaving.TryCall(VmdSaving.ToFileKey, vmd, out code, out message));
            Assert.Equal(ToolEnvelope.NotApplicable, code);
            Assert.Contains("カメラ 1 件", message, System.StringComparison.Ordinal);
        }

        [Fact]
        public void AVmdHoldingLightKeysIsRefused()
        {
            FakeVmd vmd = new FakeVmd();
            vmd.Light.Add(null);
            string code;
            string message;

            Assert.False(VmdSaving.TryCall(VmdSaving.ToFileKey, vmd, out code, out message));
        }

        [Fact]
        public void AVmdHoldingSelfShadowKeysIsRefused()
        {
            FakeVmd vmd = new FakeVmd();
            vmd.SelfShadow.Add(null);
            string code;
            string message;

            Assert.False(VmdSaving.TryCall(VmdSaving.ToFileKey, vmd, out code, out message));
        }

        [Fact]
        public void AVmdHoldingVisibleIkKeysIsRefused()
        {
            FakeVmd vmd = new FakeVmd();
            vmd.VisibleIK.Add(null);
            string code;
            string message;

            Assert.False(VmdSaving.TryCall(VmdSaving.ToFileKey, vmd, out code, out message));
        }

        [Fact]
        public void EveryKindOfKeyHeldIsNamed()
        {
            FakeVmd vmd = new FakeVmd();
            vmd.Fill();
            string code;
            string message;

            Assert.False(VmdSaving.TryCall(VmdSaving.ToFileKey, vmd, out code, out message));
            Assert.Contains("カメラ", message, System.StringComparison.Ordinal);
            Assert.Contains("照明", message, System.StringComparison.Ordinal);
            Assert.Contains("セルフシャドウ", message, System.StringComparison.Ordinal);
            Assert.Contains("IK表示", message, System.StringComparison.Ordinal);
        }

        [Fact]
        public void OtherCallsPass()
        {
            FakeVmd vmd = new FakeVmd();
            vmd.Fill();
            string code;
            string message;

            Assert.True(VmdSaving.TryCall(
                "PEPlugin.Vmd.IPEVmd.ClearKeys()", vmd, out code, out message));
        }
    }
}
