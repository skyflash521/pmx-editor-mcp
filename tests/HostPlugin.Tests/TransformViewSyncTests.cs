using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class TransformViewSyncTests
    {
        [Fact]
        public void AnOpenTransformViewIsUpdated()
        {
            FakeTransformView view = new FakeTransformView { Visible = true };

            TransformViewSync.Refresh(view);

            Assert.Equal(1, view.Updates);
        }

        [Fact]
        public void AHiddenTransformViewIsLeftAlone()
        {
            FakeTransformView view = new FakeTransformView { Visible = false };

            TransformViewSync.Refresh(view);

            Assert.Equal(0, view.Updates);
        }

        [Fact]
        public void NoTransformViewIsNothingToDo()
        {
            TransformViewSync.Refresh(null);
        }
    }
}
