using System;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class UndoGateTests
    {
        [Fact]
        public void DuplicateEditMayAskToSuppress()
        {
            Assert.True(UndoGate.TryAcceptSuppress(
                EditKind.DuplicateEdit, suppressUndo: true,
                code: out string code, message: out string message));
            Assert.Null(code);
            Assert.Null(message);
        }

        [Theory]
        [InlineData(EditKind.Read)]
        [InlineData(EditKind.DirectChange)]
        [InlineData(EditKind.ViewSession)]
        public void AnyOtherKindAskingToSuppressIsAnInvalidArgument(EditKind kind)
        {
            Assert.False(UndoGate.TryAcceptSuppress(
                kind, suppressUndo: true, code: out string code, message: out string message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Contains("複製編集型", message);
        }

        [Theory]
        [InlineData(EditKind.Read)]
        [InlineData(EditKind.DuplicateEdit)]
        [InlineData(EditKind.DirectChange)]
        [InlineData(EditKind.ViewSession)]
        public void NotAskingToSuppressPassesForEveryKind(EditKind kind)
        {
            Assert.True(UndoGate.TryAcceptSuppress(
                kind, suppressUndo: false, code: out string code, message: out string _));
            Assert.Null(code);
        }

        [Theory]
        [InlineData(EditKind.Read)]
        [InlineData(EditKind.ViewSession)]
        public void WhatDoesNotChangeTheEditorRunsWithALeftover(EditKind kind)
        {
            Assert.True(UndoGate.TryProceedWithLeftover(
                kind, code: out string code, message: out string message, warning: out string warning));
            Assert.Null(code);
            Assert.Null(message);
            Assert.Contains("戻せていない", warning);
        }

        [Theory]
        [InlineData(EditKind.DuplicateEdit)]
        [InlineData(EditKind.DirectChange)]
        public void WhatChangesTheEditorIsRefusedWithALeftover(EditKind kind)
        {
            Assert.False(UndoGate.TryProceedWithLeftover(
                kind, code: out string code, message: out string message, warning: out string warning));
            Assert.Equal(ToolEnvelope.OperationFailed, code);
            Assert.Contains("実行しない", message);
            Assert.Contains("状態は未変更", message);
            Assert.Null(warning);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AKindThatIsNotKnownStopsWhateverTheRequestAsksFor(bool suppressUndo)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UndoGate.TryAcceptSuppress(
                (EditKind)99, suppressUndo, code: out string _, message: out string _));
        }

        [Fact]
        public void AKindThatIsNotKnownStops()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UndoGate.TryProceedWithLeftover(
                (EditKind)99, code: out string _, message: out string _, warning: out string _));
        }
    }
}
