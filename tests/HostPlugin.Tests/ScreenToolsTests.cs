using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmd;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 画面の選択と表示、リストの作り直し。どれも1回の呼び出しで済み、モデルの中身は変えない。
    /// </summary>
    public sealed class ScreenToolsTests : IDisposable
    {
        /// <summary>小数の突き合わせで見る桁。</summary>
        private const int Digits = 4;

        private readonly ComposedScreenFixture _fixture = new ComposedScreenFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void SelectingEverythingPutsEveryIndexOfThatKindIntoTheSelection()
        {
            Vertices(3);

            IDictionary<string, object> value = ComposedScreenFixture.Value(Select(
                Operation(ViewSelectElements.All),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex)));

            Assert.Equal(new[] { 0, 1, 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(3, value[ViewSelectElements.SelectedName]);
        }

        [Fact]
        public void ChoosingElementsPaintsTheViewAgain()
        {
            Vertices(3);

            Select(
                Operation(ViewSelectElements.All),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex));

            Assert.Equal(1, _fixture.View.Repaints);
            Assert.Equal(0, _fixture.View.Redraws);
        }

        [Fact]
        public void AChoiceThatCannotBeShownStillCountsAsDoneAndSaysSoInAWarning()
        {
            Vertices(3);
            _fixture.View.RefusesToPaint = true;

            IDictionary<string, object> envelope = Select(
                Operation(ViewSelectElements.All),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Equal(new[] { 0, 1, 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Contains(
                ScreenRefresh.NotShownWarning,
                ((object[])envelope[ToolEnvelope.WarningsName]).Cast<string>());
        }

        [Fact]
        public void AChoiceThatIsRefusedLeavesTheViewAsItWas()
        {
            Vertices(3);

            Select(
                Operation(ViewSelectElements.All),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, "いない種類"));

            Assert.Equal(0, _fixture.View.Repaints);
        }

        [Fact]
        public void InvertingSwapsTheOnesThatWerePickedForTheOnesThatWereNot()
        {
            Vertices(3);
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 1 };

            Select(
                Operation(ViewSelectElements.Invert),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex));

            Assert.Equal(new[] { 0, 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact]
        public void SelectingEverythingTakesEveryKindThatWasNamedInOneCall()
        {
            Vertices(2);
            Bones("根");

            IDictionary<string, object> value = ComposedScreenFixture.Value(Select(
                Operation(ViewSelectElements.All),
                ComposedScreenFixture.Given(
                    ViewSelectElements.KindsName,
                    new object[] { ElementKinds.Vertex, ElementKinds.Bone })));

            Assert.Equal(new[] { 0, 1 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(new[] { 0 }, _fixture.View.Selected[ElementKinds.Bone]);
            Assert.Equal(3, value[ViewSelectElements.SelectedName]);
        }

        [Fact]
        public void SelectingSeveralKindsSaysHowManyOfEachKindItTook()
        {
            Vertices(2);
            Bones("根");

            IDictionary<string, object> value = ComposedScreenFixture.Value(Select(
                Operation(ViewSelectElements.All),
                ComposedScreenFixture.Given(
                    ViewSelectElements.KindsName,
                    new object[] { ElementKinds.Vertex, ElementKinds.Bone })));

            IList<object> counts = (IList<object>)value[ViewSelectElements.CountsName];

            Assert.Equal(2, counts.Count);
            Assert.Equal(ElementKinds.Vertex, Row(counts[0])[ViewSelectElements.KindName]);
            Assert.Equal(2, Row(counts[0])[ViewSelectElements.SelectedName]);
            Assert.Equal(ElementKinds.Bone, Row(counts[1])[ViewSelectElements.KindName]);
            Assert.Equal(1, Row(counts[1])[ViewSelectElements.SelectedName]);
        }

        [Fact]
        public void InvertingSeveralKindsSwapsEachOfThemOnItsOwn()
        {
            Vertices(2);
            Bones("根", "子");
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0 };
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 1 };

            Select(
                Operation(ViewSelectElements.Invert),
                ComposedScreenFixture.Given(
                    ViewSelectElements.KindsName,
                    new object[] { ElementKinds.Vertex, ElementKinds.Bone }));

            Assert.Equal(new[] { 1 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(new[] { 0 }, _fixture.View.Selected[ElementKinds.Bone]);
        }

        [Fact]
        public void NamingSeveralKindsForAnOperationThatTakesOneIsRefused()
        {
            Vertices(2);

            IDictionary<string, object> envelope = Select(
                Operation(ViewSelectElements.Expand),
                ComposedScreenFixture.Given(
                    ViewSelectElements.KindsName, new object[] { ElementKinds.Vertex }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void NamingBothOneKindAndSeveralIsRefused()
        {
            Vertices(2);

            IDictionary<string, object> envelope = Select(
                Operation(ViewSelectElements.All),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex),
                ComposedScreenFixture.Given(
                    ViewSelectElements.KindsName, new object[] { ElementKinds.Vertex }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void NamingTheSameKindTwiceIsRefused()
        {
            Vertices(2);

            IDictionary<string, object> envelope = Select(
                Operation(ViewSelectElements.All),
                ComposedScreenFixture.Given(
                    ViewSelectElements.KindsName,
                    new object[] { ElementKinds.Vertex, ElementKinds.Vertex }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void ExpandingAddsTheVerticesThatShareAFaceWithTheSelection()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0 };

            Select(
                Operation(ViewSelectElements.Expand),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex));

            Assert.Equal(new[] { 0, 1, 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact]
        public void ReducingDropsTheVerticesThatTouchOnesOutsideTheSelection()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 2, 3, 0));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0, 1, 2 };

            Select(
                Operation(ViewSelectElements.Reduce),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex));

            Assert.Equal(new[] { 1 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact]
        public void TheChildChainAddsEveryBoneBelowTheOnesThatWerePicked()
        {
            IList<IPXBone> bones = Bones("根", "子", "孫");
            bones[1].Parent = bones[0];
            bones[2].Parent = bones[1];
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 0 };

            Select(
                Operation(ViewSelectElements.ChildChain),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Bone));

            Assert.Equal(new[] { 0, 1, 2 }, _fixture.View.Selected[ElementKinds.Bone]);
        }

        [Fact]
        public void TakingHalfTheModelKeepsOnlyOneSideOfTheAxis()
        {
            Vertex(-1f, 0f, 0f);
            Vertex(1f, 0f, 0f);
            Vertex(2f, 0f, 0f);

            Select(
                Operation(ViewSelectElements.HalfModel),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex),
                ComposedScreenFixture.Given(
                    ViewSelectElements.AxisName, ModelEditVertices.AxisX));

            Assert.Equal(new[] { 1, 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Theory]
        [InlineData("x", new[] { 2, 3 })]
        [InlineData("negativeX", new[] { 0, 1, 2 })]
        public void TheBoundaryMovesWhereTheModelIsHalved(string axis, int[] wanted)
        {
            Vertex(-1f, 0f, 0f);
            Vertex(1f, 0f, 0f);
            Vertex(1.5f, 0f, 0f);
            Vertex(2f, 0f, 0f);

            Select(
                Operation(ViewSelectElements.HalfModel),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex),
                ComposedScreenFixture.Given(ViewSelectElements.AxisName, axis),
                ComposedScreenFixture.Given(ViewSelectElements.BoundaryName, 1.5));

            Assert.Equal(wanted, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact]
        public void TheBoundaryIsRefusedForOtherOperations()
        {
            Vertex(0f, 0f, 0f);

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ComposedScreenFixture.Code(Select(
                    Operation(ViewSelectElements.All),
                    ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex),
                    ComposedScreenFixture.Given(ViewSelectElements.BoundaryName, 1.0))));
        }

        [Theory]
        [InlineData("intersect", new[] { 1 })]
        [InlineData("subtract", new[] { 0 })]
        [InlineData("add", new[] { 0, 1, 2 })]
        [InlineData("replace", new[] { 1, 2 })]
        public void TheModeCombinesTheNewSelectionWithTheOneAlreadyThere(string mode, int[] wanted)
        {
            Vertex(-1f, 0f, 0f);
            Vertex(1f, 0f, 0f);
            Vertex(2f, 0f, 0f);
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0, 1 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(Select(
                Operation(ViewSelectElements.HalfModel),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex),
                ComposedScreenFixture.Given(
                    ViewSelectElements.AxisName, ModelEditVertices.AxisX),
                ComposedScreenFixture.Given(ViewSelection.ModeName, mode)));

            Assert.Equal(wanted, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(wanted.Length, value[ViewSelectElements.SelectedName]);
        }

        [Fact]
        public void TheRelatedSelectionCanBeNarrowedToTheOneAlreadyThere()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 3, 4, 5 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(Related(
                Operation(ViewSelectRelated.MaterialToFaces),
                ComposedScreenFixture.Given(
                    ViewSelectRelated.MaterialIndicesName, new object[] { 0, 1 }),
                ComposedScreenFixture.Given(ViewSelection.ModeName, "intersect")));

            Assert.Equal(new[] { 3, 4, 5 }, _fixture.View.Selected[ElementKinds.Face]);
            Assert.Equal(1, value[ViewSelectRelated.SelectedName]);
        }

        [Fact]
        public void AModeTheToolDoesNotKnowIsRefused()
        {
            Vertices(1);

            IDictionary<string, object> envelope = Select(
                Operation(ViewSelectElements.All),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex),
                ComposedScreenFixture.Given(ViewSelection.ModeName, "xor"));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void SelectingWithoutSayingTheKindIsRefused()
        {
            Vertices(1);

            IDictionary<string, object> envelope = Select(Operation(ViewSelectElements.All));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void TheFacesMadeOnlyOfTheSelectedVerticesAreSelected()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0, 1, 2 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(Related(
                Operation(ViewSelectRelated.VerticesToFaces)));

            Assert.Equal(new[] { 0, 1, 2 }, _fixture.View.Selected[ElementKinds.Face]);
            Assert.Equal(1, value[ViewSelectRelated.SelectedName]);
        }

        [Fact]
        public void TheVerticesThatTheSelectedFacesUseAreSelected()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 3, 4, 5 };

            Related(Operation(ViewSelectRelated.FacesToVertices));

            Assert.Equal(new[] { 1, 2, 3 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact]
        public void TheSelectionThatTheRelatedStepReadFromStaysUnlessItIsLetGo()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 3, 4, 5 };

            Related(Operation(ViewSelectRelated.FacesToVertices));

            Assert.Equal(new[] { 1, 2, 3 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(new[] { 3, 4, 5 }, _fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact]
        public void TheSelectionThatTheRelatedStepReadFromIsLetGoWhenItIsAskedFor()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 3, 4, 5 };

            Related(
                Operation(ViewSelectRelated.FacesToVertices),
                ComposedScreenFixture.Given(ViewSelectRelated.ReleaseSourceName, true));

            Assert.Equal(new[] { 1, 2, 3 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Empty(_fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact]
        public void TheBonesThatTheWeightedVerticesWereReadFromAreLetGoWhenItIsAskedFor()
        {
            IList<IPXVertex> vertices = Vertices(2);
            IList<IPXBone> bones = Bones("一");
            Weigh(vertices[0], bones[0], 1f);
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 0 };

            Related(
                Operation(ViewSelectRelated.BonesToWeightedVertices),
                ComposedScreenFixture.Given(ViewSelectRelated.ReleaseSourceName, true));

            Assert.Equal(new[] { 0 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Empty(_fixture.View.Selected[ElementKinds.Bone]);
        }

        [Fact]
        public void AStepThatReadsNoSelectionLetsNothingGo()
        {
            IList<IPXVertex> vertices = Vertices(3);
            ((FakeVertex)vertices[1]).UV = new V2(0.5f, 0.5f);
            Faces(Face(vertices, 0, 1, 2));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            Related(
                Operation(ViewSelectRelated.UvRegionVertices),
                ComposedScreenFixture.Given(ViewSelectRelated.ReleaseSourceName, true),
                ComposedScreenFixture.Given(ViewSelectRelated.MinUName, 0.4),
                ComposedScreenFixture.Given(ViewSelectRelated.MaxUName, 0.6),
                ComposedScreenFixture.Given(ViewSelectRelated.MinVName, 0.4),
                ComposedScreenFixture.Given(ViewSelectRelated.MaxVName, 0.6));

            Assert.Equal(new[] { 1 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(new[] { 0, 1, 2 }, _fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact]
        public void ASelectionToLetGoThatIsNotTrueOrFalseIsRefused()
        {
            Vertices(1);

            IDictionary<string, object> envelope = Related(
                Operation(ViewSelectRelated.UnusedVertices),
                ComposedScreenFixture.Given(ViewSelectRelated.ReleaseSourceName, "はい"));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void TheFacesThatShareAnEdgeWithTheSelectedOnesAreAdded()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            Related(Operation(ViewSelectRelated.ExpandAdjacentFaces));

            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, _fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact]
        public void PickingFacesWhileTheyAreNotDrawnSaysSo()
        {
            IList<IPXVertex> vertices = Vertices(3);
            Faces(Face(vertices, 0, 1, 2));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0, 1, 2 };
            _fixture.Setting.Visible_SelectedFace = false;

            IDictionary<string, object> envelope = Related(
                Operation(ViewSelectRelated.VerticesToFaces));

            Assert.Contains(
                "選んだ面は画面に出ない",
                string.Join(" ", ComposedScreenFixture.Warnings(envelope)));
        }

        [Fact]
        public void PickingFacesWhileTheyAreDrawnSaysNothingExtra()
        {
            IList<IPXVertex> vertices = Vertices(3);
            Faces(Face(vertices, 0, 1, 2));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0, 1, 2 };

            IDictionary<string, object> envelope = Related(
                Operation(ViewSelectRelated.VerticesToFaces));

            Assert.Empty(ComposedScreenFixture.Warnings(envelope));
        }

        [Fact]
        public void PickingVerticesSaysNothingAboutFacesBeingDrawn()
        {
            Vertices(3);
            _fixture.Setting.Visible_SelectedFace = false;

            IDictionary<string, object> envelope = Select(
                Operation(ViewSelectElements.All),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex));

            Assert.Empty(ComposedScreenFixture.Warnings(envelope));
        }

        [Fact]
        public void TheImageIsTakenFromTheViewpointThatWasGiven()
        {
            _fixture.View.CameraPosition = new V3(0f, 0f, 0f);
            _fixture.View.CameraTarget = new V3(0f, 0f, 0f);
            _fixture.View.CameraUpVector = new V3(0f, 1f, 0f);

            Captured(
                ComposedScreenFixture.Given("position", new object[] { 1, 2, 3 }),
                ComposedScreenFixture.Given("target", new object[] { 0, 0, 0 }),
                ComposedScreenFixture.Given("upVector", new object[] { 0, 1, 0 }));

            Assert.Equal(1, _fixture.View.Shots);
            Assert.Equal(1f, _fixture.View.ShotFrom.X);
            Assert.Equal(2f, _fixture.View.ShotFrom.Y);
            Assert.Equal(3f, _fixture.View.ShotFrom.Z);
        }

        [Fact]
        public void TheViewpointGoesBackToWhereItWasAfterTheShot()
        {
            _fixture.View.CameraPosition = new V3(9f, 8f, 7f);
            _fixture.View.CameraTarget = new V3(1f, 1f, 1f);
            _fixture.View.CameraUpVector = new V3(0f, 1f, 0f);

            Captured(
                ComposedScreenFixture.Given("position", new object[] { 1, 2, 3 }),
                ComposedScreenFixture.Given("target", new object[] { 0, 0, 0 }),
                ComposedScreenFixture.Given("upVector", new object[] { 0, 1, 0 }));

            Assert.Equal(9f, _fixture.View.CameraPositionSet.X);
            Assert.Equal(8f, _fixture.View.CameraPositionSet.Y);
            Assert.Equal(7f, _fixture.View.CameraPositionSet.Z);
            Assert.Equal(1f, _fixture.View.CameraTargetSet.X);
        }

        [Fact]
        public void LeavingOutTheViewpointTakesTheShotFromWhereTheCameraIs()
        {
            _fixture.View.CameraPosition = new V3(5f, 5f, 5f);

            Captured();

            Assert.Equal(1, _fixture.View.Shots);
            Assert.Null(_fixture.View.CameraPositionSet);
        }

        [Fact]
        public void TheShotIsLetGoOnceItHasBeenPackedForTheAnswer()
        {
            Captured();

            Assert.Throws<ArgumentException>(() => _fixture.View.LastShot.Width);
        }

        [Fact]
        public void GivingOnlyPartOfTheViewpointIsRefused()
        {
            IDictionary<string, object> envelope = Captured(
                ComposedScreenFixture.Given("position", new object[] { 1, 2, 3 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
            Assert.Equal(0, _fixture.View.Shots);
        }

        [Fact]
        public void AViewpointThatIsNotThreeNumbersIsRefused()
        {
            IDictionary<string, object> envelope = Captured(
                ComposedScreenFixture.Given("position", new object[] { 1, 2 }),
                ComposedScreenFixture.Given("target", new object[] { 0, 0, 0 }),
                ComposedScreenFixture.Given("upVector", new object[] { 0, 1, 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
            Assert.Equal(0, _fixture.View.Shots);
        }

        [Fact]
        public void TheFacesOfThePickedMaterialAreSelected()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));

            Related(
                Operation(ViewSelectRelated.MaterialToFaces),
                ComposedScreenFixture.Given(
                    ViewSelectRelated.MaterialIndicesName, new object[] { 1 }));

            Assert.Equal(new[] { 3, 4, 5 }, _fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact]
        public void TheMaterialListSelectionSaysWhichMaterialsFacesToSelect()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.Form.SelectedMaterials = new[] { 1 };

            Related(
                Operation(ViewSelectRelated.MaterialToFaces),
                ComposedScreenFixture.Given(ViewSelectRelated.MaterialSelectedName, true));

            Assert.Equal(new[] { 3, 4, 5 }, _fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact]
        public void EachSelectedFaceReachesTheViewAsItsThreeCorners()
        {
            IList<IPXVertex> vertices = Vertices(5);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            Faces(Face(vertices, 2, 3, 4));

            Related(
                Operation(ViewSelectRelated.MaterialToFaces),
                ComposedScreenFixture.Given(
                    ViewSelectRelated.MaterialIndicesName, new object[] { 2 }));

            Assert.Equal(new[] { 6, 7, 8 }, _fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact]
        public void TheThreeCornersTheViewHoldsForAFaceAreReadAsThatOneFace()
        {
            IList<IPXVertex> vertices = Vertices(5);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            Faces(Face(vertices, 2, 3, 4));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 6, 7, 8 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(
                Related(Operation(ViewSelectRelated.FacesToVertices)));

            Assert.Equal(new[] { 2, 3, 4 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(3, value[ViewSelectRelated.SelectedName]);
        }

        [Fact]
        public void TheFacesOfEveryMaterialThatUsesTheSelectedVerticesAreSelected()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 3 };

            Related(Operation(ViewSelectRelated.VerticesToMaterials));

            Assert.Equal(new[] { 3, 4, 5 }, _fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact]
        public void EveryFaceOfTheMaterialsThatHoldTheSelectedFacesIsSelected()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            Related(Operation(ViewSelectRelated.FacesToMaterials));

            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, _fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact]
        public void TheFacesOfTheMaterialsThatDoNotHoldTheSelectedFacesAreSelected()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            Related(Operation(ViewSelectRelated.ExcludeFacesMaterials));

            Assert.Equal(new[] { 3, 4, 5 }, _fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact]
        public void TheFacesOfTheMaterialsThatHoldTheSelectedFacesAreTakenOut()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2, 3, 4, 5 };

            Related(Operation(ViewSelectRelated.ExcludeFacesMaterials));

            Assert.Empty(_fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact]
        public void TheVerticesThatNoFaceUsesAreSelected()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));

            Related(Operation(ViewSelectRelated.UnusedVertices));

            Assert.Equal(new[] { 3 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact]
        public void TheVerticesWhoseUvSitsInsideTheGivenRegionAreSelected()
        {
            IList<IPXVertex> vertices = Vertices(3);
            ((FakeVertex)vertices[0]).UV = new V2(0.1f, 0.5f);
            ((FakeVertex)vertices[1]).UV = new V2(0.5f, 0.5f);
            ((FakeVertex)vertices[2]).UV = new V2(0.5f, 0.9f);

            IDictionary<string, object> value = ComposedScreenFixture.Value(Related(
                Operation(ViewSelectRelated.UvRegionVertices),
                ComposedScreenFixture.Given(ViewSelectRelated.MinUName, 0.4),
                ComposedScreenFixture.Given(ViewSelectRelated.MaxUName, 0.6),
                ComposedScreenFixture.Given(ViewSelectRelated.MinVName, 0.4),
                ComposedScreenFixture.Given(ViewSelectRelated.MaxVName, 0.6)));

            Assert.Equal(new[] { 1 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(ElementKinds.Vertex, value[ViewSelectRelated.KindName]);
            Assert.Equal(1, value[ViewSelectRelated.SelectedName]);
        }

        [Theory]
        [InlineData(ViewSelectRelated.VerticesToFaces, ElementKinds.Face)]
        [InlineData(ViewSelectRelated.FacesToVertices, ElementKinds.Vertex)]
        [InlineData(ViewSelectRelated.ExpandAdjacentFaces, ElementKinds.Face)]
        [InlineData(ViewSelectRelated.MaterialToFaces, ElementKinds.Face)]
        [InlineData(ViewSelectRelated.VerticesToMaterials, ElementKinds.Face)]
        [InlineData(ViewSelectRelated.FacesToMaterials, ElementKinds.Face)]
        [InlineData(ViewSelectRelated.ExcludeFacesMaterials, ElementKinds.Face)]
        [InlineData(ViewSelectRelated.UnusedVertices, ElementKinds.Vertex)]
        [InlineData(ViewSelectRelated.EdgeScaleChangedVertices, ElementKinds.Vertex)]
        [InlineData(ViewSelectRelated.BonesToWeightedVertices, ElementKinds.Vertex)]
        [InlineData(ViewSelectRelated.UvRegionVertices, ElementKinds.Vertex)]
        public void EachRelatedSelectionSaysWhichKindItSelectedAgain(
            string operation, string kind)
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0, 1, 2 };
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0 };
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 0 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(
                Related(Aimed(operation).ToArray()));

            Assert.Equal(kind, value[ViewSelectRelated.KindName]);
        }

        [Fact]
        public void OnlyTheVerticesOfTheNamedMaterialsAreTakenOutOfTheUvRegion()
        {
            IList<IPXVertex> vertices = Vertices(4);
            foreach (IPXVertex vertex in vertices)
            {
                ((FakeVertex)vertex).UV = new V2(0.5f, 0.5f);
            }

            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));

            Related(
                Operation(ViewSelectRelated.UvRegionVertices),
                ComposedScreenFixture.Given(
                    ViewSelectRelated.MaterialIndicesName, new object[] { 1 }),
                ComposedScreenFixture.Given(ViewSelectRelated.MinUName, 0.0),
                ComposedScreenFixture.Given(ViewSelectRelated.MaxUName, 1.0),
                ComposedScreenFixture.Given(ViewSelectRelated.MinVName, 0.0),
                ComposedScreenFixture.Given(ViewSelectRelated.MaxVName, 1.0));

            Assert.Equal(new[] { 1, 2, 3 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact]
        public void TheWholeModelStaysTheUvRegionTargetWhenNoMaterialIsNamed()
        {
            IList<IPXVertex> vertices = Vertices(4);
            foreach (IPXVertex vertex in vertices)
            {
                ((FakeVertex)vertex).UV = new V2(0.5f, 0.5f);
            }

            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));

            Related(
                Operation(ViewSelectRelated.UvRegionVertices),
                ComposedScreenFixture.Given(ViewSelectRelated.MinUName, 0.0),
                ComposedScreenFixture.Given(ViewSelectRelated.MaxUName, 1.0),
                ComposedScreenFixture.Given(ViewSelectRelated.MinVName, 0.0),
                ComposedScreenFixture.Given(ViewSelectRelated.MaxVName, 1.0));

            Assert.Equal(new[] { 0, 1, 2, 3 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact]
        public void AUvRegionWhoseEndComesBeforeItsStartIsRefused()
        {
            Vertices(1);

            IDictionary<string, object> envelope = Related(
                Operation(ViewSelectRelated.UvRegionVertices),
                ComposedScreenFixture.Given(ViewSelectRelated.MinUName, 0.6),
                ComposedScreenFixture.Given(ViewSelectRelated.MaxUName, 0.4),
                ComposedScreenFixture.Given(ViewSelectRelated.MinVName, 0.0),
                ComposedScreenFixture.Given(ViewSelectRelated.MaxVName, 1.0));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void AUvRegionHandedToAnotherOperationIsRefused()
        {
            Vertices(1);

            IDictionary<string, object> envelope = Related(
                Operation(ViewSelectRelated.UnusedVertices),
                ComposedScreenFixture.Given(ViewSelectRelated.MinUName, 0.4));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void TheVerticesWhoseEdgeScaleIsNotOneAreSelected()
        {
            IList<IPXVertex> vertices = Vertices(3);
            ((FakeVertex)vertices[2]).EdgeScale = 0.5f;

            Related(Operation(ViewSelectRelated.EdgeScaleChangedVertices));

            Assert.Equal(new[] { 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact]
        public void TheVerticesWeighedToTheSelectedBonesAreSelected()
        {
            IList<IPXBone> bones = Bones("腕", "指");
            IList<IPXVertex> vertices = Vertices(3);
            Weigh(vertices[0], bones[0], 1f);
            Weigh(vertices[1], bones[1], 1f);
            vertices[2].Bone4 = bones[0];
            vertices[2].Weight4 = 1f;
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 0 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(Related(
                Operation("bonesToWeightedVertices")));

            Assert.Equal(new[] { 0, 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(2, value[ViewSelectRelated.SelectedName]);
            Assert.Equal(new[] { 0 }, _fixture.View.Selected[ElementKinds.Bone]);
        }

        [Fact]
        public void AVertexWeighedToSeveralOfTheSelectedBonesIsSelectedOnce()
        {
            IList<IPXBone> bones = Bones("腕", "指");
            IList<IPXVertex> vertices = Vertices(1);
            Weigh(vertices[0], bones[0], 0.5f);
            vertices[0].Bone2 = bones[1];
            vertices[0].Weight2 = 0.5f;
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 0, 1 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(Related(
                Operation("bonesToWeightedVertices")));

            Assert.Equal(new[] { 0 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(1, value[ViewSelectRelated.SelectedName]);
        }

        [Fact]
        public void ASlotThatHoldsOneOfTheSelectedBonesWithoutWeightIsNotSelected()
        {
            IList<IPXBone> bones = Bones("腕");
            IList<IPXVertex> vertices = Vertices(2);
            Weigh(vertices[0], bones[0], 1f);
            Weigh(vertices[1], bones[0], 0f);
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 0 };

            Related(Operation("bonesToWeightedVertices"));

            Assert.Equal(new[] { 0 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact]
        public void NoBoneSelectedClearsTheVerticesThatWereSelected()
        {
            IList<IPXBone> bones = Bones("腕");
            IList<IPXVertex> vertices = Vertices(1);
            Weigh(vertices[0], bones[0], 1f);
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(Related(
                Operation("bonesToWeightedVertices")));

            Assert.Empty(_fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(0, value[ViewSelectRelated.SelectedName]);
        }

        [Fact]
        public void NarrowingToAMaterialThatIsNotListedIsRefusedAndLeavesTheChecksAlone()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.Parts.Checked = new[] { 0 };
            _fixture.Parts.MaterialItemsCount = 1;
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 3 };

            IDictionary<string, object> envelope =
                Filter(Operation(ViewFilterDisplay.MaterialsFromVertices));

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(envelope));
            Assert.Equal(new[] { 0 }, _fixture.Parts.Checked);
        }

        [Fact]
        public void OnlyTheMaterialsThatUseTheSelectedVerticesStayShown()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.Parts.MaterialItemsCount = 2;
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 3 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(Filter(
                Operation(ViewFilterDisplay.MaterialsFromVertices)));

            Assert.Equal(new[] { 1 }, _fixture.Parts.Checked);
            Assert.Equal(1, value[ViewFilterDisplay.ShownName]);
        }

        [Fact]
        public void OnlyTheMaterialsThatHoldTheSelectedFacesStayShown()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            Filter(Operation(ViewFilterDisplay.MaterialsFromFaces));

            Assert.Equal(new[] { 0 }, _fixture.Parts.Checked);
        }

        [Fact]
        public void TheMaterialsThatHoldTheSelectedFacesAreTakenOutOfWhatIsShown()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.Parts.Checked = new[] { 0, 1 };
            _fixture.Parts.MaterialItemsCount = 2;
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            Filter(Operation(ViewFilterDisplay.ExcludeMaterialsFromFaces));

            Assert.Equal(new[] { 1 }, _fixture.Parts.Checked);
        }

        [Fact]
        public void NarrowingTheDisplayByMaterialWithoutTheMaterialListIsRefused()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.Parts.Checked = new[] { 0, 1 };
            _fixture.Parts.MaterialItemsCount = 0;
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            IDictionary<string, object> envelope =
                Filter(Operation(ViewFilterDisplay.MaterialsFromFaces));

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(envelope));
            Assert.Equal(new[] { 0, 1 }, _fixture.Parts.Checked);
        }

        [Fact]
        public void ShowingThePartsSelectWindowSaysHowManyItemsItsListsNowHold()
        {
            _fixture.Parts.Visible = false;
            _fixture.Parts.MaterialItemsCount = 62;
            _fixture.Parts.BoneItemsCount = 643;
            _fixture.Parts.ExpressionItemsCount = 30;

            IDictionary<string, object> value = ComposedScreenFixture.Value(_fixture.Call(
                ViewPartsSelectWindow.ToolName,
                ComposedScreenFixture.Arguments(
                    ComposedScreenFixture.Given(ViewPartsSelectWindow.VisibleName, true))));

            Assert.True(_fixture.Parts.Visible);
            Assert.Equal(true, value[ViewPartsSelectWindow.VisibleName]);
            Assert.Equal(62, value[ViewPartsSelectWindow.MaterialItemsName]);
            Assert.Equal(643, value[ViewPartsSelectWindow.BoneItemsName]);
            Assert.Equal(30, value[ViewPartsSelectWindow.ExpressionItemsName]);
        }

        [Fact]
        public void HidingThePartsSelectWindowLeavesItsListsAsTheyAre()
        {
            _fixture.Parts.Visible = true;

            IDictionary<string, object> value = ComposedScreenFixture.Value(_fixture.Call(
                ViewPartsSelectWindow.ToolName,
                ComposedScreenFixture.Arguments(
                    ComposedScreenFixture.Given(ViewPartsSelectWindow.VisibleName, false))));

            Assert.False(_fixture.Parts.Visible);
            Assert.Equal(false, value[ViewPartsSelectWindow.VisibleName]);
        }

        [Fact]
        public void APartsSelectWindowStateThatIsNotTrueOrFalseIsRefused()
        {
            IDictionary<string, object> envelope = _fixture.Call(
                ViewPartsSelectWindow.ToolName,
                ComposedScreenFixture.Arguments(
                    ComposedScreenFixture.Given(ViewPartsSelectWindow.VisibleName, 1)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void OnlyTheVerticesWhoseEdgeScaleIsNotOneStayShown()
        {
            IList<IPXVertex> vertices = Vertices(3);
            ((FakeVertex)vertices[1]).EdgeScale = 0.5f;

            IDictionary<string, object> value = ComposedScreenFixture.Value(Filter(
                Operation(ViewFilterDisplay.VerticesByEdgeScale)));

            Assert.Equal(new[] { 1 }, _fixture.View.Narrowed);
            Assert.Equal(1, value[ViewFilterDisplay.ShownName]);
        }

        [Fact]
        public void WhatTheRelatedSelectionCountedIsSaidWithTheKindOfElement()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(
                Related(Operation(ViewSelectRelated.FacesToVertices)));

            Assert.Equal(ElementKinds.Vertex, value[ViewSelectRelated.KindName]);
            Assert.Equal(3, value[ViewSelectRelated.SelectedName]);
        }

        [Fact]
        public void WhatReachingTheMaterialsOfTheSelectedFacesCountedIsTheirFaces()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(
                Related(Operation(ViewSelectRelated.FacesToMaterials)));

            Assert.Equal(ElementKinds.Face, value[ViewSelectRelated.KindName]);
            Assert.Equal(2, value[ViewSelectRelated.SelectedName]);
        }

        [Fact]
        public void WhatStaysShownIsSaidWithTheKindOfElement()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(
                Filter(Operation(ViewFilterDisplay.MaterialsFromFaces)));

            Assert.Equal(ElementKinds.Material, value[ViewFilterDisplay.KindName]);
            Assert.Equal(1, value[ViewFilterDisplay.ShownName]);
        }

        [Fact]
        public void WhatStaysShownByTheEdgeScaleIsCountedAsVertices()
        {
            IList<IPXVertex> vertices = Vertices(3);
            ((FakeVertex)vertices[1]).EdgeScale = 0.5f;

            IDictionary<string, object> value = ComposedScreenFixture.Value(Filter(
                Operation(ViewFilterDisplay.VerticesByEdgeScale)));

            Assert.Equal(ElementKinds.Vertex, value[ViewFilterDisplay.KindName]);
            Assert.Equal(1, value[ViewFilterDisplay.ShownName]);
        }

        [Fact]
        public void TheRotateCentreGoesToTheMiddleOfTheSelectedVertices()
        {
            Vertex(0f, 0f, 0f);
            Vertex(2f, 4f, 0f);
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0, 1 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(Centre(
                Operation(ViewSetCameraRotateCenter.Vertices)));

            Assert.Equal(
                new object[] { 1f, 2f, 0f }, (object[])value[ViewSetCameraRotateCenter.CentreName]);
            Near(1.0, _fixture.View.CameraRotateCenter.X);
        }

        [Fact]
        public void TheRotateCentreGoesToTheMiddleOfTheSelectedBones()
        {
            IList<IPXBone> bones = Bones("一", "二");
            ((FakeBone)bones[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)bones[1]).Position = new V3(0f, 6f, 0f);
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 0, 1 };

            Centre(Operation(ViewSetCameraRotateCenter.Bones));

            Near(3.0, _fixture.View.CameraRotateCenter.Y);
        }

        [Fact]
        public void TheRotateCentreGoesToTheMiddleOfTheSelectedFace()
        {
            IList<IPXVertex> vertices = Vertices(3);
            ((FakeVertex)vertices[1]).Position = new V3(3f, 0f, 0f);
            ((FakeVertex)vertices[2]).Position = new V3(0f, 3f, 0f);
            Faces(Face(vertices, 0, 1, 2));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            Centre(Operation(ViewSetCameraRotateCenter.Face));

            Near(1.0, _fixture.View.CameraRotateCenter.X);
            Near(1.0, _fixture.View.CameraRotateCenter.Y);
        }

        [Fact]
        public void TheRotateCentreCountsAVertexThatTwoFacesShareOncePerFace()
        {
            IList<IPXVertex> vertices = Vertices(4);
            for (int at = 0; at < 3; at++)
            {
                ((FakeVertex)vertices[at]).Position = new V3(0f, 0f, 0f);
            }

            ((FakeVertex)vertices[3]).Position = new V3(9f, 0f, 0f);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 0, 1, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2, 3, 4, 5 };

            Centre(Operation(ViewSetCameraRotateCenter.Face));

            Near(1.5, _fixture.View.CameraRotateCenter.X);
        }

        [Fact]
        public void FacingTheSelectedFacePutsTheViewpointOnTheSideItsNormalPointsAt()
        {
            IList<IPXVertex> vertices = Vertices(3);
            ((FakeVertex)vertices[0]).Position = new V3(0f, 0f, 0f);
            ((FakeVertex)vertices[1]).Position = new V3(3f, 0f, 0f);
            ((FakeVertex)vertices[2]).Position = new V3(0f, 3f, 0f);
            Faces(Face(vertices, 0, 1, 2));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            Centre(Operation(ViewSetCameraRotateCenter.FaceFront));

            Near(1.0, _fixture.View.CameraRotateCenter.X);
            Near(1.0, _fixture.View.CameraTargetSet.X);
            Near(1.0, _fixture.View.CameraPositionSet.X);
            Near(10.0, _fixture.View.CameraPositionSet.Z);
            Near(1.0, _fixture.View.CameraUpSet.Y);
        }

        [Fact]
        public void FacingSeveralFacesLooksAtTheMiddleOfTheCornersOfEachOfThem()
        {
            IList<IPXVertex> vertices = Vertices(4);
            ((FakeVertex)vertices[0]).Position = new V3(0f, 0f, 0f);
            ((FakeVertex)vertices[1]).Position = new V3(3f, 0f, 0f);
            ((FakeVertex)vertices[2]).Position = new V3(0f, 3f, 0f);
            ((FakeVertex)vertices[3]).Position = new V3(9f, 3f, 0f);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 1, 3, 2));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2, 3, 4, 5 };

            Centre(Operation(ViewSetCameraRotateCenter.FaceFront));

            Near(2.5, _fixture.View.CameraRotateCenter.X);
            Near(2.5, _fixture.View.CameraTargetSet.X);
        }

        [Fact]
        public void TakingTheCentreWithNothingSelectedIsRefused()
        {
            Vertices(2);

            IDictionary<string, object> envelope = Centre(
                Operation(ViewSetCameraRotateCenter.Vertices));

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void ReloadingBuildsTheDrawingAgain()
        {
            Vertices(1);

            _fixture.Call(ViewReloadModel.ToolName, ComposedScreenFixture.Arguments());

            Assert.Equal(1, _fixture.View.Redraws);
            Assert.Equal(1, _fixture.View.Repaints);
            Assert.Equal(1, _fixture.SubView.Redrawn);
        }

        [Fact]
        public void LoadingTheModelOnlyLeavesTheMotionAlone()
        {
            Vertices(1);

            _fixture.Call(
                ViewLoadVmdView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    ViewLoadVmdView.PartsName, ViewLoadVmdView.ModelOnly)));

            Assert.NotNull(_fixture.View.Loaded);
            Assert.Null(_fixture.View.Motion);
            Assert.Equal(0, _fixture.View.Plays);
        }

        [Fact]
        public void LoadingTheMotionAsWellStartsPlayingIt()
        {
            Vertices(1);

            _fixture.Call(
                ViewLoadVmdView.ToolName,
                ComposedScreenFixture.Arguments(
                    ComposedScreenFixture.Given(
                        ViewLoadVmdView.PartsName, ViewLoadVmdView.WholeMotion),
                    ComposedScreenFixture.Given(
                        ViewLoadVmdView.MotionPathName, @"C:\motions\walk.vmd")));

            Assert.Equal(@"C:\motions\walk.vmd", ((FakeVmd)_fixture.View.Motion).Path);
            Assert.Equal(1, _fixture.View.Plays);
        }

        [Theory]
        [InlineData(ViewLoadVmdView.ModelMotion, true, false, false)]
        [InlineData(ViewLoadVmdView.CameraMotion, false, true, false)]
        [InlineData(ViewLoadVmdView.LightMotion, false, false, true)]
        public void LoadingOneKindOfMotionDropsTheKeysOfTheOtherKinds(
            string parts, bool model, bool camera, bool light)
        {
            Vertices(1);
            _fixture.Builder.Motion.Fill();

            _fixture.Call(
                ViewLoadVmdView.ToolName,
                ComposedScreenFixture.Arguments(
                    ComposedScreenFixture.Given(ViewLoadVmdView.PartsName, parts),
                    ComposedScreenFixture.Given(
                        ViewLoadVmdView.MotionPathName, @"C:\motions\walk.vmd")));

            FakeVmd held = (FakeVmd)_fixture.View.Motion;
            Assert.Equal(model, held.Bone.Count > 0);
            Assert.Equal(camera, held.Camera.Count > 0);
            Assert.Equal(light, held.Light.Count > 0);
        }

        [Fact]
        public void LoadingTheModelFromAFileTakesThatOneInsteadOfTheOneBeingEdited()
        {
            Vertices(1);

            _fixture.Call(
                ViewLoadVmdView.ToolName,
                ComposedScreenFixture.Arguments(
                    ComposedScreenFixture.Given(
                        ViewLoadVmdView.PartsName, ViewLoadVmdView.ModelOnly),
                    ComposedScreenFixture.Given(
                        ViewLoadVmdView.ModelPathName, @"C:\models\other.pmx")));

            Assert.Equal(@"C:\models\other.pmx", _fixture.View.Loaded.FilePath);
        }

        [Fact]
        public void LoadingAnOlderModelFileTakesTheRouteThatReadsIt()
        {
            Vertices(1);

            _fixture.Call(
                ViewLoadVmdView.ToolName,
                ComposedScreenFixture.Arguments(
                    ComposedScreenFixture.Given(
                        ViewLoadVmdView.PartsName, ViewLoadVmdView.ModelOnly),
                    ComposedScreenFixture.Given(
                        ViewLoadVmdView.ModelPathName, @"C:\models\other.pmd")));

            Assert.Equal(@"C:\models\other.pmd", _fixture.Builder.OlderPath);
            Assert.Null(_fixture.View.Loaded);
        }

        [Fact]
        public void LoadingTheMotionWithoutTheFileIsRefused()
        {
            Vertices(1);

            IDictionary<string, object> envelope = _fixture.Call(
                ViewLoadVmdView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    ViewLoadVmdView.PartsName, ViewLoadVmdView.WholeMotion)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void ClearingTheMotionOnlyStopsThePlayingAndKeepsTheModel()
        {
            _fixture.View.Booted = true;

            _fixture.Call(
                ViewClearVmdView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    ViewClearVmdView.PartsName, ViewClearVmdView.MotionOnly)));

            Assert.Equal(1, _fixture.View.Stops);
            Assert.True(_fixture.View.Booted);
        }

        [Fact]
        public void ClearingTheModelAsWellHandsTheViewAMotionWithNothingInIt()
        {
            Vertices(1);
            _fixture.View.Booted = true;

            _fixture.Call(
                ViewClearVmdView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    ViewClearVmdView.PartsName, ViewClearVmdView.ModelAndMotion)));

            Assert.Equal(1, _fixture.View.Stops);
            Assert.NotNull(_fixture.View.Loaded);
            Assert.Null(((FakeVmd)_fixture.View.Motion).Path);
        }

        [Fact]
        public void EveryListIsBuiltAgainInOneCall()
        {
            IDictionary<string, object> value = ComposedScreenFixture.Value(
                _fixture.Call(
                    SessionUpdateAllLists.ToolName, ComposedScreenFixture.Arguments()));

            Assert.Contains(UpdateObject.All, _fixture.Form.Updated);
            Assert.Equal(
                _fixture.Form.Updated.Count, value[SessionUpdateAllLists.UpdatedName]);
        }

        [Fact]
        public void TheMaterialsBehindTheSelectedFacesAreCheckedInTheList()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 3, 4, 5 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(_fixture.Call(
                SessionSelectListsFromView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    SessionSelectListsFromView.KindsName,
                    new object[] { SessionSelectListsFromView.Material }))));

            Assert.Equal(new[] { 1 }, _fixture.Form.SelectedMaterials);
            Assert.Equal(1, value[SessionSelectListsFromView.SelectedName]);
        }

        [Fact]
        public void TheFirstBoneSelectedInTheViewIsPickedInTheBoneList()
        {
            Bones("一", "二", "三");
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 2, 0 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(_fixture.Call(
                SessionSelectListsFromView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    SessionSelectListsFromView.KindsName,
                    new object[] { SessionSelectListsFromView.Bone }))));

            Assert.Equal(2, _fixture.Form.SelectedBoneIndex);
            Assert.Equal(1, value[SessionSelectListsFromView.SelectedName]);
        }

        [Fact]
        public void WritingSeveralListsSaysHowManyOfEachKindItWrote()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            Bones("一", "二");
            _fixture.View.Selected[ElementKinds.Face] = new[] { 3, 4, 5 };
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 1 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(_fixture.Call(
                SessionSelectListsFromView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    SessionSelectListsFromView.KindsName,
                    new object[]
                    {
                        SessionSelectListsFromView.Material,
                        SessionSelectListsFromView.Bone,
                    }))));

            IList<object> counts = (IList<object>)value[SessionSelectListsFromView.CountsName];

            Assert.Equal(2, value[SessionSelectListsFromView.SelectedName]);
            Assert.Equal(2, counts.Count);
            Assert.Equal(
                SessionSelectListsFromView.Material,
                Row(counts[0])[SessionSelectListsFromView.KindName]);
            Assert.Equal(1, Row(counts[0])[SessionSelectListsFromView.SelectedName]);
            Assert.Equal(
                SessionSelectListsFromView.Bone,
                Row(counts[1])[SessionSelectListsFromView.KindName]);
            Assert.Equal(1, Row(counts[1])[SessionSelectListsFromView.SelectedName]);
        }

        [Fact]
        public void PickingNoBoneInTheViewLeavesTheBoneListWithNothingPicked()
        {
            Bones("一", "二");
            _fixture.Form.SelectedBoneIndex = 1;

            IDictionary<string, object> value = ComposedScreenFixture.Value(_fixture.Call(
                SessionSelectListsFromView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    SessionSelectListsFromView.KindsName,
                    new object[] { SessionSelectListsFromView.Bone }))));

            Assert.Equal(SessionSelectListsFromView.NoBone, _fixture.Form.SelectedBoneIndex);
            Assert.Equal(0, value[SessionSelectListsFromView.SelectedName]);
        }

        [Fact]
        public void AKindTheListToolDoesNotKnowIsRefused()
        {
            Vertices(1);

            IDictionary<string, object> envelope = _fixture.Call(
                SessionSelectListsFromView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    SessionSelectListsFromView.KindsName, new object[] { "いない種類" })));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void TheGroundNoticesWhenTheModelIsTouched()
        {
            ComposedScreenFixture ground = new ComposedScreenFixture();
            ground.Model.Bone.Add(new FakeBone("一"));
            ground.Watch();
            ((FakeBone)ground.Model.Bone[0]).Name = "二";

            Assert.Throws<InvalidOperationException>(() => ground.Dispose());
        }

        private static IDictionary<string, object> Row(object counted)
        {
            return (IDictionary<string, object>)counted;
        }

        private IDictionary<string, object> Select(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ViewSelectElements.ToolName, ComposedScreenFixture.Arguments(given));
        }

        private IDictionary<string, object> Captured(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ViewCaptureImage.ToolName, ComposedScreenFixture.Arguments(given));
        }

        private IDictionary<string, object> Related(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ViewSelectRelated.ToolName, ComposedScreenFixture.Arguments(given));
        }

        private IDictionary<string, object> Filter(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ViewFilterDisplay.ToolName, ComposedScreenFixture.Arguments(given));
        }

        private IDictionary<string, object> Centre(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ViewSetCameraRotateCenter.ToolName, ComposedScreenFixture.Arguments(given));
        }

        private static KeyValuePair<string, object> Operation(string operation)
        {
            return ComposedScreenFixture.Given(ComposedOperation.OperationName, operation);
        }

        /// <summary>その操作を呼ぶための引数。その操作のときだけ渡すものを添える。</summary>
        private static IList<KeyValuePair<string, object>> Aimed(string operation)
        {
            List<KeyValuePair<string, object>> given =
                new List<KeyValuePair<string, object>> { Operation(operation) };
            if (string.Equals(operation, ViewSelectRelated.MaterialToFaces, StringComparison.Ordinal))
            {
                given.Add(ComposedScreenFixture.Given(
                    ViewSelectRelated.MaterialIndicesName, new object[] { 0 }));
            }

            if (string.Equals(operation, ViewSelectRelated.UvRegionVertices, StringComparison.Ordinal))
            {
                given.Add(ComposedScreenFixture.Given(ViewSelectRelated.MinUName, 0.0));
                given.Add(ComposedScreenFixture.Given(ViewSelectRelated.MaxUName, 1.0));
                given.Add(ComposedScreenFixture.Given(ViewSelectRelated.MinVName, 0.0));
                given.Add(ComposedScreenFixture.Given(ViewSelectRelated.MaxVName, 1.0));
            }

            return given;
        }

        private IList<IPXVertex> Vertices(int count)
        {
            List<IPXVertex> made = new List<IPXVertex>();
            for (int at = 0; at < count; at++)
            {
                made.Add(Vertex(at, 0f, 0f));
            }

            return made;
        }

        private IPXVertex Vertex(float x, float y, float z)
        {
            FakeVertex vertex = new FakeVertex(x, y, z);
            _fixture.Model.Vertex.Add(vertex);

            return vertex;
        }

        private IList<IPXBone> Bones(params string[] names)
        {
            List<IPXBone> made = new List<IPXBone>();
            foreach (string name in names)
            {
                FakeBone bone = new FakeBone(name);
                _fixture.Model.Bone.Add(bone);
                made.Add(bone);
            }

            return made;
        }

        private static void Weigh(IPXVertex vertex, IPXBone bone, float weight)
        {
            vertex.Bone1 = bone;
            vertex.Weight1 = weight;
        }

        private static IPXFace Face(IList<IPXVertex> vertices, int first, int second, int third)
        {
            return new FakeFace(vertices[first], vertices[second], vertices[third]);
        }

        /// <summary>その面を持つ材質を1つ足す。面の位置はモデル全体の通し番号になる。</summary>
        private void Faces(params IPXFace[] faces)
        {
            FakeMaterial material = new FakeMaterial("材質" + _fixture.Model.Material.Count);
            foreach (IPXFace face in faces)
            {
                material.Faces.Add(face);
            }

            _fixture.Model.Material.Add(material);
        }

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }
    }
}
