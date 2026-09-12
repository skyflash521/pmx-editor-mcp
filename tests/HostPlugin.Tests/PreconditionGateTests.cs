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
        public void AHeldModifierStopsEvenWithSomethingPicked()
        {
            string message;

            Assert.False(
                PreconditionGate.TryAccept(PreconditionKind.PickedObjects, 5, true, out message));
            Assert.Contains("修飾キー", message);
        }
    }
}
