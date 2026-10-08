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
            NowAll(bones)[0].NameE = "arm1";
            NowAll(bones)[1].NameE = "arm2";
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Weigh(vertex, NowAll(bones)[1]);

            IDictionary<string, object> value = ComposedEditFixture.Value(Bone(
                Operation(ModelEditBones.MergeSameName),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal(2, _fixture.Model.Bone.Count);
            Assert.Equal("arm1", Named("腕").NameE);
            Assert.Same(Named("腕"), Now(vertex).Bone1);
            Assert.Equal(1, value[ModelEditBones.RemovedName]);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void MergingBonesLeavesTheVerticesHoldingThemWithOneSlotForTheMergedBone()
        {
            IList<IPXBone> bones = Bones("腕", "腕", "手");
            FakeVertex spread = Vertex(0f, 0f, 0f);
            Now(spread).Bone1 = NowAll(bones)[0];
            Now(spread).Weight1 = 0.3f;
            Now(spread).Bone2 = NowAll(bones)[1];
            Now(spread).Weight2 = 0.3f;
            Now(spread).Bone3 = NowAll(bones)[2];
            Now(spread).Weight3 = 0.4f;
            FakeVertex sdef = Vertex(1f, 0f, 0f);
            Now(sdef).SDEF = true;
            Now(sdef).Bone1 = NowAll(bones)[0];
            Now(sdef).Weight1 = 0.5f;
            Now(sdef).Bone2 = NowAll(bones)[1];
            Now(sdef).Weight2 = 0.5f;

            Bone(
                Operation(ModelEditBones.MergeSameName),
                ComposedEditFixture.Given("all", true));

            Assert.True(VertexWeights.IsSound(Now(spread)));
            Near(0.6, Share(spread, Named("腕")));
            Near(0.4, Share(spread, Named("手")));
            Assert.True(VertexWeights.IsSound(Now(sdef)));
            Assert.False(Now(sdef).SDEF);
            Near(1.0, Share(sdef, Named("腕")));
        }

        [Fact]
        public void DissolvingATipLeavesTheVerticesHoldingItAndItsParentWithOneSlot()
        {
            FakeBone arm = new FakeBone("腕");
            FakeBone tip = new FakeBone("腕先") { Parent = arm };
            FakeVertex vertex = new FakeVertex(0f, 0f, 0f)
            {
                Bone1 = arm,
                Weight1 = 0.5f,
                Bone2 = tip,
                Weight2 = 0.5f,
            };
            _fixture.Model.Bone.Add(arm);
            _fixture.Model.Bone.Add(tip);
            _fixture.Model.Vertex.Add(vertex);

            Bone(
                Operation(ModelEditBones.DissolveTipBones),
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            Assert.True(VertexWeights.IsSound(Now(vertex)));
            Near(1.0, Share(vertex, Named("腕")));
        }

        [Fact]
        public void ABoneWithNoChildIsPutOutOfReach()
        {
            IList<IPXBone> bones = Bones("親", "子");
            Show(NowAll(bones)[0]);
            Show(NowAll(bones)[1]);
            NowAll(bones)[1].Parent = NowAll(bones)[0];

            Bone(
                Operation(ModelEditBones.HideTipBones),
                ComposedEditFixture.Given("all", true));

            Assert.True(NowAll(bones)[0].Visible);
            Assert.False(NowAll(bones)[1].Visible);
            Assert.False(NowAll(bones)[1].Controllable);
        }

        [Fact]
        public void HidingTipBonesRemakesOnlyTheBonesInTheView()
        {
            IList<IPXBone> bones = Bones("親", "子");
            NowAll(bones)[1].Parent = NowAll(bones)[0];

            Bone(Operation(ModelEditBones.HideTipBones), ComposedEditFixture.Given("all", true));

            Assert.Equal(new[] { ElementKinds.Bone }, _fixture.View.Remade);
            Assert.Equal(0, _fixture.View.Redraws);
        }

        [Fact]
        public void RelevelingStillRebuildsTheWholeModelInTheView()
        {
            IList<IPXBone> bones = Bones("子", "親");
            NowAll(bones)[0].Parent = NowAll(bones)[1];

            Bone(Operation(ModelEditBones.RelevelHierarchy));

            Assert.Empty(_fixture.View.Remade);
            Assert.Equal(1, _fixture.View.Redraws);
        }

        [Fact]
        public void TheTipBoneBecomesTheGapToThatBone()
        {
            IList<IPXBone> bones = Bones("元", "先");
            ((FakeBone)NowAll(bones)[0]).Position = new V3(0f, 1f, 0f);
            ((FakeBone)NowAll(bones)[1]).Position = new V3(0f, 3f, 0f);
            NowAll(bones)[0].ToBone = NowAll(bones)[1];

            Bone(
                Operation(ModelEditBones.TipToOffset),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Null(NowAll(bones)[0].ToBone);
            Near(2.0, NowAll(bones)[0].ToOffset.Y);
        }

        [Fact]
        public void TheGapBecomesTheChildItPointsAt()
        {
            IList<IPXBone> bones = Bones("元", "先");
            ((FakeBone)NowAll(bones)[0]).Position = new V3(0f, 1f, 0f);
            ((FakeBone)NowAll(bones)[1]).Position = new V3(0f, 3f, 0f);
            NowAll(bones)[1].Parent = NowAll(bones)[0];
            NowAll(bones)[0].ToOffset = new V3(0f, 2f, 0f);

            Bone(
                Operation(ModelEditBones.OffsetToTip),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Same(NowAll(bones)[1], NowAll(bones)[0].ToBone);
        }

        [Fact]
        public void AChildThatComesBeforeItsParentIsMovedAfterIt()
        {
            IList<IPXBone> bones = Bones("子", "親");
            NowAll(bones)[0].Parent = NowAll(bones)[1];

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Bone(Operation(ModelEditBones.RelevelHierarchy)));

            Assert.Equal(new[] { "親", "子" }, _fixture.Model.Bone.Select(b => b.Name).ToArray());
            Assert.Same(_fixture.Model.Bone[0], _fixture.Model.Bone[1].Parent);
            Assert.Equal(1, value[ModelEditBones.ChangedName]);
        }

        [Fact]
        public void ABoneWithNoParentGetsOneAboveIt()
        {
            Bones("根");

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Bone(Operation(ModelEditBones.AddRootParent)));

            Assert.Equal(2, _fixture.Model.Bone.Count);
            IPXBone made = Named("根").Parent;
            Assert.NotNull(made);
            Assert.NotSame(Named("根"), made);
            Assert.Contains(made, _fixture.Model.Bone);
            Assert.Single((object[])value[ModelEditBones.AddedName]);
        }

        [Fact]
        public void AStageParentIsAddedAtTheSameSpotAndTakesOverTheOldParent()
        {
            IList<IPXBone> bones = Bones("親", "腕");
            ((FakeBone)NowAll(bones)[1]).Position = new V3(1f, 2f, 3f);
            NowAll(bones)[1].Parent = NowAll(bones)[0];

            Bone(
                Operation(ModelEditBones.AddMultiStageParent),
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            IPXBone made = Named("腕").Parent;
            Assert.NotSame(Named("親"), made);
            Assert.Same(Named("親"), made.Parent);
            Near(1.0, made.Position.X);
        }

        [Fact]
        public void AStageChildIsAddedAtTheSameSpotBelowThePickedBone()
        {
            IList<IPXBone> bones = Bones("腕");
            ((FakeBone)NowAll(bones)[0]).Position = new V3(1f, 2f, 3f);

            Bone(
                Operation(ModelEditBones.AddMultiStageChild),
                ComposedEditFixture.Given("all", true));

            IPXBone made = _fixture.Model.Bone.Single(bone => bone.Name != "腕");
            Assert.Same(Named("腕"), made.Parent);
            Near(3.0, made.Position.Z);
        }

        [Fact]
        public void ABoneIsAddedHalfwayBetweenThePickedOneAndItsParent()
        {
            IList<IPXBone> bones = Bones("親", "子");
            ((FakeBone)NowAll(bones)[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)NowAll(bones)[1]).Position = new V3(0f, 4f, 0f);
            NowAll(bones)[1].Parent = NowAll(bones)[0];

            Bone(
                Operation(ModelEditBones.AddMiddle),
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            IPXBone made = Named("子").Parent;
            Assert.NotSame(Named("親"), made);
            Assert.Same(Named("親"), made.Parent);
            Near(2.0, made.Position.Y);
        }

        [Theory]
        [InlineData(ModelEditBones.AddMultiStageParent)]
        [InlineData(ModelEditBones.AddMultiStageChild)]
        [InlineData(ModelEditBones.AddMiddle)]
        public void AStagedBoneTakesTheDeformOrderOfThePickedBone(string operation)
        {
            IList<IPXBone> bones = Bones("親", "腕");
            NowAll(bones)[0].Level = 2;
            NowAll(bones)[0].IsAfterPhysics = true;
            NowAll(bones)[1].Parent = NowAll(bones)[0];
            NowAll(bones)[1].Level = 2;
            NowAll(bones)[1].IsAfterPhysics = true;

            IDictionary<string, object> value = ComposedEditFixture.Value(Bone(
                Operation(operation),
                ComposedEditFixture.Given("indices", new object[] { 1 })));

            IPXBone made = _fixture.Model.Bone[(int)((object[])value[ModelEditBones.AddedName])[0]];
            Assert.Equal(2, made.Level);
            Assert.True(made.IsAfterPhysics);
        }

        [Fact]
        public void TheAddedAppendParentIsASiblingBeforeTheBoneThatTakesItsTurnFromIt()
        {
            IList<IPXBone> bones = Bones("親", "腕");
            NowAll(bones)[1].Parent = NowAll(bones)[0];
            NowAll(bones)[1].IsRotation = true;

            IDictionary<string, object> value = ComposedEditFixture.Value(Bone(
                Operation(ModelEditBones.AddAppendParent),
                ComposedEditFixture.Given("indices", new object[] { 1 })));

            IPXBone arm = Named("腕");
            IPXBone made = _fixture.Model.Bone[(int)((object[])value[ModelEditBones.AddedName])[0]];
            Assert.Same(Named("親"), arm.Parent);
            Assert.Same(Named("親"), made.Parent);
            Assert.Same(made, arm.AppendParent);
            Assert.True(arm.IsAppendRotation);
            Assert.False(arm.IsAppendTranslation);
            Assert.Equal(1f, arm.AppendRatio);
            Assert.True(made.IsRotation);
            Assert.False(made.IsTranslation);
            Assert.True(_fixture.Model.Bone.IndexOf(made) < _fixture.Model.Bone.IndexOf(arm));
        }

        [Fact]
        public void TheAddedAppendParentCarriesTheTranslationOfTheBone()
        {
            IList<IPXBone> bones = Bones("腕");
            NowAll(bones)[0].IsTranslation = true;

            Bone(
                Operation(ModelEditBones.AddAppendParent),
                ComposedEditFixture.Given("all", true));

            IPXBone arm = Named("腕");
            Assert.True(arm.IsAppendTranslation);
            Assert.False(arm.IsAppendRotation);
            Assert.True(arm.AppendParent.IsTranslation);
            Assert.False(arm.AppendParent.IsRotation);
        }

        [Fact]
        public void NoAppendParentIsAddedToABoneThatAlreadyTakesATurnOrCannotMove()
        {
            IList<IPXBone> bones = Bones("元", "腕D", "固定");
            NowAll(bones)[1].IsRotation = true;
            NowAll(bones)[1].IsAppendRotation = true;
            NowAll(bones)[1].AppendParent = NowAll(bones)[0];

            IDictionary<string, object> value = ComposedEditFixture.Value(Bone(
                Operation(ModelEditBones.AddAppendParent),
                ComposedEditFixture.Given("indices", new object[] { 1, 2 })));

            Assert.Empty((object[])value[ModelEditBones.AddedName]);
            Assert.Equal(3, _fixture.Model.Bone.Count);
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
            NowAll(bones)[1].Parent = NowAll(bones)[0];
            NowAll(bones)[2].Parent = NowAll(bones)[1];

            Bone(
                Operation(ModelEditBones.MakeIk),
                ComposedEditFixture.Given("indices", new object[] { 2 }),
                ComposedEditFixture.Given(ModelEditBones.LinkCountName, 2));

            IPXBone made = _fixture.Model.Bone.Single(bone => Now(bone).IsIK);
            Assert.Same(Named("足首"), made.IK.Target);
            Assert.Equal(2, made.IK.Links.Count);
        }

        [Fact]
        public void ASdefVertexWeighedToAMirroredBoneHasItsCentreOnTheNewBoneAxis()
        {
            IList<IPXBone> bones = Bones("左腕", "右腕", "首");
            ((FakeBone)NowAll(bones)[0]).Position = new V3(2f, 1f, 0f);
            ((FakeBone)NowAll(bones)[1]).Position = new V3(9f, 9f, 9f);
            ((FakeBone)NowAll(bones)[2]).Position = new V3(-2f, 5f, 0f);
            FakeVertex vertex = new FakeVertex(-1f, 3f, 0f)
            {
                Bone1 = NowAll(bones)[1],
                Bone2 = NowAll(bones)[2],
                Weight1 = 0.5f,
                Weight2 = 0.5f,
                SDEF = true,
                SDEF_C = new V3(99f, 99f, 99f),
            };
            _fixture.Model.Vertex.Add(vertex);

            Bone(
                Operation(ModelEditBones.MirrorPosition),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ModelEditBones.AxisName, ModelEditVertices.AxisX));

            Near(-2.0, _fixture.Model.Vertex[0].SDEF_C.X);
            Near(3.0, _fixture.Model.Vertex[0].SDEF_C.Y);
        }

        [Fact]
        public void ABoneNamedForTheOtherSideIsMovedToTheMirroredSpot()
        {
            IList<IPXBone> bones = Bones("左腕", "右腕");
            ((FakeBone)NowAll(bones)[0]).Position = new V3(2f, 1f, 0f);
            ((FakeBone)NowAll(bones)[1]).Position = new V3(9f, 9f, 9f);

            Bone(
                Operation(ModelEditBones.MirrorPosition),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ModelEditBones.AxisName, ModelEditVertices.AxisX));

            Near(-2.0, NowAll(bones)[1].Position.X);
            Near(1.0, NowAll(bones)[1].Position.Y);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TheFixedAxisIsPointedAtTheTip(bool suppressed)
        {
            IList<IPXBone> bones = Bones("腕");
            ((FakeBone)NowAll(bones)[0]).ToOffset = new V3(0f, 5f, 0f);

            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>
            {
                Operation(ModelEditBones.FixAxisToTip),
                ComposedEditFixture.Given("all", true),
            };
            if (suppressed)
            {
                given.Add(ComposedEditFixture.Given(UndoBarrier.SuppressName, true));
            }

            Bone(given.ToArray());

            Assert.True(NowAll(bones)[0].IsFixAxis);
            Near(1.0, NowAll(bones)[0].FixAxis.Y);
            Assert.Equal(suppressed ? 1 : 0, _fixture.Partials.Count);
        }

        [Fact]
        public void TheLocalAxisIsBuiltFromTheTipAndTheParent()
        {
            IList<IPXBone> bones = Bones("親", "腕");
            ((FakeBone)NowAll(bones)[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)NowAll(bones)[1]).Position = new V3(0f, 1f, 0f);
            NowAll(bones)[1].Parent = NowAll(bones)[0];
            ((FakeBone)NowAll(bones)[1]).ToOffset = new V3(2f, 0f, 0f);

            Bone(
                Operation(ModelEditBones.SetLocalAxis),
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            V3 across;
            V3 up;
            V3 along;
            NowAll(bones)[1].GetLocalAxis(out across, out up, out along);
            Assert.True(NowAll(bones)[1].IsLocalFrame);
            Near(1.0, across.X);
        }

        [Fact]
        public void TheLocalAxisIsTakenBackOff()
        {
            IList<IPXBone> bones = Bones("腕");
            NowAll(bones)[0].IsLocalFrame = true;

            Bone(
                Operation(ModelEditBones.ResetLocalAxis),
                ComposedEditFixture.Given("all", true));

            Assert.False(NowAll(bones)[0].IsLocalFrame);
        }

        [Fact]
        public void ThePmdKindIsSetFromWhatTheBoneCanDo()
        {
            IList<IPXBone> bones = Bones("腕");
            NowAll(bones)[0].IsRotation = true;
            NowAll(bones)[0].Visible = true;

            IDictionary<string, object> value = ComposedEditFixture.Value(Bone(
                Operation(ModelEditBones.SetPmdBoneKind),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal(BoneKind.Rotate, ((FakeBone)NowAll(bones)[0]).PmdKind);
            Assert.True(NowAll(bones)[0].IsRotation);
            Assert.True(NowAll(bones)[0].Visible);
            Assert.True(NowAll(bones)[0].Controllable);
            Assert.False(NowAll(bones)[0].IsTranslation);
            Assert.Equal(1, value[ModelEditBones.ChangedName]);
        }

        /// <summary>
        /// エディタのボーンのメニューの「PMDボーン種類で設定」は、表示の有無より先に回転付与を見て
        /// 種類を選ぶ。付与率1・階層2で回転付与するボーンは、非表示でも回転影響下として設定し直され、
        /// 付与と付与率を保ったまま表示される。
        /// </summary>
        [Fact]
        public void AHiddenBoneThatAppendsRotationKeepsItsAppendWhenItsPmdKindIsSet()
        {
            IList<IPXBone> bones = Bones("親", "捩");
            NowAll(bones)[1].IsAppendRotation = true;
            NowAll(bones)[1].AppendParent = NowAll(bones)[0];
            NowAll(bones)[1].AppendRatio = 1f;
            NowAll(bones)[1].Level = 2;
            NowAll(bones)[1].Visible = false;

            Bone(
                Operation(ModelEditBones.SetPmdBoneKind),
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            Assert.True(NowAll(bones)[1].IsAppendRotation, "回転付与が外れた。");
            Near(1.0, NowAll(bones)[1].AppendRatio);
            Assert.True(NowAll(bones)[1].Visible);
            Assert.Equal(2, NowAll(bones)[1].Level);
        }

        /// <summary>
        /// エディタの「PMDボーン種類で設定」は、表示の有無より先に軸固定を見て捩りとして選ぶ。
        /// 非表示の軸固定ボーンは、軸固定を保ったまま表示される。
        /// </summary>
        [Fact]
        public void AHiddenBoneWithAFixedAxisKeepsTheAxisWhenItsPmdKindIsSet()
        {
            IList<IPXBone> bones = Bones("捩");
            NowAll(bones)[0].IsFixAxis = true;
            NowAll(bones)[0].FixAxis = new V3(1f, 0f, 0f);
            NowAll(bones)[0].Visible = false;

            Bone(
                Operation(ModelEditBones.SetPmdBoneKind),
                ComposedEditFixture.Given("all", true));

            Assert.True(NowAll(bones)[0].IsFixAxis, "軸固定が外れた。");
            Assert.True(NowAll(bones)[0].Visible);
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
            ((FakeBone)NowAll(bones)[0]).Position = new V3(1f, 2f, 3f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Physics(
                Operation(ModelCreatePhysics.BodyFollowBone),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelCreatePhysics.ShapeName, ModelCreatePhysics.Sphere)));

            Assert.Equal(2, _fixture.Model.Body.Count);
            Assert.Same(NowAll(bones)[0], _fixture.Model.Body[0].Bone);
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
            ((FakeBody)NowAll(bodies)[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBody)NowAll(bodies)[1]).Position = new V3(0f, 2f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Physics(
                Operation(ModelCreatePhysics.Joint),
                ComposedEditFixture.Given("indices", new object[] { 0, 1 })));

            IPXJoint made = Assert.Single(_fixture.Model.Joint);
            Assert.Same(NowAll(bodies)[0], made.BodyA);
            Assert.Same(NowAll(bodies)[1], made.BodyB);
            Assert.Single((object[])value[ModelCreatePhysics.AddedJointsName]);
        }

        [Fact]
        public void ABodyAndTheJointToItsParentAreAddedTogether()
        {
            IList<IPXBone> bones = Bones("親", "子");
            NowAll(bones)[1].Parent = NowAll(bones)[0];
            FakeBody held = new FakeBody("親の剛体") { Bone = NowAll(bones)[0] };
            _fixture.Model.Body.Add(held);

            Physics(
                Operation(ModelCreatePhysics.BodyAndJoint),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(
                    ModelCreatePhysics.ShapeName, ModelCreatePhysics.Capsule));

            Assert.Equal(2, _fixture.Model.Body.Count);
            IPXJoint made = Assert.Single(_fixture.Model.Joint);
            Assert.Same(_fixture.Model.Body.Single(body => body.Name == "親の剛体"), made.BodyA);
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
            Assert.Same(made, Now(bone).ToBone);
            Assert.Equal(new object[] { 1 }, (object[])value[ModelEditBones.AddedName]);
        }

        [Fact]
        public void TheAddedTipTakesTheDeformOrderOfItsBone()
        {
            FakeBone bone = new FakeBone("腕")
            {
                ToOffset = new V3(0f, 2f, 0f),
                Level = 2,
                IsAfterPhysics = true,
            };
            _fixture.Model.Bone.Add(bone);

            Bone(
                Operation(ModelEditBones.AddTipBones),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            IPXBone made = _fixture.Model.Bone[1];
            Assert.Equal(2, made.Level);
            Assert.True(made.IsAfterPhysics);
        }

        [Fact]
        public void DissolvingTheTipTakesItOutAndLeavesTheOffsetBehind()
        {
            FakeBone bone = new FakeBone("腕");
            FakeBone tip = new FakeBone("腕先") { Position = new V3(0f, 3f, 0f), Parent = bone };
            Now(bone).ToBone = tip;
            _fixture.Model.Bone.Add(bone);
            _fixture.Model.Bone.Add(tip);

            Bone(
                Operation(ModelEditBones.DissolveTipBones),
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            IPXBone kept = Assert.Single(_fixture.Model.Bone);
            Assert.Equal("腕", kept.Name);
            Assert.Null(kept.ToBone);
            Assert.Equal(3f, kept.ToOffset.Y);
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

            IPXBone kept = Assert.Single(_fixture.Model.Bone);
            Assert.Equal("腕", kept.Name);
            Assert.Same(kept, Now(vertex).Bone1);
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
            Assert.Same(_fixture.Model.Bone[1], Now(vertex).Bone1);
            Assert.Equal(1f, Now(vertex).Weight1);
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

        /// <summary>エディタのいまのボーンのうち、その名前のもの。</summary>
        private IPXBone Named(string name)
        {
            return _fixture.Model.Bone.Single(bone => Now(bone).Name == name);
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

        /// <summary>その頂点がそのボーンへ振っている重みの合計。振っていなければ0。</summary>
        private float Share(IPXVertex given, IPXBone weighed)
        {
            IPXVertex now = Now(given);
            IPXBone[] held = { now.Bone1, now.Bone2, now.Bone3, now.Bone4 };
            float[] weights = { now.Weight1, now.Weight2, now.Weight3, now.Weight4 };
            float share = 0f;
            for (int at = 0; at < held.Length; at++)
            {
                if (ReferenceEquals(held[at], weighed))
                {
                    share += weights[at];
                }
            }

            return share;
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
