using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 並びを動かす・要素を入れる・要素を消すの3つ。どれも1回の呼び出しで、1回のまとめての
    /// 反映に収まる。
    /// </summary>
    [Collection(TimedCollection.Name)]
    public sealed class ComposedElementToolsTests : IDisposable
    {
        private const int ManyElements = 100000;

        private const int ManyFaces = 10000;

        private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(2);

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
        public void APositionToMoveToIsRefusedFromTheListLengthOnAndBelowZero()
        {
            Bones("一", "二");

            foreach (object to in new object[] { 2, -1 })
            {
                IDictionary<string, object> envelope = Reorder(
                    ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                    ComposedEditFixture.Given("indices", new object[] { 0 }),
                    ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.To),
                    ComposedEditFixture.Given(ModelReorderElements.ToIndexName, to));

                Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            }

            ComposedEditFixture.Value(Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.To),
                ComposedEditFixture.Given(ModelReorderElements.ToIndexName, 1)));
            Assert.Equal(new[] { "二", "一" }, Names());
        }

        [Fact]
        public void APositionToMoveToThatIsNotAnIntegerIsRefusedAsInvalid()
        {
            Bones("一", "二");

            foreach (object to in new object[] { "a", 0.5 })
            {
                IDictionary<string, object> envelope = Reorder(
                    ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                    ComposedEditFixture.Given("indices", new object[] { 0 }),
                    ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.To),
                    ComposedEditFixture.Given(ModelReorderElements.ToIndexName, to));

                Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            }
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
            FakeVertex one = new FakeVertex();
            FakeVertex two = new FakeVertex(1f, 0f, 0f);
            _fixture.Model.Vertex.Add(one);
            _fixture.Model.Vertex.Add(two);
            FakeMaterial material = new FakeMaterial("材質");
            Now(material).Faces.Add(new FakeFace(one, one, one));
            Now(material).Faces.Add(new FakeFace(two, two, two));
            _fixture.Model.Material.Add(material);

            Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentIndices", new object[] { 0 }),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.Up));

            Assert.Equal(
                new IPXVertex[] { Now(two), Now(one) },
                _fixture.Model.Material[0].Faces.Select(face => face.Vertex1).ToArray());
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
            original.Position = new PEPlugin.SDX.V3(1f, 2f, 3f);
            original.Parent = _fixture.Model.Bone[1];

            Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.Clone),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelInsertElements.AtName, 2));

            Assert.Equal(new[] { "一", "二", "一" }, Names());
            foreach (int at in new[] { 0, 2 })
            {
                IPXBone bone = _fixture.Model.Bone[at];
                Assert.Equal(new[] { 1f, 2f, 3f }, new[] { bone.Position.X, bone.Position.Y, bone.Position.Z });
                Assert.Same(_fixture.Model.Bone[1], bone.Parent);
            }
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
            _fixture.Builder.MadeMorphKind = MorphKind.Bone;

            Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Morph),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New));

            Assert.Equal(MorphKind.Bone, Assert.Single(_fixture.Model.Morph).Kind);
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
            FakeMorph morph = new FakeMorph("笑い", MorphKind.Vertex);
            _fixture.Model.Morph.Add(morph);
            FakeNode node = new FakeNode("枠");
            Now(node).Items.Add(new FakeMorphNodeItem(morph));
            _fixture.Model.Node.Add(node);

            Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.NodeItem),
                ComposedEditFixture.Given("parentIndices", new object[] { 2 }),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.Clone),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Equal(2, Now(node).Items.Count);
            Assert.Same(Now(morph), ((IPXMorphNodeItem)Now(node).Items[1]).Morph);
        }

        [Fact]
        public void TheFramesAreNumberedAsTheEditorListsThemWithTheRootFirstAndTheExpressionSecond()
        {
            FakeBone bone = new FakeBone("センター");
            FakeMorph morph = new FakeMorph("笑い", MorphKind.Vertex);
            _fixture.Model.Bone.Add(bone);
            _fixture.Model.Morph.Add(morph);
            _fixture.Model.RootNode.Items.Add(new FakeBoneNodeItem(bone));
            _fixture.Model.ExpressionNode.Items.Add(new FakeMorphNodeItem(morph));

            IDictionary<string, object> envelope = Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.NodeItem),
                ComposedEditFixture.Given("parentIndices", new object[] { 1 }),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.Clone),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Single(_fixture.Model.RootNode.Items);
            Assert.Equal(2, _fixture.Model.ExpressionNode.Items.Count);
            Assert.Same(Now(morph), ((IPXMorphNodeItem)_fixture.Model.ExpressionNode.Items[1]).Morph);
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
            Assert.Same(Now(root), Now(vertex).Bone1);
            Assert.Equal(1, value[ModelDeleteElements.RemovedName]);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void KeepingTheRelatedOnesLeavesThePointersWithoutATarget()
        {
            FakeBone root = new FakeBone("親");
            FakeBone going = new FakeBone("消す") { Parent = root };
            FakeBone child = new FakeBone("子") { Parent = going };
            _fixture.Model.Bone.Add(root);
            _fixture.Model.Bone.Add(going);
            _fixture.Model.Bone.Add(child);

            Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ReferenceCleanup.RelatedName, ReferenceCleanup.Keep));

            Assert.Equal(new[] { "親", "子" }, Names());
            Assert.Null(_fixture.Model.Bone[1].Parent);
        }

        [Fact]
        public void RepairingTheRelatedOnesPointsThemAtTheParentOfTheRemovedOne()
        {
            FakeBone root = new FakeBone("親");
            FakeBone going = new FakeBone("消す") { Parent = root };
            FakeBone child = new FakeBone("子") { Parent = going };
            _fixture.Model.Bone.Add(root);
            _fixture.Model.Bone.Add(going);
            _fixture.Model.Bone.Add(child);

            Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            Assert.Equal(new[] { "親", "子" }, Names());
            Assert.Same(_fixture.Model.Bone[0], _fixture.Model.Bone[1].Parent);
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
            FakeVertex[] vertices = Enumerable.Range(0, 4)
                .Select(at => new FakeVertex(at, 0f, 0f))
                .ToArray();
            foreach (FakeVertex vertex in vertices)
            {
                _fixture.Model.Vertex.Add(vertex);
            }

            FakeMaterial material = new FakeMaterial("材質");
            Now(material).Faces.Add(new FakeFace(vertices[0], vertices[1], vertices[2]));
            Now(material).Faces.Add(new FakeFace(vertices[0], vertices[2], vertices[3]));
            _fixture.Model.Material.Add(material);
            FakeMaterial other = new FakeMaterial("別");
            Now(other).Faces.Add(new FakeFace(vertices[0], vertices[1], vertices[2]));
            _fixture.Model.Material.Add(other);

            Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentIndices", new object[] { 0 }),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Equal(new[] { new[] { 0, 2, 3 } }, Places(material));
            Assert.Equal(new[] { new[] { 0, 1, 2 } }, Places(other));
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
        public void AnInsertPositionIsRefusedBeyondTheEndOfTheListAndAcceptedAtTheEnd()
        {
            Bones("一");

            foreach (object at in new object[] { 2, 100, -1 })
            {
                IDictionary<string, object> refused = Insert(
                    ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                    ComposedEditFixture.Given(
                        ModelInsertElements.OperationName, ModelInsertElements.New),
                    ComposedEditFixture.Given(ModelInsertElements.AtName, at));

                Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(refused));
            }

            ComposedEditFixture.Value(Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.New),
                ComposedEditFixture.Given(ModelInsertElements.AtName, 1)));
            Assert.Equal(2, _fixture.Model.Bone.Count);
        }

        [Fact]
        public void AnInsertPositionThatIsNotAnIntegerIsRefusedAsInvalid()
        {
            Bones("一");

            foreach (object at in new object[] { "a", 0.5, true })
            {
                IDictionary<string, object> envelope = Insert(
                    ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                    ComposedEditFixture.Given(
                        ModelInsertElements.OperationName, ModelInsertElements.New),
                    ComposedEditFixture.Given(ModelInsertElements.AtName, at));

                Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            }
        }

        [Fact]
        public void InsertingIntoSeveralParentsPutsOneIntoEachOfThem()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            Now(first).Faces.Add(Triangle());
            Now(second).Faces.Add(Triangle());
            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);

            IDictionary<string, object> value = ComposedEditFixture.Value(Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.Clone),
                ComposedEditFixture.Given("indices", new object[] { 0 })));

            Assert.Equal(2, Now(first).Faces.Count);
            Assert.Equal(2, Now(second).Faces.Count);
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
                Now(first).Faces.Add(face);
            }

            foreach (IPXFace face in seconds)
            {
                Now(second).Faces.Add(face);
            }

            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);
            // 画面は選んだ面を3つの頂点の位置の組で持つ。通し番号4と1の面を選ぶ。
            _fixture.View.Selected[ElementKinds.Face] = new[] { 12, 13, 14, 3, 4, 5 };
            IPXVertex[] kept = { firsts[0].Vertex1, firsts[2].Vertex1, seconds[0].Vertex1 };

            ComposedEditFixture.Value(Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("selected", true)));

            Assert.Equal(
                new[] { Now(kept[0]), Now(kept[1]) },
                Now(first).Faces.Select(face => face.Vertex1).ToArray());
            Assert.Equal(
                new[] { Now(kept[2]) },
                Now(second).Faces.Select(face => face.Vertex1).ToArray());
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
                Now(first).Faces.Add(face);
            }

            foreach (IPXFace face in seconds)
            {
                Now(second).Faces.Add(face);
            }

            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);
            // 通し番号0と3の面、つまり一の0番目と二の1番目を選ぶ。
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2, 9, 10, 11 };
            IPXVertex[] copied = { firsts[0].Vertex1, seconds[1].Vertex1 };

            ComposedEditFixture.Value(Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.Clone),
                ComposedEditFixture.Given("selected", true)));

            Assert.Equal(3, Now(first).Faces.Count);
            Assert.Equal(3, Now(second).Faces.Count);
            Assert.Same(Now(copied[0]), Now(first).Faces[2].Vertex1);
            Assert.Same(Now(copied[1]), Now(second).Faces[2].Vertex1);
        }

        [Fact]
        public void TheScreenSelectionTogetherWithPositionsIsRefusedEvenWhenItLiesElsewhere()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            Now(first).Faces.Add(Triangle());
            Now(second).Faces.Add(Triangle());
            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            IDictionary<string, object> envelope = Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentIndices", new object[] { 1 }),
                ComposedEditFixture.Given("selected", true),
                ComposedEditFixture.Given("indices", new object[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Single(Now(second).Faces);
        }

        [Fact]
        public void TheFacesTheScreenPicksOutsideThePointedMaterialsAreKept()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            Now(first).Faces.Add(Triangle());
            Now(second).Faces.Add(Triangle());
            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            ComposedEditFixture.Value(Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentIndices", new object[] { 1 }),
                ComposedEditFixture.Given("selected", true)));

            Assert.Single(Now(first).Faces);
            Assert.Single(Now(second).Faces);
        }

        [Fact]
        public void FacesAreDeletedByTheirNumberCountedOverTheWholeModel()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            Now(first).Faces.Add(Triangle());
            Now(first).Faces.Add(Triangle());
            Now(second).Faces.Add(Triangle());
            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);

            IDictionary<string, object> value = ComposedEditFixture.Value(Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("modelIndices", new object[] { 1, 2 })));

            Assert.Equal(2, value[ModelDeleteElements.RemovedName]);
            Assert.Single(Now(first).Faces);
            Assert.Empty(Now(second).Faces);
        }

        [Fact]
        public void FacesAreMovedByTheirNumberCountedOverTheWholeModel()
        {
            FakeVertex one = new FakeVertex();
            FakeVertex two = new FakeVertex(1f, 0f, 0f);
            FakeVertex three = new FakeVertex(2f, 0f, 0f);
            _fixture.Model.Vertex.Add(one);
            _fixture.Model.Vertex.Add(two);
            _fixture.Model.Vertex.Add(three);
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            Now(first).Faces.Add(new FakeFace(one, one, one));
            Now(second).Faces.Add(new FakeFace(two, two, two));
            Now(second).Faces.Add(new FakeFace(three, three, three));
            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);

            ComposedEditFixture.Value(Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("modelIndices", new object[] { 2 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.Up)));

            Assert.Equal(
                new IPXVertex[] { Now(three), Now(two) },
                _fixture.Model.Material[1].Faces.Select(face => face.Vertex1).ToArray());
        }

        [Fact]
        public void ACopyOfAFacePickedByItsNumberLandsInTheMaterialThatOwnsIt()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            Now(first).Faces.Add(Triangle());
            Now(second).Faces.Add(Triangle());
            Now(second).Faces.Add(Triangle());
            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);

            IDictionary<string, object> value = ComposedEditFixture.Value(Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.Clone),
                ComposedEditFixture.Given("modelIndices", new object[] { 2 })));

            Assert.Single(Now(first).Faces);
            Assert.Equal(3, Now(second).Faces.Count);
            Assert.Equal(new[] { "3+1" }, Landed(value));
        }

        [Fact]
        public void CopiesOfFacesPickedByNumberAnswerWhereTheyLandCountedOverTheWholeModel()
        {
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            Now(first).Faces.Add(Triangle());
            Now(second).Faces.Add(Triangle());
            Now(second).Faces.Add(Triangle());
            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);

            IDictionary<string, object> value = ComposedEditFixture.Value(Insert(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given(
                    ModelInsertElements.OperationName, ModelInsertElements.Clone),
                ComposedEditFixture.Given("modelIndices", new object[] { 0, 2 })));

            Assert.Equal(new[] { "1+1", "4+1" }, Landed(value));
        }

        [Fact]
        public void FacesMovedByNumberAnswerTheirNewPlaceCountedOverTheWholeModel()
        {
            FakeVertex one = new FakeVertex();
            FakeVertex two = new FakeVertex(1f, 0f, 0f);
            FakeVertex three = new FakeVertex(2f, 0f, 0f);
            _fixture.Model.Vertex.Add(one);
            _fixture.Model.Vertex.Add(two);
            _fixture.Model.Vertex.Add(three);
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            Now(first).Faces.Add(new FakeFace(one, one, one));
            Now(second).Faces.Add(new FakeFace(two, two, two));
            Now(second).Faces.Add(new FakeFace(three, three, three));
            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);

            IDictionary<string, object> value = ComposedEditFixture.Value(Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("modelIndices", new object[] { 2 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.Up)));

            Assert.Equal(new[] { "1+1" }, Moved(value));
        }

        [Fact]
        public void FacesMovedByNumberAcrossMaterialsAnswerOneRunWhereTheNumbersAdjoin()
        {
            FakeVertex one = new FakeVertex();
            FakeVertex two = new FakeVertex(1f, 0f, 0f);
            _fixture.Model.Vertex.Add(one);
            _fixture.Model.Vertex.Add(two);
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            Now(first).Faces.Add(new FakeFace(one, one, one));
            Now(second).Faces.Add(new FakeFace(two, two, two));
            Now(second).Faces.Add(new FakeFace(two, two, two));
            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);

            IDictionary<string, object> value = ComposedEditFixture.Value(Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("modelIndices", new object[] { 0, 1 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.Top)));

            Assert.Equal(new[] { "0+2" }, Moved(value));
        }

        [Fact]
        public void FacesMovedToAPositionByNumberIgnoreAMaterialHoldingNoneOfThem()
        {
            FakeVertex one = new FakeVertex();
            FakeVertex two = new FakeVertex(1f, 0f, 0f);
            FakeVertex three = new FakeVertex(2f, 0f, 0f);
            _fixture.Model.Vertex.Add(one);
            _fixture.Model.Vertex.Add(two);
            _fixture.Model.Vertex.Add(three);
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            Now(first).Faces.Add(new FakeFace(one, one, one));
            Now(second).Faces.Add(new FakeFace(two, two, two));
            Now(second).Faces.Add(new FakeFace(three, three, three));
            _fixture.Model.Material.Add(first);
            _fixture.Model.Material.Add(second);

            ComposedEditFixture.Value(Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("modelIndices", new object[] { 1 }),
                ComposedEditFixture.Given(ModelReorderElements.MoveName, ModelReorderElements.To),
                ComposedEditFixture.Given(ModelReorderElements.ToIndexName, 1)));

            Assert.Equal(
                new IPXVertex[] { Now(three), Now(two) },
                _fixture.Model.Material[1].Faces.Select(face => face.Vertex1).ToArray());
        }

        [Fact]
        public void NumbersCountedOverTheWholeModelAreRefusedForAKindThatIsNotAFace()
        {
            Bones("一");

            IDictionary<string, object> envelope = Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given("modelIndices", new object[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.DoesNotContain("知らない引数", ComposedEditFixture.Message(envelope));
            Assert.Single(_fixture.Model.Bone);
        }

        [Fact]
        public void NumbersCountedOverTheWholeModelAreRefusedTogetherWithAParent()
        {
            FakeMaterial first = new FakeMaterial("一");
            Now(first).Faces.Add(Triangle());
            _fixture.Model.Material.Add(first);

            IDictionary<string, object> envelope = Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("parentIndices", new object[] { 0 }),
                ComposedEditFixture.Given("modelIndices", new object[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.DoesNotContain("知らない引数", ComposedEditFixture.Message(envelope));
            Assert.Single(Now(first).Faces);
        }

        [Fact]
        public void ANumberIsRefusedInAModelWithNoMaterial()
        {
            IDictionary<string, object> envelope = Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("modelIndices", new object[] { 0 }));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void ANumberPastTheWholeModelIsRefusedAndDeletesNothing()
        {
            FakeMaterial first = new FakeMaterial("一");
            Now(first).Faces.Add(Triangle());
            _fixture.Model.Material.Add(first);

            IDictionary<string, object> envelope = Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given("modelIndices", new object[] { 1 }));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            Assert.Single(Now(first).Faces);
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

        [Fact]
        public void RepairingCountsTheElementsItDropsAsFollowingAndNotAsRepaired()
        {
            FakeVertex[] corners = Enumerable.Range(0, 4)
                .Select(at => new FakeVertex(at, 0f, 0f))
                .ToArray();
            foreach (FakeVertex vertex in corners)
            {
                _fixture.Model.Vertex.Add(vertex);
            }

            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(corners[0], corners[1], corners[2]));
            material.Faces.Add(new FakeFace(corners[1], corners[2], corners[3]));
            _fixture.Model.Material.Add(material);
            FakeMorph morph = new FakeMorph("頂点");
            morph.Offsets.Add(new FakeVertexMorphOffset(corners[0]));
            morph.Offsets.Add(new FakeVertexMorphOffset(corners[3]));
            _fixture.Model.Morph.Add(morph);

            IDictionary<string, object> value = ComposedEditFixture.Value(Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Vertex),
                ComposedEditFixture.Given("indices", new object[] { 0 })));

            Dictionary<object, object> following = ((object[])value[ModelDeleteElements.FollowingName])
                .Cast<IDictionary<string, object>>()
                .ToDictionary(
                    entry => entry[ModelDeleteElements.KindName],
                    entry => entry[ModelDeleteElements.RemovedName]);
            Assert.Equal(1, following[ElementKinds.Face]);
            Assert.Equal(1, following[ElementKinds.MorphOffset]);
            Assert.Equal(2, following.Count);
            Assert.Equal(0, value[ModelDeleteElements.RepairedName]);
            Assert.Single(_fixture.Model.Material[0].Faces);
        }

        [Fact]
        public void TheChildrenOfElementsTakenAlongAreCountedAsFollowing()
        {
            FakeVertex alone = new FakeVertex();
            _fixture.Model.Vertex.Add(alone);
            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(alone, alone, alone));
            _fixture.Model.Material.Add(material);
            FakeMorph morph = new FakeMorph("頂点");
            morph.Offsets.Add(new FakeVertexMorphOffset(alone));
            _fixture.Model.Morph.Add(morph);

            IDictionary<string, object> value = ComposedEditFixture.Value(Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Vertex),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ReferenceCleanup.RelatedName, ReferenceCleanup.Cascade)));

            Dictionary<object, object> following = ((object[])value[ModelDeleteElements.FollowingName])
                .Cast<IDictionary<string, object>>()
                .ToDictionary(
                    entry => entry[ModelDeleteElements.KindName],
                    entry => entry[ModelDeleteElements.RemovedName]);
            Assert.Equal(1, following[ElementKinds.Material]);
            Assert.Equal(1, following[ElementKinds.Face]);
            Assert.Equal(1, following[ElementKinds.Morph]);
            Assert.Equal(1, following[ElementKinds.MorphOffset]);
        }

        [Fact]
        public void DeletingEveryVertexOfALargeModelFinishesInTime()
        {
            for (int at = 0; at < ManyElements; at++)
            {
                _fixture.Model.Vertex.Add(new FakeVertex());
            }

            TimeSpan editor = _fixture.EditorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            IDictionary<string, object> value = ComposedEditFixture.Value(Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Vertex),
                ComposedEditFixture.Given("all", true)));
            elapsed.Stop();
            TimeSpan spent = elapsed.Elapsed - (_fixture.EditorTime - editor);

            Assert.Equal(ManyElements, value[ModelDeleteElements.RemovedName]);
            Assert.Empty(_fixture.Model.Vertex);
            Assert.True(spent < TimeLimit, "消すのに " + spent + " かかった");
        }

        [Fact]
        public void CascadingAMaterialWithManyFacesFinishesInTime()
        {
            FakeMaterial going = new FakeMaterial("消す");
            for (int at = 0; at < ManyFaces; at++)
            {
                FakeVertex[] corners = { new FakeVertex(), new FakeVertex(), new FakeVertex() };
                foreach (FakeVertex corner in corners)
                {
                    _fixture.Model.Vertex.Add(corner);
                }

                going.Faces.Add(new FakeFace(corners[0], corners[1], corners[2]));
            }

            _fixture.Model.Material.Add(going);

            TimeSpan editor = _fixture.EditorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            Delete(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Material),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ReferenceCleanup.RelatedName, ReferenceCleanup.Cascade));
            elapsed.Stop();
            TimeSpan spent = elapsed.Elapsed - (_fixture.EditorTime - editor);

            Assert.Empty(_fixture.Model.Vertex);
            Assert.True(spent < TimeLimit, "消すのに " + spent + " かかった");
        }

        [Fact]
        public void MovingEveryVertexOfALargeModelToTheBottomFinishesInTime()
        {
            for (int at = 0; at < ManyElements; at++)
            {
                _fixture.Model.Vertex.Add(new FakeVertex(at, 0f, 0f));
            }

            TimeSpan editor = _fixture.EditorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            Reorder(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Vertex),
                ComposedEditFixture.Given("range", Span(1, ManyElements - 1)),
                ComposedEditFixture.Given(
                    ModelReorderElements.MoveName, ModelReorderElements.Top));
            elapsed.Stop();
            TimeSpan spent = elapsed.Elapsed - (_fixture.EditorTime - editor);

            Assert.Equal(1f, _fixture.Model.Vertex[0].Position.X);
            Assert.True(spent < TimeLimit, "並べ替えるのに " + spent + " かかった");
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

        private int[][] Places(IPXMaterial held)
        {
            return Now(held).Faces
                .Select(face => new[] { face.Vertex1, face.Vertex2, face.Vertex3 }
                    .Select(_fixture.Model.Vertex.IndexOf)
                    .ToArray())
                .ToArray();
        }

        /// <summary>握った要素が並んでいた位置に、いまのモデルで並んでいる要素。</summary>
        private T Now<T>(T held)
            where T : class
        {
            return _fixture.Now(held);
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
