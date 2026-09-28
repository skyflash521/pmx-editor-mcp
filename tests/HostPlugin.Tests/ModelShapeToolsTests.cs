using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 面と材質と頂点の形を変える5つ。どれも1回の呼び出しで、1回のまとめての反映に収まる。
    /// </summary>
    [Collection(TimedCollection.Name)]
    public sealed class ModelShapeToolsTests : IDisposable
    {
        private const int ManyFaces = 20000;

        private const int StripQuads = 3000;

        private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(2);

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void AFaceThatDoesNotHaveThreeDistinctVerticesIsDropped()
        {
            IList<IPXVertex> vertices = Vertices(3);
            FakeMaterial material = Material("材質", Face(vertices, 0, 1, 2), Face(vertices, 0, 0, 1));

            IDictionary<string, object> value = ComposedEditFixture.Value(Clean(
                Operation(ModelCleanFaces.Invalid),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal(new[] { new[] { 0, 1, 2 } }, Places(material));
            Assert.Equal(1, value[ModelCleanFaces.RemovedName]);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void TheSameThreeVerticesInTwoMaterialsCountAsOverlappingForTheWholeModel()
        {
            IList<IPXVertex> vertices = Vertices(3);
            FakeMaterial first = Material("一", Face(vertices, 0, 1, 2));
            FakeMaterial second = Material("二", Face(vertices, 0, 1, 2));

            IDictionary<string, object> value = ComposedEditFixture.Value(Clean(
                Operation(ModelCleanFaces.Duplicate),
                ComposedEditFixture.Given("all", true)));

            Assert.Single(Now(first).Faces);
            Assert.Empty(Now(second).Faces);
            Assert.Equal(1, value[ModelCleanFaces.RemovedName]);
        }

        [Fact]
        public void TheOverlappingFaceThatSurvivesIsTheFirstOneInTheMaterial()
        {
            IList<IPXVertex> vertices = Vertices(3);
            FakeMaterial material = Material(
                "材質", Face(vertices, 0, 1, 2), Face(vertices, 1, 2, 0));

            Clean(
                Operation(ModelCleanFaces.DuplicateInMaterial),
                ComposedEditFixture.Given("all", true));

            Assert.Same(NowAll(vertices)[0], Assert.Single(Now(material).Faces).Vertex1);
        }

        [Fact]
        public void PerMaterialOverlapLeavesTheSameThreeVerticesInAnotherMaterialAlone()
        {
            IList<IPXVertex> vertices = Vertices(3);
            FakeMaterial first = Material("一", Face(vertices, 0, 1, 2), Face(vertices, 0, 1, 2));
            FakeMaterial second = Material("二", Face(vertices, 0, 1, 2));

            IDictionary<string, object> value = ComposedEditFixture.Value(Clean(
                Operation(ModelCleanFaces.DuplicateInMaterial),
                ComposedEditFixture.Given("all", true)));

            Assert.Single(Now(first).Faces);
            Assert.Single(Now(second).Faces);
            Assert.Equal(1, value[ModelCleanFaces.RemovedName]);
        }

        [Fact]
        public void AnOperationTheCleaningToolDoesNotKnowIsRefused()
        {
            Material("材質");

            IDictionary<string, object> envelope = Clean(
                Operation("みがく"),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void FlippingAFaceSwapsItsSecondAndThirdVertices()
        {
            IList<IPXVertex> vertices = Vertices(3);
            FakeMaterial material = Material("材質", Face(vertices, 0, 1, 2));

            IDictionary<string, object> value = ComposedEditFixture.Value(EditFaces(
                Operation(ModelEditFaces.Flip),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true)));

            IPXFace face = Now(material).Faces[0];
            Assert.Same(NowAll(vertices)[0], face.Vertex1);
            Assert.Same(NowAll(vertices)[2], face.Vertex2);
            Assert.Same(NowAll(vertices)[1], face.Vertex3);
            Assert.Equal(1, value[ModelEditFaces.ChangedName]);
        }

        [Fact]
        public void SwappingTheDiagonalRedrawsTheSquareAndKeepsBothFacesFacingTheSameWay()
        {
            IList<IPXVertex> vertices = Quad();
            FakeMaterial material = Material(
                "材質", Face(vertices, 0, 1, 2), Face(vertices, 0, 2, 3));

            IDictionary<string, object> value = ComposedEditFixture.Value(EditFaces(
                Operation(ModelEditFaces.SwapDiagonal),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal(
                new[] { Corners(vertices, 0, 1, 3), Corners(vertices, 1, 2, 3) },
                new[] { Corners(Now(material).Faces[0]), Corners(Now(material).Faces[1]) });
            Assert.Equal(2, value[ModelEditFaces.ChangedName]);
        }

        [Fact]
        public void ExtrudingAFaceRaisesItAndWallsInEveryEdgeItLeavesBehind()
        {
            IList<IPXVertex> vertices = Triangle();
            FakeMaterial material = Material("材質", Face(vertices, 0, 1, 2));

            IDictionary<string, object> value = ComposedEditFixture.Value(EditFaces(
                Operation(ModelEditFaces.Extrude),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditFaces.DistanceName, 2.0)));

            Assert.Equal(6, _fixture.Model.Vertex.Count);
            Assert.Equal(7, Now(material).Faces.Count);
            Assert.Equal(3, value[ModelEditFaces.AddedVerticesName]);
            Assert.Equal(6, value[ModelEditFaces.AddedFacesName]);
            IPXFace wall = Now(material).Faces.Single(
                f => ReferenceEquals(f.Vertex1, NowAll(vertices)[0])
                    && ReferenceEquals(f.Vertex2, NowAll(vertices)[1]));
            IPXVertex raised = Raised(material, NowAll(vertices)[1]);
            Assert.Same(raised, wall.Vertex3);
            Assert.Equal(2f, raised.Position.Z);
            Assert.Equal(1f, raised.Position.X);
        }

        [Fact]
        public void AFaceWhoseEdgesSquareToNothingInSinglePrecisionIsStillRaisedTheWholeWay()
        {
            List<IPXVertex> corners = new List<IPXVertex>
            {
                new FakeVertex(0f, 0f, 0f),
                new FakeVertex(1e-25f, 0f, 0f),
                new FakeVertex(0f, 1e-25f, 0f),
            };
            foreach (IPXVertex corner in corners)
            {
                _fixture.Model.Vertex.Add(corner);
            }

            Material("材質", new FakeFace(NowAll(corners)[0], NowAll(corners)[1], NowAll(corners)[2]));

            EditFaces(
                Operation(ModelEditFaces.Extrude),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditFaces.DistanceName, 2.0));

            Assert.Equal(3, _fixture.Model.Vertex.Count(v => v.Position.Z == 2f));
        }

        [Fact]
        public void AFaceWhoseEdgesAreFarApartInSizeIsStillRaisedTheWholeWay()
        {
            List<IPXVertex> corners = new List<IPXVertex>
            {
                new FakeVertex(0f, 0f, 0f),
                new FakeVertex(float.MaxValue, 0f, 0f),
                new FakeVertex(float.MaxValue, float.Epsilon, 0f),
            };
            foreach (IPXVertex corner in corners)
            {
                _fixture.Model.Vertex.Add(corner);
            }

            Material("材質", new FakeFace(NowAll(corners)[0], NowAll(corners)[1], NowAll(corners)[2]));

            EditFaces(
                Operation(ModelEditFaces.Extrude),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditFaces.DistanceName, 2.0));

            Assert.Equal(3, _fixture.Model.Vertex.Count(v => v.Position.Z == 2f));
        }

        [Fact]
        public void AFaceWhoseEdgeIsTooLongToHoldAsASingleIsStillRaisedTheWholeWay()
        {
            List<IPXVertex> corners = new List<IPXVertex>
            {
                new FakeVertex(-float.MaxValue, 0f, 0f),
                new FakeVertex(float.MaxValue, 0f, 0f),
                new FakeVertex(-float.MaxValue, 1f, 0f),
            };
            foreach (IPXVertex corner in corners)
            {
                _fixture.Model.Vertex.Add(corner);
            }

            Material("材質", new FakeFace(NowAll(corners)[0], NowAll(corners)[1], NowAll(corners)[2]));

            EditFaces(
                Operation(ModelEditFaces.Extrude),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditFaces.DistanceName, 2.0));

            Assert.Equal(3, _fixture.Model.Vertex.Count(v => v.Position.Z == 2f));
        }

        [Fact]
        public void ExtrudingTwoFacesSideBySideRaisesTheSharedCornersOnceAndWallsOnlyTheOutline()
        {
            IList<IPXVertex> vertices = Quad();
            FakeMaterial material = Material(
                "材質", Face(vertices, 0, 1, 2), Face(vertices, 0, 2, 3));

            IDictionary<string, object> value = ComposedEditFixture.Value(EditFaces(
                Operation(ModelEditFaces.Extrude),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditFaces.DistanceName, 2.0)));

            Assert.Equal(8, _fixture.Model.Vertex.Count);
            Assert.Equal(10, Now(material).Faces.Count);
            Assert.Equal(4, value[ModelEditFaces.AddedVerticesName]);
            Assert.Equal(8, value[ModelEditFaces.AddedFacesName]);
            Assert.Equal(2f, Raised(material, NowAll(vertices)[1]).Position.Z);
        }

        [Fact]
        public void ExtrudingWithoutTheLengthIsRefused()
        {
            IList<IPXVertex> vertices = Vertices(3);
            Material("材質", Face(vertices, 0, 1, 2));

            IDictionary<string, object> envelope = EditFaces(
                Operation(ModelEditFaces.Extrude),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void TheLengthIsRefusedWhenTheOperationDoesNotPushAnythingOut()
        {
            IList<IPXVertex> vertices = Vertices(3);
            Material("材質", Face(vertices, 0, 1, 2));

            IDictionary<string, object> envelope = EditFaces(
                Operation(ModelEditFaces.Flip),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditFaces.DistanceName, 2.0));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void ALengthThatDoesNotFitInASinglePrecisionNumberIsRefused()
        {
            IList<IPXVertex> vertices = Vertices(3);
            Material("材質", Face(vertices, 0, 1, 2));

            IDictionary<string, object> envelope = EditFaces(
                Operation(ModelEditFaces.Extrude),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditFaces.DistanceName, 1e39));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void SeparatingSharedVerticesGivesThePickedFaceItsOwnCopies()
        {
            IList<IPXVertex> vertices = Vertices(4);
            FakeMaterial material = Material(
                "材質", Face(vertices, 0, 1, 2), Face(vertices, 0, 2, 3));

            IDictionary<string, object> value = ComposedEditFixture.Value(EditFaces(
                Operation(ModelEditFaces.SeparateSharedVertices),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("indices", new object[] { 0 })));

            Assert.Equal(6, _fixture.Model.Vertex.Count);
            Assert.NotSame(Now(material).Faces[0].Vertex1, Now(material).Faces[1].Vertex1);
            Assert.Equal(2, value[ModelEditFaces.AddedVerticesName]);
        }

        [Fact]
        public void MergingMaterialsMovesEveryFaceIntoTheFirstOnePicked()
        {
            IList<IPXVertex> vertices = Vertices(3);
            Material("一", Face(vertices, 0, 1, 2));
            Material("二", Face(vertices, 0, 1, 2));

            IDictionary<string, object> value = ComposedEditFixture.Value(EditMaterials(
                Operation(ModelEditMaterials.Merge),
                ComposedEditFixture.Given("indices", new object[] { 0, 1 })));

            IPXMaterial kept = Assert.Single(_fixture.Model.Material);
            Assert.Equal("一", kept.Name);
            Assert.Equal(2, kept.Faces.Count);
            Assert.Equal(1, value[ModelEditMaterials.RemovedName]);
        }

        [Fact]
        public void MergingMaterialsPointsTheMorphsAndSoftBodiesAtTheOneThatIsKept()
        {
            IList<IPXVertex> vertices = Vertices(3);
            Material("一", Face(vertices, 0, 1, 2));
            FakeMaterial second = Material("二", Face(vertices, 0, 1, 2));
            FakeMorph morph = new FakeMorph("材質モーフ", MorphKind.Material);
            Now(morph).Offsets.Add(new FakeMaterialMorphOffset(second));
            _fixture.Model.Morph.Add(morph);
            FakeSoftBody soft = new FakeSoftBody { Material = second };
            _fixture.Model.SoftBody.Add(soft);

            EditMaterials(
                Operation(ModelEditMaterials.Merge),
                ComposedEditFixture.Given("indices", new object[] { 0, 1 }));

            IPXMaterial kept = Assert.Single(_fixture.Model.Material);
            Assert.Equal("一", kept.Name);
            Assert.Same(kept, ((IPXMaterialMorphOffset)Now(morph).Offsets[0]).Material);
            Assert.Same(kept, Now(soft).Material);
        }

        [Fact]
        public void MergingEveryMaterialLeavesOne()
        {
            IList<IPXVertex> vertices = Vertices(3);
            Material("一", Face(vertices, 0, 1, 2));
            Material("二", Face(vertices, 0, 1, 2));
            Material("三", Face(vertices, 0, 1, 2));

            EditMaterials(
                Operation(ModelEditMaterials.MergeAll),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(3, Assert.Single(_fixture.Model.Material).Faces.Count);
        }

        [Fact]
        public void MaterialsThatAgreeWithinTheColourWidthAreMergedTogether()
        {
            IList<IPXVertex> vertices = Vertices(3);
            Material("一", Face(vertices, 0, 1, 2));
            FakeMaterial second = Material("二", Face(vertices, 0, 1, 2));
            Now(second).Diffuse = new V4(1f, 1f, 1f, 0.95f);
            FakeMaterial apart = Material("三", Face(vertices, 0, 1, 2));
            Now(apart).Diffuse = new V4(0f, 0f, 0f, 1f);

            EditMaterials(
                Operation(ModelEditMaterials.MergeSame),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditMaterials.ColorToleranceName, 0.1));

            Assert.Equal(
                new[] { "一", "三" }, _fixture.Model.Material.Select(material => material.Name).ToArray());
            Assert.Equal(2, _fixture.Model.Material[0].Faces.Count);
            Assert.Equal(0f, _fixture.Model.Material[1].Diffuse.X);
        }

        [Fact]
        public void MergingBySamenessWithoutTheColourWidthIsRefused()
        {
            Material("材質");

            IDictionary<string, object> envelope = EditMaterials(
                Operation(ModelEditMaterials.MergeSame),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void TakingFacesOutPutsThemIntoAMaterialOfTheirOwn()
        {
            IList<IPXVertex> vertices = Vertices(4);
            FakeMaterial material = Material(
                "材質", Face(vertices, 0, 1, 2), Face(vertices, 0, 2, 3));

            IDictionary<string, object> value = ComposedEditFixture.Value(EditMaterials(
                Operation(ModelEditMaterials.ExtractFaces),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(
                    ModelEditMaterials.FaceIndicesName, new object[] { 1 })));

            Assert.Equal(2, _fixture.Model.Material.Count);
            Assert.Equal(new[] { new[] { 0, 1, 2 } }, Places(material));
            Assert.Equal(new[] { new[] { 0, 2, 3 } }, Places(_fixture.Model.Material[1]));
            Assert.Equal(new object[] { 1 }, (object[])value[ModelEditMaterials.AddedName]);
        }

        [Fact]
        public void TakingFacesOutWithoutSayingWhichOnesIsRefused()
        {
            IList<IPXVertex> vertices = Vertices(3);
            Material("材質", Face(vertices, 0, 1, 2));

            IDictionary<string, object> envelope = EditMaterials(
                Operation(ModelEditMaterials.ExtractFaces),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void TakingFacesOutWithTheirVerticesCopiesTheOnesTheOtherFacesShare()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Material("材質", Face(vertices, 0, 1, 2), Face(vertices, 0, 2, 3));

            EditMaterials(
                Operation(ModelEditMaterials.ExtractFacesWithVertices),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(
                    ModelEditMaterials.FaceIndicesName, new object[] { 1 }));

            IPXFace moved = Assert.Single(_fixture.Model.Material[1].Faces);
            Assert.Equal(6, _fixture.Model.Vertex.Count);
            Assert.NotSame(NowAll(vertices)[0], moved.Vertex1);
            Assert.NotSame(NowAll(vertices)[2], moved.Vertex2);
            Assert.Same(NowAll(vertices)[3], moved.Vertex3);
        }

        [Fact]
        public void TheFacesMadeOnlyOfThePickedVerticesGoIntoAMaterialOfTheirOwn()
        {
            IList<IPXVertex> vertices = Vertices(4);
            FakeMaterial material = Material(
                "材質", Face(vertices, 0, 1, 2), Face(vertices, 0, 2, 3));

            IDictionary<string, object> value = ComposedEditFixture.Value(EditMaterials(
                Operation(ModelEditMaterials.ExtractVertices),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelEditMaterials.VertexIndicesName, new object[] { 0, 2, 3 })));

            Assert.Equal(2, _fixture.Model.Material.Count);
            Assert.Single(Now(material).Faces);
            Assert.Same(NowAll(vertices)[1], Now(material).Faces[0].Vertex2);
            Assert.Single(_fixture.Model.Material[1].Faces);
            Assert.Equal(new object[] { 1 }, (object[])value[ModelEditMaterials.AddedName]);
        }

        [Fact]
        public void TheScreenSelectionSaysWhichVerticesToTakeTheFacesOutBy()
        {
            IList<IPXVertex> vertices = Vertices(4);
            FakeMaterial material = Material(
                "材質", Face(vertices, 0, 1, 2), Face(vertices, 0, 2, 3));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0, 2, 3 };

            ComposedEditFixture.Value(EditMaterials(
                Operation(ModelEditMaterials.ExtractVertices),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditMaterials.VertexSelectedName, true)));

            Assert.Equal(2, _fixture.Model.Material.Count);
            Assert.Equal(new[] { new[] { 0, 1, 2 } }, Places(material));
            Assert.Equal(new[] { new[] { 0, 2, 3 } }, Places(_fixture.Model.Material[1]));
        }

        [Fact]
        public void PointingTheVerticesBothWaysAtOnceIsRefused()
        {
            IList<IPXVertex> vertices = Vertices(3);
            Material("材質", Face(vertices, 0, 1, 2));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0 };

            IDictionary<string, object> envelope = EditMaterials(
                Operation(ModelEditMaterials.ExtractVertices),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelEditMaterials.VertexIndicesName, new object[] { 0 }),
                ComposedEditFixture.Given(ModelEditMaterials.VertexSelectedName, true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AnEmptyListOfVerticesStaysAWayOfSayingNoVertices()
        {
            IList<IPXVertex> vertices = Vertices(3);
            FakeMaterial material = Material("材質", Face(vertices, 0, 1, 2));

            IDictionary<string, object> value = ComposedEditFixture.Value(EditMaterials(
                Operation(ModelEditMaterials.ExtractVertices),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelEditMaterials.VertexIndicesName, new object[0])));

            Assert.Single(_fixture.Model.Material);
            Assert.Single(Now(material).Faces);
            Assert.Empty((object[])value[ModelEditMaterials.AddedName]);
        }

        [Fact]
        public void TakingFacesOutByVerticesWithoutSayingWhichVerticesIsRefused()
        {
            IList<IPXVertex> vertices = Vertices(3);
            Material("材質", Face(vertices, 0, 1, 2));

            IDictionary<string, object> envelope = EditMaterials(
                Operation(ModelEditMaterials.ExtractVertices),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void DuplicatingOnlyTheFacesLeavesTheVerticesShared()
        {
            IList<IPXVertex> vertices = Vertices(3);
            FakeMaterial material = Material("材質", Face(vertices, 0, 1, 2));

            EditMaterials(
                Operation(ModelEditMaterials.DuplicateParts),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(
                    ModelEditMaterials.PartsName, ModelEditMaterials.FacesOnly));

            Assert.Equal(2, _fixture.Model.Material.Count);
            Assert.Equal(3, _fixture.Model.Vertex.Count);
            Assert.Same(Now(material).Faces[0].Vertex1, _fixture.Model.Material[1].Faces[0].Vertex1);
        }

        [Fact]
        public void DuplicatingWithTheVerticesGivesTheNewMaterialItsOwn()
        {
            IList<IPXVertex> vertices = Vertices(3);
            FakeMaterial material = Material("材質", Face(vertices, 0, 1, 2));

            EditMaterials(
                Operation(ModelEditMaterials.DuplicateParts),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(
                    ModelEditMaterials.PartsName, ModelEditMaterials.WithVertices));

            Assert.Equal(6, _fixture.Model.Vertex.Count);
            Assert.NotSame(Now(material).Faces[0].Vertex1, _fixture.Model.Material[1].Faces[0].Vertex1);
        }

        [Fact]
        public void DuplicatingWithTheMorphsCarriesTheOffsetsThatPointAtTheCopiedVertices()
        {
            IList<IPXVertex> vertices = Vertices(3);
            Material("材質", Face(vertices, 0, 1, 2));
            FakeMorph morph = new FakeMorph("そのモーフ", MorphKind.Vertex);
            Now(morph).Offsets.Add(new FakeVertexMorphOffset(NowAll(vertices)[0]));
            _fixture.Model.Morph.Add(morph);

            EditMaterials(
                Operation(ModelEditMaterials.DuplicateParts),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(
                    ModelEditMaterials.PartsName, ModelEditMaterials.WithMorphs));

            Assert.Equal(2, _fixture.Model.Morph.Count);
            Assert.NotSame(
                ((IPXVertexMorphOffset)Now(morph).Offsets[0]).Vertex,
                ((IPXVertexMorphOffset)_fixture.Model.Morph[1].Offsets[0]).Vertex);
        }

        [Fact]
        public void ClampingPullsEveryColourComponentIntoTheUnitRange()
        {
            FakeMaterial material = Material("材質");
            Now(material).Diffuse = new V4(2f, -1f, 0.5f, 1f);
            Now(material).Specular = new V3(-0.5f, 3f, 0.25f);

            IDictionary<string, object> value = ComposedEditFixture.Value(EditMaterials(
                Operation(ModelEditMaterials.ClampColor),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal(1f, Now(material).Diffuse.X);
            Assert.Equal(0f, Now(material).Diffuse.Y);
            Assert.Equal(0.5f, Now(material).Diffuse.Z);
            Assert.Equal(0f, Now(material).Specular.X);
            Assert.Equal(1f, Now(material).Specular.Y);
            Assert.Equal(1, value[ModelEditMaterials.ChangedName]);
        }

        [Fact]
        public void WeldingPointsTheFacesThatSurviveAtTheOneVertexThatIsLeft()
        {
            IList<IPXVertex> vertices = Quad();
            FakeMaterial material = Material(
                "材質", Face(vertices, 0, 1, 2), Face(vertices, 0, 2, 3));

            IDictionary<string, object> value = ComposedEditFixture.Value(EditVertices(
                Operation(ModelEditVertices.Weld),
                ComposedEditFixture.Given("indices", new object[] { 0, 1 })));

            Assert.Equal(3, _fixture.Model.Vertex.Count);
            IPXFace left = Assert.Single(Now(material).Faces);
            Assert.Same(_fixture.Model.Vertex[0], left.Vertex1);
            Assert.Same(_fixture.Model.Vertex[1], left.Vertex2);
            Assert.Same(_fixture.Model.Vertex[2], left.Vertex3);
            Assert.Equal(1, value[ModelEditVertices.RemovedName]);
            Assert.Equal(1, value[ModelEditVertices.RemovedFacesName]);
        }

        [Fact]
        public void JoiningNearVerticesDropsTheFacesThatLoseTheirThirdCorner()
        {
            FakeVertex first = new FakeVertex(0f, 0f, 0f);
            FakeVertex near = new FakeVertex(0.05f, 0f, 0f);
            FakeVertex apart = new FakeVertex(0f, 5f, 0f);
            _fixture.Model.Vertex.Add(first);
            _fixture.Model.Vertex.Add(near);
            _fixture.Model.Vertex.Add(apart);
            FakeMaterial material = Material("材質", new FakeFace(first, near, apart));

            IDictionary<string, object> value = ComposedEditFixture.Value(EditVertices(
                Operation(ModelEditVertices.WeldNear),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditVertices.ThresholdName, 0.1)));

            Assert.Empty(Now(material).Faces);
            Assert.Equal(1, value[ModelEditVertices.RemovedFacesName]);
        }

        [Fact]
        public void WeldingNearOnlyJoinsTheOnesInsideTheThreshold()
        {
            _fixture.Model.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            _fixture.Model.Vertex.Add(new FakeVertex(0.05f, 0f, 0f));
            _fixture.Model.Vertex.Add(new FakeVertex(5f, 0f, 0f));

            IDictionary<string, object> value = ComposedEditFixture.Value(EditVertices(
                Operation(ModelEditVertices.WeldNear),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditVertices.ThresholdName, 0.1)));

            Assert.Equal(2, _fixture.Model.Vertex.Count);
            Assert.Equal(1, value[ModelEditVertices.RemovedName]);
        }

        [Fact]
        public void WeldingNearWithoutTheThresholdIsRefused()
        {
            Vertices(2);

            IDictionary<string, object> envelope = EditVertices(
                Operation(ModelEditVertices.WeldNear),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AligningRemakesOnlyTheVerticesInTheView()
        {
            _fixture.Model.Vertex.Add(new FakeVertex(1f, 2f, 3f));
            _fixture.Model.Vertex.Add(new FakeVertex(5f, 6f, 7f));

            EditVertices(
                Operation(ModelEditVertices.Align),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditVertices.AxisName, ModelEditVertices.AxisY));

            Assert.Equal(new[] { ElementKinds.Vertex }, _fixture.View.Remade);
            Assert.Equal(0, _fixture.View.Redraws);
        }

        [Fact]
        public void WeldingStillRebuildsTheWholeModelInTheView()
        {
            IList<IPXVertex> vertices = Quad();
            Material("材質", Face(vertices, 0, 1, 2), Face(vertices, 0, 2, 3));

            EditVertices(
                Operation(ModelEditVertices.Weld),
                ComposedEditFixture.Given("indices", new object[] { 0, 1 }));

            Assert.Empty(_fixture.View.Remade);
            Assert.Equal(1, _fixture.View.Redraws);
        }

        [Fact]
        public void AligningPutsThePickedVerticesOnTheAverageOfTheChosenAxis()
        {
            _fixture.Model.Vertex.Add(new FakeVertex(1f, 2f, 3f));
            _fixture.Model.Vertex.Add(new FakeVertex(5f, 6f, 7f));

            EditVertices(
                Operation(ModelEditVertices.Align),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditVertices.AxisName, ModelEditVertices.AxisY));

            Assert.Equal(4f, _fixture.Model.Vertex[0].Position.Y);
            Assert.Equal(4f, _fixture.Model.Vertex[1].Position.Y);
            Assert.Equal(1f, _fixture.Model.Vertex[0].Position.X);
        }

        [Fact]
        public void MirroringACopyAddsTheOppositeSideAndLeavesTheOriginal()
        {
            _fixture.Model.Vertex.Add(new FakeVertex(1f, 2f, 3f));

            IDictionary<string, object> value = ComposedEditFixture.Value(EditVertices(
                Operation(ModelEditVertices.MirrorCopy),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditVertices.AxisName, ModelEditVertices.AxisX)));

            Assert.Equal(2, _fixture.Model.Vertex.Count);
            Assert.Equal(1f, _fixture.Model.Vertex[0].Position.X);
            Assert.Equal(-1f, _fixture.Model.Vertex[1].Position.X);
            IDictionary<string, object> added =
                (IDictionary<string, object>)value[ModelEditVertices.AddedName];
            Assert.Equal(1, added[TargetInput.StartName]);
            Assert.Equal(1, added[TargetInput.CountName]);
        }

        [Fact]
        public void MirroringTheModelMovesTheVerticesThemselves()
        {
            _fixture.Model.Vertex.Add(new FakeVertex(1f, 2f, 3f));

            EditVertices(
                Operation(ModelEditVertices.MirrorModel),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditVertices.AxisName, ModelEditVertices.AxisX));

            Assert.Single(_fixture.Model.Vertex);
            Assert.Equal(-1f, _fixture.Model.Vertex[0].Position.X);
        }

        [Fact]
        public void MirroringTheModelTurnsTheFaceItMovedWholeInsideOut()
        {
            IList<IPXVertex> vertices = Triangle();
            FakeMaterial material = Material("材質", Face(vertices, 0, 1, 2));

            EditVertices(
                Operation(ModelEditVertices.MirrorModel),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditVertices.AxisName, ModelEditVertices.AxisX));

            Assert.Equal(Corners(vertices, 0, 2, 1), Corners(Now(material).Faces[0]));
        }

        [Fact]
        public void MirroringWithoutTheAxisIsRefused()
        {
            Vertices(1);

            IDictionary<string, object> envelope = EditVertices(
                Operation(ModelEditVertices.MirrorModel),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void TheFaceEditActsOnTheFacesTheScreenPicks()
        {
            IList<IPXVertex> vertices = Vertices(3);
            FakeMaterial material = Material(
                "材質", Face(vertices, 0, 1, 2), Face(vertices, 0, 1, 2));
            // 画面は選んだ面を3つの頂点の位置の組で持つ。通し番号1の面を選ぶ。
            _fixture.View.Selected[ElementKinds.Face] = new[] { 3, 4, 5 };

            IDictionary<string, object> value = ComposedEditFixture.Value(EditFaces(
                Operation(ModelEditFaces.Flip),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("selected", true)));

            Assert.Equal(1, value[ModelEditFaces.ChangedName]);
            Assert.Same(NowAll(vertices)[1], Now(material).Faces[0].Vertex2);
            Assert.Same(NowAll(vertices)[2], Now(material).Faces[1].Vertex2);
        }

        [Theory]
        [InlineData(ModelEditUv.FlipU, 0.75f, 0.25f, false)]
        [InlineData(ModelEditUv.FlipV, 0.25f, 0.75f, false)]
        [InlineData(ModelEditUv.FlipUV, 0.75f, 0.75f, false)]
        [InlineData(ModelEditUv.FlipUV, 0.75f, 0.75f, true)]
        public void FlippingTheUvTakesEachAxisAwayFromOne(
            string operation, float u, float v, bool suppressed)
        {
            FakeVertex vertex = new FakeVertex();
            Now(vertex).UV = new V2(0.25f, 0.25f);
            _fixture.Model.Vertex.Add(vertex);

            IDictionary<string, object> value = ComposedEditFixture.Value(EditUv(Undo(
                suppressed,
                Operation(operation),
                ComposedEditFixture.Given("all", true))));

            Assert.Equal(u, Now(vertex).UV.X);
            Assert.Equal(v, Now(vertex).UV.Y);
            Assert.Equal(1, value[ModelEditUv.ChangedName]);
            Assert.Equal(suppressed ? 1 : 0, _fixture.Partials.Count);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AligningPutsEveryPickedVertexOnTheMiddleOfTheAxis(bool suppressed)
        {
            FakeVertex first = new FakeVertex(1f, 2f, 3f);
            FakeVertex second = new FakeVertex(5f, 6f, 7f);
            _fixture.Model.Vertex.Add(first);
            _fixture.Model.Vertex.Add(second);

            ComposedEditFixture.Value(EditVertices(Undo(
                suppressed,
                Operation(ModelEditVertices.Align),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditVertices.AxisName, ModelEditVertices.AxisY))));

            Assert.Equal(4f, Now(first).Position.Y);
            Assert.Equal(4f, Now(second).Position.Y);
            Assert.Equal(1f, Now(first).Position.X);
            Assert.Equal(suppressed ? 1 : 0, _fixture.Partials.Count);
        }

        /// <summary>
        /// <paramref name="suppressed"/> が真なら、項目の組へUndoの抑止の頼みを足す。抑止を頼むと、
        /// 書き換えた種類だけを部分反映する経路を通る。
        /// </summary>
        private static KeyValuePair<string, object>[] Undo(
            bool suppressed, params KeyValuePair<string, object>[] given)
        {
            return suppressed
                ? given.Concat(new[] { ComposedEditFixture.Given(UndoBarrier.SuppressName, true) }).ToArray()
                : given;
        }

        [Fact]
        public void RewritingTheUvRemakesOnlyTheVerticesInTheView()
        {
            FakeVertex vertex = new FakeVertex();
            Now(vertex).UV = new V2(0.25f, 0.25f);
            _fixture.Model.Vertex.Add(vertex);

            EditUv(Operation(ModelEditUv.FlipU), ComposedEditFixture.Given("all", true));

            Assert.Equal(new[] { ElementKinds.Vertex }, _fixture.View.Remade);
            Assert.Equal(0, _fixture.View.Redraws);
        }

        [Fact]
        public void CopyingTheUvTakesItFromTheVertexThatWasNamed()
        {
            FakeVertex source = new FakeVertex();
            Now(source).UV = new V2(0.5f, 0.75f);
            FakeVertex target = new FakeVertex();
            _fixture.Model.Vertex.Add(source);
            _fixture.Model.Vertex.Add(target);

            EditUv(
                Operation(ModelEditUv.Copy),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ModelEditUv.SourceName, 0));

            Assert.Equal(0.5f, Now(target).UV.X);
            Assert.Equal(0.75f, Now(target).UV.Y);
        }

        [Fact]
        public void CopyingTheUvWithoutSayingWhereFromIsRefused()
        {
            Vertices(2);

            IDictionary<string, object> envelope = EditUv(
                Operation(ModelEditUv.Copy),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void ProjectingFromAViewLaysTheUvOnThePlaneFacingThatWay()
        {
            FakeVertex vertex = new FakeVertex(2f, 3f, 4f);
            _fixture.Model.Vertex.Add(vertex);

            EditUv(
                Operation(ModelEditUv.ProjectFromView),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelEditUv.DirectionName, new object[] { 0, 0, 1 }));

            Assert.Equal(2f, Now(vertex).UV.X);
            Assert.Equal(3f, Now(vertex).UV.Y);
        }

        [Fact]
        public void ProjectingFromAVeryShortDirectionStillLaysTheUvDown()
        {
            FakeVertex vertex = new FakeVertex(2f, 3f, 4f);
            _fixture.Model.Vertex.Add(vertex);

            EditUv(
                Operation(ModelEditUv.ProjectFromView),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelEditUv.DirectionName, new object[] { 0, 0, 1e-7 }));

            Assert.Equal(2f, Now(vertex).UV.X);
            Assert.Equal(3f, Now(vertex).UV.Y);
        }

        [Fact]
        public void ProjectingFromStraightAboveFallsBackToTheOtherReferenceDirection()
        {
            FakeVertex vertex = new FakeVertex(2f, 3f, 4f);
            _fixture.Model.Vertex.Add(vertex);

            EditUv(
                Operation(ModelEditUv.ProjectFromView),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelEditUv.DirectionName, new object[] { 0, 1, 0 }));

            Assert.Equal(-2f, Now(vertex).UV.X);
            Assert.Equal(4f, Now(vertex).UV.Y);
        }

        [Fact]
        public void ProjectingFromADirectionWithNoLengthIsRefused()
        {
            Vertices(1);

            IDictionary<string, object> envelope = EditUv(
                Operation(ModelEditUv.ProjectFromView),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelEditUv.DirectionName, new object[] { 0, 0, 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void ProjectingWithoutTheDirectionIsRefused()
        {
            Vertices(1);

            IDictionary<string, object> envelope = EditUv(
                Operation(ModelEditUv.ProjectFromView),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        private IDictionary<string, object> Clean(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ModelCleanFaces.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IDictionary<string, object> EditFaces(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ModelEditFaces.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IDictionary<string, object> EditMaterials(
            params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelEditMaterials.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IDictionary<string, object> EditVertices(
            params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ModelEditVertices.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IDictionary<string, object> EditUv(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ModelEditUv.ToolName, ComposedEditFixture.Arguments(given));
        }

        private static KeyValuePair<string, object> Operation(string operation)
        {
            return ComposedEditFixture.Given(ComposedOperation.OperationName, operation);
        }

        [Fact]
        public void SwappingTheDiagonalsOfManyLooseFacesFinishesInTime()
        {
            List<IPXFace> faces = new List<IPXFace>();
            for (int at = 0; at < ManyFaces; at++)
            {
                IList<IPXVertex> corners = Vertices(3);
                faces.Add(Face(corners, 0, 1, 2));
            }

            Material("材質", faces.ToArray());

            TimeSpan editor = _fixture.EditorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            IDictionary<string, object> value = ComposedEditFixture.Value(EditFaces(
                Operation(ModelEditFaces.SwapDiagonal),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true)));
            elapsed.Stop();
            TimeSpan spent = elapsed.Elapsed - (_fixture.EditorTime - editor);

            Assert.Equal(0, value[ModelEditFaces.ChangedName]);
            Assert.True(spent < TimeLimit, "入れ替えるのに " + spent + " かかった");
        }

        [Fact]
        public void SwappingTheDiagonalsOfAWideFanFinishesInTime()
        {
            IList<IPXVertex> rim = Vertices(ManyFaces * 2 + 1);
            List<IPXFace> faces = new List<IPXFace>();
            for (int at = 0; at < ManyFaces; at++)
            {
                faces.Add(new FakeFace(rim[0], rim[(at * 2) + 1], rim[(at * 2) + 2]));
            }

            Material("材質", faces.ToArray());

            TimeSpan editor = _fixture.EditorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            IDictionary<string, object> value = ComposedEditFixture.Value(EditFaces(
                Operation(ModelEditFaces.SwapDiagonal),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true)));
            elapsed.Stop();
            TimeSpan spent = elapsed.Elapsed - (_fixture.EditorTime - editor);

            Assert.Equal(0, value[ModelEditFaces.ChangedName]);
            Assert.True(spent < TimeLimit, "入れ替えるのに " + spent + " かかった");
        }

        [Fact]
        public void ExtrudingALongStripFinishesInTime()
        {
            FakeMaterial material = Material("材質", Strip(StripQuads).ToArray());

            TimeSpan editor = _fixture.EditorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            IDictionary<string, object> value = ComposedEditFixture.Value(EditFaces(
                Operation(ModelEditFaces.Extrude),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditFaces.DistanceName, 1.0)));
            elapsed.Stop();
            TimeSpan spent = elapsed.Elapsed - (_fixture.EditorTime - editor);

            Assert.Equal((StripQuads + 1) * 2, value[ModelEditFaces.AddedVerticesName]);
            Assert.Equal((StripQuads * 2 + 2) * 2, value[ModelEditFaces.AddedFacesName]);
            Assert.True(spent < TimeLimit, "押し出すのに " + spent + " かかった");
        }

        /// <summary>X方向へ並べた四角の帯。四角ごとに面を2つ作る。</summary>
        private IList<IPXFace> Strip(int quads)
        {
            List<IPXVertex> bottom = new List<IPXVertex>();
            List<IPXVertex> top = new List<IPXVertex>();
            for (int at = 0; at <= quads; at++)
            {
                FakeVertex low = new FakeVertex(at, 0f, 0f);
                FakeVertex high = new FakeVertex(at, 1f, 0f);
                _fixture.Model.Vertex.Add(low);
                _fixture.Model.Vertex.Add(high);
                bottom.Add(low);
                top.Add(high);
            }

            List<IPXFace> faces = new List<IPXFace>();
            for (int at = 0; at < quads; at++)
            {
                faces.Add(new FakeFace(bottom[at], bottom[at + 1], top[at + 1]));
                faces.Add(new FakeFace(bottom[at], top[at + 1], top[at]));
            }

            return faces;
        }

        private IList<IPXVertex> Vertices(int count)
        {
            List<IPXVertex> made = new List<IPXVertex>();
            for (int at = 0; at < count; at++)
            {
                FakeVertex vertex = new FakeVertex(at, 0f, 0f);
                _fixture.Model.Vertex.Add(vertex);
                made.Add(vertex);
            }

            return made;
        }

        private FakeMaterial Material(string name, params IPXFace[] faces)
        {
            FakeMaterial material = new FakeMaterial(name);
            foreach (IPXFace face in faces)
            {
                Now(material).Faces.Add(face);
            }

            _fixture.Model.Material.Add(material);

            return material;
        }

        private static IPXFace Face(IList<IPXVertex> given, int one, int two, int three)
        {
            return new FakeFace(given[one], given[two], given[three]);
        }

        /// <summary>Z軸の向きを法線に持つ、共線でない3つの頂点。</summary>
        private IList<IPXVertex> Triangle()
        {
            List<IPXVertex> made = new List<IPXVertex>
            {
                new FakeVertex(0f, 0f, 0f),
                new FakeVertex(1f, 0f, 0f),
                new FakeVertex(0f, 1f, 0f),
            };
            foreach (IPXVertex vertex in made)
            {
                _fixture.Model.Vertex.Add(vertex);
            }

            return made;
        }

        /// <summary>同じ平面の上で四角を作る、共線でない4つの頂点。</summary>
        private IList<IPXVertex> Quad()
        {
            float[][] corners =
            {
                new[] { 0f, 0f, 0f },
                new[] { 1f, 0f, 0f },
                new[] { 1f, 1f, 0f },
                new[] { 0f, 1f, 0f },
            };
            List<IPXVertex> made = new List<IPXVertex>();
            foreach (float[] corner in corners)
            {
                FakeVertex vertex = new FakeVertex(corner[0], corner[1], corner[2]);
                _fixture.Model.Vertex.Add(vertex);
                made.Add(vertex);
            }

            return made;
        }

        private int[][] Places(IPXMaterial held)
        {
            return Now(held).Faces
                .Select(face => Corners(face).Select(_fixture.Model.Vertex.IndexOf).ToArray())
                .ToArray();
        }

        private static IPXVertex[] Corners(IPXFace face)
        {
            return new[] { face.Vertex1, face.Vertex2, face.Vertex3 };
        }

        /// <summary>握った頂点の並びのうち3つを、いまのモデルで同じ位置に並んでいる頂点で返す。</summary>
        private IPXVertex[] Corners(
            IList<IPXVertex> held, int one, int two, int three)
        {
            IList<IPXVertex> now = NowAll(held);

            return new[] { now[one], now[two], now[three] };
        }

        /// <summary>
        /// 押し出しでその頂点から作られた、同じ位置の高さ違いの頂点。材質も頂点も、いまのモデルで
        /// 同じ位置に並んでいるものを読む。
        /// </summary>
        private IPXVertex Raised(FakeMaterial held, IPXVertex given)
        {
            IPXVertex from = Now(given);

            return Now(held).Faces
                .SelectMany(Corners)
                .Distinct()
                .Single(v => !ReferenceEquals(v, from)
                    && Math.Abs(v.Position.X - from.Position.X) < 0.0001f
                    && Math.Abs(v.Position.Y - from.Position.Y) < 0.0001f);
        }

        /// <summary>握った要素が並んでいた位置に、いまのモデルで並んでいる要素。</summary>
        private T Now<T>(T held)
            where T : class
        {
            return _fixture.Now(held);
        }

        /// <summary>握った要素の並びを、それぞれいまのモデルで同じ位置に並んでいる要素へ読み直す。</summary>
        private IList<T> NowAll<T>(IList<T> held)
            where T : class
        {
            return held.Select(_fixture.Now).ToList();
        }

    }
}
