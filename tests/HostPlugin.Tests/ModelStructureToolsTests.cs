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
            Morph("笑い", MorphKind.Vertex, Shift(vertex, 1f)).NameE = "smile1";
            Morph("笑い", MorphKind.Vertex, Shift(vertex, 2f)).NameE = "smile2";

            IDictionary<string, object> value = ComposedEditFixture.Value(Morphs(
                Operation(ModelEditMorphs.MergeSameName),
                ComposedEditFixture.Given("all", true)));

            IPXMorph kept = Assert.Single(_fixture.Model.Morph);
            Assert.Equal("smile1", kept.NameE);
            Assert.Equal(2, kept.Offsets.Count);
            Assert.Equal(1, value[ModelEditMorphs.RemovedName]);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void MorphsOfTheSameKindAreMergedIntoTheFirstOne()
        {
            IPXVertex vertex = Vertex(0f, 0f, 0f);
            Morph("一", MorphKind.Vertex, Shift(vertex, 1f));
            Morph("二", MorphKind.Vertex, Shift(vertex, 2f));
            Morph("色", MorphKind.Material);

            Morphs(
                Operation(ModelEditMorphs.MergeSameKind),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(new[] { "一", "色" }, _fixture.Model.Morph.Select(morph => morph.Name).ToArray());
            Assert.Equal(2, _fixture.Model.Morph[0].Offsets.Count);
        }

        [Fact]
        public void MergingByKindWithAddingPutsTheOffsetsForOneVertexTogether()
        {
            IPXVertex vertex = Vertex(0f, 0f, 0f);
            Morph("一", MorphKind.Vertex, Shift(vertex, 1f));
            Morph("二", MorphKind.Vertex, Shift(vertex, 2f));

            Morphs(
                Operation(ModelEditMorphs.MergeSameKindAdd),
                ComposedEditFixture.Given("all", true));

            IPXMorph kept = Assert.Single(_fixture.Model.Morph);
            Assert.Equal("一", kept.Name);
            IPXVertexMorphOffset held = (IPXVertexMorphOffset)Assert.Single(kept.Offsets);
            Near(3.0, held.Offset.X);
        }

        [Fact]
        public void MergingLargeMorphsByKindWithAddingFinishesInTime()
        {
            IPXVertex[] vertices = Enumerable.Range(0, ManyOffsets)
                .Select(at => Vertex(at, 0f, 0f))
                .ToArray();
            Morph("一", MorphKind.Vertex, vertices.Select(v => Shift(v, 1f)).ToArray());
            Morph("二", MorphKind.Vertex, vertices.Reverse().Select(v => Shift(v, 2f)).ToArray());

            TimeSpan editor = _fixture.EditorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            Morphs(
                Operation(ModelEditMorphs.MergeSameKindAdd),
                ComposedEditFixture.Given("all", true));
            elapsed.Stop();
            TimeSpan spent = elapsed.Elapsed - (_fixture.EditorTime - editor);

            IPXMorph kept = Assert.Single(_fixture.Model.Morph);
            Assert.Equal("一", kept.Name);
            Assert.Equal(ManyOffsets, kept.Offsets.Count);
            Near(3.0, ((IPXVertexMorphOffset)kept.Offsets[0]).Offset.X);
            Assert.True(spent < TimeLimit, "合わせるのに " + spent + " かかった");
        }

        [Fact]
        public void AGroupMorphIsAddedThatCallsTheOnesThatWerePicked()
        {
            Morph("笑い", MorphKind.Vertex);

            IDictionary<string, object> value = ComposedEditFixture.Value(Morphs(
                Operation(ModelEditMorphs.GroupInto),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ModelEditMorphs.NameName, "まとめ")));

            IPXMorph made = _fixture.Model.Morph.Single(morph => Now(morph).Kind == MorphKind.Group);
            Assert.Equal("まとめ", made.Name);
            Assert.Same(
                _fixture.Model.Morph.Single(morph => morph.Name == "笑い"),
                ((IPXGroupMorphOffset)Assert.Single(made.Offsets)).Morph);
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

            IPXMorph made = _fixture.Model.Morph.Single(morph => Now(morph).Kind == MorphKind.Flip);
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
                _fixture.Model.Morph.Where(morph => Now(morph).Kind == MorphKind.Vertex),
                morph => Assert.Single(morph.Offsets));
        }

        [Fact]
        public void SplittingSeveralMorphsAnswersWhereEachPartStandsAfterTheCall()
        {
            IPXVertex near = Vertex(0f, 0f, 0f);
            IPXVertex apart = Vertex(20f, 0f, 0f);
            Face(near, apart);
            Morph("一", MorphKind.Vertex, Shift(near, 1f), Shift(apart, 1f));
            Morph("二", MorphKind.Vertex, Shift(near, 2f), Shift(apart, 2f));

            IDictionary<string, object> value = ComposedEditFixture.Value(Morphs(
                Operation(ModelEditMorphs.SplitVertices),
                ComposedEditFixture.Given("all", true)));

            int[] added = ((object[])value[ModelEditMorphs.AddedName]).Cast<int>().ToArray();
            Assert.Equal(4, added.Distinct().Count());
            Assert.Equal(
                new[] { 1f, 1f, 2f, 2f },
                added.Select(at => ((IPXVertexMorphOffset)Assert.Single(
                    _fixture.Model.Morph[at].Offsets)).Offset.X).ToArray());
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

            TimeSpan editor = _fixture.EditorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            IDictionary<string, object> value = ComposedEditFixture.Value(Morphs(
                Operation(ModelEditMorphs.SplitVertices),
                ComposedEditFixture.Given("all", true)));
            elapsed.Stop();
            TimeSpan spent = elapsed.Elapsed - (_fixture.EditorTime - editor);

            Assert.Single((object[])value[ModelEditMorphs.AddedName]);
            Assert.True(spent < TimeLimit, "分けるのに " + spent + " かかった");
        }

        [Fact]
        public void AMaterialMorphIsAddedThatHoldsWhatTheMaterialsNowShow()
        {
            FakeMaterial material = new FakeMaterial("材質");
            _fixture.Model.Material.Add(material);

            Morphs(
                Operation(ModelEditMorphs.MaterialFromCurrent),
                ComposedEditFixture.Given(ModelEditMorphs.NameName, "いまの色"));

            IPXMorph made = _fixture.Model.Morph.Single(morph => Now(morph).Kind == MorphKind.Material);
            Assert.Same(Now(material), ((IPXMaterialMorphOffset)Assert.Single(made.Offsets)).Material);
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

            Assert.Equal(2, Now(node).Items.Count);
            Assert.Same(Now(missing), ((IPXBoneNodeItem)Now(node).Items[1]).Bone);
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

            Assert.Same(Now(missing), ((IPXMorphNodeItem)Assert.Single(Now(node).Items)).Morph);
        }

        [Fact]
        public void PuttingAHiddenMorphOnTheExpressionNodeIsWarnedAbout()
        {
            Morph("隠し", MorphKind.Vertex);

            IDictionary<string, object> envelope = Nodes(
                Operation(ModelEditNodes.RegisterUnlistedMorphs),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Contains(
                ((object[])envelope[ToolEnvelope.WarningsName]).Cast<string>(),
                warning => warning.Contains("「隠し」"));
        }

        [Fact]
        public void PuttingAShownMorphOnTheExpressionNodeIsNotWarnedAbout()
        {
            Morph("表示", MorphKind.Vertex).Panel = 4;

            IDictionary<string, object> envelope = Nodes(
                Operation(ModelEditNodes.RegisterUnlistedMorphs),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.False(envelope.ContainsKey(ToolEnvelope.WarningsName), "警告が付いている。");
        }

        [Fact]
        public void AHiddenMorphAlreadyOnTheExpressionNodeIsNotWarnedAboutAgain()
        {
            FakeMorph kept = Morph("前から", MorphKind.Vertex);
            _fixture.Model.ExpressionNode.Items.Add(new FakeMorphNodeItem(kept));
            Morph("表示", MorphKind.Vertex).Panel = 4;

            IDictionary<string, object> envelope = Nodes(
                Operation(ModelEditNodes.RegisterUnlistedMorphs),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.False(envelope.ContainsKey(ToolEnvelope.WarningsName), "警告が付いている。");
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

            Assert.Equal(2, Now(node).Items.Count);
            Assert.Same(Now(wanted), ((IPXBoneNodeItem)Now(node).Items[1]).Bone);
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
                Now(wanted),
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

            Assert.Single(Now(node).Items);
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
                Now(missing),
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
                Now(missing),
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

            Assert.Single(Now(node).Items);
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

            Assert.Equal(2, Now(node).Items.Count);
            Assert.Same(Now(wanted), ((IPXBoneNodeItem)Now(node).Items[1]).Bone);
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

            Assert.Same(Now(wanted), ((IPXMorphNodeItem)Assert.Single(Now(node).Items)).Morph);
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

            Assert.Same(Now(wanted), ((IPXBoneNodeItem)Assert.Single(Now(node).Items)).Bone);
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

            Assert.Same(Now(first), ((IPXMorphNodeItem)Now(node).Items[0]).Morph);
            Assert.Same(Now(second), ((IPXMorphNodeItem)Now(node).Items[1]).Morph);
            Assert.Equal(1, value[ModelEditNodes.ChangedName]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ABodyTakesTheNameOfTheBoneItHolds(bool suppressed)
        {
            IPXBone bone = Bone("腕");
            FakeBody body = Body("古い名前", bone);

            IDictionary<string, object> value = ComposedEditFixture.Value(Copy(Undo(
                suppressed,
                Operation(ModelCopyFromReference.BodyNameFromBone),
                ComposedEditFixture.Given("all", true))));

            Assert.Equal("腕", Now(body).Name);
            Assert.Equal(1, value[ModelCopyFromReference.ChangedName]);
            Assert.Equal(suppressed ? 1 : 0, _fixture.Partials.Count);
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AJointTakesTheNameOfTheFirstBodyItTies(bool suppressed)
        {
            FakeBody first = Body("一", null);
            FakeBody second = Body("二", null);
            FakeJoint joint = Joint("古い名前", first, second);

            Copy(Undo(
                suppressed,
                Operation(ModelCopyFromReference.JointNameFromBodyA),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal("一", Now(joint).Name);
            Assert.Equal(suppressed ? 1 : 0, _fixture.Partials.Count);
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

            Assert.Equal("二", Now(joint).Name);
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

            Near(1.0, Now(joint).Position.X);
            Near(3.0, Now(joint).Position.Z);
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

            Near(4.0, Now(joint).Position.X);
            Near(6.0, Now(joint).Position.Z);
        }

        [Fact]
        public void AJointWithNoBoneOfTheSameNameStaysWhereItIs()
        {
            Bone("腕");
            FakeJoint joint = Joint("Joint", Body("一", null), Body("二", null));
            Now(joint).Position = new V3(1f, 1f, 1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Copy(
                Operation(ModelCopyFromReference.JointPositionFromSameNameBone),
                ComposedEditFixture.Given("all", true)));

            Near(1.0, Now(joint).Position.X);
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
            Assert.Equal(2, Now(morph).Offsets.Count);
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

        /// <summary>
        /// 読み込んだままのモデルは、軽い方のボーンを先に持つBDEF2頂点を持ちうる。エディタは反映の
        /// たびに頂点のウェイトを重い順へ並べ直すので、複製を取ってから頂点を動かすと、いまの頂点は
        /// 同じボーンと重みを別の並びで持つ。
        /// </summary>
        [Fact]
        public void ACopyTakenBeforeTheWeightsWereSortedIsStillTakenAfterAMove()
        {
            _fixture.Model.Bone.Add(new FakeBone("腕"));
            _fixture.Model.Bone.Add(new FakeBone("ひじ"));
            _fixture.Model.Vertex.Add(new FakeVertex(0f, 0f, 0f)
            {
                Bone1 = _fixture.Model.Bone[0],
                Weight1 = 0.25f,
                Bone2 = _fixture.Model.Bone[1],
                Weight2 = 0.75f,
            });
            int handle = _fixture.Handles.Issue(
                typeof(IPXPmx).FullName, FakeEditorState.Duplicate(_fixture.Model), () => { });
            Moved(2f);

            IDictionary<string, object> value = ComposedEditFixture.Value(FromMoved(
                ComposedEditFixture.Given(ModelMorphFromMoved.NameName, "伸ばした分"),
                ComposedEditFixture.Given(ModelMorphFromMoved.BasePmxHandleName, (long)handle)));

            Assert.Equal(1, value[ModelMorphFromMoved.OffsetsName]);
            IPXVertexMorphOffset offset =
                (IPXVertexMorphOffset)Assert.Single(_fixture.Model.Morph[0].Offsets);
            Assert.Equal(2f, offset.Offset.X, 3);
            Assert.Equal(0f, _fixture.Model.Vertex[0].Position.X, 3);
        }

        /// <summary>
        /// エディタは反映のたびにSDEF頂点のボーンを並びの位置の小さい順へ直し、R0とR1も一緒に
        /// 入れ替える。複製を取ってから頂点を動かすと、いまの頂点は同じ中身を別の並びで持つ。
        /// </summary>
        [Fact]
        public void ACopyTakenBeforeTheSdefBonesWereSortedIsStillTakenAfterAMove()
        {
            _fixture.Model.Bone.Add(new FakeBone("腕"));
            _fixture.Model.Bone.Add(new FakeBone("ひじ"));
            _fixture.Model.Vertex.Add(new FakeVertex(0f, 0f, 0f)
            {
                Bone1 = _fixture.Model.Bone[1],
                Weight1 = 0.5f,
                Bone2 = _fixture.Model.Bone[0],
                Weight2 = 0.5f,
                SDEF = true,
                SDEF_R0 = new V3(1f, 0f, 0f),
                SDEF_R1 = new V3(-1f, 0f, 0f),
            });
            int handle = _fixture.Handles.Issue(
                typeof(IPXPmx).FullName, FakeEditorState.Duplicate(_fixture.Model), () => { });
            Moved(2f);

            IDictionary<string, object> value = ComposedEditFixture.Value(FromMoved(
                ComposedEditFixture.Given(ModelMorphFromMoved.NameName, "伸ばした分"),
                ComposedEditFixture.Given(ModelMorphFromMoved.BasePmxHandleName, (long)handle)));

            Assert.Equal(1, value[ModelMorphFromMoved.OffsetsName]);
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

        /// <summary>エディタの操作として、先頭の頂点をXへ動かして反映する。</summary>
        private void Moved(float along)
        {
            ComposedEditFixture.Value(_fixture.Call(
                ModelPlaceElements.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given(
                        ComposedOperation.OperationName, ModelPlaceElements.TranslateBy),
                    ComposedEditFixture.Given(
                        ModelPlaceElements.OffsetName, new object[] { along, 0f, 0f }),
                    ComposedEditFixture.Given(
                        ModelPlaceElements.TargetsName,
                        new object[]
                        {
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                { ModelPlaceElements.KindName, ElementKinds.Vertex },
                                { "indices", new object[] { 0 } },
                            },
                        }))));
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
                Now(morph).Offsets.Add(offset);
            }

            _fixture.Model.Morph.Add(morph);

            return morph;
        }

        private FakeNode Node(string name, params IPXNodeItem[] items)
        {
            FakeNode node = new FakeNode(name);
            foreach (IPXNodeItem item in items)
            {
                Now(node).Items.Add(item);
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

        /// <summary>握った要素が並んでいた位置に、いまのモデルで並んでいる要素。</summary>
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
