using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelSurfaceDistancesToolsTests : IDisposable
    {
        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheVerticesOnEitherSideOfTheSurfaceAreCountedWithTheirSignedDistances()
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(0f, 0.5f, 0f);
            Vertex(0.2f, -0.3f, 0.1f);
            Vertex(5f, 0f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 4, 5, 6 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 })));

            Assert.Equal(3, value["count"]);
            Assert.Equal(2, value["frontCount"]);
            Assert.Equal(1, value["backCount"]);
            Assert.Equal(-0.3f, (float)value["minDistance"], 5);
            Assert.Equal(5, value["minVertex"]);
            AssertPoint(0.2f, 0f, 0.1f, value["minPoint"]);
            Assert.Equal(4f, (float)value["maxDistance"], 5);
            Assert.Equal(6, value["maxVertex"]);
            AssertPoint(1f, 0f, 0f, value["maxPoint"]);
        }

        [Fact]
        public void TheFrontOfTheSurfaceFollowsTheNormalsOfItsVerticesRatherThanTheWinding()
        {
            Floor(new V3(0f, -1f, 0f));
            Vertex(0f, 0.5f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 4 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 })));

            Assert.Equal(0, value["frontCount"]);
            Assert.Equal(1, value["backCount"]);
            Assert.Equal(-0.5f, (float)value["minDistance"], 5);
        }

        [Fact]
        public void FacesWithoutAreaAreNotMeasuredAgainst()
        {
            Floor(new V3(0f, 1f, 0f));
            IPXVertex point = Vertex(0f, -0.25f, 0f);
            point.Normal = new V3(0f, 0f, 0f);
            ((FakeMaterial)_fixture.Model.Material[0]).Faces.Add(new FakeFace(point, point, point));
            Vertex(0f, -0.3f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 5 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 })));

            Assert.Equal(1, value["backCount"]);
            Assert.Equal(-0.3f, (float)value["minDistance"], 5);
        }

        [Fact]
        public void ASurfaceOfFacesWithoutAreaIsRefused()
        {
            IPXVertex point = Vertex(0f, 0f, 0f);
            FakeMaterial flat = new FakeMaterial("点");
            flat.Faces.Add(new FakeFace(point, point, point));
            _fixture.Model.Material.Add(flat);

            IDictionary<string, object> envelope = Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }));

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void VerticesFartherThanTheLimitAreCountedApart()
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(0f, 0.5f, 0f);
            Vertex(0f, 3f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 4, 5 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("distanceLimit", 1.0)));

            Assert.Equal(1, value["count"]);
            Assert.Equal(1, value["farCount"]);
            Assert.Equal(0.5f, (float)value["maxDistance"], 5);
        }

        [Fact]
        public void EachBoxCountsTheVerticesInsideIt()
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(-0.5f, 0.2f, 0f);
            Vertex(0.5f, -0.1f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 4, 5 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("boxes", new object[] { Box("minX", 0.0), Box("maxX", -2.0) })));

            object[] boxes = (object[])value["boxes"];
            IDictionary<string, object> first = (IDictionary<string, object>)boxes[0];
            Assert.Equal(1, first["count"]);
            Assert.Equal(1, first["backCount"]);
            Assert.Equal(5, first["minVertex"]);
            IDictionary<string, object> second = (IDictionary<string, object>)boxes[1];
            Assert.Equal(0, second["count"]);
            Assert.False(second.ContainsKey("minDistance"));
        }

        [Fact]
        public void TheVerticesOfMaterialsCanBeMeasuredWithoutChangingTheModelOrTheSelection()
        {
            Floor(new V3(0f, 1f, 0f));
            IPXVertex first = Vertex(0f, 1f, 0f);
            IPXVertex second = Vertex(1f, 2f, 0f);
            IPXVertex third = Vertex(0f, 2f, 1f);
            FakeMaterial foot = new FakeMaterial("足");
            foot.Faces.Add(new FakeFace(first, second, third));
            _fixture.Model.Material.Add(foot);
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0 };

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("materialIndices", new object[] { 1 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 })));

            Assert.Equal(3, value["count"]);
            Assert.Equal(1f, (float)value["minDistance"], 5);
            Assert.Equal(4, value["minVertex"]);
            Assert.Equal(new[] { 0 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(0, _fixture.Commits);
        }

        [Fact]
        public void LeavingOutTheSurfaceIsRefused()
        {
            Floor(new V3(0f, 1f, 0f));

            IDictionary<string, object> envelope = Find(ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void ASurfaceOutsideTheMaterialsIsRefused()
        {
            Floor(new V3(0f, 1f, 0f));

            IDictionary<string, object> envelope = Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 3 }));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void ASurfaceWithoutFacesIsRefused()
        {
            Vertex(0f, 0f, 0f);
            _fixture.Model.Material.Add(new FakeMaterial("空"));

            IDictionary<string, object> envelope = Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }));

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedEditFixture.Code(envelope));
        }

        [Theory]
        [InlineData(-1.0)]
        [InlineData("a")]
        public void AMalformedLimitIsRefused(object limit)
        {
            Floor(new V3(0f, 1f, 0f));

            IDictionary<string, object> envelope = Find(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("distanceLimit", limit));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        private void Floor(V3 normal)
        {
            IPXVertex[] corners =
            {
                Vertex(-1f, 0f, -1f),
                Vertex(1f, 0f, -1f),
                Vertex(1f, 0f, 1f),
                Vertex(-1f, 0f, 1f),
            };
            foreach (IPXVertex corner in corners)
            {
                corner.Normal = normal;
            }

            FakeMaterial floor = new FakeMaterial("床");
            floor.Faces.Add(new FakeFace(corners[0], corners[1], corners[2]));
            floor.Faces.Add(new FakeFace(corners[0], corners[2], corners[3]));
            _fixture.Model.Material.Add(floor);
        }

        private IDictionary<string, object> Find(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelFindSurfaceDistances.ToolName, ComposedEditFixture.Arguments(given));
        }

        private static void AssertPoint(float x, float y, float z, object given)
        {
            object[] point = (object[])given;
            Assert.Equal(x, (float)point[0], 5);
            Assert.Equal(y, (float)point[1], 5);
            Assert.Equal(z, (float)point[2], 5);
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
