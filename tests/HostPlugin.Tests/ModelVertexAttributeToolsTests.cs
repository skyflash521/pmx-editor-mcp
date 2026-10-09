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
    /// 頂点が持つ法線・ウェイト・変形方式を変える3つ。どれも1回の呼び出しで、1回のまとめての
    /// 反映に収まる。
    /// </summary>
    [Collection(TimedCollection.Name)]
    public sealed class ModelVertexAttributeToolsTests : IDisposable
    {
        /// <summary>小数の突き合わせで見る桁。</summary>
        private const int Digits = 4;

        private const int ManyVertices = 30000;

        private const int WeighedVertices = 50000;

        private const int ManyBones = 1000;

        private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(2);

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AveragingTheNormalsPutsEveryPickedOneOnTheSharedDirection(bool suppressed)
        {
            FakeVertex first = Vertex(0f, 0f, 0f);
            Now(first).Normal = new V3(1f, 0f, 0f);
            FakeVertex second = Vertex(1f, 0f, 0f);
            Now(second).Normal = new V3(0f, 1f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Normals(Undo(
                suppressed,
                Operation(ModelEditNormals.Average),
                ComposedEditFixture.Given("all", true))));

            Near(0.70710678, Now(first).Normal.X);
            Near(0.70710678, Now(first).Normal.Y);
            Near(0.70710678, Now(second).Normal.X);
            Assert.Equal(2, value[ModelEditNormals.ChangedName]);
            Assert.Equal(1, _fixture.Commits);
            Assert.Equal(suppressed ? 1 : 0, _fixture.Partials.Count);
        }

        [Fact]
        public void AveragingNearOnlyJoinsTheOnesInsideTheThreshold()
        {
            FakeVertex first = Vertex(0f, 0f, 0f);
            Now(first).Normal = new V3(1f, 0f, 0f);
            FakeVertex near = Vertex(0.05f, 0f, 0f);
            Now(near).Normal = new V3(0f, 1f, 0f);
            FakeVertex apart = Vertex(5f, 0f, 0f);
            Now(apart).Normal = new V3(0f, 0f, 1f);

            Normals(
                Operation(ModelEditNormals.AverageNear),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditNormals.ThresholdName, 0.1));

            Near(0.70710678, Now(first).Normal.X);
            Near(0.70710678, Now(near).Normal.Y);
            Near(1.0, Now(apart).Normal.Z);
        }

        [Fact]
        public void AveragingNearWithoutTheThresholdIsRefused()
        {
            Vertex(0f, 0f, 0f);

            IDictionary<string, object> envelope = Normals(
                Operation(ModelEditNormals.AverageNear),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void TakingTheNormalFromTheFacesUsesTheDirectionTheyFace()
        {
            FakeVertex first = Vertex(0f, 0f, 0f);
            FakeVertex second = Vertex(1f, 0f, 0f);
            FakeVertex third = Vertex(0f, 1f, 0f);
            FakeMaterial material = new FakeMaterial("材質");
            Now(material).Faces.Add(new FakeFace(first, second, third));
            _fixture.Model.Material.Add(material);

            Normals(
                Operation(ModelEditNormals.FromFaces),
                ComposedEditFixture.Given("all", true));

            Near(0.0, Now(first).Normal.X);
            Near(0.0, Now(first).Normal.Y);
            Near(1.0, Now(first).Normal.Z);
        }

        [Fact]
        public void NormalisingPutsTheNormalBackOnTheUnitLength()
        {
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Normal = new V3(0f, 0f, 2f);

            Normals(
                Operation(ModelEditNormals.Normalize),
                ComposedEditFixture.Given("all", true));

            Near(1.0, Now(vertex).Normal.Z);
        }

        [Fact]
        public void FlippingTurnsTheNormalTheOtherWay()
        {
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Normal = new V3(0f, 0f, 1f);

            Normals(
                Operation(ModelEditNormals.Flip),
                ComposedEditFixture.Given("all", true));

            Near(-1.0, Now(vertex).Normal.Z);
        }

        [Fact]
        public void RotatingTurnsTheNormalAboutTheAxisAndLeavesThePositionAlone()
        {
            FakeVertex vertex = Vertex(1f, 2f, 3f);
            Now(vertex).Normal = new V3(1f, 0f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Normals(
                Operation(ModelEditNormals.Rotate),
                ComposedEditFixture.Given(ModelEditNormals.RotationAxisName, new object[] { 1f, 1f, 1f }),
                ComposedEditFixture.Given(ModelEditNormals.RotationAngleName, 120f),
                ComposedEditFixture.Given("all", true)));

            Near(0.0, Now(vertex).Normal.X);
            Near(1.0, Now(vertex).Normal.Y);
            Near(0.0, Now(vertex).Normal.Z);
            Near(1.0, Now(vertex).Position.X);
            Near(3.0, Now(vertex).Position.Z);
            Assert.Equal(1, value[ModelEditNormals.ChangedName]);
        }

        [Fact]
        public void RotatingNeedsAnAxisWithLength()
        {
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Normal = new V3(1f, 0f, 0f);

            IDictionary<string, object> answer = Normals(
                Operation(ModelEditNormals.Rotate),
                ComposedEditFixture.Given(ModelEditNormals.RotationAxisName, new object[] { 0f, 0f, 0f }),
                ComposedEditFixture.Given(ModelEditNormals.RotationAngleName, 120f),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(answer));
            Near(1.0, Now(vertex).Normal.X);
        }

        [Fact]
        public void RotatingNeedsTheAngle()
        {
            Vertex(0f, 0f, 0f);

            IDictionary<string, object> answer = Normals(
                Operation(ModelEditNormals.Rotate),
                ComposedEditFixture.Given(ModelEditNormals.RotationAxisName, new object[] { 0f, 1f, 0f }),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(answer));
        }

        [Fact]
        public void TheAxisIsRefusedOutsideRotating()
        {
            Vertex(0f, 0f, 0f);

            IDictionary<string, object> answer = Normals(
                Operation(ModelEditNormals.Flip),
                ComposedEditFixture.Given(ModelEditNormals.RotationAxisName, new object[] { 0f, 1f, 0f }),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(answer));
        }

        [Fact]
        public void RewritingTheNormalsRemakesOnlyTheVerticesInTheView()
        {
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Normal = new V3(0f, 0f, 1f);

            Normals(Operation(ModelEditNormals.Flip), ComposedEditFixture.Given("all", true));

            Assert.Equal(new[] { ElementKinds.Vertex }, _fixture.View.Remade);
            Assert.Equal(0, _fixture.View.Redraws);
        }

        [Fact]
        public void TheScreenSelectionPicksTheVerticesToFlip()
        {
            FakeVertex first = Vertex(0f, 0f, 0f);
            Now(first).Normal = new V3(0f, 0f, 1f);
            FakeVertex second = Vertex(1f, 0f, 0f);
            Now(second).Normal = new V3(0f, 0f, 1f);
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 1 };

            Normals(
                Operation(ModelEditNormals.Flip),
                ComposedEditFixture.Given("selected", true));

            Near(1.0, Now(first).Normal.Z);
            Near(-1.0, Now(second).Normal.Z);
        }

        [Fact]
        public void AnEmptyScreenSelectionLeavesTheNormalsAlone()
        {
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Normal = new V3(0f, 0f, 1f);

            IDictionary<string, object> answer = Normals(
                Operation(ModelEditNormals.Flip),
                ComposedEditFixture.Given("selected", true));

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedEditFixture.Code(answer));
            Near(1.0, Now(vertex).Normal.Z);
        }

        [Fact]
        public void NormalisingANormalThatIsAlreadyTheRightLengthIsNotCounted()
        {
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Normal = new V3(0f, 0f, 1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Normals(
                Operation(ModelEditNormals.Normalize),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal(0, value[ModelEditNormals.ChangedName]);
        }

        [Fact]
        public void AveragingNormalsTooLargeToAddInSinglePrecisionStillPointsTheRightWay()
        {
            FakeVertex first = Vertex(0f, 0f, 0f);
            Now(first).Normal = new V3(float.MaxValue, 0f, 0f);
            FakeVertex second = Vertex(1f, 0f, 0f);
            Now(second).Normal = new V3(float.MaxValue, 0f, 0f);

            Normals(
                Operation(ModelEditNormals.Average),
                ComposedEditFixture.Given("all", true));

            Near(1.0, Now(first).Normal.X);
            Near(1.0, Now(second).Normal.X);
        }

        [Fact]
        public void AveragingTheWeightsPutsEveryPickedVertexOnTheSharedShare()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex first = Vertex(0f, 0f, 0f);
            Weigh(first, NowAll(bones)[0], 1f);
            FakeVertex second = Vertex(1f, 0f, 0f);
            Weigh(second, NowAll(bones)[1], 1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Weights(
                Operation(ModelEditWeights.Average),
                ComposedEditFixture.Given("all", true)));

            Near(0.5, Share(first, NowAll(bones)[0]));
            Near(0.5, Share(first, NowAll(bones)[1]));
            Near(0.5, Share(second, NowAll(bones)[0]));
            Assert.Equal(2, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void SmoothingMovesTheWeightTowardsTheNeighboursByTheStrength()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex first = Vertex(0f, 0f, 0f);
            Weigh(first, NowAll(bones)[0], 1f);
            FakeVertex second = Vertex(1f, 0f, 0f);
            Weigh(second, NowAll(bones)[1], 1f);
            FakeMaterial material = new FakeMaterial("材質");
            Now(material).Faces.Add(new FakeFace(first, second, first));
            _fixture.Model.Material.Add(material);

            Weights(
                Operation(ModelEditWeights.Smooth),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelEditWeights.StrengthName, 0.5));

            Near(0.5, Share(first, NowAll(bones)[0]));
            Near(0.5, Share(first, NowAll(bones)[1]));
            Near(1.0, Share(second, NowAll(bones)[1]));
        }

        [Fact]
        public void SmoothingWithoutTheStrengthIsRefused()
        {
            Bones("一");
            Vertex(0f, 0f, 0f);

            IDictionary<string, object> envelope = Weights(
                Operation(ModelEditWeights.Smooth),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AStrengthOutsideTheUnitRangeIsRefused()
        {
            Bones("一");
            Vertex(0f, 0f, 0f);

            IDictionary<string, object> envelope = Weights(
                Operation(ModelEditWeights.Smooth),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditWeights.StrengthName, 1.5));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void TakingTheWeightFromTheNearestBonePutsItAllOnThatBone()
        {
            IList<IPXBone> bones = Bones("近い", "遠い");
            ((FakeBone)NowAll(bones)[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)NowAll(bones)[1]).Position = new V3(10f, 0f, 0f);
            FakeVertex vertex = Vertex(1f, 0f, 0f);

            Weights(
                Operation(ModelEditWeights.FromNearestBonePosition),
                ComposedEditFixture.Given("all", true));

            Near(1.0, Share(vertex, NowAll(bones)[0]));
            Near(0.0, Share(vertex, NowAll(bones)[1]));
        }

        [Fact]
        public void TakingTheWeightFromTheNearestBoneLineUsesTheStretchToTheTip()
        {
            IList<IPXBone> bones = Bones("線", "点");
            ((FakeBone)NowAll(bones)[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)NowAll(bones)[0]).ToOffset = new V3(10f, 0f, 0f);
            ((FakeBone)NowAll(bones)[1]).Position = new V3(0f, 4f, 0f);
            FakeVertex vertex = Vertex(8f, 1f, 0f);

            Weights(
                Operation(ModelEditWeights.FromNearestBoneAxis),
                ComposedEditFixture.Given("all", true));

            Near(1.0, Share(vertex, NowAll(bones)[0]));
        }

        [Fact]
        public void TakingTheWeightFromTheMirrorSwapsTheSideInTheBoneName()
        {
            IList<IPXBone> bones = Bones("左腕", "右腕");
            FakeVertex left = Vertex(1f, 0f, 0f);
            Weigh(left, NowAll(bones)[0], 1f);
            FakeVertex right = Vertex(-1f, 0f, 0f);

            Weights(
                Operation(ModelEditWeights.FromMirror),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ModelEditWeights.AxisName, ModelEditVertices.AxisX));

            Near(1.0, Share(right, NowAll(bones)[1]));
        }

        [Theory]
        [InlineData("左腕捩左", "右腕捩左", "右腕捩右")]
        [InlineData("右腕捩右", "左腕捩右", "左腕捩左")]
        [InlineData("腕左捩右", "腕左捩左", "腕右捩右")]
        public void TakingTheWeightFromTheMirrorSwapsOnlyTheSideAtTheStartOrElseAtTheEndOfTheBoneName(
            string weighed, string partner, string other)
        {
            IList<IPXBone> bones = Bones(weighed, partner, other);
            FakeVertex left = Vertex(1f, 0f, 0f);
            Weigh(left, NowAll(bones)[0], 1f);
            FakeVertex right = Vertex(-1f, 0f, 0f);

            Weights(
                Operation(ModelEditWeights.FromMirror),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ModelEditWeights.AxisName, ModelEditVertices.AxisX));

            Near(1.0, Share(right, NowAll(bones)[1]));
            Near(0.0, Share(right, NowAll(bones)[2]));
        }

        [Fact]
        public void TakingTheWeightFromTheMirrorKeepsABoneWhoseSideIsOnlyInTheMiddleOfTheName()
        {
            IList<IPXBone> bones = Bones("腕左捩", "腕右捩");
            FakeVertex left = Vertex(1f, 0f, 0f);
            Weigh(left, NowAll(bones)[0], 1f);
            FakeVertex right = Vertex(-1f, 0f, 0f);

            Weights(
                Operation(ModelEditWeights.FromMirror),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ModelEditWeights.AxisName, ModelEditVertices.AxisX));

            Near(1.0, Share(right, NowAll(bones)[0]));
            Near(0.0, Share(right, NowAll(bones)[1]));
        }

        [Theory]
        [InlineData(ModelEditVertices.AxisX)]
        [InlineData(ModelEditVertices.AxisY)]
        [InlineData(ModelEditVertices.AxisZ)]
        public void TakingTheWeightFromAnSdefMirrorTakesItsPointsTurnedAcrossTheAxis(string axis)
        {
            IList<IPXBone> bones = Bones("左腕", "左ひじ", "右腕", "右ひじ");
            ((FakeBone)NowAll(bones)[0]).Position = new V3(1f, 1f, 1f);
            ((FakeBone)NowAll(bones)[1]).Position = new V3(1f, 3f, 3f);
            ((FakeBone)NowAll(bones)[2]).Position = Across(new V3(1f, 1f, 1f), axis);
            ((FakeBone)NowAll(bones)[3]).Position = Across(new V3(1f, 3f, 3f), axis);
            FakeVertex left = Vertex(2f, 3f, 1f);
            Now(left).SDEF = true;
            Now(left).Bone1 = NowAll(bones)[0];
            Now(left).Weight1 = 0.4f;
            Now(left).Bone2 = NowAll(bones)[1];
            Now(left).Weight2 = 0.6f;
            Now(left).SDEF_C = new V3(1.5f, 2f, 2f);
            Now(left).SDEF_R0 = new V3(1f, 1.5f, 1.25f);
            Now(left).SDEF_R1 = new V3(1f, 2.5f, 2.75f);
            V3 across = Across(new V3(2f, 3f, 1f), axis);
            FakeVertex right = Vertex(across.X, across.Y, across.Z);
            Weigh(right, NowAll(bones)[2], 1f);

            Weights(
                Operation(ModelEditWeights.FromMirror),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ModelEditWeights.AxisName, axis));

            IPXVertex now = Now(right);
            Assert.True(now.SDEF);
            Near(0.4, Share(right, NowAll(bones)[2]));
            Near(0.6, Share(right, NowAll(bones)[3]));
            V3 upper = now.Bone1.Name == "右腕" ? now.SDEF_R0 : now.SDEF_R1;
            V3 lower = now.Bone1.Name == "右腕" ? now.SDEF_R1 : now.SDEF_R0;
            NearPoint(Across(new V3(1f, 1.5f, 1.25f), axis), upper);
            NearPoint(Across(new V3(1f, 2.5f, 2.75f), axis), lower);
            NearPoint(Across(new V3(1f, 2f, 2f), axis), now.SDEF_C);
        }

        [Fact]
        public void TakingTheWeightFromMirrorsOnBothSidesReadsWhatEachHeldBefore()
        {
            IList<IPXBone> bones = Bones("左腕", "左ひじ", "右腕", "右ひじ");
            FakeVertex left = Vertex(1f, 0f, 0f);
            Now(left).SDEF = true;
            Now(left).Bone1 = NowAll(bones)[0];
            Now(left).Weight1 = 0.4f;
            Now(left).Bone2 = NowAll(bones)[1];
            Now(left).Weight2 = 0.6f;
            FakeVertex right = Vertex(-1f, 0f, 0f);
            Now(right).Bone1 = NowAll(bones)[2];
            Now(right).Weight1 = 0.5f;
            Now(right).Bone2 = NowAll(bones)[3];
            Now(right).Weight2 = 0.5f;

            Weights(
                Operation(ModelEditWeights.FromMirror),
                ComposedEditFixture.Given("indices", new object[] { 0, 1 }),
                ComposedEditFixture.Given(ModelEditWeights.AxisName, ModelEditVertices.AxisX));

            Assert.False(Now(left).SDEF);
            Near(0.5, Share(left, NowAll(bones)[0]));
            Assert.True(Now(right).SDEF);
            Near(0.4, Share(right, NowAll(bones)[2]));
            Near(0.6, Share(right, NowAll(bones)[3]));
        }

        [Fact]
        public void TakingOnlyTheSdefFromTheMirrorCountsTheVertexAsChanged()
        {
            IList<IPXBone> bones = Bones("腕", "ひじ");
            FakeVertex left = Vertex(1f, 0f, 0f);
            Now(left).SDEF = true;
            Now(left).Bone1 = NowAll(bones)[0];
            Now(left).Weight1 = 0.4f;
            Now(left).Bone2 = NowAll(bones)[1];
            Now(left).Weight2 = 0.6f;
            FakeVertex right = Vertex(-1f, 0f, 0f);
            Now(right).Bone1 = NowAll(bones)[0];
            Now(right).Weight1 = 0.4f;
            Now(right).Bone2 = NowAll(bones)[1];
            Now(right).Weight2 = 0.6f;

            IDictionary<string, object> value = ComposedEditFixture.Value(Weights(
                Operation(ModelEditWeights.FromMirror),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ModelEditWeights.AxisName, ModelEditVertices.AxisX)));

            Assert.True(Now(right).SDEF);
            Assert.Equal(1, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void TakingTheWeightFromAMirrorThatIsNotSdefLeavesNoSdef()
        {
            IList<IPXBone> bones = Bones("左腕", "左ひじ", "右腕", "右ひじ");
            FakeVertex left = Vertex(1f, 0f, 0f);
            Now(left).Bone1 = NowAll(bones)[0];
            Now(left).Weight1 = 0.4f;
            Now(left).Bone2 = NowAll(bones)[1];
            Now(left).Weight2 = 0.6f;
            FakeVertex right = Vertex(-1f, 0f, 0f);
            Now(right).SDEF = true;
            Now(right).Bone1 = NowAll(bones)[2];
            Now(right).Weight1 = 0.5f;
            Now(right).Bone2 = NowAll(bones)[3];
            Now(right).Weight2 = 0.5f;

            Weights(
                Operation(ModelEditWeights.FromMirror),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ModelEditWeights.AxisName, ModelEditVertices.AxisX));

            Assert.False(Now(right).SDEF);
            Near(0.4, Share(right, NowAll(bones)[2]));
            Near(0.6, Share(right, NowAll(bones)[3]));
        }

        [Fact]
        public void ReplacingTheBoneMovesItsShareToTheOtherBoneOnlyOnThePickedVerticesHoldingIt()
        {
            IList<IPXBone> bones = Bones("元", "先", "他");
            FakeVertex holding = Vertex(0f, 0f, 0f);
            Now(holding).Bone1 = NowAll(bones)[2];
            Now(holding).Weight1 = 0.4f;
            Now(holding).Bone2 = NowAll(bones)[0];
            Now(holding).Weight2 = 0.6f;
            FakeVertex bare = Vertex(1f, 0f, 0f);
            Weigh(bare, NowAll(bones)[2], 1f);
            FakeVertex unpicked = Vertex(2f, 0f, 0f);
            Weigh(unpicked, NowAll(bones)[0], 1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Weights(
                Operation(ModelEditWeights.ReplaceBone),
                ComposedEditFixture.Given("indices", new object[] { 0, 1 }),
                ComposedEditFixture.Given(ModelEditWeights.FromBoneName, 0),
                ComposedEditFixture.Given(ModelEditWeights.ToBoneName, 1)));

            Near(0.0, Share(holding, NowAll(bones)[0]));
            Near(0.6, Share(holding, NowAll(bones)[1]));
            Near(0.4, Share(holding, NowAll(bones)[2]));
            Near(1.0, Share(bare, NowAll(bones)[2]));
            Near(1.0, Share(unpicked, NowAll(bones)[0]));
            Assert.Equal(1, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void ReplacingTheBoneAddsUpWhenTheOtherBoneAlreadyHoldsAShare()
        {
            IList<IPXBone> bones = Bones("元", "先");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Bone1 = NowAll(bones)[0];
            Now(vertex).Weight1 = 0.3f;
            Now(vertex).Bone2 = NowAll(bones)[1];
            Now(vertex).Weight2 = 0.7f;

            Weights(
                Operation(ModelEditWeights.ReplaceBone),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditWeights.FromBoneName, 0),
                ComposedEditFixture.Given(ModelEditWeights.ToBoneName, 1));

            Near(0.0, Share(vertex, NowAll(bones)[0]));
            Near(1.0, Share(vertex, NowAll(bones)[1]));
            Assert.Null(Now(vertex).Bone2);
        }

        [Fact]
        public void ReplacingTheBoneLeavesTheVerticesWithoutItUntouchedAndUncounted()
        {
            IList<IPXBone> bones = Bones("元", "先", "甲", "乙");
            FakeVertex bare = Vertex(0f, 0f, 0f);
            Now(bare).Bone1 = NowAll(bones)[2];
            Now(bare).Weight1 = 0.3f;
            Now(bare).Bone2 = NowAll(bones)[3];
            Now(bare).Weight2 = 0.7f;

            IDictionary<string, object> value = ComposedEditFixture.Value(Weights(
                Operation(ModelEditWeights.ReplaceBone),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditWeights.FromBoneName, 0),
                ComposedEditFixture.Given(ModelEditWeights.ToBoneName, 1)));

            Assert.Equal(0, value[ModelEditWeights.ChangedName]);
            Near(0.3, Share(bare, NowAll(bones)[2]));
            Near(0.7, Share(bare, NowAll(bones)[3]));
        }

        [Fact]
        public void ReplacingTheBoneLeavesAVertexWhoseShareOfItIsZeroUntouchedAndUncounted()
        {
            IList<IPXBone> bones = Bones("元", "先", "甲");
            FakeVertex empty = Vertex(0f, 0f, 0f);
            Now(empty).Bone1 = NowAll(bones)[2];
            Now(empty).Weight1 = 1f;
            Now(empty).Bone2 = NowAll(bones)[0];
            Now(empty).Weight2 = 0f;

            IDictionary<string, object> value = ComposedEditFixture.Value(Weights(
                Operation(ModelEditWeights.ReplaceBone),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditWeights.FromBoneName, 0),
                ComposedEditFixture.Given(ModelEditWeights.ToBoneName, 1)));

            Assert.Equal(0, value[ModelEditWeights.ChangedName]);
            Assert.Same(NowAll(bones)[0], Now(empty).Bone2);
            Near(1.0, Share(empty, NowAll(bones)[2]));
        }

        [Fact]
        public void ReplacingTheBoneSettlesTheTotalOfTheVerticesItTouchesToOne()
        {
            IList<IPXBone> bones = Bones("元", "先", "甲");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Bone1 = NowAll(bones)[0];
            Now(vertex).Weight1 = 0.6f;
            Now(vertex).Bone2 = NowAll(bones)[2];
            Now(vertex).Weight2 = 0.6f;

            Weights(
                Operation(ModelEditWeights.ReplaceBone),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditWeights.FromBoneName, 0),
                ComposedEditFixture.Given(ModelEditWeights.ToBoneName, 1));

            Near(0.5, Share(vertex, NowAll(bones)[1]));
            Near(0.5, Share(vertex, NowAll(bones)[2]));
        }

        [Fact]
        public void ReplacingTheBoneOfAnSdefVertexKeepsTheBoneInItsSlot()
        {
            IList<IPXBone> bones = Bones("元", "先", "甲");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).SDEF = true;
            Now(vertex).Bone1 = NowAll(bones)[2];
            Now(vertex).Weight1 = 0.3f;
            Now(vertex).Bone2 = NowAll(bones)[0];
            Now(vertex).Weight2 = 0.7f;
            Now(vertex).SDEF_R0 = new V3(1f, 0f, 0f);
            Now(vertex).SDEF_R1 = new V3(0f, 1f, 0f);

            Weights(
                Operation(ModelEditWeights.ReplaceBone),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditWeights.FromBoneName, 0),
                ComposedEditFixture.Given(ModelEditWeights.ToBoneName, 1));

            IPXVertex now = Now(vertex);
            Assert.True(now.SDEF);
            Near(0.3, Share(vertex, NowAll(bones)[2]));
            Near(0.7, Share(vertex, NowAll(bones)[1]));
            V3 firstCentre = now.Bone1.Name == "甲" ? now.SDEF_R0 : now.SDEF_R1;
            V3 secondCentre = now.Bone1.Name == "甲" ? now.SDEF_R1 : now.SDEF_R0;
            Near(1.0, firstCentre.X);
            Near(1.0, secondCentre.Y);
        }

        [Fact]
        public void ReplacingOneOfTheTwoBonesOfAnSdefVertexWithTheOtherLeavesOneBoneAndNoSdef()
        {
            IList<IPXBone> bones = Bones("元", "先");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).SDEF = true;
            Now(vertex).Bone1 = NowAll(bones)[0];
            Now(vertex).Weight1 = 0.3f;
            Now(vertex).Bone2 = NowAll(bones)[1];
            Now(vertex).Weight2 = 0.7f;

            Weights(
                Operation(ModelEditWeights.ReplaceBone),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditWeights.FromBoneName, 0),
                ComposedEditFixture.Given(ModelEditWeights.ToBoneName, 1));

            Assert.False(Now(vertex).SDEF);
            Assert.Equal("先", Now(vertex).Bone1.Name);
            Assert.Null(Now(vertex).Bone2);
            Near(1.0, Now(vertex).Weight1);
        }

        [Fact]
        public void ReplacingTheBoneNeedsBothBonesInsideTheModelAndDifferent()
        {
            Bones("元", "先");
            Vertex(0f, 0f, 0f);
            object[][] wrong =
            {
                new object[] { 0, null, ToolEnvelope.InvalidArgument },
                new object[] { null, 1, ToolEnvelope.InvalidArgument },
                new object[] { 0, 0.5, ToolEnvelope.InvalidArgument },
                new object[] { "a", 1, ToolEnvelope.InvalidArgument },
                new object[] { 0, 2, ToolEnvelope.IndexOutOfRange },
                new object[] { -1, 1, ToolEnvelope.IndexOutOfRange },
                new object[] { 1, -1, ToolEnvelope.IndexOutOfRange },
                new object[] { 1, 1, ToolEnvelope.InvalidArgument },
            };
            foreach (object[] pair in wrong)
            {
                List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>
                {
                    Operation(ModelEditWeights.ReplaceBone),
                    ComposedEditFixture.Given("all", true),
                };
                if (pair[0] != null)
                {
                    given.Add(ComposedEditFixture.Given(ModelEditWeights.FromBoneName, pair[0]));
                }

                if (pair[1] != null)
                {
                    given.Add(ComposedEditFixture.Given(ModelEditWeights.ToBoneName, pair[1]));
                }

                Assert.Equal(
                    (string)pair[2],
                    ComposedEditFixture.Code(Weights(given.ToArray())));
            }
        }

        [Fact]
        public void TheBoneInputsOfReplacingAreRefusedForOtherOperations()
        {
            Bones("元", "先");
            Vertex(0f, 0f, 0f);

            IDictionary<string, object> envelope = Weights(
                Operation(ModelEditWeights.Normalize),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditWeights.FromBoneName, 0));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void NormalisingTheWeightsMakesThemAddUpToOne()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Bone1 = NowAll(bones)[0];
            Now(vertex).Weight1 = 1f;
            Now(vertex).Bone2 = NowAll(bones)[1];
            Now(vertex).Weight2 = 1f;

            Weights(
                Operation(ModelEditWeights.Normalize),
                ComposedEditFixture.Given("all", true));

            Near(0.5, Now(vertex).Weight1);
            Near(0.5, Now(vertex).Weight2);
        }

        [Fact]
        public void NormalisingAnSdefVertexKeepsItsBonesWithTheirCentres()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).SDEF = true;
            Now(vertex).Bone1 = NowAll(bones)[0];
            Now(vertex).Weight1 = 0.25f;
            Now(vertex).Bone2 = NowAll(bones)[1];
            Now(vertex).Weight2 = 0.75f;
            Now(vertex).SDEF_R0 = new V3(0f, 1f, 0f);
            Now(vertex).SDEF_R1 = new V3(0f, 2f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Weights(
                Operation(ModelEditWeights.Normalize),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal(0, value[ModelEditWeights.ChangedName]);
            Assert.Same(NowAll(bones)[0], Now(vertex).Bone1);
            Assert.Same(NowAll(bones)[1], Now(vertex).Bone2);
            Near(1.0, Now(vertex).SDEF_R0.Y);
            Near(2.0, Now(vertex).SDEF_R1.Y);
        }

        [Fact]
        public void RewritingTheWeightsRemakesOnlyTheWeightsInTheView()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Bone1 = NowAll(bones)[0];
            Now(vertex).Weight1 = 1f;

            Weights(
                Operation(ModelEditWeights.Normalize),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(new[] { ScreenRefresh.WeightKind }, _fixture.View.Remade);
            Assert.Equal(0, _fixture.View.Redraws);
        }

        [Fact]
        public void ConvertingTheDeformTypeRemakesOnlyTheWeightsInTheView()
        {
            IList<IPXBone> bones = Bones("一");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Bone1 = NowAll(bones)[0];
            Now(vertex).Weight1 = 1f;

            Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelSetDeformType.DeformName, ModelSetDeformType.Bdef1));

            Assert.Equal(new[] { ScreenRefresh.WeightKind }, _fixture.View.Remade);
            Assert.Equal(0, _fixture.View.Redraws);
        }

        [Fact]
        public void NormalisingAVertexWithNoWeightAtAllPutsItAllOnItsFirstBone()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Bone1 = NowAll(bones)[0];
            Now(vertex).Weight1 = 0f;
            Now(vertex).Bone2 = NowAll(bones)[1];
            Now(vertex).Weight2 = 0f;

            Weights(
                Operation(ModelEditWeights.Normalize),
                ComposedEditFixture.Given("all", true));

            Assert.Same(NowAll(bones)[0], Now(vertex).Bone1);
            Near(1.0, Now(vertex).Weight1);
            Assert.Null(Now(vertex).Bone2);
        }

        [Fact]
        public void AveragingLeavesNoWeightOnABoneThatIsNotInTheList()
        {
            IList<IPXBone> bones = Bones("親");
            FakeBone gone = new FakeBone("消えた") { Parent = NowAll(bones)[0] };
            FakeVertex first = Vertex(0f, 0f, 0f);
            Weigh(first, gone, 1f);
            FakeVertex second = Vertex(1f, 0f, 0f);
            Weigh(second, NowAll(bones)[0], 1f);

            Weights(
                Operation(ModelEditWeights.Average),
                ComposedEditFixture.Given("all", true),
                _fixture.HoldModel());

            Assert.Same(NowAll(bones)[0], Now(first).Bone1);
            Assert.Null(Now(first).Bone2);
            Near(1.0, Share(first, NowAll(bones)[0]));
            Near(1.0, Share(second, NowAll(bones)[0]));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void NormalisingClearsAWeightLeftInASlotWithNoBone(bool suppressed)
        {
            IList<IPXBone> bones = Bones("一");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Bone1 = NowAll(bones)[0];
            Now(vertex).Weight1 = 1f;
            Now(vertex).Weight2 = 0.5f;

            IDictionary<string, object> value = ComposedEditFixture.Value(Weights(Undo(
                suppressed,
                Operation(ModelEditWeights.Normalize),
                ComposedEditFixture.Given("all", true))));

            Near(0.0, Now(vertex).Weight2);
            Assert.Equal(1, value[ModelEditWeights.ChangedName]);
            Assert.Equal(suppressed ? 1 : 0, _fixture.Partials.Count);
        }

        [Fact]
        public void AveragingWeightsTooLargeToAddInSinglePrecisionStillSharesThemOut()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex first = Vertex(0f, 0f, 0f);
            Now(first).Bone1 = NowAll(bones)[0];
            Now(first).Weight1 = float.MaxValue;
            Now(first).Bone2 = NowAll(bones)[0];
            Now(first).Weight2 = float.MaxValue;
            FakeVertex second = Vertex(1f, 0f, 0f);
            Weigh(second, NowAll(bones)[1], 1f);

            Weights(
                Operation(ModelEditWeights.Average),
                ComposedEditFixture.Given("all", true));

            Near(1.0, Share(first, NowAll(bones)[0]));
            Near(1.0, Share(second, NowAll(bones)[0]));
        }

        [Fact]
        public void RepairingMovesAWeightOffABoneThatIsNotInTheList()
        {
            IList<IPXBone> bones = Bones("親");
            FakeBone gone = new FakeBone("消えた") { Parent = NowAll(bones)[0] };
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Weigh(vertex, gone, 1f);

            Weights(
                Operation(ModelEditWeights.RepairMissingBone),
                ComposedEditFixture.Given("all", true),
                _fixture.HoldModel());

            Assert.Same(NowAll(bones)[0], Now(vertex).Bone1);
            Near(1.0, Now(vertex).Weight1);
        }

        [Fact]
        public void WeightsTooLargeToAddLandOnTheAncestorAsOneWholeShare()
        {
            IList<IPXBone> bones = Bones("親");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Bone1 = new FakeBone("一") { Parent = NowAll(bones)[0] };
            Now(vertex).Weight1 = float.MaxValue;
            Now(vertex).Bone2 = new FakeBone("二") { Parent = NowAll(bones)[0] };
            Now(vertex).Weight2 = float.MaxValue;
            Now(vertex).Bone3 = new FakeBone("三") { Parent = NowAll(bones)[0] };
            Now(vertex).Weight3 = float.MaxValue;
            Now(vertex).Bone4 = new FakeBone("四") { Parent = NowAll(bones)[0] };
            Now(vertex).Weight4 = float.MaxValue;

            Weights(
                Operation(ModelEditWeights.RepairMissingBone),
                ComposedEditFixture.Given("all", true),
                _fixture.HoldModel());

            Assert.Same(NowAll(bones)[0], Now(vertex).Bone1);
            Near(1.0, Now(vertex).Weight1);
            Assert.Null(Now(vertex).Bone2);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ConvertingToOneBoneKeepsTheHeaviestAndDropsTheRest(bool suppressed)
        {
            IList<IPXBone> bones = Bones("重い", "軽い");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Bone1 = NowAll(bones)[1];
            Now(vertex).Weight1 = 0.25f;
            Now(vertex).Bone2 = NowAll(bones)[0];
            Now(vertex).Weight2 = 0.75f;

            IDictionary<string, object> value = ComposedEditFixture.Value(Deform(Undo(
                suppressed,
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Bdef1))));

            Assert.Same(NowAll(bones)[0], Now(vertex).Bone1);
            Near(1.0, Now(vertex).Weight1);
            Assert.Null(Now(vertex).Bone2);
            Assert.Equal(1, value[ModelSetDeformType.ChangedName]);
            Assert.Equal(suppressed ? 1 : 0, _fixture.Partials.Count);
        }

        [Fact]
        public void ConvertingToSdefKeepsTwoBonesAndTurnsTheStyleOn()
        {
            IList<IPXBone> bones = Bones("一", "二");
            ((FakeBone)NowAll(bones)[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)NowAll(bones)[1]).Position = new V3(2f, 0f, 0f);
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Bone1 = NowAll(bones)[0];
            Now(vertex).Weight1 = 0.5f;
            Now(vertex).Bone2 = NowAll(bones)[1];
            Now(vertex).Weight2 = 0.5f;

            Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Sdef));

            Assert.True(Now(vertex).SDEF);
            Assert.False(Now(vertex).QDEF);
            Near(1.0, Now(vertex).SDEF_C.X);
        }

        [Fact]
        public void ConvertingToSdefUsesTheBonesThatOutliveTheOnesItDropped()
        {
            IList<IPXBone> bones = Bones("親一", "親二");
            ((FakeBone)NowAll(bones)[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)NowAll(bones)[1]).Position = new V3(4f, 0f, 0f);
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Bone1 = new FakeBone("消えた一")
            {
                Parent = NowAll(bones)[0],
                Position = new V3(100f, 0f, 0f),
            };
            Now(vertex).Weight1 = 0.5f;
            Now(vertex).Bone2 = new FakeBone("消えた二")
            {
                Parent = NowAll(bones)[1],
                Position = new V3(200f, 0f, 0f),
            };
            Now(vertex).Weight2 = 0.5f;

            Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Sdef),
                _fixture.HoldModel());

            Assert.Same(NowAll(bones)[0], Now(vertex).Bone1);
            Assert.Same(NowAll(bones)[1], Now(vertex).Bone2);
            Assert.True(Now(vertex).SDEF);
            Near(2.0, Now(vertex).SDEF_C.X);
        }

        [Fact]
        public void ConvertingToTwoBonesKeepsTheTwoHeaviestAndShareThemOut()
        {
            IList<IPXBone> bones = Bones("一", "二", "三");
            FakeVertex vertex = Spread(bones, 0.5f, 0.3f, 0.2f);

            Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Bdef2));

            Assert.Same(NowAll(bones)[0], Now(vertex).Bone1);
            Assert.Same(NowAll(bones)[1], Now(vertex).Bone2);
            Assert.Null(Now(vertex).Bone3);
            Near(0.625, Now(vertex).Weight1);
            Near(0.375, Now(vertex).Weight2);
            Assert.False(Now(vertex).SDEF);
            Assert.False(Now(vertex).QDEF);
        }

        [Fact]
        public void ConvertingToFourBonesKeepsEveryOneItAlreadyHad()
        {
            IList<IPXBone> bones = Bones("一", "二", "三");
            FakeVertex vertex = Spread(bones, 0.5f, 0.3f, 0.2f);

            Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Bdef4));

            Assert.Same(NowAll(bones)[2], Now(vertex).Bone3);
            Near(0.0, Now(vertex).Weight4);
            Near(0.5, Now(vertex).Weight1);
            Near(0.2, Now(vertex).Weight3);
            Assert.False(Now(vertex).SDEF);
            Assert.False(Now(vertex).QDEF);
        }

        [Fact]
        public void ConvertingToTheDualQuaternionStyleTurnsThatStyleOnAndTheOtherOff()
        {
            IList<IPXBone> bones = Bones("一", "二", "三");
            FakeVertex vertex = Spread(bones, 0.5f, 0.3f, 0.2f);
            Now(vertex).SDEF = true;

            Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Qdef));

            Assert.True(Now(vertex).QDEF);
            Assert.False(Now(vertex).SDEF);
            Assert.Same(NowAll(bones)[2], Now(vertex).Bone3);
        }

        [Fact]
        public void ConvertingToSdefWithOnlyOneBoneLeavesThatStyleOff()
        {
            IList<IPXBone> bones = Bones("一");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Weigh(vertex, NowAll(bones)[0], 1f);

            Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Sdef));

            Assert.False(Now(vertex).SDEF);
            Assert.Same(NowAll(bones)[0], Now(vertex).Bone1);
            Near(1.0, Now(vertex).Weight1);
        }

        [Fact]
        public void ConvertingAVertexWhoseWeightsAddUpToNothingPutsItAllOnItsFirstBone()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Bone1 = NowAll(bones)[0];
            Now(vertex).Weight1 = 0f;
            Now(vertex).Bone2 = NowAll(bones)[1];
            Now(vertex).Weight2 = 0f;

            Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Bdef2));

            Assert.Same(NowAll(bones)[0], Now(vertex).Bone1);
            Near(1.0, Now(vertex).Weight1);
            Assert.Null(Now(vertex).Bone2);
        }

        [Fact]
        public void ConvertingWithoutTheStyleIsRefused()
        {
            Bones("一");
            Vertex(0f, 0f, 0f);

            IDictionary<string, object> envelope = Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void NormalisingTheSdefCentreTakesTheMiddleOfTheTwoBones()
        {
            IList<IPXBone> bones = Bones("一", "二");
            ((FakeBone)NowAll(bones)[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)NowAll(bones)[1]).Position = new V3(4f, 0f, 0f);
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).SDEF = true;
            Now(vertex).Bone1 = NowAll(bones)[0];
            Now(vertex).Weight1 = 0.5f;
            Now(vertex).Bone2 = NowAll(bones)[1];
            Now(vertex).Weight2 = 0.5f;
            Now(vertex).SDEF_C = new V3(99f, 99f, 99f);

            Deform(
                Operation(ModelSetDeformType.NormalizeSdefC),
                ComposedEditFixture.Given("all", true));

            Near(2.0, Now(vertex).SDEF_C.X);
            Near(0.0, Now(vertex).SDEF_C.Y);
        }

        [Fact]
        public void AnSdefVertexThatCannotHoldTwoBonesGoesBackToTwoBoneBlending()
        {
            IList<IPXBone> bones = Bones("一");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).SDEF = true;
            Weigh(vertex, NowAll(bones)[0], 1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Deform(
                Operation(ModelSetDeformType.RepairInvalidSdef),
                ComposedEditFixture.Given("all", true)));

            Assert.False(Now(vertex).SDEF);
            Assert.False(Now(vertex).QDEF);
            Assert.Same(NowAll(bones)[0], Now(vertex).Bone1);
            Near(1.0, Now(vertex).Weight1);
            Assert.Equal(1, value[ModelSetDeformType.ChangedName]);
        }

        [Fact]
        public void AnSdefVertexThatKeepsTwoBonesIsLeftAlone()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).SDEF = true;
            Now(vertex).Bone1 = NowAll(bones)[0];
            Now(vertex).Weight1 = 0.5f;
            Now(vertex).Bone2 = NowAll(bones)[1];
            Now(vertex).Weight2 = 0.5f;

            IDictionary<string, object> value = ComposedEditFixture.Value(Deform(
                Operation(ModelSetDeformType.RepairInvalidSdef),
                ComposedEditFixture.Given("all", true)));

            Assert.True(Now(vertex).SDEF);
            Assert.Equal(0, value[ModelSetDeformType.ChangedName]);
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

        private IDictionary<string, object> Normals(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ModelEditNormals.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IDictionary<string, object> Weights(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ModelEditWeights.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IDictionary<string, object> Deform(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ModelSetDeformType.ToolName, ComposedEditFixture.Arguments(given));
        }

        [Fact]
        public void AveragingNearAcrossManyVerticesFinishesInTime()
        {
            for (int at = 0; at < ManyVertices; at++)
            {
                Vertex(at, at % 2 == 0 ? 0f : 0.05f, 0f).Normal = new V3(0f, 0f, 1f);
            }

            TimeSpan editor = _fixture.EditorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            Normals(
                Operation(ModelEditNormals.AverageNear),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditNormals.ThresholdName, 0.1));
            elapsed.Stop();
            TimeSpan spent = elapsed.Elapsed - (_fixture.EditorTime - editor);

            Assert.True(spent < TimeLimit, "平均するのに " + spent + " かかった");
        }

        [Fact]
        public void AveragingOnlyTheSameSpotsAcrossManyCloseVerticesFinishesInTime()
        {
            for (int at = 0; at < ManyVertices; at++)
            {
                Vertex(at / (float)ManyVertices, 0f, 0f).Normal = new V3(0f, 0f, 1f);
            }

            TimeSpan editor = _fixture.EditorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            Normals(
                Operation(ModelEditNormals.AverageNear),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditNormals.ThresholdName, 0.0));
            elapsed.Stop();
            TimeSpan spent = elapsed.Elapsed - (_fixture.EditorTime - editor);

            Assert.True(spent < TimeLimit, "平均するのに " + spent + " かかった");
        }

        [Fact]
        public void TakingTheWeightFromTheNearestBoneForManyVerticesFinishesInTime()
        {
            IList<IPXBone> bones = Bones(
                Enumerable.Range(0, ManyBones).Select(at => "骨" + at).ToArray());
            for (int at = 0; at < ManyBones; at++)
            {
                ((FakeBone)NowAll(bones)[at]).Position = new V3(at, 0f, 0f);
            }

            for (int at = 0; at < WeighedVertices; at++)
            {
                Vertex(at % ManyBones, 1f, 0f);
            }

            TimeSpan editor = _fixture.EditorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            Weights(
                Operation(ModelEditWeights.FromNearestBonePosition),
                ComposedEditFixture.Given("all", true));
            elapsed.Stop();
            TimeSpan spent = elapsed.Elapsed - (_fixture.EditorTime - editor);

            Assert.True(spent < TimeLimit, "振るのに " + spent + " かかった");
        }

        private static KeyValuePair<string, object> Operation(string operation)
        {
            return ComposedEditFixture.Given(ComposedOperation.OperationName, operation);
        }

        private FakeVertex Vertex(float x, float y, float z)
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

        /// <summary>3つのボーンへ重みを振った頂点。</summary>
        private FakeVertex Spread(IList<IPXBone> bones, float first, float second, float third)
        {
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Bone1 = NowAll(bones)[0];
            Now(vertex).Weight1 = first;
            Now(vertex).Bone2 = NowAll(bones)[1];
            Now(vertex).Weight2 = second;
            Now(vertex).Bone3 = NowAll(bones)[2];
            Now(vertex).Weight3 = third;

            return vertex;
        }

        private static void Weigh(FakeVertex weighed, IPXBone bone, float share)
        {
            weighed.Bone1 = bone;
            weighed.Weight1 = share;
        }

        /// <summary>
        /// その頂点がそのボーンへ振っている重み。振っていなければ0。頂点もボーンも、いまのモデルで
        /// 同じ位置に並んでいるものを読む。
        /// </summary>
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

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }

        private static void NearPoint(V3 wanted, V3 found)
        {
            Near(wanted.X, found.X);
            Near(wanted.Y, found.Y);
            Near(wanted.Z, found.Z);
        }

        private static V3 Across(V3 given, string axis)
        {
            switch (axis)
            {
                case ModelEditVertices.AxisX:
                    return new V3(-given.X, given.Y, given.Z);

                case ModelEditVertices.AxisY:
                    return new V3(given.X, -given.Y, given.Z);

                default:
                    return new V3(given.X, given.Y, -given.Z);
            }
        }
    }
}
