using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditWeightsFromSurfaceTests : IDisposable
    {
        private const string FromSurface = "fromSurface";

        private const string SurfaceMaterialIndices = "surfaceMaterialIndices";

        private const string Projection = "projection";

        private const string ExcludeBones = "excludeBones";

        private const string MaxBones = "maxBones";

        private const string MinWeight = "minWeight";

        private const string Falloff = "falloff";

        private const int Digits = 4;

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        private FakeMaterial _surface;

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheWeightsOfTheThreeCornersAreInterpolatedByTheBarycentricCoordinatesOfTheNearestPoint()
        {
            IList<IPXBone> bones = Bones("一", "二", "三", "元");
            FakeVertex[] corners = Triangle(bones[0], bones[1], bones[2]);
            FakeVertex target = Target(0.25f, 0.1f, 0.5f, bones[3]);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                FromTheSurface(ComposedEditFixture.Given("indices", new object[] { 3 })));

            Near(0.25, Share(target, bones[0]));
            Near(0.25, Share(target, bones[1]));
            Near(0.5, Share(target, bones[2]));
            Near(0.0, Share(target, bones[3]));
            Assert.Equal(1, value[ModelEditWeights.ChangedName]);
            Near(1.0, Share(corners[0], bones[0]));
        }

        [Fact]
        public void APointBesideTheFaceTakesTheWeightsFromTheNearestPointOnTheEdge()
        {
            IList<IPXBone> bones = Bones("一", "二", "三", "元");
            Triangle(bones[0], bones[1], bones[2]);
            FakeVertex target = Target(0.5f, 0.1f, -0.5f, bones[3]);

            FromTheSurface(ComposedEditFixture.Given("indices", new object[] { 3 }));

            Near(0.5, Share(target, bones[0]));
            Near(0.5, Share(target, bones[1]));
            Near(0.0, Share(target, bones[2]));
        }

        [Fact]
        public void TheWeightsAfterTheCopyAddUpToOne()
        {
            IList<IPXBone> bones = Bones("一", "二", "三", "元");
            Triangle(bones[0], bones[1], bones[2]);
            FakeVertex target = Target(0.3f, 0.1f, 0.3f, bones[3]);

            FromTheSurface(ComposedEditFixture.Given("indices", new object[] { 3 }));

            Near(0.0, Share(target, bones[3]));
            Near(
                1.0,
                bones.Sum(bone => (double)Share(target, bone)));
        }

        [Fact]
        public void AVertexThatAlreadyHoldsTheCopiedWeightsIsNotCounted()
        {
            IList<IPXBone> bones = Bones("一");
            Triangle(bones[0], bones[0], bones[0]);
            Target(0.25f, 0.1f, 0.5f, bones[0]);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                FromTheSurface(ComposedEditFixture.Given("indices", new object[] { 3 })));

            Assert.Equal(0, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void OnlyThePickedVerticesAreChanged()
        {
            IList<IPXBone> bones = Bones("一", "二", "三", "元");
            Triangle(bones[0], bones[1], bones[2]);
            FakeVertex picked = Target(0.25f, 0.1f, 0.5f, bones[3]);
            FakeVertex left = Target(0.25f, 0.1f, 0.5f, bones[3]);

            FromTheSurface(ComposedEditFixture.Given("indices", new object[] { 3 }));

            Near(0.5, Share(picked, bones[2]));
            Near(1.0, Share(left, bones[3]));
        }

        [Fact]
        public void TheFaceOfTheOtherMaterialsIsNotUsed()
        {
            IList<IPXBone> bones = Bones("面", "別", "元");
            Triangle(bones[0], bones[0], bones[0]);
            FakeVertex[] other =
            {
                new FakeVertex(-1f, 0.05f, -1f),
                new FakeVertex(1f, 0.05f, -1f),
                new FakeVertex(0f, 0.05f, 1f),
            };
            foreach (FakeVertex corner in other)
            {
                Weigh(corner, bones[1], 1f);
                _fixture.Model.Vertex.Add(corner);
            }

            FakeMaterial another = new FakeMaterial("別");
            another.Faces.Add(new FakeFace(other[0], other[1], other[2]));
            _fixture.Model.Material.Add(another);
            FakeVertex target = Target(0f, 0.1f, 0f, bones[2]);

            FromTheSurface(ComposedEditFixture.Given("indices", new object[] { 6 }));

            Near(1.0, Share(target, bones[0]));
            Near(0.0, Share(target, bones[1]));
        }

        [Fact]
        public void WithoutTheSurfaceMaterialsTheOperationIsRefused()
        {
            IList<IPXBone> bones = Bones("一", "元");
            Triangle(bones[0], bones[0], bones[0]);
            Target(0.25f, 0.1f, 0.5f, bones[1]);
            ComposedEditFixture.Value(
                FromTheSurface(ComposedEditFixture.Given("indices", new object[] { 3 })));

            IDictionary<string, object> envelope = Weights(
                Operation(FromSurface),
                ComposedEditFixture.Given("indices", new object[] { 3 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(SurfaceMaterialIndices, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void AMaterialOutsideTheListIsRefused()
        {
            IList<IPXBone> bones = Bones("一", "元");
            Triangle(bones[0], bones[0], bones[0]);
            Target(0.25f, 0.1f, 0.5f, bones[1]);
            ComposedEditFixture.Value(
                FromTheSurface(ComposedEditFixture.Given("indices", new object[] { 3 })));

            IDictionary<string, object> envelope = Weights(
                Operation(FromSurface),
                ComposedEditFixture.Given("indices", new object[] { 3 }),
                ComposedEditFixture.Given(SurfaceMaterialIndices, new object[] { 5 }));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            Assert.Contains(SurfaceMaterialIndices, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void TheNearestProjectionIsTheDefault()
        {
            IList<IPXBone> bones = Bones("床", "壁", "元");
            FakeVertex target = FloorAndWall(bones, 0.2f);

            FromTheSurface(ComposedEditFixture.Given("indices", new object[] { 6 }));

            Near(1.0, Share(target, bones[1]));
        }

        [Fact]
        public void TheNearestProjectionTakesTheClosestFaceWhateverTheNormalFaces()
        {
            IList<IPXBone> bones = Bones("床", "壁", "元");
            FakeVertex target = FloorAndWall(bones, 0.2f);

            FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 6 }),
                ComposedEditFixture.Given(Projection, "nearest"));

            Near(1.0, Share(target, bones[1]));
        }

        [Fact]
        public void TheRayProjectionTakesTheFaceTheNormalFirstHits()
        {
            IList<IPXBone> bones = Bones("床", "壁", "元");
            FakeVertex target = FloorAndWall(bones, 0.2f);

            FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 6 }),
                ComposedEditFixture.Given(Projection, "ray"));

            Near(1.0, Share(target, bones[0]));
            Near(0.0, Share(target, bones[1]));
        }

        [Fact]
        public void TheRayProjectionTakesTheNearerOfTheFrontAndTheBack()
        {
            IList<IPXBone> bones = Bones("床", "壁", "元", "天井");
            FloorAndWall(bones, 0.1f);
            FakeVertex[] ceiling =
            {
                new FakeVertex(-2f, 0.8f, -2f),
                new FakeVertex(2f, 0.8f, -2f),
                new FakeVertex(0f, 0.8f, 2f),
            };
            foreach (FakeVertex corner in ceiling)
            {
                Weigh(corner, bones[3], 1f);
                _fixture.Model.Vertex.Add(corner);
            }

            _surface.Faces.Add(new FakeFace(ceiling[0], ceiling[1], ceiling[2]));
            FakeVertex target = (FakeVertex)_fixture.Model.Vertex[6];

            FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 6 }),
                ComposedEditFixture.Given(Projection, "ray"));

            Near(1.0, Share(target, bones[3]));
        }

        [Fact]
        public void TheRayProjectionFallsBackToTheNearestPointWhenNothingIsHit()
        {
            IList<IPXBone> bones = Bones("床", "壁", "元");
            FakeVertex target = FloorAndWall(bones, 0.2f);
            target.Normal = new V3(0f, 0f, 1f);

            FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 6 }),
                ComposedEditFixture.Given(Projection, "ray"));

            Near(1.0, Share(target, bones[1]));
        }

        [Theory]
        [InlineData(1f)]
        [InlineData(-1f)]
        public void TheRayProjectionTakesTheFaceTheVertexLiesOnWhicheverWayTheNormalFaces(float normalY)
        {
            IList<IPXBone> bones = Bones("下", "上", "元");
            Triangle(bones[0], bones[0], bones[0]);
            FakeVertex[] upper =
            {
                new FakeVertex(0f, 1f, 0f),
                new FakeVertex(1f, 1f, 0f),
                new FakeVertex(0f, 1f, 1f),
            };
            foreach (FakeVertex corner in upper)
            {
                Weigh(corner, bones[1], 1f);
                _fixture.Model.Vertex.Add(corner);
            }

            _surface.Faces.Add(new FakeFace(upper[0], upper[1], upper[2]));
            FakeVertex target = Target(0.25f, 0f, 0.5f, bones[2]);
            target.Normal = new V3(0f, normalY, 0f);

            FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 6 }),
                ComposedEditFixture.Given(Projection, "ray"));

            Near(1.0, Share(target, bones[0]));
            Near(0.0, Share(target, bones[1]));
        }

        [Fact]
        public void AProjectionOtherThanNearestAndRayIsRefused()
        {
            IList<IPXBone> bones = Bones("一", "元");
            Triangle(bones[0], bones[0], bones[0]);
            Target(0.25f, 0.1f, 0.5f, bones[1]);
            ComposedEditFixture.Value(
                FromTheSurface(
                    ComposedEditFixture.Given("indices", new object[] { 3 }),
                    ComposedEditFixture.Given(Projection, "ray")));

            IDictionary<string, object> envelope = FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 3 }),
                ComposedEditFixture.Given(Projection, "sphere"));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Projection, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void ExcludedBonesAndTheirDescendantsAreDroppedAndTheRestIsRenormalised()
        {
            IList<IPXBone> bones = Bones("一", "二", "三", "他", "元");
            ((FakeBone)bones[1]).Parent = bones[0];
            ((FakeBone)bones[2]).Parent = bones[1];
            FakeVertex[] corners = Triangle(bones[0], bones[1], bones[2]);
            Weigh(corners[2], bones[2], 0.5f);
            corners[2].Bone2 = bones[3];
            corners[2].Weight2 = 0.5f;
            FakeVertex target = Target(0.25f, 0.1f, 0.5f, bones[4]);

            FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 3 }),
                ComposedEditFixture.Given(ExcludeBones, new object[] { 0 }));

            Near(0.0, Share(target, bones[0]));
            Near(0.0, Share(target, bones[1]));
            Near(0.0, Share(target, bones[2]));
            Near(1.0, Share(target, bones[3]));
        }

        [Fact]
        public void ExcludingABoneWithoutDescendantsKeepsTheOthersInTheirRatio()
        {
            IList<IPXBone> bones = Bones("一", "二", "三", "元");
            Triangle(bones[0], bones[1], bones[2]);
            FakeVertex target = Target(0.25f, 0.1f, 0.5f, bones[3]);

            FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 3 }),
                ComposedEditFixture.Given(ExcludeBones, new object[] { 2 }));

            Near(0.5, Share(target, bones[0]));
            Near(0.5, Share(target, bones[1]));
            Near(0.0, Share(target, bones[2]));
        }

        [Fact]
        public void AnExcludedBoneOutsideTheListIsRefused()
        {
            IList<IPXBone> bones = Bones("一", "元");
            Triangle(bones[0], bones[0], bones[0]);
            Target(0.25f, 0.1f, 0.5f, bones[1]);
            ComposedEditFixture.Value(
                FromTheSurface(
                    ComposedEditFixture.Given("indices", new object[] { 3 }),
                    ComposedEditFixture.Given(ExcludeBones, new object[] { 1 })));

            IDictionary<string, object> envelope = FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 3 }),
                ComposedEditFixture.Given(ExcludeBones, new object[] { 9 }));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            Assert.Contains(ExcludeBones, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void AnExcludedBoneThatIsNotAPositionIsRefusedAsInvalid()
        {
            IList<IPXBone> bones = Bones("一", "元");
            Triangle(bones[0], bones[0], bones[0]);
            Target(0.25f, 0.1f, 0.5f, bones[1]);
            ComposedEditFixture.Value(
                FromTheSurface(
                    ComposedEditFixture.Given("indices", new object[] { 3 }),
                    ComposedEditFixture.Given(ExcludeBones, new object[] { 1 })));

            object[] wrong = { 1, "a", new object[] { "a" }, new object[] { 0.5 } };
            foreach (object excluded in wrong)
            {
                IDictionary<string, object> envelope = FromTheSurface(
                    ComposedEditFixture.Given("indices", new object[] { 3 }),
                    ComposedEditFixture.Given(ExcludeBones, excluded));

                Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
                Assert.Contains(ExcludeBones, ComposedEditFixture.Message(envelope));
            }
        }

        [Fact]
        public void ANegativeExcludedBoneIsRefusedAsOutsideTheList()
        {
            IList<IPXBone> bones = Bones("一", "元");
            Triangle(bones[0], bones[0], bones[0]);
            Target(0.25f, 0.1f, 0.5f, bones[1]);
            ComposedEditFixture.Value(
                FromTheSurface(
                    ComposedEditFixture.Given("indices", new object[] { 3 }),
                    ComposedEditFixture.Given(ExcludeBones, new object[] { 1 })));

            IDictionary<string, object> envelope = FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 3 }),
                ComposedEditFixture.Given(ExcludeBones, new object[] { -1 }));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            Assert.Contains(ExcludeBones, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void TheBonesAreCutToTheFourHeaviestByDefault()
        {
            IList<IPXBone> bones = Bones("A", "D", "B", "E", "C", "元");
            FakeVertex[] corners = Triangle(bones[0], bones[2], bones[4]);
            Weigh(corners[0], bones[0], 0.5f);
            corners[0].Bone2 = bones[1];
            corners[0].Weight2 = 0.5f;
            Weigh(corners[1], bones[2], 0.6f);
            corners[1].Bone2 = bones[3];
            corners[1].Weight2 = 0.4f;
            FakeVertex target = Target(0.35f, 0.1f, 0.25f, bones[5]);

            FromTheSurface(ComposedEditFixture.Given("indices", new object[] { 3 }));

            double kept = 0.25 + 0.21 + 0.2 + 0.2;
            Near(0.25 / kept, Share(target, bones[4]));
            Near(0.21 / kept, Share(target, bones[2]));
            Near(0.2 / kept, Share(target, bones[0]));
            Near(0.2 / kept, Share(target, bones[1]));
            Near(0.0, Share(target, bones[3]));
        }

        [Theory]
        [InlineData(1, 1.0, 0.0, 0.0)]
        [InlineData(2, 0.625, 0.375, 0.0)]
        [InlineData(3, 0.5, 0.3, 0.2)]
        public void TheBonesAreCutToTheHeaviestOnesTheLimitAllows(
            int limit, double first, double second, double third)
        {
            IList<IPXBone> bones = Bones("一", "二", "三", "元");
            Triangle(bones[0], bones[1], bones[2]);
            FakeVertex target = Target(0.3f, 0.1f, 0.2f, bones[3]);

            FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 3 }),
                ComposedEditFixture.Given(MaxBones, limit));

            Near(first, Share(target, bones[0]));
            Near(second, Share(target, bones[1]));
            Near(third, Share(target, bones[2]));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        public void ALimitOutsideOneToFourIsRefused(int limit)
        {
            IList<IPXBone> bones = Bones("一", "元");
            Triangle(bones[0], bones[0], bones[0]);
            Target(0.25f, 0.1f, 0.5f, bones[1]);
            ComposedEditFixture.Value(
                FromTheSurface(
                    ComposedEditFixture.Given("indices", new object[] { 3 }),
                    ComposedEditFixture.Given(MaxBones, 4)));

            IDictionary<string, object> envelope = FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 3 }),
                ComposedEditFixture.Given(MaxBones, limit));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(MaxBones, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void BonesLighterThanTheMinimumAreDroppedAndTheRestIsRenormalised()
        {
            IList<IPXBone> bones = Bones("一", "二", "三", "元");
            Triangle(bones[0], bones[1], bones[2]);
            FakeVertex target = Target(0.3f, 0.1f, 0.2f, bones[3]);

            FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 3 }),
                ComposedEditFixture.Given(MinWeight, 0.25));

            Near(0.625, Share(target, bones[0]));
            Near(0.375, Share(target, bones[1]));
            Near(0.0, Share(target, bones[2]));
        }

        [Fact]
        public void ANegativeMinimumIsRefused()
        {
            IList<IPXBone> bones = Bones("一", "元");
            Triangle(bones[0], bones[0], bones[0]);
            Target(0.25f, 0.1f, 0.5f, bones[1]);
            ComposedEditFixture.Value(
                FromTheSurface(
                    ComposedEditFixture.Given("indices", new object[] { 3 }),
                    ComposedEditFixture.Given(MinWeight, 0.1)));

            IDictionary<string, object> envelope = FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 3 }),
                ComposedEditFixture.Given(MinWeight, -0.1));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(MinWeight, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void TheCopiedWeightsFadeBackToTheOriginalOnesBetweenNearAndNearPlusFade()
        {
            IList<IPXBone> bones = Bones("面", "元");
            Triangle(bones[0], bones[0], bones[0]);
            FakeVertex inside = Target(0f, 0.1f, 0f, bones[1]);
            FakeVertex closer = Target(0f, 0.4f, 0f, bones[1]);
            FakeVertex further = Target(0f, 0.5f, 0f, bones[1]);
            FakeVertex outside = Target(0f, 0.9f, 0f, bones[1]);

            FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 3, 4, 5, 6 }),
                ComposedEditFixture.Given(
                    Falloff,
                    ComposedEditFixture.Arguments(
                        ComposedEditFixture.Given("near", 0.2),
                        ComposedEditFixture.Given("fade", 0.4))));

            Near(1.0, Share(inside, bones[0]));
            Near(0.0, Share(outside, bones[0]));
            Near(1.0, Share(outside, bones[1]));
            double mid = Share(closer, bones[0]);
            double far = Share(further, bones[0]);
            Assert.InRange(far, 0.0001, 0.9999);
            Assert.InRange(mid, 0.0001, 0.9999);
            Assert.True(mid > far);
            Near(1.0, Share(closer, bones[0]) + Share(closer, bones[1]));
            Near(1.0, Share(further, bones[0]) + Share(further, bones[1]));
        }

        [Fact]
        public void TheFalloffNeedsBothTheNearAndTheFade()
        {
            IList<IPXBone> bones = Bones("一", "元");
            Triangle(bones[0], bones[0], bones[0]);
            Target(0.25f, 0.1f, 0.5f, bones[1]);
            ComposedEditFixture.Value(
                FromTheSurface(
                    ComposedEditFixture.Given("indices", new object[] { 3 }),
                    ComposedEditFixture.Given(
                        Falloff,
                        ComposedEditFixture.Arguments(
                            ComposedEditFixture.Given("near", 0.2),
                            ComposedEditFixture.Given("fade", 0.4)))));

            IDictionary<string, object> envelope = FromTheSurface(
                ComposedEditFixture.Given("indices", new object[] { 3 }),
                ComposedEditFixture.Given(
                    Falloff,
                    ComposedEditFixture.Arguments(ComposedEditFixture.Given("near", 0.2))));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Falloff, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void ACornerTripleOfTheSameSdefPairMakesTheCopiedVertexSdefWithInterpolatedCenterAndReferences()
        {
            IList<IPXBone> bones = Bones("一", "二", "元");
            SdefTriangle(bones[0], bones[1]);
            FakeVertex target = Target(0.25f, 0.1f, 0.5f, bones[2]);

            FromTheSurface(ComposedEditFixture.Given("indices", new object[] { 3 }));

            FakeVertex made = Now(target);
            Assert.True(made.SDEF);
            Assert.Same(Now(bones[0]), made.Bone1);
            Assert.Same(Now(bones[1]), made.Bone2);
            Near(0.25 * 0.8 + 0.25 * 0.5 + 0.5 * 0.2, made.Weight1);
            Near(1.0 - made.Weight1, made.Weight2);
            Near(0.25 * 0.0 + 0.25 * 4.0 + 0.5 * 0.0, made.SDEF_C.X);
            Near(0.25 * 0.0 + 0.25 * 0.0 + 0.5 * 8.0, made.SDEF_C.Y);
            Near(0.25 * 1.0 + 0.25 * 2.0 + 0.5 * 3.0, made.SDEF_R0.X);
            Near(0.25 * 5.0 + 0.25 * 6.0 + 0.5 * 7.0, made.SDEF_R1.Z);
        }

        [Fact]
        public void ACornerWithAnotherPairOfBonesLeavesTheCopiedVertexOutOfSdef()
        {
            IList<IPXBone> bones = Bones("一", "二", "三", "元");
            FakeVertex[] corners = SdefTriangle(bones[0], bones[1]);
            corners[2].Bone2 = bones[2];
            FakeVertex target = Target(0.25f, 0.1f, 0.5f, bones[3]);

            FromTheSurface(ComposedEditFixture.Given("indices", new object[] { 3 }));

            Near(0.25 * 0.8 + 0.25 * 0.5 + 0.5 * 0.2, Share(target, bones[0]));
            Assert.False(Now(target).SDEF);
        }

        [Fact]
        public void ACornerThatIsNotSdefLeavesTheCopiedVertexOutOfSdef()
        {
            IList<IPXBone> bones = Bones("一", "二", "元");
            FakeVertex[] corners = SdefTriangle(bones[0], bones[1]);
            corners[1].SDEF = false;
            FakeVertex target = Target(0.25f, 0.1f, 0.5f, bones[2]);

            FromTheSurface(ComposedEditFixture.Given("indices", new object[] { 3 }));

            Near(0.25 * 0.8 + 0.25 * 0.5 + 0.5 * 0.2, Share(target, bones[0]));
            Assert.False(Now(target).SDEF);
        }

        private IDictionary<string, object> FromTheSurface(
            params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>
            {
                Operation(FromSurface),
                ComposedEditFixture.Given(SurfaceMaterialIndices, new object[] { 0 }),
            };
            all.AddRange(given);

            return Weights(all.ToArray());
        }

        private IDictionary<string, object> Weights(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ModelEditWeights.ToolName, ComposedEditFixture.Arguments(given));
        }

        private static KeyValuePair<string, object> Operation(string operation)
        {
            return ComposedEditFixture.Given(ComposedOperation.OperationName, operation);
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

        private FakeMaterial Surface()
        {
            if (_surface == null)
            {
                _surface = new FakeMaterial("面");
                _fixture.Model.Material.Add(_surface);
            }

            return _surface;
        }

        private FakeVertex[] Triangle(IPXBone first, IPXBone second, IPXBone third)
        {
            FakeVertex[] corners =
            {
                new FakeVertex(0f, 0f, 0f),
                new FakeVertex(1f, 0f, 0f),
                new FakeVertex(0f, 0f, 1f),
            };
            IPXBone[] given = { first, second, third };
            for (int at = 0; at < corners.Length; at++)
            {
                Weigh(corners[at], given[at], 1f);
                _fixture.Model.Vertex.Add(corners[at]);
            }

            Surface().Faces.Add(new FakeFace(corners[0], corners[1], corners[2]));

            return corners;
        }

        private FakeVertex[] SdefTriangle(IPXBone first, IPXBone second)
        {
            FakeVertex[] corners = Triangle(first, first, first);
            float[] firstShare = { 0.8f, 0.5f, 0.2f };
            for (int at = 0; at < corners.Length; at++)
            {
                corners[at].Bone1 = first;
                corners[at].Weight1 = firstShare[at];
                corners[at].Bone2 = second;
                corners[at].Weight2 = 1f - firstShare[at];
                corners[at].SDEF = true;
                corners[at].SDEF_C = new V3(0f, 0f, 0f);
                corners[at].SDEF_R0 = new V3(1f + at, 0f, 0f);
                corners[at].SDEF_R1 = new V3(0f, 0f, 5f + at);
            }

            corners[1].SDEF_C = new V3(4f, 0f, 0f);
            corners[2].SDEF_C = new V3(0f, 8f, 0f);

            return corners;
        }

        private FakeVertex Target(float x, float y, float z, IPXBone bone)
        {
            FakeVertex target = new FakeVertex(x, y, z);
            Weigh(target, bone, 1f);
            _fixture.Model.Vertex.Add(target);

            return target;
        }

        private FakeVertex FloorAndWall(IList<IPXBone> bones, float wallX)
        {
            FakeVertex[] floor =
            {
                new FakeVertex(-2f, 0f, -2f),
                new FakeVertex(2f, 0f, -2f),
                new FakeVertex(0f, 0f, 2f),
            };
            FakeVertex[] wall =
            {
                new FakeVertex(wallX, 0.3f, -0.5f),
                new FakeVertex(wallX, 0.3f, 0.5f),
                new FakeVertex(wallX, 0.7f, 0f),
            };
            foreach (FakeVertex corner in floor)
            {
                Weigh(corner, bones[0], 1f);
                _fixture.Model.Vertex.Add(corner);
            }

            foreach (FakeVertex corner in wall)
            {
                Weigh(corner, bones[1], 1f);
                _fixture.Model.Vertex.Add(corner);
            }

            Surface().Faces.Add(new FakeFace(floor[0], floor[1], floor[2]));
            Surface().Faces.Add(new FakeFace(wall[0], wall[1], wall[2]));

            return Target(0f, 0.5f, 0f, bones[2]);
        }

        private static void Weigh(FakeVertex weighed, IPXBone bone, float share)
        {
            weighed.Bone1 = bone;
            weighed.Weight1 = share;
        }

        private float Share(IPXVertex given, IPXBone weighed)
        {
            IPXVertex now = _fixture.Now(given);
            IPXBone target = _fixture.Now(weighed);
            IPXBone[] held = { now.Bone1, now.Bone2, now.Bone3, now.Bone4 };
            float[] weights = { now.Weight1, now.Weight2, now.Weight3, now.Weight4 };
            float share = 0f;
            for (int at = 0; at < held.Length; at++)
            {
                if (ReferenceEquals(held[at], target))
                {
                    share += weights[at];
                }
            }

            return share;
        }

        private T Now<T>(T held)
            where T : class
        {
            return _fixture.Now(held);
        }

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }
    }
}
