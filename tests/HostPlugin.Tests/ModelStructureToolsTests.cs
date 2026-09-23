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
    /// モーフの整理と生成、表示枠への登録、指している相手からの写し取り。どれも1回の呼び出しで、
    /// 1回のまとめての反映に収まる。
    /// </summary>
    [Collection(TimedCollection.Name)]
    public sealed class ModelStructureToolsTests : IDisposable
    {
        /// <summary>小数の突き合わせで見る桁。</summary>
        private const int Digits = 4;

        private const int ManyOffsets = 30000;

        private const int StripQuads = 5000;

        private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(2);

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void MorphsThatShareANameAreMergedIntoTheFirstOne()
        {
            IPXVertex vertex = Vertex(0f, 0f, 0f);
            FakeMorph first = Morph("笑い", MorphKind.Vertex, Shift(vertex, 1f));
            Morph("笑い", MorphKind.Vertex, Shift(vertex, 2f));

            IDictionary<string, object> value = ComposedEditFixture.Value(Morphs(
                Operation(ModelEditMorphs.MergeSameName),
                ComposedEditFixture.Given("all", true)));

            Assert.Same(first, Assert.Single(_fixture.Model.Morph));
            Assert.Equal(2, first.Offsets.Count);
            Assert.Equal(1, value[ModelEditMorphs.RemovedName]);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void MorphsOfTheSameKindAreMergedIntoTheFirstOne()
        {
            IPXVertex vertex = Vertex(0f, 0f, 0f);
            FakeMorph first = Morph("一", MorphKind.Vertex, Shift(vertex, 1f));
            Morph("二", MorphKind.Vertex, Shift(vertex, 2f));
            Morph("色", MorphKind.Material);

            Morphs(
                Operation(ModelEditMorphs.MergeSameKind),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(2, _fixture.Model.Morph.Count);
            Assert.Equal(2, first.Offsets.Count);
        }

        [Fact]
        public void MergingByKindWithAddingPutsTheOffsetsForOneVertexTogether()
        {
            IPXVertex vertex = Vertex(0f, 0f, 0f);
            FakeMorph first = Morph("一", MorphKind.Vertex, Shift(vertex, 1f));
            Morph("二", MorphKind.Vertex, Shift(vertex, 2f));

            Morphs(
                Operation(ModelEditMorphs.MergeSameKindAdd),
                ComposedEditFixture.Given("all", true));

            IPXVertexMorphOffset held = (IPXVertexMorphOffset)Assert.Single(first.Offsets);
            Near(3.0, held.Offset.X);
        }

        [Fact]
        public void MergingLargeMorphsByKindWithAddingFinishesInTime()
        {
            IPXVertex[] vertices = Enumerable.Range(0, ManyOffsets)
                .Select(at => Vertex(at, 0f, 0f))
                .ToArray();
            FakeMorph first = Morph(
                "一", MorphKind.Vertex, vertices.Select(v => Shift(v, 1f)).ToArray());
            Morph("二", MorphKind.Vertex, vertices.Reverse().Select(v => Shift(v, 2f)).ToArray());

            Stopwatch elapsed = Stopwatch.StartNew();
            Morphs(
                Operation(ModelEditMorphs.MergeSameKindAdd),
                ComposedEditFixture.Given("all", true));
            elapsed.Stop();

            Assert.Equal(ManyOffsets, first.Offsets.Count);
            Near(3.0, ((IPXVertexMorphOffset)first.Offsets[0]).Offset.X);
            Assert.True(elapsed.Elapsed < TimeLimit, "合わせるのに " + elapsed.Elapsed + " かかった");
        }

        [Fact]
        public void AGroupMorphIsAddedThatCallsTheOnesThatWerePicked()
        {
            FakeMorph held = Morph("笑い", MorphKind.Vertex);

            IDictionary<string, object> value = ComposedEditFixture.Value(Morphs(
                Operation(ModelEditMorphs.GroupInto),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditMorphs.NameName, "まとめ")));

            IPXMorph made = _fixture.Model.Morph.Single(morph => morph.Kind == MorphKind.Group);
            Assert.Equal("まとめ", made.Name);
            Assert.Same(held, ((IPXGroupMorphOffset)Assert.Single(made.Offsets)).Morph);
            Assert.Single((object[])value[ModelEditMorphs.AddedName]);
        }

        [Fact]
        public void AFlipMorphIsAddedThatSwitchesBetweenTheOnesThatWerePicked()
        {
            Morph("一", MorphKind.Vertex);
            Morph("二", MorphKind.Vertex);

            Morphs(
                Operation(ModelEditMorphs.FlipInto),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditMorphs.NameName, "切り替え"));

            IPXMorph made = _fixture.Model.Morph.Single(morph => morph.Kind == MorphKind.Flip);
            Assert.Equal(2, made.Offsets.Count);
        }

        [Fact]
        public void AVertexMorphIsSplitIntoOneMorphPerGroupOfVerticesThatMove()
        {
            IPXVertex near = Vertex(0f, 0f, 0f);
            IPXVertex apart = Vertex(20f, 0f, 0f);
            Morph("笑い", MorphKind.Vertex, Shift(near, 1f), Shift(apart, 1f));
            Face(near, apart);

            IDictionary<string, object> value = ComposedEditFixture.Value(Morphs(
                Operation(ModelEditMorphs.SplitVertices),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal(2, ((object[])value[ModelEditMorphs.AddedName]).Length);
            Assert.All(
                _fixture.Model.Morph.Where(morph => morph.Kind == MorphKind.Vertex),
                morph => Assert.Single(morph.Offsets));
        }

        [Fact]
        public void SplittingAMorphOverALongStripFinishesInTime()
        {
            List<IPXVertex> bottom = new List<IPXVertex>();
            List<IPXVertex> top = new List<IPXVertex>();
            for (int at = 0; at <= StripQuads; at++)
            {
                bottom.Add(Vertex(at, 0f, 0f));
                top.Add(Vertex(at, 1f, 0f));
            }

            FakeMaterial material = new FakeMaterial("材質");
            for (int at = StripQuads - 1; at >= 0; at--)
            {
                material.Faces.Add(new FakeFace(bottom[at], bottom[at + 1], top[at + 1]));
                material.Faces.Add(new FakeFace(bottom[at], top[at + 1], top[at]));
            }

            _fixture.Model.Material.Add(material);
            Morph("笑い", MorphKind.Vertex, bottom.Concat(top).Select(v => Shift(v, 1f)).ToArray());

            Stopwatch elapsed = Stopwatch.StartNew();
            IDictionary<string, object> value = ComposedEditFixture.Value(Morphs(
                Operation(ModelEditMorphs.SplitVertices),
                ComposedEditFixture.Given("all", true)));
            elapsed.Stop();

            Assert.Single((object[])value[ModelEditMorphs.AddedName]);
            Assert.True(elapsed.Elapsed < TimeLimit, "分けるのに " + elapsed.Elapsed + " かかった");
        }

        [Fact]
        public void AMaterialMorphIsAddedThatHoldsWhatTheMaterialsNowShow()
        {
            FakeMaterial material = new FakeMaterial("材質");
            _fixture.Model.Material.Add(material);

            Morphs(
                Operation(ModelEditMorphs.MaterialFromCurrent),
                ComposedEditFixture.Given(ModelEditMorphs.NameName, "いまの色"));

            IPXMorph made = _fixture.Model.Morph.Single(morph => morph.Kind == MorphKind.Material);
            Assert.Same(material, ((IPXMaterialMorphOffset)Assert.Single(made.Offsets)).Material);
        }

        [Fact]
        public void MakingAGroupWithoutANameIsRefused()
        {
            Morph("笑い", MorphKind.Vertex);

            IDictionary<string, object> envelope = Morphs(
                Operation(ModelEditMorphs.GroupInto),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void PickingMorphsForTheOneThatReadsTheMaterialsIsRefused()
        {
            Morph("笑い", MorphKind.Vertex);

            IDictionary<string, object> envelope = Morphs(
                Operation(ModelEditMorphs.MaterialFromCurrent),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditMorphs.NameName, "いまの色"));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void ABoneOnNoNodeAtAllIsAddedToThePickedNode()
        {
            IPXBone listed = Bone("載っている");
            IPXBone missing = Bone("載っていない");
            FakeNode node = Node("枠", new FakeBoneNodeItem(listed));

            IDictionary<string, object> value = ComposedEditFixture.Value(Nodes(
                Operation(ModelEditNodes.RegisterUnlistedBones),
                ComposedEditFixture.Given("indices", new object[] { 2 })));

            Assert.Equal(2, node.Items.Count);
            Assert.Same(missing, ((IPXBoneNodeItem)node.Items[1]).Bone);
            Assert.Equal(1, value[ModelEditNodes.AddedName]);
        }

        [Fact]
        public void AMorphOnNoNodeAtAllIsAddedToThePickedNode()
        {
            IPXMorph missing = Morph("笑い", MorphKind.Vertex);
            FakeNode node = Node("枠");

            Nodes(
                Operation(ModelEditNodes.RegisterUnlistedMorphs),
                ComposedEditFixture.Given("indices", new object[] { 2 }));

            Assert.Same(missing, ((IPXMorphNodeItem)Assert.Single(node.Items)).Morph);
        }

        [Fact]
        public void OnlyThePickedBoneThatIsOnNoNodeIsAdded()
        {
            IPXBone listed = Bone("載っている");
            IPXBone wanted = Bone("足したい");
            Bone("足したくない");
            FakeNode node = Node("枠", new FakeBoneNodeItem(listed));

            IDictionary<string, object> value = ComposedEditFixture.Value(Nodes(
                Operation(ModelEditNodes.RegisterPickedBones),
                ComposedEditFixture.Given("indices", new object[] { 2 }),
                ComposedEditFixture.Given(
                    ModelEditNodes.TargetIndicesName, new object[] { 0, 1 })));

            Assert.Equal(2, node.Items.Count);
            Assert.Same(wanted, ((IPXBoneNodeItem)node.Items[1]).Bone);
            Assert.Equal(1, value[ModelEditNodes.AddedName]);
        }

        [Fact]
        public void TheFramesTheModelHoldsApartComeFirstAmongThePositions()
        {
            IPXBone wanted = Bone("足したい");
            Node("枠");

            IDictionary<string, object> value = ComposedEditFixture.Value(Nodes(
                Operation(ModelEditNodes.RegisterPickedBones),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(
                    ModelEditNodes.TargetIndicesName, new object[] { 0 })));

            Assert.Same(
                wanted,
                ((IPXBoneNodeItem)((FakeNode)_fixture.Model.RootNode).Items[0]).Bone);
            Assert.Equal(1, value[ModelEditNodes.AddedName]);
        }

        [Fact]
        public void ThePositionPastTheFramesReachesTheListTheModelKeeps()
        {
            Bone("足したい");
            FakeNode node = Node("枠");

            ComposedEditFixture.Value(Nodes(
                Operation(ModelEditNodes.RegisterPickedBones),
                ComposedEditFixture.Given("indices", new object[] { 2 }),
                ComposedEditFixture.Given(
                    ModelEditNodes.TargetIndicesName, new object[] { 0 })));

            Assert.Single(node.Items);
        }

        [Fact]
        public void BonesGoPastTheExpressionFrameToTheNextOneThatWasPicked()
        {
            IPXBone missing = Bone("載っていない");
            Node("枠");

            ComposedEditFixture.Value(Nodes(
                Operation(ModelEditNodes.RegisterUnlistedBones),
                ComposedEditFixture.Given("all", true)));

            Assert.Empty(((FakeNode)_fixture.Model.ExpressionNode).Items);
            Assert.Same(
                missing,
                ((IPXBoneNodeItem)Assert.Single(((FakeNode)_fixture.Model.RootNode).Items)).Bone);
        }

        [Fact]
        public void PickingOnlyTheExpressionFrameForBonesIsRefused()
        {
            Bone("載っていない");

            IDictionary<string, object> envelope = Nodes(
                Operation(ModelEditNodes.RegisterUnlistedBones),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains("表情の枠にはボーンを載せられない", ComposedEditFixture.Message(envelope));
            Assert.Empty(((FakeNode)_fixture.Model.ExpressionNode).Items);
        }

        [Fact]
        public void MorphsGoIntoTheExpressionFrameWhenItIsThePickedOne()
        {
            IPXMorph missing = Morph("笑い", MorphKind.Vertex);

            ComposedEditFixture.Value(Nodes(
                Operation(ModelEditNodes.RegisterUnlistedMorphs),
                ComposedEditFixture.Given("indices", new object[] { 0 })));

            Assert.Same(
                missing,
                ((IPXMorphNodeItem)Assert.Single(
                    ((FakeNode)_fixture.Model.ExpressionNode).Items)).Morph);
        }

        [Fact]
        public void AnEmptyListOfTargetsIsTakenAsPickingNothing()
        {
            IPXBone listed = Bone("載っている");
            Bone("足していない");
            FakeNode node = Node("枠", new FakeBoneNodeItem(listed));

            IDictionary<string, object> value = ComposedEditFixture.Value(Nodes(
                Operation(ModelEditNodes.RegisterPickedBones),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelEditNodes.TargetIndicesName, new object[0])));

            Assert.Single(node.Items);
            Assert.Equal(0, value[ModelEditNodes.AddedName]);
        }

        [Fact]
        public void TheScreenSelectionSaysWhichBonesToRegister()
        {
            IPXBone listed = Bone("載っている");
            IPXBone wanted = Bone("足したい");
            Bone("足したくない");
            FakeNode node = Node("枠", new FakeBoneNodeItem(listed));
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 0, 1 };

            IDictionary<string, object> value = ComposedEditFixture.Value(Nodes(
                Operation(ModelEditNodes.RegisterPickedBones),
                ComposedEditFixture.Given("indices", new object[] { 2 }),
                ComposedEditFixture.Given(ModelEditNodes.TargetSelectedName, true)));

            Assert.Equal(2, node.Items.Count);
            Assert.Same(wanted, ((IPXBoneNodeItem)node.Items[1]).Bone);
            Assert.Equal(1, value[ModelEditNodes.AddedName]);
        }

        [Fact]
        public void RegisteringMorphsRefusesTheScreenSelectionBecauseTheScreenCannotSelectThem()
        {
            Morph("足したい", MorphKind.Vertex);
            Node("枠");

            IDictionary<string, object> envelope = Nodes(
                Operation(ModelEditNodes.RegisterPickedMorphs),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditNodes.TargetSelectedName, true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void OnlyThePickedMorphThatIsOnNoNodeIsAdded()
        {
            IPXMorph wanted = Morph("足したい", MorphKind.Vertex);
            Morph("足したくない", MorphKind.Vertex);
            FakeNode node = Node("枠");

            Nodes(
                Operation(ModelEditNodes.RegisterPickedMorphs),
                ComposedEditFixture.Given("indices", new object[] { 2 }),
                ComposedEditFixture.Given(
                    ModelEditNodes.TargetIndicesName, new object[] { 0 }));

            Assert.Same(wanted, ((IPXMorphNodeItem)Assert.Single(node.Items)).Morph);
        }

        [Fact]
        public void APickedBoneNamedTwiceIsAddedOnlyOnce()
        {
            IPXBone wanted = Bone("足したい");
            FakeNode node = Node("枠");

            IDictionary<string, object> value = ComposedEditFixture.Value(Nodes(
                Operation(ModelEditNodes.RegisterPickedBones),
                ComposedEditFixture.Given("indices", new object[] { 2 }),
                ComposedEditFixture.Given(
                    ModelEditNodes.TargetIndicesName, new object[] { 0, 0 })));

            Assert.Same(wanted, ((IPXBoneNodeItem)Assert.Single(node.Items)).Bone);
            Assert.Equal(1, value[ModelEditNodes.AddedName]);
        }

        [Fact]
        public void RegisteringPickedElementsWithoutSayingWhichOnesIsRefused()
        {
            Bone("ボーン");
            Node("枠");

            IDictionary<string, object> envelope = Nodes(
                Operation(ModelEditNodes.RegisterPickedBones),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void TheExpressionNodeIsPutBackIntoTheOrderTheMorphsAreIn()
        {
            IPXMorph first = Morph("一", MorphKind.Vertex);
            IPXMorph second = Morph("二", MorphKind.Vertex);
            FakeNode node = Node(
                "表情", new FakeMorphNodeItem(second), new FakeMorphNodeItem(first));

            IDictionary<string, object> value = ComposedEditFixture.Value(Nodes(
                Operation(ModelEditNodes.NormalizeExpressionNode),
                ComposedEditFixture.Given("all", true)));

            Assert.Same(first, ((IPXMorphNodeItem)node.Items[0]).Morph);
            Assert.Same(second, ((IPXMorphNodeItem)node.Items[1]).Morph);
            Assert.Equal(1, value[ModelEditNodes.ChangedName]);
        }

        [Fact]
        public void ABodyTakesTheNameOfTheBoneItHolds()
        {
            IPXBone bone = Bone("腕");
            FakeBody body = Body("古い名前", bone);

            IDictionary<string, object> value = ComposedEditFixture.Value(Copy(
                Operation(ModelCopyFromReference.BodyNameFromBone),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal("腕", body.Name);
            Assert.Equal(1, value[ModelCopyFromReference.ChangedName]);
        }

        [Fact]
        public void NamingBodiesRemakesOnlyTheBodiesInTheView()
        {
            IPXBone bone = Bone("腕");
            Body("古い名前", bone);

            Copy(
                Operation(ModelCopyFromReference.BodyNameFromBone),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(new[] { ElementKinds.Body }, _fixture.View.Remade);
            Assert.Equal(0, _fixture.View.Redraws);
        }

        [Fact]
        public void MovingJointsRemakesOnlyTheJointsInTheView()
        {
            FakeBone bone = (FakeBone)Bone("腕");
            bone.Position = new V3(1f, 2f, 3f);
            Joint("Joint", Body("一", bone), Body("二", null));

            Copy(
                Operation(ModelCopyFromReference.JointPositionFromBone),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(new[] { ElementKinds.Joint }, _fixture.View.Remade);
            Assert.Equal(0, _fixture.View.Redraws);
        }

        [Fact]
        public void AJointTakesTheNameOfTheFirstBodyItTies()
        {
            FakeBody first = Body("一", null);
            FakeBody second = Body("二", null);
            FakeJoint joint = Joint("古い名前", first, second);

            Copy(
                Operation(ModelCopyFromReference.JointNameFromBodyA),
                ComposedEditFixture.Given("all", true));

            Assert.Equal("一", joint.Name);
        }

        [Fact]
        public void AJointTakesTheNameOfTheSecondBodyItTies()
        {
            FakeBody first = Body("一", null);
            FakeBody second = Body("二", null);
            FakeJoint joint = Joint("古い名前", first, second);

            Copy(
                Operation(ModelCopyFromReference.JointNameFromBodyB),
                ComposedEditFixture.Given("all", true));

            Assert.Equal("二", joint.Name);
        }

        [Fact]
        public void AJointMovesToTheBoneThatTheBodyItTiesHolds()
        {
            FakeBone bone = (FakeBone)Bone("腕");
            bone.Position = new V3(1f, 2f, 3f);
            FakeBody first = Body("一", bone);
            FakeJoint joint = Joint("Joint", first, Body("二", null));

            Copy(
                Operation(ModelCopyFromReference.JointPositionFromBone),
                ComposedEditFixture.Given("all", true));

            Near(1.0, joint.Position.X);
            Near(3.0, joint.Position.Z);
        }

        [Fact]
        public void AJointMovesToTheBoneThatCarriesTheSameName()
        {
            FakeBone bone = (FakeBone)Bone("Joint");
            bone.Position = new V3(4f, 5f, 6f);
            FakeJoint joint = Joint("Joint", Body("一", null), Body("二", null));

            Copy(
                Operation(ModelCopyFromReference.JointPositionFromSameNameBone),
                ComposedEditFixture.Given("all", true));

            Near(4.0, joint.Position.X);
            Near(6.0, joint.Position.Z);
        }

        [Fact]
        public void AJointWithNoBoneOfTheSameNameStaysWhereItIs()
        {
            Bone("腕");
            FakeJoint joint = Joint("Joint", Body("一", null), Body("二", null));
            joint.Position = new V3(1f, 1f, 1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Copy(
                Operation(ModelCopyFromReference.JointPositionFromSameNameBone),
                ComposedEditFixture.Given("all", true)));

            Near(1.0, joint.Position.X);
            Assert.Equal(0, value[ModelCopyFromReference.ChangedName]);
        }

        [Fact]
        public void AnOperationTheCopyingToolDoesNotKnowIsRefused()
        {
            Body("剛体", null);

            IDictionary<string, object> envelope = Copy(
                Operation("いない操作"),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AddingOffsetsPutsOneOffsetPerTargetIntoEveryMorphThatWasPicked()
        {
            _fixture.Model.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            _fixture.Model.Vertex.Add(new FakeVertex(1f, 0f, 0f));
            _fixture.Model.Morph.Add(new FakeMorph("笑い", MorphKind.Vertex));

            Morphs(
                Operation(ModelEditMorphs.AddOffsets),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(
                    ModelEditMorphs.TargetIndicesName, new object[] { 0, 1 }));

            IPXMorph morph = _fixture.Model.Morph[0];
            Assert.Equal(2, morph.Offsets.Count);
            Assert.Same(
                _fixture.Model.Vertex[1],
                ((IPXVertexMorphOffset)morph.Offsets[1]).Vertex);
        }

        [Fact]
        public void TheScreenSelectionSaysWhichVerticesTheOffsetsPointAt()
        {
            _fixture.Model.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            _fixture.Model.Vertex.Add(new FakeVertex(1f, 0f, 0f));
            _fixture.Model.Morph.Add(new FakeMorph("笑い", MorphKind.Vertex));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 1 };

            Morphs(
                Operation(ModelEditMorphs.AddOffsets),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelEditMorphs.TargetSelectedName, true));

            IPXMorph morph = _fixture.Model.Morph[0];
            Assert.Single(morph.Offsets);
            Assert.Same(
                _fixture.Model.Vertex[1],
                ((IPXVertexMorphOffset)morph.Offsets[0]).Vertex);
        }

        [Fact]
        public void AnAddedMaterialOffsetLeavesTheMaterialWhereItIs()
        {
            _fixture.Model.Material.Add(new FakeMaterial("材質"));
            _fixture.Model.Morph.Add(new FakeMorph("色", MorphKind.Material));

            Morphs(
                Operation(ModelEditMorphs.AddOffsets),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(
                    ModelEditMorphs.TargetIndicesName, new object[] { 0 }));

            IPXMaterialMorphOffset made =
                (IPXMaterialMorphOffset)Assert.Single(_fixture.Model.Morph[0].Offsets);
            Assert.Equal(1, made.Op);
            Assert.Equal(0f, made.Diffuse.X);
            Assert.Equal(0f, made.EdgeSize);
        }

        [Fact]
        public void AddingOffsetsToMorphsOfDifferentKindsIsRefused()
        {
            _fixture.Model.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            _fixture.Model.Morph.Add(new FakeMorph("笑い", MorphKind.Vertex));
            _fixture.Model.Morph.Add(new FakeMorph("曲げ", MorphKind.Bone));

            IDictionary<string, object> envelope = Morphs(
                Operation(ModelEditMorphs.AddOffsets),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ModelEditMorphs.TargetIndicesName, new object[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AVertexMorphCarriesHowFarTheVerticesMovedFromTheCopy()
        {
            FakePmx held = new FakePmx();
            held.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            held.Vertex.Add(new FakeVertex(1f, 0f, 0f));
            _fixture.Model.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            _fixture.Model.Vertex.Add(new FakeVertex(3f, 0f, 0f));
            int handle = _fixture.Handles.Issue(typeof(IPXPmx).FullName, held, () => { });

            IDictionary<string, object> value = ComposedEditFixture.Value(FromMoved(
                ComposedEditFixture.Given(ModelMorphFromMoved.NameName, "伸ばした分"),
                ComposedEditFixture.Given(ModelMorphFromMoved.BasePmxHandleName, (long)handle)));

            IPXMorph made = Assert.Single(_fixture.Model.Morph);
            Assert.Equal("伸ばした分", made.Name);
            Assert.Equal(MorphKind.Vertex, made.Kind);
            IPXVertexMorphOffset offset = (IPXVertexMorphOffset)Assert.Single(made.Offsets);
            Assert.Same(_fixture.Model.Vertex[1], offset.Vertex);
            Assert.Equal(2f, offset.Offset.X, 3);
            Assert.Equal(0, value[ModelMorphFromMoved.AddedName]);
            Assert.Equal(1, value[ModelMorphFromMoved.OffsetsName]);
        }

        [Fact]
        public void TheVerticesGoBackToTheCopyOnceTheMorphCarriesTheMove()
        {
            FakePmx held = new FakePmx();
            held.Vertex.Add(new FakeVertex(1f, 0f, 0f));
            _fixture.Model.Vertex.Add(new FakeVertex(3f, 0f, 0f));
            int handle = _fixture.Handles.Issue(typeof(IPXPmx).FullName, held, () => { });

            ComposedEditFixture.Value(FromMoved(
                ComposedEditFixture.Given(ModelMorphFromMoved.NameName, "伸ばした分"),
                ComposedEditFixture.Given(ModelMorphFromMoved.BasePmxHandleName, (long)handle)));

            Assert.Equal(1f, _fixture.Model.Vertex[0].Position.X, 3);
        }

        [Fact]
        public void ACopyThatHoldsItsOwnBonesIsStillTakenWhenTheyStandInTheSamePlaces()
        {
            FakePmx copy = new FakePmx();
            copy.Bone.Add(new FakeBone("ボーン"));
            copy.Vertex.Add(new FakeVertex(0f, 0f, 0f) { Weight1 = 1f, Bone1 = copy.Bone[0] });
            _fixture.Model.Bone.Add(new FakeBone("ボーン"));
            _fixture.Model.Vertex.Add(
                new FakeVertex(3f, 0f, 0f) { Weight1 = 1f, Bone1 = _fixture.Model.Bone[0] });
            int handle = _fixture.Handles.Issue(typeof(IPXPmx).FullName, copy, () => { });

            IDictionary<string, object> value = ComposedEditFixture.Value(FromMoved(
                ComposedEditFixture.Given(ModelMorphFromMoved.NameName, "伸ばした分"),
                ComposedEditFixture.Given(ModelMorphFromMoved.BasePmxHandleName, (long)handle)));

            Assert.Equal(1, value[ModelMorphFromMoved.OffsetsName]);
            Assert.Equal(0f, _fixture.Model.Vertex[0].Position.X, 3);
        }

        [Theory]
        [InlineData("normal")]
        [InlineData("uva1")]
        [InlineData("sdef")]
        [InlineData("edgeScale")]
        public void ACopyWhoseVerticesDifferInAnyOtherAttributeIsRefused(string attribute)
        {
            FakePmx copy = new FakePmx();
            copy.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            FakeVertex moved = new FakeVertex(3f, 0f, 0f);
            switch (attribute)
            {
                case "normal":
                    moved.Normal = new V3(1f, 0f, 0f);
                    break;

                case "uva1":
                    moved.UVA1 = new V4(1f, 0f, 0f, 0f);
                    break;

                case "sdef":
                    moved.SDEF = true;
                    break;

                default:
                    moved.EdgeScale = 2f;
                    break;
            }

            _fixture.Model.Vertex.Add(moved);
            int handle = _fixture.Handles.Issue(typeof(IPXPmx).FullName, copy, () => { });

            IDictionary<string, object> envelope = FromMoved(
                ComposedEditFixture.Given(ModelMorphFromMoved.NameName, "伸ばした分"),
                ComposedEditFixture.Given(ModelMorphFromMoved.BasePmxHandleName, (long)handle));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Empty(_fixture.Model.Morph);
        }

        [Fact]
        public void ACopyWhoseVertexPointsAtABoneOutsideTheListIsRefused()
        {
            FakePmx copy = new FakePmx();
            copy.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            _fixture.Model.Vertex.Add(
                new FakeVertex(3f, 0f, 0f) { Weight1 = 1f, Bone1 = new FakeBone("並びに居ない") });
            int handle = _fixture.Handles.Issue(typeof(IPXPmx).FullName, copy, () => { });

            IDictionary<string, object> envelope = FromMoved(
                ComposedEditFixture.Given(ModelMorphFromMoved.NameName, "伸ばした分"),
                ComposedEditFixture.Given(ModelMorphFromMoved.BasePmxHandleName, (long)handle));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Empty(_fixture.Model.Morph);
        }

        [Fact]
        public void ACopyWhoseVerticesDifferInAnythingButTheirPlaceIsRefused()
        {
            FakePmx copy = new FakePmx();
            copy.Bone.Add(new FakeBone("ボーン"));
            copy.Vertex.Add(new FakeVertex(0f, 0f, 0f) { Weight1 = 1f, Bone1 = copy.Bone[0] });
            _fixture.Model.Bone.Add(new FakeBone("ボーン"));
            _fixture.Model.Vertex.Add(
                new FakeVertex(3f, 0f, 0f) { Weight1 = 0.5f, Bone1 = _fixture.Model.Bone[0] });
            int handle = _fixture.Handles.Issue(typeof(IPXPmx).FullName, copy, () => { });

            IDictionary<string, object> envelope = FromMoved(
                ComposedEditFixture.Given(ModelMorphFromMoved.NameName, "伸ばした分"),
                ComposedEditFixture.Given(ModelMorphFromMoved.BasePmxHandleName, (long)handle));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Empty(_fixture.Model.Morph);
            Assert.Equal(3f, _fixture.Model.Vertex[0].Position.X, 3);
        }

        [Fact]
        public void ACopyThatHoldsADifferentCountOfAnythingElseIsRefused()
        {
            FakePmx held = new FakePmx();
            held.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            held.Material.Add(new FakeMaterial("材質"));
            _fixture.Model.Vertex.Add(new FakeVertex(1f, 0f, 0f));
            int handle = _fixture.Handles.Issue(typeof(IPXPmx).FullName, held, () => { });

            IDictionary<string, object> envelope = FromMoved(
                ComposedEditFixture.Given(ModelMorphFromMoved.NameName, "伸ばした分"),
                ComposedEditFixture.Given(ModelMorphFromMoved.BasePmxHandleName, (long)handle));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Empty(_fixture.Model.Morph);
            Assert.Equal(1f, _fixture.Model.Vertex[0].Position.X, 3);
        }

        [Fact]
        public void ACopyWithADifferentCountOfVerticesIsRefused()
        {
            FakePmx held = new FakePmx();
            held.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            _fixture.Model.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            _fixture.Model.Vertex.Add(new FakeVertex(1f, 0f, 0f));
            int handle = _fixture.Handles.Issue(typeof(IPXPmx).FullName, held, () => { });

            IDictionary<string, object> envelope = FromMoved(
                ComposedEditFixture.Given(ModelMorphFromMoved.NameName, "伸ばした分"),
                ComposedEditFixture.Given(ModelMorphFromMoved.BasePmxHandleName, (long)handle));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Empty(_fixture.Model.Morph);
        }

        [Fact]
        public void AVertexMorphIsMadeFromTheVerticesThatWerePointedAt()
        {
            _fixture.Model.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            _fixture.Model.Vertex.Add(new FakeVertex(1f, 0f, 0f));

            IDictionary<string, object> value = ComposedEditFixture.Value(Morphs(
                Operation(ModelEditMorphs.VertexMorphFromVertices),
                ComposedEditFixture.Given(ModelEditMorphs.NameName, "分けた分"),
                ComposedEditFixture.Given(
                    ModelEditMorphs.TargetIndicesName, new object[] { 1 })));

            IPXMorph made = Assert.Single(_fixture.Model.Morph);
            Assert.Equal("分けた分", made.Name);
            Assert.Equal(MorphKind.Vertex, made.Kind);
            Assert.Same(
                _fixture.Model.Vertex[1],
                ((IPXVertexMorphOffset)Assert.Single(made.Offsets)).Vertex);
            Assert.Equal(new object[] { 0 }, (object[])value[ModelEditMorphs.AddedName]);
        }

        private IDictionary<string, object> FromMoved(
            params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelMorphFromMoved.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IDictionary<string, object> Morphs(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ModelEditMorphs.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IDictionary<string, object> Nodes(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ModelEditNodes.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IDictionary<string, object> Copy(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelCopyFromReference.ToolName, ComposedEditFixture.Arguments(given));
        }

        private static KeyValuePair<string, object> Operation(string operation)
        {
            return ComposedEditFixture.Given(ComposedOperation.OperationName, operation);
        }

        private IPXVertex Vertex(float x, float y, float z)
        {
            FakeVertex vertex = new FakeVertex(x, y, z);
            _fixture.Model.Vertex.Add(vertex);

            return vertex;
        }

        private IPXBone Bone(string name)
        {
            FakeBone bone = new FakeBone(name);
            _fixture.Model.Bone.Add(bone);

            return bone;
        }

        private FakeMorph Morph(string name, MorphKind kind, params IPXMorphOffset[] offsets)
        {
            FakeMorph morph = new FakeMorph(name, kind);
            foreach (IPXMorphOffset offset in offsets)
            {
                morph.Offsets.Add(offset);
            }

            _fixture.Model.Morph.Add(morph);

            return morph;
        }

        private FakeNode Node(string name, params IPXNodeItem[] items)
        {
            FakeNode node = new FakeNode(name);
            foreach (IPXNodeItem item in items)
            {
                node.Items.Add(item);
            }

            _fixture.Model.Node.Add(node);

            return node;
        }

        private FakeBody Body(string name, IPXBone bone)
        {
            FakeBody body = new FakeBody(name) { Bone = bone };
            _fixture.Model.Body.Add(body);

            return body;
        }

        private FakeJoint Joint(string name, IPXBody first, IPXBody second)
        {
            FakeJoint joint = new FakeJoint(name) { BodyA = first, BodyB = second };
            _fixture.Model.Joint.Add(joint);

            return joint;
        }

        private void Face(IPXVertex first, IPXVertex second)
        {
            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(first, second, first));
            _fixture.Model.Material.Add(material);
        }

        /// <summary>その頂点をX方向へ動かす頂点モーフのオフセット。</summary>
        private static IPXMorphOffset Shift(IPXVertex vertex, float by)
        {
            return new FakeVertexMorphOffset(vertex) { Offset = new V3(by, 0f, 0f) };
        }

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }
    }
}
