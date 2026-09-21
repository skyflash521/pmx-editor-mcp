using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ScreenRefreshTests
    {
        private const string SelectionRow =
            "PEPlugin.View.IPEPMDViewConnector.SetSelectedVertexIndices(System.Int32[])";

        private const string CameraRow = "PEPlugin.View.IPEPMDViewConnector.CameraPosition()";

        private const string MoveRow =
            "PEPlugin.View.IPEVertexEditConnector.Move(PEPlugin.Pmd.IPEVector3)";

        private const string ReadRow =
            "PEPlugin.View.IPEPMDViewConnector.GetSelectedVertexIndices()";

        [Fact]
        public void WritingTheSelectionNeedsTheViewDrawnAgain()
        {
            Assert.Equal(
                ScreenRefreshKind.Drawn,
                ScreenRefresh.Needed(EditKind.ViewSession, new[] { SelectionRow }));
        }

        [Fact]
        public void WritingTheCameraNeedsTheViewDrawnAgain()
        {
            Assert.Equal(
                ScreenRefreshKind.Drawn,
                ScreenRefresh.Needed(EditKind.ViewSession, new[] { CameraRow }));
        }

        [Fact]
        public void ReadingTheSameRowNeedsNothing()
        {
            Assert.Equal(
                ScreenRefreshKind.None, ScreenRefresh.Needed(EditKind.Read, new[] { CameraRow }));
        }

        [Fact]
        public void MovingTheVerticesNeedsTheListsAndTheModelRebuilt()
        {
            Assert.Equal(
                ScreenRefreshKind.Rebuilt,
                ScreenRefresh.Needed(EditKind.DirectChange, new[] { MoveRow }));
        }

        [Fact]
        public void ARowThatDoesNotShowOnTheScreenNeedsNothing()
        {
            Assert.Equal(
                ScreenRefreshKind.None,
                ScreenRefresh.Needed(EditKind.ViewSession, new[] { ReadRow }));
        }

        [Fact]
        public void TheHeaviestOfTheRowsIsWhatIsNeeded()
        {
            Assert.Equal(
                ScreenRefreshKind.Rebuilt,
                ScreenRefresh.Needed(EditKind.DirectChange, new[] { SelectionRow, MoveRow }));
        }

        [Fact]
        public void TheDuplicateEditIsShownByTheReflectionItselfAndNotByTheWrapper()
        {
            Assert.Equal(
                ScreenRefreshKind.None,
                ScreenRefresh.Needed(EditKind.DuplicateEdit, new[] { SelectionRow }));
        }

        [Fact]
        public void DrawingAgainOnlyPaintsTheView()
        {
            FakePmxView view = new FakePmxView();
            FakeFormConnector form = new FakeFormConnector();

            Refresh(view, form).Apply(ScreenRefreshKind.Drawn);

            Assert.Equal(1, view.Repaints);
            Assert.Equal(0, view.Redraws);
            Assert.Empty(form.Updated);
        }

        [Fact]
        public void RebuildingRemakesTheListsAndTheModelAndThenPaints()
        {
            FakePmxView view = new FakePmxView();
            FakeFormConnector form = new FakeFormConnector();

            Refresh(view, form).Apply(ScreenRefreshKind.Rebuilt);

            Assert.Equal(new[] { PEPlugin.Pmd.UpdateObject.All }, form.Updated);
            Assert.Equal(1, view.Redraws);
            Assert.Equal(1, view.Repaints);
        }

        [Fact]
        public void NeedingNothingTouchesNeitherTheViewNorTheLists()
        {
            FakePmxView view = new FakePmxView();
            FakeFormConnector form = new FakeFormConnector();

            Refresh(view, form).Apply(ScreenRefreshKind.None);

            Assert.Equal(0, view.Repaints);
            Assert.Equal(0, view.Redraws);
            Assert.Empty(form.Updated);
        }

        [Fact]
        public void AViewThatCannotBePaintedIsToldAsFalseInsteadOfThrowing()
        {
            FakePmxView view = new FakePmxView { RefusesToPaint = true };
            FakeFormConnector form = new FakeFormConnector();

            Assert.False(Refresh(view, form).Apply(ScreenRefreshKind.Drawn));
        }

        [Fact]
        public void PaintingTheViewIsToldAsTrue()
        {
            Assert.True(
                Refresh(new FakePmxView(), new FakeFormConnector())
                    .Apply(ScreenRefreshKind.Drawn));
        }

        [Fact]
        public void TheOpeningsThatCannotBeTakenAreLeftAlone()
        {
            ScreenRefresh refresh = new ScreenRefresh(() => null, () => null);

            refresh.Apply(ScreenRefreshKind.Rebuilt);
            refresh.Apply(ScreenRefreshKind.Drawn);
        }

        [Fact]
        public void TheListsAreRemadeEvenWhenTheViewIsNotThere()
        {
            FakeFormConnector form = new FakeFormConnector();

            new ScreenRefresh(() => null, () => form).Apply(ScreenRefreshKind.Rebuilt);

            Assert.Equal(new[] { PEPlugin.Pmd.UpdateObject.All }, form.Updated);
        }

        private static ScreenRefresh Refresh(FakePmxView view, FakeFormConnector form)
        {
            return new ScreenRefresh(() => view, () => form);
        }
    }
}
