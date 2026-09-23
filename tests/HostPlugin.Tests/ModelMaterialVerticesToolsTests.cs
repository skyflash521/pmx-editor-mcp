using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelMaterialVerticesToolsTests : IDisposable
    {
        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheVerticesTheFacesOfTheMaterialUseComeBackOnceInAscendingOrder()
        {
            IList<IPXVertex> vertices = Vertices(5);
            Material(Face(vertices, 4, 2, 0), Face(vertices, 0, 2, 3));
            Material(Face(vertices, 1, 1, 1));

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 0 })));

            Assert.Equal(4, value["total"]);
            Assert.Equal(new object[] { 0, 2, 3, 4 }, (object[])value["vertexIndices"]);
            Assert.False(value.ContainsKey("nextOffset"));
        }

        [Fact]
        public void TheMaterialsPickedOnTheScreenAreReadWithoutChangingTheSelection()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Material(Face(vertices, 0, 1, 2));
            Material(Face(vertices, 1, 2, 3));
            _fixture.Form.SelectedMaterials = new[] { 1 };
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0 };

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("selected", true)));

            Assert.Equal(new object[] { 1, 2, 3 }, (object[])value["vertexIndices"]);
            Assert.Equal(new[] { 1 }, _fixture.Form.SelectedMaterials);
            Assert.Equal(new[] { 0 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(0, _fixture.Commits);
        }

        [Fact]
        public void TheListIsReadInPartsFromTheOffset()
        {
            IList<IPXVertex> vertices = Vertices(6);
            Material(Face(vertices, 0, 1, 2), Face(vertices, 3, 4, 5));

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("offset", 2),
                ComposedEditFixture.Given("limit", 3)));

            Assert.Equal(6, value["total"]);
            Assert.Equal(new object[] { 2, 3, 4 }, (object[])value["vertexIndices"]);
            Assert.Equal(5, value["nextOffset"]);
        }

        [Fact]
        public void RunsGatherTheConsecutivePositionsIntoStartAndCount()
        {
            IList<IPXVertex> vertices = Vertices(5);
            Material(Face(vertices, 4, 2, 0), Face(vertices, 0, 2, 3));

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given("runs", true)));

            Assert.Equal(2, value["total"]);
            Assert.False(value.ContainsKey("vertexIndices"));
            object[] runs = (object[])value["vertexRuns"];
            Assert.Equal(2, runs.Length);
            Assert.Equal(0, ((IDictionary<string, object>)runs[0])["start"]);
            Assert.Equal(1, ((IDictionary<string, object>)runs[0])["count"]);
            Assert.Equal(2, ((IDictionary<string, object>)runs[1])["start"]);
            Assert.Equal(3, ((IDictionary<string, object>)runs[1])["count"]);
        }

        [Fact]
        public void RunsAreReadInPartsFromTheOffset()
        {
            IList<IPXVertex> vertices = Vertices(6);
            Material(Face(vertices, 0, 2, 4));

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("runs", true),
                ComposedEditFixture.Given("offset", 1),
                ComposedEditFixture.Given("limit", 1)));

            Assert.Equal(3, value["total"]);
            Assert.Equal(2, ((IDictionary<string, object>)((object[])value["vertexRuns"])[0])["start"]);
            Assert.Equal(2, value["nextOffset"]);
        }

        [Fact]
        public void ALimitOfZeroIsRefused()
        {
            IList<IPXVertex> vertices = Vertices(3);
            Material(Face(vertices, 0, 1, 2));

            IDictionary<string, object> envelope = Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("limit", 0));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        private IDictionary<string, object> Find(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelFindMaterialVertices.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IList<IPXVertex> Vertices(int count)
        {
            List<IPXVertex> made = new List<IPXVertex>();
            for (int at = 0; at < count; at++)
            {
                FakeVertex vertex = new FakeVertex(at, 0f, 0f);
                _fixture.Model.Vertex.Add(vertex);
                made.Add(vertex);
            }

            return made;
        }

        private void Material(params IPXFace[] faces)
        {
            FakeMaterial material = new FakeMaterial("材質" + _fixture.Model.Material.Count);
            foreach (IPXFace face in faces)
            {
                material.Faces.Add(face);
            }

            _fixture.Model.Material.Add(material);
        }

        private static IPXFace Face(IList<IPXVertex> vertices, int first, int second, int third)
        {
            return new FakeFace(vertices[first], vertices[second], vertices[third]);
        }
    }
}
