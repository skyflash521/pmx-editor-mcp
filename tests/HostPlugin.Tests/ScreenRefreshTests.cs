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

        [Theory]
        [InlineData("PEPlugin.Pmx.IPXPmx.Bone()", ElementKinds.Bone)]
        [InlineData("PEPlugin.Pmx.IPXPmx.Body()", ElementKinds.Body)]
        [InlineData("PEPlugin.Pmx.IPXPmx.Joint()", ElementKinds.Joint)]
        public void AReflectionIntoOneKindRemakesOnlyThatKindInTheView(string listRow, string kind)
        {
            FakePmxView view = new FakePmxView();
            FakeFormConnector form = new FakeFormConnector();

            Assert.True(Refresh(view, form).ApplyReflected(listRow));

            Assert.Equal(new[] { PEPlugin.Pmd.UpdateObject.All }, form.Updated);
            Assert.Equal(new[] { kind }, view.Remade);
            Assert.Equal(0, view.Redraws);
            Assert.Equal(1, view.Repaints);
        }

        [Theory]
        [InlineData("PEPlugin.Pmx.IPXPmx.Vertex()")]
        [InlineData("PEPlugin.Pmx.IPXPmx.Material()")]
        [InlineData("PEPlugin.Pmx.IPXPmx.Morph()")]
        [InlineData(null)]
        public void AnyOtherReflectionRebuildsTheWholeModel(string listRow)
        {
            FakePmxView view = new FakePmxView();
            FakeFormConnector form = new FakeFormConnector();

            Assert.True(Refresh(view, form).ApplyReflected(listRow));

            Assert.Equal(new[] { PEPlugin.Pmd.UpdateObject.All }, form.Updated);
            Assert.Empty(view.Remade);
            Assert.Equal(1, view.Redraws);
            Assert.Equal(1, view.Repaints);
        }

        [Fact]
        public void RewritingTheVerticesTellsTheWeightsTooSinceTheUpdateCanWriteThem()
        {
            Assert.Equal(
                new[] { ElementKinds.Vertex, ScreenRefresh.WeightKind },
                ScreenRefresh.RewrittenIn("PEPlugin.Pmx.IPXPmx.Vertex()"));
        }

        [Theory]
        [InlineData("PEPlugin.Pmx.IPXPmx.Bone()", ElementKinds.Bone)]
        [InlineData("PEPlugin.Pmx.IPXPmx.Body()", ElementKinds.Body)]
        [InlineData("PEPlugin.Pmx.IPXPmx.Joint()", ElementKinds.Joint)]
        public void RewritingTheItemsOfAListTellsTheKindItHolds(string listRow, string kind)
        {
            Assert.Equal(new[] { kind }, ScreenRefresh.RewrittenIn(listRow));
        }

        [Theory]
        [InlineData("PEPlugin.Pmx.IPXPmx.Material()")]
        [InlineData("PEPlugin.Pmx.IPXPmx.Morph()")]
        [InlineData(null)]
        public void RewritingTheItemsOfAnyOtherListTellsNoKind(string listRow)
        {
            Assert.Null(ScreenRefresh.RewrittenIn(listRow));
        }

        [Fact]
        public void RewritingTheKindsInPlaceRemakesOnlyThoseKindsInTheView()
        {
            FakePmxView view = new FakePmxView();
            FakeFormConnector form = new FakeFormConnector();

            Assert.True(Refresh(view, form).ApplyRewritten(new[]
            {
                ElementKinds.Vertex, ElementKinds.Bone, ElementKinds.Body, ElementKinds.Joint,
            }));

            Assert.Equal(new[] { PEPlugin.Pmd.UpdateObject.All }, form.Updated);
            Assert.Equal(
                new[] { ElementKinds.Vertex, ElementKinds.Bone, ElementKinds.Body, ElementKinds.Joint },
                view.Remade);
            Assert.Equal(0, view.Redraws);
            Assert.Equal(1, view.Repaints);
        }

        [Fact]
        public void RewritingAKindWithoutItsOwnRemakeRebuildsTheWholeModel()
        {
            FakePmxView view = new FakePmxView();
            FakeFormConnector form = new FakeFormConnector();

            Assert.True(Refresh(view, form).ApplyRewritten(
                new[] { ElementKinds.Bone, ElementKinds.Material }));

            Assert.Empty(view.Remade);
            Assert.Equal(1, view.Redraws);
            Assert.Equal(1, view.Repaints);
        }

        private static ScreenRefresh Refresh(FakePmxView view, FakeFormConnector form)
        {
            return new ScreenRefresh(() => view, () => form);
        }
    }
}
