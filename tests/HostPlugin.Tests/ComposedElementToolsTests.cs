using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 並びを動かす・要素を入れる・要素を消すの3つ。どれも1回の呼び出しで、1回のまとめての
    /// 反映に収まる。
    /// </summary>
    public sealed class ComposedElementToolsTests : IDisposable
    {

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void MovingUpSwapsWithTheOneAboveAndAnswersTheNewPosition()
        {
            Bones("一", "二", "三");

            IDictionary<string, object> value = ComposedEditFixture.Value(Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 2 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.Up)));

            Assert.Equal(new[] { "一", "三", "二" }, Names());
            Assert.Equal(new object[] { 1 }, (object[])value[ModelReorderElements.IndicesName]);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void MovingUpTheTopOneLeavesTheOrderAlone()
        {
            Bones("一", "二");

            Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.Up));

            Assert.Equal(new[] { "一", "二" }, Names());
        }

        [Fact]
        public void MovingDownTheBottomOneLeavesTheOrderAlone()
        {
            Bones("一", "二");

            Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(
                    ModelReorderElements.MoveName, ModelReorderElements.Down));

            Assert.Equal(new[] { "一", "二" }, Names());
        }

        [Fact]
        public void MovingToTheTopKeepsThePickedOnesInTheOrderTheyStoodIn()
        {
            Bones("一", "二", "三", "四");

            Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 3, 1 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.Top));

            Assert.Equal(new[] { "二", "四", "一", "三" }, Names());
        }

        [Fact]
        public void MovingToTheBottomPutsThemLast()
        {
            Bones("一", "二", "三");

            Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(
                    ModelReorderElements.MoveName, ModelReorderElements.Bottom));

            Assert.Equal(new[] { "二", "三", "一" }, Names());
        }

        [Fact]
        public void MovingToAPositionPutsThemThereInOrder()
        {
            Bones("一", "二", "三", "四");

            Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 0, 1 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.To),
                ComposedEditFixture.Given(ModelReorderElements.ToIndexName, 2));

            Assert.Equal(new[] { "三", "四", "一", "二" }, Names());
        }

        [Fact]
        public void MovingToAPositionWithoutSayingWhereIsRefused()
        {
            Bones("一", "二");

            IDictionary<string, object> envelope = Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.To));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Equal(0, _fixture.Commits);
        }

        [Fact]
        public void SayingWhereWithoutMovingToAPositionIsRefused()
        {
            Bones("一", "二");

            IDictionary<string, object> envelope = Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.Up),
                ComposedEditFixture.Given(ModelReorderElements.ToIndexName, 1));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void APositionOutsideTheListIsRefused()
        {
            Bones("一", "二");

            IDictionary<string, object> envelope = Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.To),
                ComposedEditFixture.Given(ModelReorderElements.ToIndexName, 5));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AMoveTheToolDoesNotKnowIsRefused()
        {
            Bones("一", "二");

            IDictionary<string, object> envelope = Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, "ななめ"));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void FacesMoveInsideTheMaterialThatOwnsThem()
        {
            FakeVertex vertex = new FakeVertex();
            _fixture.Model.Vertex.Add(vertex);
            FakeMaterial material = new FakeMaterial("材質");
            FakeFace first = new FakeFace(vertex, vertex, vertex);
            FakeFace second = new FakeFace(vertex, vertex, vertex);
            material.Faces.Add(first);
            material.Faces.Add(second);
            _fixture.Model.Material.Add(material);

            Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentIndices", new object[] { 0 }),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.Up));

            Assert.Equal(new IPXFace[] { second, first }, material.Faces.ToArray());
        }

        [Fact]
        public void AKindThatPmxLinesUpDoesNotTakeAParentSet()
        {
            Bones("一");

            IDictionary<string, object> envelope = Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("parentIndices", new object[] { 0 }),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.Up));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AKindAnElementLinesUpNeedsTheParentSet()
        {
            _fixture.Model.Material.Add(new FakeMaterial("材質"));

            IDictionary<string, object> envelope = Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.Up));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void LeavingOutTheKindIsRefused()
        {
            IDictionary<string, object> envelope = Reorder(
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.Up));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void NewElementsGoInAtThePositionAndAnswerWhereTheyLanded()
        {
            Bones("一", "二");

            IDictionary<string, object> value = ComposedEditFixture.Value(Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New),
                ComposedEditFixture.Given(ModelInsertElements.CountName, 2),
                ComposedEditFixture.Given(ModelInsertElements.AtName, 1)));

            Assert.Equal(4, _fixture.Model.Bone.Count);
            Assert.Equal(new object[] { 1, 2 }, (object[])value[ModelInsertElements.IndicesName]);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void LeavingOutThePositionPutsThemAtTheEnd()
        {
            Bones("一");

            Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New));

            Assert.Equal(2, _fixture.Model.Bone.Count);
        }

        [Fact]
        public void CloningPutsACopyOfThePickedOneInWithoutTouchingTheOriginal()
        {
            Bones("一", "二");
            IPXBone original = _fixture.Model.Bone[0];

            Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.Clone),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelInsertElements.AtName, 2));

            Assert.Equal(new[] { "一", "二", "一" }, Names());
            Assert.Same(original, _fixture.Model.Bone[0]);
            Assert.NotSame(original, _fixture.Model.Bone[2]);
        }

        [Fact]
        public void CloningWithoutPickingWhatToCopyIsRefused()
        {
            Bones("一");

            IDictionary<string, object> envelope = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.Clone));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void MakingNewOnesWhilePickingWhatToCopyIsRefused()
        {
            Bones("一");

            IDictionary<string, object> envelope = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void APositionPastTheEndOfTheListIsRefused()
        {
            Bones("一");

            IDictionary<string, object> envelope = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New),
                ComposedEditFixture.Given(ModelInsertElements.AtName, 2));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void MakingANodeItemNeedsTheVariantThatSaysWhichOneToMake()
        {
            _fixture.Model.Node.Add(new FakeNode("枠"));

            IDictionary<string, object> envelope = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.NodeItem),
                ComposedEditFixture.Given("parentIndices", new object[] { 0 }),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(ElementKinds.VariantName, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void TheVariantMakesTheNodeItemThatWasAskedFor()
        {
            FakeNode node = new FakeNode("枠");
            _fixture.Model.Node.Add(node);

            Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.NodeItem),
                ComposedEditFixture.Given("parentIndices", new object[] { 0 }),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New),
                ComposedEditFixture.Given(ElementKinds.VariantName, ElementKinds.MorphVariant));

            Assert.IsAssignableFrom<IPXMorphNodeItem>(Assert.Single(node.Items));
        }

        [Fact]
        public void DeletingTakesTheOnesPickedAndRepairsWhatPointedAtThem()
        {
            FakeBone root = new FakeBone("親");
            FakeBone going = new FakeBone("消す") { Parent = root };
            _fixture.Model.Bone.Add(root);
            _fixture.Model.Bone.Add(going);
            FakeVertex vertex = new FakeVertex { Bone1 = going, Weight1 = 1f };
            _fixture.Model.Vertex.Add(vertex);

            IDictionary<string, object> value = ComposedEditFixture.Value(Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 1 })));

            Assert.Equal(new[] { "親" }, Names());
            Assert.Same(root, vertex.Bone1);
            Assert.Equal(1, value[ModelDeleteElements.RemovedName]);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void KeepingTheRelatedOnesLeavesThePointersDangling()
        {
            FakeBone going = new FakeBone("消す");
            _fixture.Model.Bone.Add(going);
            FakeVertex vertex = new FakeVertex { Bone1 = going, Weight1 = 1f };
            _fixture.Model.Vertex.Add(vertex);

            Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ReferenceCleanup.RelatedName, ReferenceCleanup.Keep));

            Assert.Same(going, vertex.Bone1);
        }

        [Fact]
        public void CascadingTakesTheOnesOnlyTheRemovedElementUsedAndSaysHowMany()
        {
            FakeVertex alone = new FakeVertex();
            _fixture.Model.Vertex.Add(alone);
            FakeMaterial going = new FakeMaterial("消す");
            going.Faces.Add(new FakeFace(alone, alone, alone));
            _fixture.Model.Material.Add(going);

            IDictionary<string, object> value = ComposedEditFixture.Value(Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Material),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ReferenceCleanup.RelatedName, ReferenceCleanup.Cascade)));

            Assert.Empty(_fixture.Model.Vertex);
            IDictionary<string, object> dragged = ((object[])value[ModelDeleteElements.FollowingName])
                .Cast<IDictionary<string, object>>()
                .Single(entry => Equals(entry[ModelDeleteElements.KindName], ElementKinds.Vertex));
            Assert.Equal(1, dragged[ModelDeleteElements.RemovedName]);
        }

        [Fact]
        public void DeletingFacesTakesThemFromTheMaterialThatOwnsThem()
        {
            FakeVertex vertex = new FakeVertex();
            _fixture.Model.Vertex.Add(vertex);
            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(vertex, vertex, vertex));
            material.Faces.Add(new FakeFace(vertex, vertex, vertex));
            _fixture.Model.Material.Add(material);

            Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentIndices", new object[] { 0 }),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Single(material.Faces);
        }

        [Fact]
        public void DeletingEverythingTakesTheWholeList()
        {
            Bones("一", "二", "三");

            Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("all", true));

            Assert.Empty(_fixture.Model.Bone);
        }

        [Fact]
        public void APositionOutsideTheListIsRefusedWithoutReflecting()
        {
            Bones("一");

            IDictionary<string, object> envelope = Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 3 }));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            Assert.Single(_fixture.Model.Bone);
            Assert.Equal(0, _fixture.Commits);
        }

        [Fact]
        public void TheTableAndTheFrameAreRequired()
        {
            Assert.Throws<ArgumentNullException>(
                () => ComposedModelTools.AddTo(null, _fixture.Edit, () => _fixture.Builder));
            Assert.Throws<ArgumentNullException>(
                () => ComposedModelTools.AddTo(new McpMethodTable(), null, () => _fixture.Builder));
            Assert.Throws<ArgumentNullException>(
                () => ComposedModelTools.AddTo(new McpMethodTable(), _fixture.Edit, null));
        }

        [Fact]
        public void MovingSeveralOnesThatAreNotNextToEachOtherLiftsEachOfThem()
        {
            Bones("一", "二", "三", "四");

            IDictionary<string, object> value = ComposedEditFixture.Value(Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 1, 3 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.Up)));

            Assert.Equal(new[] { "二", "一", "四", "三" }, Names());
            Assert.Equal(new object[] { 0, 2 }, (object[])value[ModelReorderElements.IndicesName]);
        }

        [Fact]
        public void LeavingOutWhichOperationToRunIsRefused()
        {
            Bones("一");

            IDictionary<string, object> envelope = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AnOperationTheToolDoesNotKnowIsRefused()
        {
            Bones("一");

            IDictionary<string, object> envelope = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(ModelInsertElements.OperationName, "つくろう"));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1.5)]
        public void AHowManyThatIsNotAPositiveWholeNumberIsRefused(object count)
        {
            Bones("一");

            IDictionary<string, object> envelope = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New),
                ComposedEditFixture.Given(ModelInsertElements.CountName, count));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Single(_fixture.Model.Bone);
        }

        [Fact]
        public void APositionBeforeTheStartOfTheListIsRefused()
        {
            Bones("一");

            IDictionary<string, object> envelope = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New),
                ComposedEditFixture.Given(ModelInsertElements.AtName, -1));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AVariantForAKindThatDecidesItsOwnShapeIsRefused()
        {
            Bones("一");

            IDictionary<string, object> envelope = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New),
                ComposedEditFixture.Given(ElementKinds.VariantName, ElementKinds.BoneVariant));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void InsertingIntoSeveralParentsPutsOneIntoEachOfThem()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);

            IDictionary<string, object> value = ComposedEditFixture.Value(Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New)));

            Assert.Single(first.Faces);
            Assert.Single(second.Faces);
            Assert.Equal(new object[] { 0, 0 }, (object[])value[ModelInsertElements.IndicesName]);
        }

        [Fact]
        public void LeavingOutWhatToDeleteIsRefused()
        {
            Bones("一");

            IDictionary<string, object> envelope = Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Single(_fixture.Model.Bone);
        }

        [Fact]
        public void DeletingAnswersHowManyPointersItPutBackInOrder()
        {
            FakeBone going = new FakeBone("消す");
            _fixture.Model.Bone.Add(going);
            _fixture.Model.Body.Add(new FakeBody("剛体") { Bone = going });

            IDictionary<string, object> value = ComposedEditFixture.Value(Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 0 })));

            Assert.Equal(1, value[ModelDeleteElements.RepairedName]);
            Assert.Null(_fixture.Model.Body[0].Bone);
        }

        private IDictionary<string, object> Reorder(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelReorderElements.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IDictionary<string, object> Insert(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelInsertElements.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IDictionary<string, object> Delete(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelDeleteElements.ToolName, ComposedEditFixture.Arguments(given));
        }

        private void Bones(params string[] names)
        {
            foreach (string name in names)
            {
                _fixture.Model.Bone.Add(new FakeBone(name));
            }
        }

        private string[] Names()
        {
            return _fixture.Model.Bone.Select(bone => bone.Name).ToArray();
        }
    }
}
