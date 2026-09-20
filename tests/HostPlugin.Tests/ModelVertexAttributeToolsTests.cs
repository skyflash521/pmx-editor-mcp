using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 頂点が持つ法線・ウェイト・変形方式を変える3つ。どれも1回の呼び出しで、1回のまとめての
    /// 反映に収まる。
    /// </summary>
    public sealed class ModelVertexAttributeToolsTests : IDisposable
    {
        private const string Pending = "impl pending: 頂点の法線とウェイトと変形方式を1回の呼び出しで変える";

        /// <summary>小数の突き合わせで見る桁。</summary>
        private const int Digits = 4;

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact(Skip = Pending)]
        public void AveragingTheNormalsPutsEveryPickedOneOnTheSharedDirection()
        {
            FakeVertex first = Vertex(0f, 0f, 0f);
            first.Normal = new V3(1f, 0f, 0f);
            FakeVertex second = Vertex(1f, 0f, 0f);
            second.Normal = new V3(0f, 1f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Normals(
                Operation(ModelEditNormals.Average),
                ComposedEditFixture.Given("all", true)));

            Near(0.70710678, first.Normal.X);
            Near(0.70710678, first.Normal.Y);
            Near(0.70710678, second.Normal.X);
            Assert.Equal(2, value[ModelEditNormals.ChangedName]);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact(Skip = Pending)]
        public void AveragingNearOnlyJoinsTheOnesInsideTheThreshold()
        {
            FakeVertex first = Vertex(0f, 0f, 0f);
            first.Normal = new V3(1f, 0f, 0f);
            FakeVertex near = Vertex(0.05f, 0f, 0f);
            near.Normal = new V3(0f, 1f, 0f);
            FakeVertex apart = Vertex(5f, 0f, 0f);
            apart.Normal = new V3(0f, 0f, 1f);

            Normals(
                Operation(ModelEditNormals.AverageNear),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditNormals.ThresholdName, 0.1));

            Near(0.70710678, first.Normal.X);
            Near(0.70710678, near.Normal.Y);
            Near(1.0, apart.Normal.Z);
        }

        [Fact(Skip = Pending)]
        public void AveragingNearWithoutTheThresholdIsRefused()
        {
            Vertex(0f, 0f, 0f);

            IDictionary<string, object> envelope = Normals(
                Operation(ModelEditNormals.AverageNear),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact(Skip = Pending)]
        public void TakingTheNormalFromTheFacesUsesTheDirectionTheyFace()
        {
            FakeVertex first = Vertex(0f, 0f, 0f);
            FakeVertex second = Vertex(1f, 0f, 0f);
            FakeVertex third = Vertex(0f, 1f, 0f);
            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(first, second, third));
            _fixture.Model.Material.Add(material);

            Normals(
                Operation(ModelEditNormals.FromFaces),
                ComposedEditFixture.Given("all", true));

            Near(0.0, first.Normal.X);
            Near(0.0, first.Normal.Y);
            Near(1.0, first.Normal.Z);
        }

        [Fact(Skip = Pending)]
        public void NormalisingPutsTheNormalBackOnTheUnitLength()
        {
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            vertex.Normal = new V3(0f, 0f, 2f);

            Normals(
                Operation(ModelEditNormals.Normalize),
                ComposedEditFixture.Given("all", true));

            Near(1.0, vertex.Normal.Z);
        }

        [Fact(Skip = Pending)]
        public void FlippingTurnsTheNormalTheOtherWay()
        {
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            vertex.Normal = new V3(0f, 0f, 1f);

            Normals(
                Operation(ModelEditNormals.Flip),
                ComposedEditFixture.Given("all", true));

            Near(-1.0, vertex.Normal.Z);
        }

        [Fact(Skip = Pending)]
        public void AveragingTheWeightsPutsEveryPickedVertexOnTheSharedShare()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex first = Vertex(0f, 0f, 0f);
            Weigh(first, bones[0], 1f);
            FakeVertex second = Vertex(1f, 0f, 0f);
            Weigh(second, bones[1], 1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Weights(
                Operation(ModelEditWeights.Average),
                ComposedEditFixture.Given("all", true)));

            Near(0.5, Share(first, bones[0]));
            Near(0.5, Share(first, bones[1]));
            Near(0.5, Share(second, bones[0]));
            Assert.Equal(2, value[ModelEditWeights.ChangedName]);
        }

        [Fact(Skip = Pending)]
        public void SmoothingMovesTheWeightTowardsTheNeighboursByTheStrength()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex first = Vertex(0f, 0f, 0f);
            Weigh(first, bones[0], 1f);
            FakeVertex second = Vertex(1f, 0f, 0f);
            Weigh(second, bones[1], 1f);
            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(first, second, first));
            _fixture.Model.Material.Add(material);

            Weights(
                Operation(ModelEditWeights.Smooth),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelEditWeights.StrengthName, 0.5));

            Near(0.5, Share(first, bones[0]));
            Near(0.5, Share(first, bones[1]));
            Near(1.0, Share(second, bones[1]));
        }

        [Fact(Skip = Pending)]
        public void SmoothingWithoutTheStrengthIsRefused()
        {
            Bones("一");
            Vertex(0f, 0f, 0f);

            IDictionary<string, object> envelope = Weights(
                Operation(ModelEditWeights.Smooth),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact(Skip = Pending)]
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

        [Fact(Skip = Pending)]
        public void TakingTheWeightFromTheNearestBonePutsItAllOnThatBone()
        {
            IList<IPXBone> bones = Bones("近い", "遠い");
            ((FakeBone)bones[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)bones[1]).Position = new V3(10f, 0f, 0f);
            FakeVertex vertex = Vertex(1f, 0f, 0f);

            Weights(
                Operation(ModelEditWeights.FromNearestBonePosition),
                ComposedEditFixture.Given("all", true));

            Near(1.0, Share(vertex, bones[0]));
            Near(0.0, Share(vertex, bones[1]));
        }

        [Fact(Skip = Pending)]
        public void TakingTheWeightFromTheNearestBoneLineUsesTheStretchToTheTip()
        {
            IList<IPXBone> bones = Bones("線", "点");
            ((FakeBone)bones[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)bones[0]).ToOffset = new V3(10f, 0f, 0f);
            ((FakeBone)bones[1]).Position = new V3(0f, 4f, 0f);
            FakeVertex vertex = Vertex(8f, 1f, 0f);

            Weights(
                Operation(ModelEditWeights.FromNearestBoneAxis),
                ComposedEditFixture.Given("all", true));

            Near(1.0, Share(vertex, bones[0]));
        }

        [Fact(Skip = Pending)]
        public void TakingTheWeightFromTheMirrorSwapsTheSideInTheBoneName()
        {
            IList<IPXBone> bones = Bones("左腕", "右腕");
            FakeVertex left = Vertex(1f, 0f, 0f);
            Weigh(left, bones[0], 1f);
            FakeVertex right = Vertex(-1f, 0f, 0f);

            Weights(
                Operation(ModelEditWeights.FromMirror),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ModelEditWeights.AxisName, ModelEditVertices.AxisX));

            Near(1.0, Share(right, bones[1]));
        }

        [Fact(Skip = Pending)]
        public void NormalisingTheWeightsMakesThemAddUpToOne()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            vertex.Bone1 = bones[0];
            vertex.Weight1 = 1f;
            vertex.Bone2 = bones[1];
            vertex.Weight2 = 1f;

            Weights(
                Operation(ModelEditWeights.Normalize),
                ComposedEditFixture.Given("all", true));

            Near(0.5, vertex.Weight1);
            Near(0.5, vertex.Weight2);
        }

        [Fact(Skip = Pending)]
        public void RepairingMovesAWeightOffABoneThatIsNotInTheList()
        {
            IList<IPXBone> bones = Bones("親");
            FakeBone gone = new FakeBone("消えた") { Parent = bones[0] };
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Weigh(vertex, gone, 1f);

            Weights(
                Operation(ModelEditWeights.RepairMissingBone),
                ComposedEditFixture.Given("all", true));

            Assert.Same(bones[0], vertex.Bone1);
            Near(1.0, vertex.Weight1);
        }

        [Fact(Skip = Pending)]
        public void ConvertingToOneBoneKeepsTheHeaviestAndDropsTheRest()
        {
            IList<IPXBone> bones = Bones("重い", "軽い");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            vertex.Bone1 = bones[1];
            vertex.Weight1 = 0.25f;
            vertex.Bone2 = bones[0];
            vertex.Weight2 = 0.75f;

            IDictionary<string, object> value = ComposedEditFixture.Value(Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Bdef1)));

            Assert.Same(bones[0], vertex.Bone1);
            Near(1.0, vertex.Weight1);
            Assert.Null(vertex.Bone2);
            Assert.Equal(1, value[ModelSetDeformType.ChangedName]);
        }

        [Fact(Skip = Pending)]
        public void ConvertingToSdefKeepsTwoBonesAndTurnsTheStyleOn()
        {
            IList<IPXBone> bones = Bones("一", "二");
            ((FakeBone)bones[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)bones[1]).Position = new V3(2f, 0f, 0f);
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            vertex.Bone1 = bones[0];
            vertex.Weight1 = 0.5f;
            vertex.Bone2 = bones[1];
            vertex.Weight2 = 0.5f;

            Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Sdef));

            Assert.True(vertex.SDEF);
            Assert.False(vertex.QDEF);
            Near(1.0, vertex.SDEF_C.X);
        }

        [Fact(Skip = Pending)]
        public void ConvertingToTwoBonesKeepsTheTwoHeaviestAndShareThemOut()
        {
            IList<IPXBone> bones = Bones("一", "二", "三");
            FakeVertex vertex = Spread(bones, 0.5f, 0.3f, 0.2f);

            Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Bdef2));

            Assert.Same(bones[0], vertex.Bone1);
            Assert.Same(bones[1], vertex.Bone2);
            Assert.Null(vertex.Bone3);
            Near(0.625, vertex.Weight1);
            Near(0.375, vertex.Weight2);
            Assert.False(vertex.SDEF);
            Assert.False(vertex.QDEF);
        }

        [Fact(Skip = Pending)]
        public void ConvertingToFourBonesKeepsEveryOneItAlreadyHad()
        {
            IList<IPXBone> bones = Bones("一", "二", "三");
            FakeVertex vertex = Spread(bones, 0.5f, 0.3f, 0.2f);

            Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Bdef4));

            Assert.Same(bones[2], vertex.Bone3);
            Assert.Null(vertex.Bone4);
            Near(0.5, vertex.Weight1);
            Near(0.2, vertex.Weight3);
            Assert.False(vertex.SDEF);
            Assert.False(vertex.QDEF);
        }

        [Fact(Skip = Pending)]
        public void ConvertingToTheDualQuaternionStyleTurnsThatStyleOnAndTheOtherOff()
        {
            IList<IPXBone> bones = Bones("一", "二", "三");
            FakeVertex vertex = Spread(bones, 0.5f, 0.3f, 0.2f);
            vertex.SDEF = true;

            Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Qdef));

            Assert.True(vertex.QDEF);
            Assert.False(vertex.SDEF);
            Assert.Same(bones[2], vertex.Bone3);
        }

        [Fact(Skip = Pending)]
        public void ConvertingToSdefWithOnlyOneBoneLeavesThatStyleOff()
        {
            IList<IPXBone> bones = Bones("一");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Weigh(vertex, bones[0], 1f);

            Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Sdef));

            Assert.False(vertex.SDEF);
            Assert.Same(bones[0], vertex.Bone1);
            Near(1.0, vertex.Weight1);
        }

        [Fact(Skip = Pending)]
        public void ConvertingAVertexWhoseWeightsAddUpToNothingPutsItAllOnItsFirstBone()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            vertex.Bone1 = bones[0];
            vertex.Weight1 = 0f;
            vertex.Bone2 = bones[1];
            vertex.Weight2 = 0f;

            Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelSetDeformType.DeformName, ModelSetDeformType.Bdef2));

            Assert.Same(bones[0], vertex.Bone1);
            Near(1.0, vertex.Weight1);
            Assert.Null(vertex.Bone2);
        }

        [Fact(Skip = Pending)]
        public void ConvertingWithoutTheStyleIsRefused()
        {
            Bones("一");
            Vertex(0f, 0f, 0f);

            IDictionary<string, object> envelope = Deform(
                Operation(ModelSetDeformType.Convert),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact(Skip = Pending)]
        public void NormalisingTheSdefCentreTakesTheMiddleOfTheTwoBones()
        {
            IList<IPXBone> bones = Bones("一", "二");
            ((FakeBone)bones[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)bones[1]).Position = new V3(4f, 0f, 0f);
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            vertex.SDEF = true;
            vertex.Bone1 = bones[0];
            vertex.Weight1 = 0.5f;
            vertex.Bone2 = bones[1];
            vertex.Weight2 = 0.5f;
            vertex.SDEF_C = new V3(99f, 99f, 99f);

            Deform(
                Operation(ModelSetDeformType.NormalizeSdefC),
                ComposedEditFixture.Given("all", true));

            Near(2.0, vertex.SDEF_C.X);
            Near(0.0, vertex.SDEF_C.Y);
        }

        [Fact(Skip = Pending)]
        public void AnSdefVertexThatCannotHoldTwoBonesGoesBackToTwoBoneBlending()
        {
            IList<IPXBone> bones = Bones("一");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            vertex.SDEF = true;
            Weigh(vertex, bones[0], 1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Deform(
                Operation(ModelSetDeformType.RepairInvalidSdef),
                ComposedEditFixture.Given("all", true)));

            Assert.False(vertex.SDEF);
            Assert.False(vertex.QDEF);
            Assert.Same(bones[0], vertex.Bone1);
            Near(1.0, vertex.Weight1);
            Assert.Equal(1, value[ModelSetDeformType.ChangedName]);
        }

        [Fact(Skip = Pending)]
        public void AnSdefVertexThatKeepsTwoBonesIsLeftAlone()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            vertex.SDEF = true;
            vertex.Bone1 = bones[0];
            vertex.Weight1 = 0.5f;
            vertex.Bone2 = bones[1];
            vertex.Weight2 = 0.5f;

            IDictionary<string, object> value = ComposedEditFixture.Value(Deform(
                Operation(ModelSetDeformType.RepairInvalidSdef),
                ComposedEditFixture.Given("all", true)));

            Assert.True(vertex.SDEF);
            Assert.Equal(0, value[ModelSetDeformType.ChangedName]);
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
            vertex.Bone1 = bones[0];
            vertex.Weight1 = first;
            vertex.Bone2 = bones[1];
            vertex.Weight2 = second;
            vertex.Bone3 = bones[2];
            vertex.Weight3 = third;

            return vertex;
        }

        private static void Weigh(FakeVertex vertex, IPXBone bone, float share)
        {
            vertex.Bone1 = bone;
            vertex.Weight1 = share;
        }

        /// <summary>その頂点がそのボーンへ振っている重み。振っていなければ0。</summary>
        private static float Share(IPXVertex vertex, IPXBone bone)
        {
            IPXBone[] bones = { vertex.Bone1, vertex.Bone2, vertex.Bone3, vertex.Bone4 };
            float[] weights = { vertex.Weight1, vertex.Weight2, vertex.Weight3, vertex.Weight4 };
            float share = 0f;
            for (int at = 0; at < bones.Length; at++)
            {
                if (ReferenceEquals(bones[at], bone))
                {
                    share += weights[at];
                }
            }

            return share;
        }

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }
    }
}
