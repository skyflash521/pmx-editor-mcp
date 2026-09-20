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
    /// ボーンの整理と生成、剛体とJointの生成。どちらも1回の呼び出しで、1回のまとめての反映に
    /// 収まる。
    /// </summary>
    public sealed class ModelRigToolsTests : IDisposable
    {
        /// <summary>小数の突き合わせで見る桁。</summary>
        private const int Digits = 4;

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void BonesThatShareANameAreMergedIntoTheFirstOne()
        {
            IList<IPXBone> bones = Bones("腕", "腕", "手");
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Weigh(vertex, bones[1]);

            IDictionary<string, object> value = ComposedEditFixture.Value(Bone(
                Operation(ModelEditBones.MergeSameName),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal(2, _fixture.Model.Bone.Count);
            Assert.Same(bones[0], vertex.Bone1);
            Assert.Equal(1, value[ModelEditBones.RemovedName]);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void ABoneWithNoChildIsPutOutOfReach()
        {
            IList<IPXBone> bones = Bones("親", "子");
            Show(bones[0]);
            Show(bones[1]);
            bones[1].Parent = bones[0];

            Bone(
                Operation(ModelEditBones.HideTipBones),
                ComposedEditFixture.Given("all", true));

            Assert.True(bones[0].Visible);
            Assert.False(bones[1].Visible);
            Assert.False(bones[1].Controllable);
        }

        [Fact]
        public void TheTipBoneBecomesTheGapToThatBone()
        {
            IList<IPXBone> bones = Bones("元", "先");
            ((FakeBone)bones[0]).Position = new V3(0f, 1f, 0f);
            ((FakeBone)bones[1]).Position = new V3(0f, 3f, 0f);
            bones[0].ToBone = bones[1];

            Bone(
                Operation(ModelEditBones.TipToOffset),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Null(bones[0].ToBone);
            Near(2.0, bones[0].ToOffset.Y);
        }

        [Fact]
        public void TheGapBecomesTheChildItPointsAt()
        {
            IList<IPXBone> bones = Bones("元", "先");
            ((FakeBone)bones[0]).Position = new V3(0f, 1f, 0f);
            ((FakeBone)bones[1]).Position = new V3(0f, 3f, 0f);
            bones[1].Parent = bones[0];
            bones[0].ToOffset = new V3(0f, 2f, 0f);

            Bone(
                Operation(ModelEditBones.OffsetToTip),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Same(bones[1], bones[0].ToBone);
        }

        [Fact]
        public void AChildThatComesBeforeItsParentIsMovedAfterIt()
        {
            IList<IPXBone> bones = Bones("子", "親");
            bones[0].Parent = bones[1];

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Bone(Operation(ModelEditBones.RelevelHierarchy)));

            Assert.Same(bones[1], _fixture.Model.Bone[0]);
            Assert.Same(bones[0], _fixture.Model.Bone[1]);
            Assert.Equal(1, value[ModelEditBones.ChangedName]);
        }

        [Fact]
        public void ABoneWithNoParentGetsOneAboveIt()
        {
            IList<IPXBone> bones = Bones("根");

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Bone(Operation(ModelEditBones.AddRootParent)));

            Assert.Equal(2, _fixture.Model.Bone.Count);
            Assert.NotNull(bones[0].Parent);
            Assert.Single((object[])value[ModelEditBones.AddedName]);
        }

        [Fact]
        public void AStageParentIsAddedAtTheSameSpotAndTakesOverTheOldParent()
        {
            IList<IPXBone> bones = Bones("親", "腕");
            ((FakeBone)bones[1]).Position = new V3(1f, 2f, 3f);
            bones[1].Parent = bones[0];

            Bone(
                Operation(ModelEditBones.AddMultiStageParent),
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            IPXBone made = bones[1].Parent;
            Assert.NotSame(bones[0], made);
            Assert.Same(bones[0], made.Parent);
            Near(1.0, made.Position.X);
        }

        [Fact]
        public void AStageChildIsAddedAtTheSameSpotBelowThePickedBone()
        {
            IList<IPXBone> bones = Bones("腕");
            ((FakeBone)bones[0]).Position = new V3(1f, 2f, 3f);

            Bone(
                Operation(ModelEditBones.AddMultiStageChild),
                ComposedEditFixture.Given("all", true));

            IPXBone made = _fixture.Model.Bone.Single(bone => !ReferenceEquals(bone, bones[0]));
            Assert.Same(bones[0], made.Parent);
            Near(3.0, made.Position.Z);
        }

        [Fact]
        public void ABoneIsAddedHalfwayBetweenThePickedOneAndItsParent()
        {
            IList<IPXBone> bones = Bones("親", "子");
            ((FakeBone)bones[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)bones[1]).Position = new V3(0f, 4f, 0f);
            bones[1].Parent = bones[0];

            Bone(
                Operation(ModelEditBones.AddMiddle),
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            IPXBone made = bones[1].Parent;
            Assert.NotSame(bones[0], made);
            Near(2.0, made.Position.Y);
        }

        [Fact]
        public void TheAddedParentTakesItsTurnFromTheBoneItWasAddedFor()
        {
            IList<IPXBone> bones = Bones("腕");

            Bone(
                Operation(ModelEditBones.AddAppendParent),
                ComposedEditFixture.Given("all", true));

            IPXBone made = bones[0].Parent;
            Assert.Same(bones[0], made.AppendParent);
            Assert.True(made.IsAppendRotation);
        }

        [Fact]
        public void ABoneIsAddedAtTheMiddleOfThePickedVertices()
        {
            Vertex(0f, 0f, 0f);
            Vertex(2f, 0f, 0f);
            Vertex(1f, 3f, 0f);

            Bone(
                Operation(ModelEditBones.AddAtVertices),
                ComposedEditFixture.Given("all", true));

            IPXBone made = Assert.Single(_fixture.Model.Bone);
            Near(1.0, made.Position.X);
            Near(1.0, made.Position.Y);
        }

        [Fact]
        public void AnIkBoneIsAddedThatReachesForThePickedBone()
        {
            IList<IPXBone> bones = Bones("根", "膝", "足首");
            bones[1].Parent = bones[0];
            bones[2].Parent = bones[1];

            Bone(
                Operation(ModelEditBones.MakeIk),
                ComposedEditFixture.Given("indices", new object[] { 2 }),
                ComposedEditFixture.Given(ModelEditBones.LinkCountName, 2));

            IPXBone made = _fixture.Model.Bone.Single(bone => bone.IsIK);
            Assert.Same(bones[2], made.IK.Target);
            Assert.Equal(2, made.IK.Links.Count);
        }

        [Fact]
        public void ABoneNamedForTheOtherSideIsMovedToTheMirroredSpot()
        {
            IList<IPXBone> bones = Bones("左腕", "右腕");
            ((FakeBone)bones[0]).Position = new V3(2f, 1f, 0f);
            ((FakeBone)bones[1]).Position = new V3(9f, 9f, 9f);

            Bone(
                Operation(ModelEditBones.MirrorPosition),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ModelEditBones.AxisName, ModelEditVertices.AxisX));

            Near(-2.0, bones[1].Position.X);
            Near(1.0, bones[1].Position.Y);
        }

        [Fact]
        public void TheFixedAxisIsPointedAtTheTip()
        {
            IList<IPXBone> bones = Bones("腕");
            ((FakeBone)bones[0]).ToOffset = new V3(0f, 5f, 0f);

            Bone(
                Operation(ModelEditBones.FixAxisToTip),
                ComposedEditFixture.Given("all", true));

            Assert.True(bones[0].IsFixAxis);
            Near(1.0, bones[0].FixAxis.Y);
        }

        [Fact]
        public void TheLocalAxisIsBuiltFromTheTipAndTheParent()
        {
            IList<IPXBone> bones = Bones("親", "腕");
            ((FakeBone)bones[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)bones[1]).Position = new V3(0f, 1f, 0f);
            bones[1].Parent = bones[0];
            ((FakeBone)bones[1]).ToOffset = new V3(2f, 0f, 0f);

            Bone(
                Operation(ModelEditBones.SetLocalAxis),
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            V3 across;
            V3 up;
            V3 along;
            bones[1].GetLocalAxis(out across, out up, out along);
            Assert.True(bones[1].IsLocalFrame);
            Near(1.0, across.X);
        }

        [Fact]
        public void TheLocalAxisIsTakenBackOff()
        {
            IList<IPXBone> bones = Bones("腕");
            bones[0].IsLocalFrame = true;

            Bone(
                Operation(ModelEditBones.ResetLocalAxis),
                ComposedEditFixture.Given("all", true));

            Assert.False(bones[0].IsLocalFrame);
        }

        [Fact]
        public void ThePmdKindIsSetFromWhatTheBoneCanDo()
        {
            IList<IPXBone> bones = Bones("腕");
            bones[0].IsRotation = true;
            bones[0].Visible = true;

            IDictionary<string, object> value = ComposedEditFixture.Value(Bone(
                Operation(ModelEditBones.SetPmdBoneKind),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal(BoneKind.Rotate, ((FakeBone)bones[0]).PmdKind);
            Assert.Equal(1, value[ModelEditBones.ChangedName]);
        }

        [Fact]
        public void PickingBonesForAnOperationThatTakesTheWholeListIsRefused()
        {
            Bones("腕");

            IDictionary<string, object> envelope = Bone(
                Operation(ModelEditBones.RelevelHierarchy),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AnOperationTheBoneToolDoesNotKnowIsRefused()
        {
            Bones("腕");

            IDictionary<string, object> envelope = Bone(
                Operation("いない操作"),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void ABodyThatFollowsItsBoneIsAddedForEachPickedBone()
        {
            IList<IPXBone> bones = Bones("腕", "手");
            ((FakeBone)bones[0]).Position = new V3(1f, 2f, 3f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Physics(
                Operation(ModelCreatePhysics.BodyFollowBone),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelCreatePhysics.ShapeName, ModelCreatePhysics.Sphere)));

            Assert.Equal(2, _fixture.Model.Body.Count);
            Assert.Same(bones[0], _fixture.Model.Body[0].Bone);
            Near(1.0, _fixture.Model.Body[0].Position.X);
            Assert.Equal(2, ((object[])value[ModelCreatePhysics.AddedBodiesName]).Length);
        }

        [Fact]
        public void ABodyThatThePhysicsMovesIsAddedForEachPickedBone()
        {
            Bones("腕");

            Physics(
                Operation(ModelCreatePhysics.BodyPhysics),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelCreatePhysics.ShapeName, ModelCreatePhysics.Box));

            IPXBody made = Assert.Single(_fixture.Model.Body);
            Assert.Equal(BodyMode.Dynamic, made.Mode);
            Assert.Equal(BodyBoxKind.Box, made.BoxKind);
        }

        [Fact]
        public void AJointIsAddedBetweenThePickedBodies()
        {
            IList<IPXBody> bodies = Bodies("一", "二");
            ((FakeBody)bodies[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBody)bodies[1]).Position = new V3(0f, 2f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Physics(
                Operation(ModelCreatePhysics.Joint),
                ComposedEditFixture.Given("indices", new object[] { 0, 1 })));

            IPXJoint made = Assert.Single(_fixture.Model.Joint);
            Assert.Same(bodies[0], made.BodyA);
            Assert.Same(bodies[1], made.BodyB);
            Assert.Single((object[])value[ModelCreatePhysics.AddedJointsName]);
        }

        [Fact]
        public void ABodyAndTheJointToItsParentAreAddedTogether()
        {
            IList<IPXBone> bones = Bones("親", "子");
            bones[1].Parent = bones[0];
            FakeBody held = new FakeBody("親の剛体") { Bone = bones[0] };
            _fixture.Model.Body.Add(held);

            Physics(
                Operation(ModelCreatePhysics.BodyAndJoint),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(
                    ModelCreatePhysics.ShapeName, ModelCreatePhysics.Capsule));

            Assert.Equal(2, _fixture.Model.Body.Count);
            IPXJoint made = Assert.Single(_fixture.Model.Joint);
            Assert.Same(held, made.BodyA);
        }

        [Fact]
        public void OneBodyIsAddedAroundAllThePickedVertices()
        {
            Vertex(-1f, 0f, 0f);
            Vertex(1f, 4f, 0f);

            Physics(
                Operation(ModelCreatePhysics.BodyAtVertices),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelCreatePhysics.ShapeName, ModelCreatePhysics.Box));

            IPXBody made = Assert.Single(_fixture.Model.Body);
            Near(0.0, made.Position.X);
            Near(2.0, made.Position.Y);
            Near(1.0, made.BoxSize.X);
        }

        [Fact]
        public void WrappingVerticesThatStillFitAroundTheLineIsNotRefused()
        {
            float far = float.MaxValue * 0.8f;
            Vertex(0f, -far, 0f);
            Vertex(0f, far, 0f);
            Vertex(-far, 0f, 0f);
            Vertex(far, 0f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Physics(
                Operation(ModelCreatePhysics.BodyAtVertices),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelCreatePhysics.ShapeName, ModelCreatePhysics.Capsule)));

            Assert.Single(_fixture.Model.Body);
            Assert.Single((object[])value[ModelCreatePhysics.AddedBodiesName]);
        }

        [Fact]
        public void WrappingVerticesTooFarApartToHoldAsASizeIsRefused()
        {
            Vertex(-float.MaxValue, -float.MaxValue, 0f);
            Vertex(float.MaxValue, float.MaxValue, 0f);

            IDictionary<string, object> envelope = Physics(
                Operation(ModelCreatePhysics.BodyAtVertices),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelCreatePhysics.ShapeName, ModelCreatePhysics.Capsule));

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void MakingABodyWithoutSayingTheShapeIsRefused()
        {
            Bones("腕");

            IDictionary<string, object> envelope = Physics(
                Operation(ModelCreatePhysics.BodyPhysics),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AddingTheTipPutsABoneWhereTheOffsetPointsAndAimsAtIt()
        {
            FakeBone bone = new FakeBone("腕") { ToOffset = new V3(0f, 2f, 0f) };
            _fixture.Model.Bone.Add(bone);

            IDictionary<string, object> value = ComposedEditFixture.Value(Bone(
                Operation(ModelEditBones.AddTipBones),
                ComposedEditFixture.Given("indices", new object[] { 0 })));

            IPXBone made = _fixture.Model.Bone[1];
            Assert.Equal("腕先", made.Name);
            Assert.Equal(2f, made.Position.Y);
            Assert.Same(made, bone.ToBone);
            Assert.Equal(new object[] { 1 }, (object[])value[ModelEditBones.AddedName]);
        }

        [Fact]
        public void DissolvingTheTipTakesItOutAndLeavesTheOffsetBehind()
        {
            FakeBone bone = new FakeBone("腕");
            FakeBone tip = new FakeBone("腕先") { Position = new V3(0f, 3f, 0f), Parent = bone };
            bone.ToBone = tip;
            _fixture.Model.Bone.Add(bone);
            _fixture.Model.Bone.Add(tip);

            Bone(
                Operation(ModelEditBones.DissolveTipBones),
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            Assert.Single(_fixture.Model.Bone);
            Assert.Null(bone.ToBone);
            Assert.Equal(3f, bone.ToOffset.Y);
        }

        [Fact]
        public void DissolvingAChainOfTipsMovesWhatPointedAtThemToABoneThatStays()
        {
            FakeBone arm = new FakeBone("腕");
            FakeBone first = new FakeBone("腕先") { Parent = arm };
            FakeBone second = new FakeBone("腕先先") { Parent = first };
            FakeVertex vertex = new FakeVertex(0f, 0f, 0f) { Bone1 = second, Weight1 = 1f };
            _fixture.Model.Bone.Add(arm);
            _fixture.Model.Bone.Add(first);
            _fixture.Model.Bone.Add(second);
            _fixture.Model.Vertex.Add(vertex);

            Bone(
                Operation(ModelEditBones.DissolveTipBones),
                ComposedEditFixture.Given("indices", new object[] { 1, 2 }));

            Assert.Single(_fixture.Model.Bone);
            Assert.Same(arm, vertex.Bone1);
        }

        [Fact]
        public void DissolvingLeavesABoneThatIsNotNamedAsATipAlone()
        {
            FakeBone bone = new FakeBone("腕");
            _fixture.Model.Bone.Add(bone);

            Bone(
                Operation(ModelEditBones.DissolveTipBones),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Single(_fixture.Model.Bone);
        }

        [Fact]
        public void TheBoneAtTheVerticesTakesTheirWeightWhenItIsAskedFor()
        {
            FakeVertex vertex = new FakeVertex(0f, 4f, 0f);
            _fixture.Model.Bone.Add(new FakeBone("元"));
            _fixture.Model.Vertex.Add(vertex);

            Bone(
                Operation(ModelEditBones.AddAtVerticesWithWeight),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Equal(2, _fixture.Model.Bone.Count);
            Assert.Same(_fixture.Model.Bone[1], vertex.Bone1);
            Assert.Equal(1f, vertex.Weight1);
        }

        private IDictionary<string, object> Bone(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ModelEditBones.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IDictionary<string, object> Physics(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelCreatePhysics.ToolName, ComposedEditFixture.Arguments(given));
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

        private IList<IPXBody> Bodies(params string[] names)
        {
            List<IPXBody> made = new List<IPXBody>();
            foreach (string name in names)
            {
                FakeBody body = new FakeBody(name);
                _fixture.Model.Body.Add(body);
                made.Add(body);
            }

            return made;
        }

        private FakeVertex Vertex(float x, float y, float z)
        {
            FakeVertex vertex = new FakeVertex(x, y, z);
            _fixture.Model.Vertex.Add(vertex);

            return vertex;
        }

        private static void Weigh(FakeVertex vertex, IPXBone bone)
        {
            vertex.Bone1 = bone;
            vertex.Weight1 = 1f;
        }

        /// <summary>画面で見え、操作もできるボーンにする。</summary>
        private static void Show(IPXBone bone)
        {
            bone.Visible = true;
            bone.Controllable = true;
        }

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }
    }
}
