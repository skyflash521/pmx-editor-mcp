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

        private IDictionary<string, object> Find(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelFindVertexBounds.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IPXVertex Vertex(float x, float y, float z)
        {
            FakeVertex made = new FakeVertex(x, y, z);
            _fixture.Model.Vertex.Add(made);

            return made;
        }
    }
}
