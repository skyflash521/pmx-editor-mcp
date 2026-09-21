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
        public void TouchingAListThatHasNotBeenBuiltStops()
        {
            string message;

            Assert.False(
                PreconditionGate.TryAccept(PreconditionKind.ListedParts, 0, false, out message));
            Assert.Contains("絞込の窓を一度表示するまで組まれず", message);
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
        public void AHeldModifierStopsEvenWithSomethingPicked()
        {
            string message;

            Assert.False(
                PreconditionGate.TryAccept(PreconditionKind.PickedObjects, 5, true, out message));
            Assert.Contains("修飾キー", message);
        }
    }
}
