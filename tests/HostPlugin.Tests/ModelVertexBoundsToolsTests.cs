using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelVertexBoundsToolsTests : IDisposable
    {
        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheBoundsOfThePointedVerticesComeBackWithTheirCountAndCentre()
        {
            Vertex(1f, 2f, 3f);
            Vertex(-1f, 4f, 0f);
            Vertex(100f, 100f, 100f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 0, 1 })));

            Assert.Equal(2, value["count"]);
            Assert.Equal(new object[] { -1f, 2f, 0f }, (object[])value["min"]);
            Assert.Equal(new object[] { 1f, 4f, 3f }, (object[])value["max"]);
            Assert.Equal(new object[] { 0f, 3f, 1.5f }, (object[])value["center"]);
        }

        [Fact]
        public void TheVerticesThatHoldTheLeastAndGreatestOfEachAxisComeBack()
        {
            Vertex(100f, 100f, 100f);
            Vertex(1f, 2f, 3f);
            Vertex(-1f, 4f, 0f);
            Vertex(1f, 2f, 5f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 3, 2, 1 })));

            Assert.Equal(new object[] { 2, 1, 2 }, (object[])value["minVertices"]);
            Assert.Equal(new object[] { 1, 2, 3 }, (object[])value["maxVertices"]);
        }

        [Fact]
        public void EachBoxCarriesTheVerticesThatHoldItsLeastAndGreatest()
        {
            Vertex(0f, 0f, 0f);
            Vertex(1f, 5f, 2f);
            Vertex(2f, 10f, 4f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("boxes", new object[] { Box("minY", 1.0) })));

            IDictionary<string, object> box = (IDictionary<string, object>)((object[])value["boxes"])[0];
            Assert.Equal(new object[] { 1, 1, 1 }, (object[])box["minVertices"]);
            Assert.Equal(new object[] { 2, 2, 2 }, (object[])box["maxVertices"]);
        }

        [Fact]
        public void TheVerticesTheFacesOfTheMaterialsUseCanBePointedInstead()
        {
            IPXVertex first = Vertex(1f, 0f, 0f);
            IPXVertex second = Vertex(0f, 5f, 0f);
            IPXVertex third = Vertex(0f, 0f, -2f);
            Vertex(50f, 50f, 50f);
            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(first, second, third));
            _fixture.Model.Material.Add(material);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 })));

            Assert.Equal(3, value["count"]);
            Assert.Equal(new object[] { 0f, 0f, -2f }, (object[])value["min"]);
            Assert.Equal(new object[] { 1f, 5f, 0f }, (object[])value["max"]);
        }

        [Fact]
        public void TheScreenSelectionIsReadWithoutChangingIt()
        {
            Vertex(1f, 1f, 1f);
            Vertex(3f, 3f, 3f);
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 1 };

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("selected", true)));

            Assert.Equal(1, value["count"]);
            Assert.Equal(new object[] { 3f, 3f, 3f }, (object[])value["min"]);
            Assert.Equal(new[] { 1 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(0, _fixture.Commits);
        }

        [Fact]
        public void MaterialsWithoutFacesCountNothingAndCarryNoBounds()
        {
            Vertex(1f, 1f, 1f);
            _fixture.Model.Material.Add(new FakeMaterial("空"));

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 })));

            Assert.Equal(0, value["count"]);
            Assert.False(value.ContainsKey("min"));
            Assert.False(value.ContainsKey("max"));
            Assert.False(value.ContainsKey("center"));
            Assert.False(value.ContainsKey("minVertices"));
            Assert.False(value.ContainsKey("maxVertices"));
        }

        [Fact]
        public void PointingBothVerticesAndMaterialsIsRefused()
        {
            Vertex(1f, 1f, 1f);
            _fixture.Model.Material.Add(new FakeMaterial("材質"));

            IDictionary<string, object> envelope = Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("materialIndices", new object[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void EachBoxNarrowsThePointedVerticesAndComesBackInTheSameOrder()
        {
            Vertex(0f, 0f, 0f);
            Vertex(1f, 5f, 2f);
            Vertex(2f, 10f, 4f);
            Vertex(3f, 15f, 6f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("boxes", new object[]
                {
                    Box("minY", 5.0, "maxY", 10.0),
                    Box("minZ", 4.0),
                    Box("maxX", -1.0),
                })));

            Assert.Equal(4, value["count"]);
            object[] boxes = (object[])value["boxes"];
            Assert.Equal(3, boxes.Length);
            IDictionary<string, object> first = (IDictionary<string, object>)boxes[0];
            Assert.Equal(2, first["count"]);
            Assert.Equal(new object[] { 1f, 5f, 2f }, (object[])first["min"]);
            Assert.Equal(new object[] { 2f, 10f, 4f }, (object[])first["max"]);
            Assert.Equal(new object[] { 1.5f, 7.5f, 3f }, (object[])first["center"]);
            IDictionary<string, object> second = (IDictionary<string, object>)boxes[1];
            Assert.Equal(2, second["count"]);
            Assert.Equal(new object[] { 2f, 10f, 4f }, (object[])second["min"]);
            IDictionary<string, object> third = (IDictionary<string, object>)boxes[2];
            Assert.Equal(0, third["count"]);
            Assert.False(third.ContainsKey("min"));
        }

        [Fact]
        public void BoxesNarrowTheVerticesOfTheMaterials()
        {
            IPXVertex first = Vertex(1f, 0f, 0f);
            IPXVertex second = Vertex(0f, 5f, 0f);
            IPXVertex third = Vertex(0f, 0f, -2f);
            Vertex(0f, 3f, 0f);
            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(first, second, third));
            _fixture.Model.Material.Add(material);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("boxes", new object[] { Box("minY", 1.0) })));

            IDictionary<string, object> box =
                (IDictionary<string, object>)((object[])value["boxes"])[0];
            Assert.Equal(1, box["count"]);
            Assert.Equal(new object[] { 0f, 5f, 0f }, (object[])box["min"]);
        }

        [Theory]
        [InlineData("minX", 2.0, "maxX", 1.0)]
        [InlineData("lowX", 0.0, "maxX", 1.0)]
        [InlineData("minX", "a", "maxX", 1.0)]
        public void MalformedBoxesAreRefused(string lowName, object low, string highName, object high)
        {
            Vertex(1f, 1f, 1f);

            IDictionary<string, object> envelope = Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("boxes", new object[] { Box(lowName, low, highName, high) }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void BoxesThatAreNotObjectsAreRefused()
        {
            Vertex(1f, 1f, 1f);

            IDictionary<string, object> envelope = Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("boxes", new object[] { 1.0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        private IDictionary<string, object> Find(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelFindVertexBounds.ToolName, ComposedEditFixture.Arguments(given));
        }

        private static IDictionary<string, object> Box(params object[] pairs)
        {
            Dictionary<string, object> box = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int at = 0; at < pairs.Length; at += 2)
            {
                box.Add((string)pairs[at], pairs[at + 1]);
            }

            return box;
        }

        private IPXVertex Vertex(float x, float y, float z)
        {
            FakeVertex made = new FakeVertex(x, y, z);
            _fixture.Model.Vertex.Add(made);

            return made;
        }
    }
}
