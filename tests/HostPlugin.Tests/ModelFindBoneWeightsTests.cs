using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelFindBoneWeightsTests : IDisposable
    {
        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void EachBoneCarriesTheCountSumAndGreatestOfItsWeightsOverThePointedVertices()
        {
            IPXBone spine = Bone("上半身");
            IPXBone neck = Bone("首");
            Vertex(Share(spine, 0.75f), Share(neck, 0.25f));
            Vertex(Share(spine, 0.5f), Share(neck, 0.5f));
            Vertex(Share(neck, 1f));

            IDictionary<string, object> value = Value(Find(
                ComposedEditFixture.Given("all", true)));

            Assert.Equal(3, value["count"]);
            IList<IDictionary<string, object>> bones = Rows(value);
            Assert.Equal(2, bones.Count);
            Assert.Equal(1, bones[0]["index"]);
            Assert.Equal("首", bones[0]["name"]);
            Assert.Equal(3, bones[0]["vertexCount"]);
            Assert.Equal(1.75f, (float)bones[0]["weightSum"], 5);
            Assert.Equal(1f, (float)bones[0]["weightMax"], 5);
            Assert.Equal(0, bones[1]["index"]);
            Assert.Equal(2, bones[1]["vertexCount"]);
            Assert.Equal(1.25f, (float)bones[1]["weightSum"], 5);
            Assert.Equal(0.75f, (float)bones[1]["weightMax"], 5);
        }

        [Fact]
        public void OnlyThePointedVerticesAreCounted()
        {
            IPXBone spine = Bone("上半身");
            IPXBone neck = Bone("首");
            Vertex(Share(spine, 1f));
            Vertex(Share(neck, 1f));
            Vertex(Share(neck, 1f));

            IDictionary<string, object> value = Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 0, 1 })));

            Assert.Equal(2, value["count"]);
            IList<IDictionary<string, object>> bones = Rows(value);
            Assert.Equal(2, bones.Count);
            Assert.All(bones, row => Assert.Equal(1, row["vertexCount"]));
        }

        [Fact]
        public void ASlotWithoutWeightIsNotCountedAndABoneInTwoSlotsCountsTheVertexOnce()
        {
            IPXBone spine = Bone("上半身");
            IPXBone neck = Bone("首");
            Vertex(Share(spine, 0.25f), Share(spine, 0.5f), Share(neck, 0f));

            IDictionary<string, object> value = Value(Find(
                ComposedEditFixture.Given("all", true)));

            IList<IDictionary<string, object>> bones = Rows(value);
            Assert.Single(bones);
            Assert.Equal(0, bones[0]["index"]);
            Assert.Equal(1, bones[0]["vertexCount"]);
            Assert.Equal(0.75f, (float)bones[0]["weightSum"], 5);
            Assert.Equal(0.75f, (float)bones[0]["weightMax"], 5);
        }

        [Fact]
        public void TheVerticesTheFacesOfTheMaterialsUseCanBePointedInstead()
        {
            IPXBone spine = Bone("上半身");
            IPXBone neck = Bone("首");
            IPXVertex first = Vertex(Share(spine, 1f));
            IPXVertex second = Vertex(Share(spine, 1f));
            IPXVertex third = Vertex(Share(spine, 1f));
            Vertex(Share(neck, 1f));
            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(first, second, third));
            _fixture.Model.Material.Add(material);

            IDictionary<string, object> value = Value(Find(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 })));

            Assert.Equal(3, value["count"]);
            IList<IDictionary<string, object>> bones = Rows(value);
            Assert.Single(bones);
            Assert.Equal(0, bones[0]["index"]);
            Assert.Equal(3, bones[0]["vertexCount"]);
        }

        [Fact]
        public void TheScreenSelectionIsReadWithoutChangingIt()
        {
            IPXBone spine = Bone("上半身");
            IPXBone neck = Bone("首");
            Vertex(Share(spine, 1f));
            Vertex(Share(neck, 1f));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 1 };

            IDictionary<string, object> value = Value(Find(
                ComposedEditFixture.Given("selected", true)));

            IList<IDictionary<string, object>> bones = Rows(value);
            Assert.Single(bones);
            Assert.Equal(1, bones[0]["index"]);
            Assert.Equal(new[] { 1 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(0, _fixture.Commits);
        }

        [Fact]
        public void BonesOfEqualWeightSumComeInTheOrderOfTheirPositions()
        {
            IPXBone first = Bone("a");
            IPXBone second = Bone("b");
            Vertex(Share(second, 0.5f), Share(first, 0.5f));

            IList<IDictionary<string, object>> bones = Rows(Value(Find(
                ComposedEditFixture.Given("all", true))));

            Assert.Equal(new object[] { 0, 1 }, bones.Select(row => row["index"]).ToArray());
        }

        [Fact]
        public void NoVerticesGiveNoBones()
        {
            Bone("上半身");
            Vertex();
            _fixture.Model.Material.Add(new FakeMaterial("空"));

            IDictionary<string, object> value = Value(Find(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 })));

            Assert.Equal(0, value["count"]);
            Assert.Empty(Rows(value));
            Assert.Equal(0, value["total"]);
        }

        [Fact]
        public void TheBonesAreCutByOffsetAndLimitWithTheTotalAndTheNextOffset()
        {
            IPXBone first = Bone("a");
            IPXBone second = Bone("b");
            IPXBone third = Bone("c");
            Vertex(Share(first, 0.5f), Share(second, 0.3f), Share(third, 0.2f));

            IDictionary<string, object> value = Value(Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("offset", 1),
                ComposedEditFixture.Given("limit", 1)));

            Assert.Equal(3, value["total"]);
            IList<IDictionary<string, object>> bones = Rows(value);
            Assert.Single(bones);
            Assert.Equal(1, bones[0]["index"]);
            Assert.Equal(2, value["nextOffset"]);
        }

        [Fact]
        public void PointingBothVerticesAndMaterialsIsRefused()
        {
            Vertex();
            _fixture.Model.Material.Add(new FakeMaterial("材質"));

            IDictionary<string, object> envelope = Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("materialIndices", new object[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void ALimitBelowOneIsRefused()
        {
            Vertex();

            IDictionary<string, object> envelope = Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("limit", 0));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        private static KeyValuePair<IPXBone, float> Share(IPXBone bone, float weight)
        {
            return new KeyValuePair<IPXBone, float>(bone, weight);
        }

        private static IDictionary<string, object> Value(IDictionary<string, object> envelope)
        {
            return ComposedEditFixture.Value(envelope);
        }

        private static IList<IDictionary<string, object>> Rows(IDictionary<string, object> value)
        {
            return ((object[])value["bones"]).Cast<IDictionary<string, object>>().ToList();
        }

        private IDictionary<string, object> Find(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelFindBoneWeights.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IPXBone Bone(string name)
        {
            FakeBone made = new FakeBone(name);
            _fixture.Model.Bone.Add(made);

            return made;
        }

        private IPXVertex Vertex(params KeyValuePair<IPXBone, float>[] shares)
        {
            FakeVertex made = new FakeVertex();
            VertexWeights.Write(made, shares);
            _fixture.Model.Vertex.Add(made);

            return made;
        }
    }
}
