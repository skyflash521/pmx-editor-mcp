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
            Assert.Equal(new[] { "1+1" }, Moved(value));
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void MovingManyAtOnceAnswersTheRunTheyLandedIn()
        {
            Bones(Enumerable.Range(0, 30000).Select(at => "骨" + at).ToArray());

            IDictionary<string, object> value = ComposedEditFixture.Value(Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("range", Span(0, 29999)),
                ComposedEditFixture.Given(
                    ModelReorderElements.MoveName, ModelReorderElements.Bottom)));

            Assert.Equal("骨29999", _fixture.Model.Bone[0].Name);
            Assert.Equal(new[] { "1+29999" }, Moved(value));
        }

        [Fact]
        public void MovingSoManyScatteredOnesThatTheAnswerCannotFitChangesNothing()
        {
            Bones(Enumerable.Range(0, 1000).Select(at => "骨" + at).ToArray());
            object[] odd = Enumerable.Range(0, 500).Select(at => (object)(at * 2 + 1)).ToArray();

            IDictionary<string, object> envelope = _fixture.Call(
                ModelReorderElements.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                    ComposedEditFixture.Given("indices", odd),
                    ComposedEditFixture.Given(
                        ModelReorderElements.MoveName, ModelReorderElements.Up)),
                10000);

            Assert.Equal(ToolEnvelope.ResponseTooLarge, ComposedEditFixture.Code(envelope));
            Assert.Equal("骨0", _fixture.Model.Bone[0].Name);
            Assert.Equal(0, _fixture.Commits);
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
            Assert.Equal(new[] { "1+2" }, Landed(value));
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void ManyNewElementsAnswerOneRangeInsteadOfEveryPosition()
        {
            Bones("一");

            IDictionary<string, object> value = ComposedEditFixture.Value(Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New),
                ComposedEditFixture.Given(ModelInsertElements.CountName, 30000)));

            Assert.Equal(30001, _fixture.Model.Bone.Count);
            Assert.Equal(new[] { "1+30000" }, Landed(value));
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
        public void MakingNewOnesRefusesTheScreenSelectionLikeTheOtherWaysOfPointing()
        {
            Bones("一", "二");
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 0 };

            IDictionary<string, object> answer = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New),
                ComposedEditFixture.Given("selected", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(answer));
            Assert.Contains("写す元を指さない", ComposedEditFixture.Message(answer));
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

        [Theory]
        [InlineData(ModelInsertElements.VertexMorph, MorphKind.Vertex)]
        [InlineData(ModelInsertElements.UvMorph, MorphKind.UV)]
        [InlineData(ModelInsertElements.BoneMorph, MorphKind.Bone)]
        [InlineData(ModelInsertElements.MaterialMorph, MorphKind.Material)]
        [InlineData(ModelInsertElements.GroupMorph, MorphKind.Group)]
        [InlineData(ModelInsertElements.FlipMorph, MorphKind.Flip)]
        [InlineData(ModelInsertElements.ImpulseMorph, MorphKind.Impulse)]
        public void AMorphIsMadeWithTheKindThatWasAskedFor(string variant, MorphKind kind)
        {
            Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Morph),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New),
                ComposedEditFixture.Given(ModelInsertElements.VariantName, variant));

            Assert.Equal(kind, Assert.Single(_fixture.Model.Morph).Kind);
        }

        [Fact]
        public void AMorphMadeWithoutSayingTheKindKeepsTheOneItWasBuiltWith()
        {
            Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Morph),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New));

            Assert.Single(_fixture.Model.Morph);
        }

        [Fact]
        public void TheMorphKindIsNotTakenForAnotherKindOfElement()
        {
            IDictionary<string, object> envelope = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New),
                ComposedEditFixture.Given(
                    ModelInsertElements.VariantName, ModelInsertElements.VertexMorph));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void TheMorphKindIsNotTakenWhenTheMorphIsCopied()
        {
            _fixture.Model.Morph.Add(new FakeMorph("笑い", MorphKind.Vertex));

            IDictionary<string, object> envelope = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Morph),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.Clone),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(
                    ModelInsertElements.VariantName, ModelInsertElements.VertexMorph));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AMorphKindTheToolDoesNotKnowIsRefused()
        {
            IDictionary<string, object> envelope = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Morph),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New),
                ComposedEditFixture.Given(ModelInsertElements.VariantName, "いない種類"));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Empty(_fixture.Model.Morph);
        }

        [Fact]
        public void MakingAKindThatMustPointAtSomethingElseIsRefused()
        {
            _fixture.Model.Node.Add(new FakeNode("枠"));

            IDictionary<string, object> envelope = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.NodeItem),
                ComposedEditFixture.Given("parentIndices", new object[] { 0 }),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(
                ModelInsertElements.Clone, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void CopyingMakesTheKindThatMustPointAtSomethingElse()
        {
            FakeNode node = new FakeNode("枠");
            node.Items.Add(new FakeMorphNodeItem(new FakeMorph("笑い", MorphKind.Vertex)));
            _fixture.Model.Node.Add(node);

            Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.NodeItem),
                ComposedEditFixture.Given("parentIndices", new object[] { 2 }),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.Clone),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Equal(2, node.Items.Count);
            Assert.IsAssignableFrom<IPXMorphNodeItem>(node.Items[1]);
        }

        [Fact]
        public void DeletingAFrameTheModelHoldsApartIsRefusedAndChangesNothing()
        {
            _fixture.Model.Node.Add(new FakeNode("枠"));

            IDictionary<string, object> envelope = Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Node),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.NotNull(ComposedEditFixture.Code(envelope));
            Assert.Single(_fixture.Model.Node);
            Assert.Equal(0, _fixture.Commits);
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
            Assert.Equal(new[] { "0+1", "2+1" }, Moved(value));
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
        public void InsertingIntoSeveralParentsPutsOneIntoEachOfThem()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            first.Faces.Add(Triangle());
            second.Faces.Add(Triangle());
            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);

            IDictionary<string, object> value = ComposedEditFixture.Value(Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.Clone),
                ComposedEditFixture.Given("indices", new object[] { 0 })));

            Assert.Equal(2, first.Faces.Count);
            Assert.Equal(2, second.Faces.Count);
            Assert.Equal(new[] { "1+1", "1+1" }, Landed(value));
        }

        [Fact]
        public void DeletingTheFacesTheScreenPicksTakesThemOutOfEachMaterial()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            IPXFace[] firsts = { Triangle(), Triangle(), Triangle() };
            IPXFace[] seconds = { Triangle(), Triangle() };
            foreach (IPXFace face in firsts)
            {
                first.Faces.Add(face);
            }

            foreach (IPXFace face in seconds)
            {
                second.Faces.Add(face);
            }

            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);
            // 画面は選んだ面を3つの頂点の位置の組で持つ。通し番号4と1の面を選ぶ。
            _fixture.View.Selected[ElementKinds.Face] = new[] { 12, 13, 14, 3, 4, 5 };

            ComposedEditFixture.Value(Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("selected", true)));

            Assert.Equal(new[] { firsts[0], firsts[2] }, first.Faces.ToArray());
            Assert.Equal(new[] { seconds[0] }, second.Faces.ToArray());
        }

        [Fact]
        public void CloningTheFacesTheScreenPicksReadsTheSelectionBeforeAnyMaterialGrows()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            IPXFace[] firsts = { Triangle(), Triangle() };
            IPXFace[] seconds = { Triangle(), Triangle() };
            foreach (IPXFace face in firsts)
            {
                first.Faces.Add(face);
            }

            foreach (IPXFace face in seconds)
            {
                second.Faces.Add(face);
            }

            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);
            // 通し番号0と3の面、つまり一の0番目と二の1番目を選ぶ。
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2, 9, 10, 11 };

            ComposedEditFixture.Value(Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.Clone),
                ComposedEditFixture.Given("selected", true)));

            Assert.Equal(3, first.Faces.Count);
            Assert.Equal(3, second.Faces.Count);
            Assert.Same(firsts[0].Vertex1, first.Faces[2].Vertex1);
            Assert.Same(seconds[1].Vertex1, second.Faces[2].Vertex1);
        }

        [Fact]
        public void TheScreenSelectionTogetherWithPositionsIsRefusedEvenWhenItLiesElsewhere()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            first.Faces.Add(Triangle());
            second.Faces.Add(Triangle());
            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            IDictionary<string, object> envelope = Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentIndices", new object[] { 1 }),
                ComposedEditFixture.Given("selected", true),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Single(second.Faces);
        }

        [Fact]
        public void TheFacesTheScreenPicksOutsideThePointedMaterialsAreKept()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            first.Faces.Add(Triangle());
            second.Faces.Add(Triangle());
            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            ComposedEditFixture.Value(Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentIndices", new object[] { 1 }),
                ComposedEditFixture.Given("selected", true)));

            Assert.Single(first.Faces);
            Assert.Single(second.Faces);
        }

        private IPXFace Triangle()
        {
            FakeVertex[] corners =
            {
                new FakeVertex(0f, 0f, 0f),
                new FakeVertex(1f, 0f, 0f),
                new FakeVertex(0f, 1f, 0f),
            };
            foreach (FakeVertex corner in corners)
            {
                _fixture.Model.Vertex.Add(corner);
            }

            return new FakeFace(corners[0], corners[1], corners[2]);
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

        private static string[] Landed(IDictionary<string, object> value)
        {
            return Runs(value[ModelInsertElements.RangesName]);
        }

        private static string[] Moved(IDictionary<string, object> value)
        {
            return Runs(value[ModelReorderElements.RangesName]);
        }

        private static string[] Runs(object runs)
        {
            return ((object[])runs)
                .Cast<IDictionary<string, object>>()
                .Select(range => range[TargetInput.StartName] + "+" + range[TargetInput.CountName])
                .ToArray();
        }

        private static IDictionary<string, object> Span(int start, int count)
        {
            return new Dictionary<string, object>
            {
                { TargetInput.StartName, start },
                { TargetInput.CountName, count },
            };
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
