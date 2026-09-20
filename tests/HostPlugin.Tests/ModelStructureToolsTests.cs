using System;
using System.Collections.Generic;
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
    public sealed class ModelStructureToolsTests : IDisposable
    {
        /// <summary>小数の突き合わせで見る桁。</summary>
        private const int Digits = 4;

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
                ComposedEditFixture.Given("all", true)));

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
                ComposedEditFixture.Given("all", true));

            Assert.Same(missing, ((IPXMorphNodeItem)Assert.Single(node.Items)).Morph);
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
        public void AnOperationTheCopyingToolDoesNotKnowIsRefused()
        {
            Body("剛体", null);

            IDictionary<string, object> envelope = Copy(
                Operation("いない操作"),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
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
