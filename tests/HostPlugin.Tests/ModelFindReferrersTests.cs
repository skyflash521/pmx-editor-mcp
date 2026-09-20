using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelFindReferrersTests : IDisposable
    {
        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheCountOfEachKindThatPointsAtTheGivenElementComesBack()
        {
            Rigged();

            IDictionary<string, object> found = Found(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.Indices, new object[] { 0 }));

            Assert.Equal(
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    { ElementKinds.Vertex, 1 },
                    { ElementKinds.Bone, 1 },
                    { ElementKinds.Morph, 1 },
                    { ElementKinds.Node, 1 },
                    { ElementKinds.Body, 1 },
                },
                Counted(found));
        }

        [Fact]
        public void AnElementThatNothingPointsAtComesBackWithEveryKindAtZero()
        {
            Rigged();

            IDictionary<string, object> found = Found(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.Indices, new object[] { 1 }));

            Assert.All(Counted(found).Values, count => Assert.Equal(0, count));
        }

        [Theory]
        [InlineData(ElementKinds.Vertex, ElementKinds.Face)]
        [InlineData(ElementKinds.Material, ElementKinds.Morph)]
        [InlineData(ElementKinds.Bone, ElementKinds.Vertex)]
        [InlineData(ElementKinds.Morph, ElementKinds.Morph)]
        [InlineData(ElementKinds.Body, ElementKinds.Joint)]
        public void EveryKindThatCanBePointedAtIsTakenAtTheEntrance(
            string kind, string referrerKind)
        {
            Wired();

            IDictionary<string, object> found = Found(
                ComposedEditFixture.Given(ElementKinds.KindName, kind),
                ComposedEditFixture.Given(TargetNames.Element.Indices, new object[] { 0 }));

            Assert.Equal(1, Counted(found)[referrerKind]);
        }

        [Fact]
        public void ARangePicksTheSameElementsAsTheIndicesThatSpanIt()
        {
            IList<IPXBone> bones = Bones(3);
            Weighted(bones[2]);

            IDictionary<string, object> found = Found(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(
                    TargetNames.Element.Range,
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { TargetInput.StartName, 2 },
                        { TargetInput.CountName, 1 },
                    }));

            Assert.Equal(1, Counted(found)[ElementKinds.Vertex]);
        }

        [Fact]
        public void OneReferrerThatPointsAtSeveralOfTheGivenElementsIsCountedOnce()
        {
            IList<IPXBone> bones = Bones(2);
            IPXVertex vertex = Weighted(bones[0]);
            vertex.Weight1 = 0.5f;
            vertex.Bone2 = bones[1];
            vertex.Weight2 = 0.5f;

            IDictionary<string, object> found = Found(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.All, true));

            Assert.Equal(1, Counted(found)[ElementKinds.Vertex]);
        }

        [Fact]
        public void EachGivenElementGetsItsOwnCountWithTheTotalBeside()
        {
            IList<IPXBone> bones = Bones(3);
            Weighted(bones[2]);

            IDictionary<string, object> found = Found(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.All, true),
                ComposedEditFixture.Given(
                    "detail", "countPerTarget"));

            Assert.Equal(3, found["total"]);
            IList<IDictionary<string, object>> items = Items(found);
            Assert.Equal(
                new[] { 0, 1, 2 },
                items.Select(item => (int)item["index"]).ToArray());
            Assert.Equal(0, Counted(items[0])[ElementKinds.Vertex]);
            Assert.Equal(1, Counted(items[2])[ElementKinds.Vertex]);
            Assert.False(found.ContainsKey("nextOffset"));
        }

        [Fact]
        public void TheRestOfTheElementsComeBackFromTheOffsetThatWasHandedOver()
        {
            Bones(3);

            IDictionary<string, object> found = Found(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.All, true),
                ComposedEditFixture.Given(
                    "detail", "countPerTarget"),
                ComposedEditFixture.Given("limit", 2));

            Assert.Equal(3, found["total"]);
            Assert.Equal(2, Items(found).Count);
            Assert.Equal(2, found["nextOffset"]);

            IDictionary<string, object> rest = Found(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.All, true),
                ComposedEditFixture.Given(
                    "detail", "countPerTarget"),
                ComposedEditFixture.Given("offset", 2));

            Assert.Equal(
                new[] { 2 },
                Items(rest).Select(item => (int)item["index"]).ToArray());
            Assert.False(rest.ContainsKey("nextOffset"));
        }

        [Fact]
        public void TheIndicesOfTheNamedKindComeBack()
        {
            IList<IPXBone> bones = Bones(1);
            Weighted(bones[0]);
            Weighted(bones[0]);

            IDictionary<string, object> found = Found(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.Indices, new object[] { 0 }),
                ComposedEditFixture.Given(
                    "detail", "indices"),
                ComposedEditFixture.Given(
                    "referrerKind", ElementKinds.Vertex));

            Assert.Equal(2, found["total"]);
            Assert.Equal(new[] { 0, 1 }, Places(found));
            Assert.False(found.ContainsKey("nextOffset"));
        }

        [Fact]
        public void TheRestOfTheIndicesComeBackFromTheOffsetThatWasHandedOver()
        {
            IList<IPXBone> bones = Bones(1);
            Weighted(bones[0]);
            Weighted(bones[0]);
            Weighted(bones[0]);

            IDictionary<string, object> found = Found(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.Indices, new object[] { 0 }),
                ComposedEditFixture.Given(
                    "detail", "indices"),
                ComposedEditFixture.Given(
                    "referrerKind", ElementKinds.Vertex),
                ComposedEditFixture.Given("offset", 1),
                ComposedEditFixture.Given("limit", 1));

            Assert.Equal(3, found["total"]);
            Assert.Equal(new[] { 1 }, Places(found));
            Assert.Equal(2, found["nextOffset"]);
        }

        [Fact]
        public void ASlotWhoseWeightDoesNotPassTheThresholdIsNotCounted()
        {
            IList<IPXBone> bones = Bones(2);
            IPXVertex vertex = Weighted(bones[0]);
            vertex.Weight1 = 0.9f;
            vertex.Bone2 = bones[1];
            vertex.Weight2 = 0.1f;

            Assert.Equal(1, Weighed(0, 0.5));
            Assert.Equal(0, Weighed(1, 0.5));
        }

        [Fact]
        public void ASlotWhoseWeightIsExactlyTheThresholdIsNotCounted()
        {
            IList<IPXBone> bones = Bones(1);
            IPXVertex vertex = Weighted(bones[0]);
            vertex.Weight1 = 0.5f;

            Assert.Equal(0, Weighed(0, 0.5));
            Assert.Equal(1, Weighed(0, 0.25));
        }

        [Fact]
        public void AKindThatNothingCanPointAtIsRefusedWithTheOnesThatCan()
        {
            Bones(1);

            IDictionary<string, object> envelope = Call(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Face),
                ComposedEditFixture.Given(TargetNames.Element.All, true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(ElementKinds.Bone, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void NamingNoKindToListIsRefusedWhenTheIndicesAreAsked()
        {
            Bones(1);

            IDictionary<string, object> envelope = Call(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.All, true),
                ComposedEditFixture.Given(
                    "detail", "indices"));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(
                "referrerKind", ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void AKindThatCannotPointAtTheGivenKindIsRefusedWithTheOnesThatCan()
        {
            Bones(1);

            IDictionary<string, object> envelope = Call(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.All, true),
                ComposedEditFixture.Given(
                    "detail", "indices"),
                ComposedEditFixture.Given(
                    "referrerKind", ElementKinds.Joint));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(ElementKinds.Vertex, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void NamingAKindToListIsRefusedWhenTheIndicesAreNotAsked()
        {
            Bones(1);

            IDictionary<string, object> envelope = Call(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.All, true),
                ComposedEditFixture.Given(
                    "referrerKind", ElementKinds.Vertex));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains("indices", ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void AThresholdIsRefusedForAKindThatHoldsNoWeight()
        {
            _fixture.Model.Material.Add(new FakeMaterial("材質"));

            IDictionary<string, object> envelope = Call(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Material),
                ComposedEditFixture.Given(TargetNames.Element.All, true),
                ComposedEditFixture.Given("minWeight", 0.5));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(ElementKinds.Bone, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void CuttingOutIsRefusedWhenOnlyTheWholeCountIsAsked()
        {
            Bones(1);

            IDictionary<string, object> envelope = Call(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.All, true),
                ComposedEditFixture.Given("offset", 1));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains("offset", ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void TheCountIsCutDownUntilTheValueFitsInTheRoomLeftForIt()
        {
            Bones(400);
            const int Budget = 10000;

            IDictionary<string, object> envelope = _fixture.Call(
                "model_find_referrers",
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                    ComposedEditFixture.Given(TargetNames.Element.All, true),
                    ComposedEditFixture.Given(
                        "detail", "countPerTarget")),
                Budget);
            IDictionary<string, object> found = ComposedEditFixture.Value(envelope);

            Assert.Equal(400, found["total"]);
            Assert.NotEmpty(Items(found));
            Assert.Equal(Items(found).Count, found["nextOffset"]);
            Assert.InRange(Size(found), 1, ResponseSize.ValueChars(Budget));
            Assert.True(Size(WithOneMore(found)) > ResponseSize.ValueChars(Budget));
            Assert.NotEmpty((IEnumerable<object>)envelope[ToolEnvelope.WarningsName]);
        }

        [Fact]
        public void EveryElementComesBackWhenTheRoomHoldsThemAndNoCountWasAsked()
        {
            Bones(400);

            IDictionary<string, object> found = Found(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.All, true),
                ComposedEditFixture.Given(
                    "detail", "countPerTarget"));

            Assert.Equal(400, Items(found).Count);
            Assert.False(found.ContainsKey("nextOffset"));
        }

        [Theory]
        [InlineData("offset", -1)]
        [InlineData("limit", 0)]
        public void ACutOutsideTheRangeTheListingContractAllowsIsRefused(string name, int given)
        {
            Bones(1);

            IDictionary<string, object> envelope = Call(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.All, true),
                ComposedEditFixture.Given(
                    "detail", "countPerTarget"),
                ComposedEditFixture.Given(name, given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void LookingDoesNotChangeTheModel()
        {
            Rigged();

            Found(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.All, true));

            Assert.Equal(0, _fixture.Commits);
            Assert.Equal(2, _fixture.Model.Bone.Count);
            Assert.Single(_fixture.Model.Vertex);
        }

        private int Weighed(int at, double minWeight)
        {
            return Counted(Found(
                ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone),
                ComposedEditFixture.Given(TargetNames.Element.Indices, new object[] { at }),
                ComposedEditFixture.Given("minWeight", minWeight)))[
                ElementKinds.Vertex];
        }

        private IDictionary<string, object> Found(params KeyValuePair<string, object>[] given)
        {
            return ComposedEditFixture.Value(Call(given));
        }

        private IDictionary<string, object> Call(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                "model_find_referrers", ComposedEditFixture.Arguments(given));
        }

        private static IDictionary<string, int> Counted(IDictionary<string, object> held)
        {
            return ((IEnumerable<object>)held["referrers"])
                .Cast<IDictionary<string, object>>()
                .ToDictionary(
                    row => (string)row["kind"],
                    row => (int)row["count"],
                    StringComparer.Ordinal);
        }

        private static IList<IDictionary<string, object>> Items(IDictionary<string, object> held)
        {
            return ((IEnumerable<object>)held["items"])
                .Cast<IDictionary<string, object>>()
                .ToList();
        }

        private static int[] Places(IDictionary<string, object> held)
        {
            return ((IEnumerable<object>)held["referrerIndices"])
                .Select(Convert.ToInt32)
                .ToArray();
        }

        private static int Size(IDictionary<string, object> held)
        {
            return new System.Web.Script.Serialization.JavaScriptSerializer()
                .Serialize(held)
                .Length;
        }

        private static IDictionary<string, object> WithOneMore(IDictionary<string, object> held)
        {
            IList<IDictionary<string, object>> items = Items(held);

            return new Dictionary<string, object>(held, StringComparer.Ordinal)
            {
                ["items"] = items.Concat(new[] { items.Last() }).ToArray(),
            };
        }

        private void Rigged()
        {
            IList<IPXBone> bones = Bones(2);
            Weighted(bones[0]);
            bones[1].Parent = bones[0];
            FakeMorph morph = new FakeMorph("モーフ", MorphKind.Bone);
            morph.Offsets.Add(new FakeBoneMorphOffset(bones[0]));
            _fixture.Model.Morph.Add(morph);
            FakeNode node = new FakeNode("枠");
            node.Items.Add(new FakeBoneNodeItem(bones[0]));
            _fixture.Model.Node.Add(node);
            FakeBody body = new FakeBody("剛体");
            body.Bone = bones[0];
            _fixture.Model.Body.Add(body);
        }

        private void Wired()
        {
            IList<IPXBone> bones = Bones(1);
            IPXVertex vertex = Weighted(bones[0]);
            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(vertex, vertex, vertex));
            _fixture.Model.Material.Add(material);
            FakeMorph painted = new FakeMorph("材質モーフ", MorphKind.Material);
            painted.Offsets.Add(new FakeMaterialMorphOffset(material));
            _fixture.Model.Morph.Add(painted);
            FakeMorph grouped = new FakeMorph("グループ", MorphKind.Group);
            grouped.Offsets.Add(new FakeGroupMorphOffset(painted));
            _fixture.Model.Morph.Add(grouped);
            FakeBody body = new FakeBody("剛体");
            _fixture.Model.Body.Add(body);
            FakeJoint joint = new FakeJoint("Joint");
            joint.BodyA = body;
            _fixture.Model.Joint.Add(joint);
        }

        private IList<IPXBone> Bones(int count)
        {
            List<IPXBone> made = new List<IPXBone>();
            for (int at = 0; at < count; at++)
            {
                IPXBone bone = new FakeBone("ボーン" + at);
                _fixture.Model.Bone.Add(bone);
                made.Add(bone);
            }

            return made;
        }

        private IPXVertex Weighted(IPXBone bone)
        {
            IPXVertex vertex = new FakeVertex(0f, 0f, 0f);
            vertex.Bone1 = bone;
            vertex.Weight1 = 1f;
            _fixture.Model.Vertex.Add(vertex);

            return vertex;
        }
    }
}
