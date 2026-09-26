using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>呼ぶ前に確かめることを満たしているかの判じ方。</summary>
    public sealed class PreconditionGateTests
    {
        [Fact]
        public void NothingToCheckPasses()
        {
            string message;

            Assert.True(PreconditionGate.TryAccept(PreconditionKind.None, 0, true, out message));
            Assert.Null(message);
        }

        [Fact]
        public void SomethingPickedWithoutAModifierPasses()
        {
            string message;

            Assert.True(
                PreconditionGate.TryAccept(PreconditionKind.PickedObjects, 1, false, out message));
            Assert.Null(message);
        }

        [Fact]
        public void NothingPickedStops()
        {
            string message;

            Assert.False(
                PreconditionGate.TryAccept(PreconditionKind.PickedObjects, 0, false, out message));
            Assert.Contains("選ばれていない", message);
        }

        [Fact]
        public void NotBeingAbleToCountWhatIsPickedStops()
        {
            string message;

            Assert.False(
                PreconditionGate.TryAccept(
                    PreconditionKind.PickedObjects, null, false, out message));
            Assert.Contains("数えられなかった", message);
        }

        [Fact]
        public void ClosingWithNothingToUndoPasses()
        {
            string message;

            Assert.True(
                PreconditionGate.TryAccept(PreconditionKind.SavedEdits, 0, true, out message));
            Assert.Null(message);
        }

        [Fact]
        public void ClosingWithSomethingToUndoStops()
        {
            string message;

            Assert.False(
                PreconditionGate.TryAccept(PreconditionKind.SavedEdits, 2, false, out message));
            Assert.Contains("取り消せる編集が残っている", message);
            Assert.Contains("ことがあり", message);
        }

        [Fact]
        public void ClosingWithoutKnowingWhatCanBeUndoneStops()
        {
            string message;

            Assert.False(
                PreconditionGate.TryAccept(PreconditionKind.SavedEdits, null, false, out message));
            Assert.Contains("読めなかった", message);
        }

        [Fact]
        public void TouchingAListWithSomethingOnItPasses()
        {
            string message;

            Assert.True(
                PreconditionGate.TryAccept(PreconditionKind.ListedParts, 62, true, out message));
            Assert.Null(message);
        }

        [Fact]
        public void PointingPastTheItemsThatAreListedStops()
        {
            string message;

            Assert.False(PreconditionGate.TryAccept(
                PreconditionKind.ListedParts, 62, false, new long[] { 0, 62 }, out message));
            Assert.Contains("62", message);
        }

        [Fact]
        public void PointingBelowTheFirstItemStops()
        {
            string message;

            Assert.False(PreconditionGate.TryAccept(
                PreconditionKind.ListedParts, 62, false, new long[] { -1 }, out message));
            Assert.NotNull(message);
        }

        [Fact]
        public void PointingAtTheItemsThatAreListedGoesThrough()
        {
            string message;

            Assert.True(PreconditionGate.TryAccept(
                PreconditionKind.ListedParts, 62, false, new long[] { 0, 61 }, out message));
            Assert.Null(message);
        }

        [Fact]
        public void TouchingAListThatHasNotBeenBuiltStops()
        {
            string message;

            Assert.False(
                PreconditionGate.TryAccept(PreconditionKind.ListedParts, 0, false, out message));
            Assert.Contains("絞込の窓を一度表示するまで組まれず", message);
            Assert.Contains(ViewPartsSelectWindow.ToolName, message);
        }

        [Fact]
        public void TouchingAListWhoseItemsCannotBeCountedStops()
        {
            string message;

            Assert.False(
                PreconditionGate.TryAccept(PreconditionKind.ListedParts, null, false, out message));
            Assert.Contains("読めなかった", message);
        }

        [Fact]
        public void GoingThroughTheHistoryWithSomethingLeftPasses()
        {
            string message;

            Assert.True(PreconditionGate.TryAccept(PreconditionKind.UndoHistory, 1, true, out message));
            Assert.Null(message);
        }

        [Fact]
        public void GoingThroughTheHistoryWithNothingLeftStops()
        {
            string message;

            Assert.False(PreconditionGate.TryAccept(PreconditionKind.UndoHistory, 0, false, out message));
            Assert.Contains("戻せる操作が残っていない", message);
        }

        [Fact]
        public void GoingThroughTheHistoryWithoutKnowingWhatIsLeftStops()
        {
            string message;

            Assert.False(PreconditionGate.TryAccept(PreconditionKind.UndoHistory, null, false, out message));
            Assert.Contains("読めなかった", message);
        }

        [Fact]
        public void MovingAChosenBoneWithoutAModifierPasses()
        {
            string message;

            Assert.True(PreconditionGate.TryAccept(PreconditionKind.TransformedBone, 0, false, out message));
            Assert.Null(message);
        }

        [Fact]
        public void MovingWithNoBoneChosenStops()
        {
            string message;

            Assert.False(PreconditionGate.TryAccept(PreconditionKind.TransformedBone, -1, false, out message));
            Assert.Contains("ボーンが選ばれていない", message);
        }

        [Fact]
        public void MovingAChosenBoneWithAModifierHeldStops()
        {
            string message;

            Assert.False(PreconditionGate.TryAccept(PreconditionKind.TransformedBone, 3, true, out message));
            Assert.Contains("修飾キー", message);
        }

        [Fact]
        public void MovingWithoutKnowingWhichBoneIsChosenStops()
        {
            string message;

            Assert.False(PreconditionGate.TryAccept(PreconditionKind.TransformedBone, null, false, out message));
            Assert.Contains("読めなかった", message);
        }

        [Fact]
        public void AHeldModifierStopsEvenWithSomethingPicked()
        {
            string message;

            Assert.False(
                PreconditionGate.TryAccept(PreconditionKind.PickedObjects, 5, true, out message));
            Assert.Contains("修飾キー", message);
        }
    }
}
