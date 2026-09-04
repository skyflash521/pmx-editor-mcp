using System;
using PmxEditorMcp;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class ConfirmGateTests
    {
        [Theory]
        [InlineData(DangerKind.Shutdown)]
        [InlineData(DangerKind.Overwrite)]
        [InlineData(DangerKind.Reset)]
        public void EveryDangerousKindNeedsConfirm(DangerKind kind)
        {
            Assert.True(ConfirmGate.NeedsConfirm(kind));
        }

        [Fact]
        public void ACallThatIsNotDangerousNeverNeedsConfirm()
        {
            Assert.False(ConfirmGate.NeedsConfirm(DangerKind.None));
        }

        [Fact]
        public void AKindThatIsNotKnownStops()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ConfirmGate.NeedsConfirm((DangerKind)99));
        }

        [Fact]
        public void EmptyingTheOpenPmxNeedsConfirm()
        {
            Assert.True(ConfirmGate.ClearNeedsConfirm(emptiesOpenPmx: true));
        }

        [Fact]
        public void EmptyingSomethingOtherThanTheOpenPmxDoesNotNeedConfirm()
        {
            Assert.False(ConfirmGate.ClearNeedsConfirm(emptiesOpenPmx: false));
        }

        [Fact]
        public void AConfirmedDangerousCallPasses()
        {
            Assert.True(ConfirmGate.TryPass(
                DangerKind.Overwrite, confirm: true, code: out string code, message: out string message));
            Assert.Null(code);
            Assert.Null(message);
        }

        [Fact]
        public void AnUnconfirmedDangerousCallIsRefusedWithTheConfirmCode()
        {
            Assert.False(ConfirmGate.TryPass(
                DangerKind.Shutdown, confirm: false, code: out string code, message: out string message));
            Assert.Equal(ToolEnvelope.ConfirmRequired, code);
            Assert.Contains("エディタを終わらせる", message);
            Assert.Contains("confirm", message);
        }

        [Fact]
        public void TheRefusalNamesWhatTheCallWouldDo()
        {
            ConfirmGate.TryPass(
                DangerKind.Reset, confirm: false, code: out string _, message: out string message);

            Assert.Contains("空にする", message);
        }

        [Fact]
        public void AnUnconfirmedCallThatIsNotDangerousPasses()
        {
            Assert.True(ConfirmGate.TryPass(
                DangerKind.None, confirm: false, code: out string code, message: out string _));
            Assert.Null(code);
        }

        [Fact]
        public void AKindThatIsNotKnownStopsEvenWhenConfirmed()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ConfirmGate.TryPass(
                (DangerKind)99, confirm: true, code: out string _, message: out string _));
        }

        [Fact]
        public void ClearingTheOpenPmxWithoutConfirmIsRefused()
        {
            Assert.False(ConfirmGate.TryPassClear(
                emptiesOpenPmx: true, confirm: false, code: out string code, message: out string message));
            Assert.Equal(ToolEnvelope.ConfirmRequired, code);
            Assert.Contains("空にする", message);
        }

        [Fact]
        public void ClearingSomethingOtherThanTheOpenPmxPassesWithoutConfirm()
        {
            Assert.True(ConfirmGate.TryPassClear(
                emptiesOpenPmx: false, confirm: false, code: out string code, message: out string message));
            Assert.Null(code);
            Assert.Null(message);
        }

        [Fact]
        public void ClearingTheOpenPmxWithConfirmPasses()
        {
            Assert.True(ConfirmGate.TryPassClear(
                emptiesOpenPmx: true, confirm: true, code: out string code, message: out string _));
            Assert.Null(code);
        }
    }
}
