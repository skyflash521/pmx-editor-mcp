using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelFindExposedVerticesToolsTests : IDisposable
    {
        private const string ToolName = "model_find_exposed_vertices";

        private const int FirstSkin = 8;

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        private FakePmx _held;

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void VerticesInsideTheSurfaceInTheCopyAndOutsideItNowAreCountedWithTheirDistanceToIt()
        {
            int handle = Mixed();

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(handle, Skin()));

            Assert.Equal(3, value["count"]);
            Assert.Equal(2f, (float)value["maxDepth"], 5);
            Assert.Equal(9, value["maxVertex"]);
        }

        [Fact]
        public void TheVerticesAreListedFromTheFarthestFromTheSurface()
        {
            int handle = Mixed();

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(handle, Skin()));

            IDictionary<string, object>[] listed = Listed(value);
            Assert.Equal(new[] { 9, 10, 8 }, listed.Select(row => (int)row["vertex"]).ToArray());
            Assert.Equal(2f, (float)listed[0]["depth"], 5);
            Assert.Equal(0.75f, (float)listed[1]["depth"], 5);
            Assert.Equal(0.5f, (float)listed[2]["depth"], 5);
            Assert.False(value.ContainsKey("nextOffset"));
        }

        [Fact]
        public void OnlyTheVerticesGivenAreJudged()
        {
            int handle = Mixed();

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                handle, new object[] { 8, 11, 12, 13 }));

            Assert.Equal(1, value["count"]);
            Assert.Equal(0.5f, (float)value["maxDepth"], 5);
            Assert.Equal(8, value["maxVertex"]);
            Assert.Equal(new[] { 8 }, Listed(value).Select(row => (int)row["vertex"]).ToArray());
        }

        [Fact]
        public void VerticesThatStayInsideOrWereOutsideFromTheStartAreNotCounted()
        {
            int handle = Pair(
                new[] { P(0.3f, 0.3f, 0.3f), P(4f, 0f, 0f), P(3f, 0f, 0f) },
                new[] { P(0.4f, 0.3f, 0.3f), P(5f, 0f, 0f), P(0f, 0f, 0f) });

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                handle, new object[] { 8, 9, 10 }));

            Assert.Equal(0, value["count"]);
            Assert.False(value.ContainsKey("maxDepth"));
            Assert.False(value.ContainsKey("maxVertex"));
            Assert.Empty(Listed(value));
            Assert.False(value.ContainsKey("nextOffset"));
        }

        [Fact]
        public void ThePresentModelCanBePassedByItsHandle()
        {
            int handle = Mixed();
            KeyValuePair<string, object> held = _fixture.HoldModel();

            IDictionary<string, object> value = ComposedEditFixture.Value(_fixture.Call(
                ToolName,
                ComposedEditFixture.Arguments(
                    held,
                    ComposedEditFixture.Given("basePmxHandle", (long)handle),
                    ComposedEditFixture.Given("indices", Skin()),
                    ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }))));

            Assert.Equal(3, value["count"]);
            Assert.Equal(9, value["maxVertex"]);
        }

        [Fact]
        public void TheListIsReadFromTheOffsetAndSaysWhereToGoOn()
        {
            int handle = Pair(
                new[] { P(0f, 0f, 0f), P(0f, 0f, 0f), P(0f, 0f, 0f), P(0f, 0f, 0f), P(0f, 0f, 0f) },
                new[] { P(2f, 0f, 0f), P(3f, 0f, 0f), P(4f, 0f, 0f), P(5f, 0f, 0f), P(6f, 0f, 0f) });
            object[] all = new object[] { 8, 9, 10, 11, 12 };

            IDictionary<string, object> first = ComposedEditFixture.Value(Find(
                handle, all, ComposedEditFixture.Given("limit", 2)));
            IDictionary<string, object> middle = ComposedEditFixture.Value(Find(
                handle,
                all,
                ComposedEditFixture.Given("offset", 2),
                ComposedEditFixture.Given("limit", 2)));
            IDictionary<string, object> last = ComposedEditFixture.Value(Find(
                handle, all, ComposedEditFixture.Given("offset", 4)));

            Assert.Equal(5, first["count"]);
            Assert.Equal(new[] { 12, 11 }, Listed(first).Select(row => (int)row["vertex"]).ToArray());
            Assert.Equal(2, first["nextOffset"]);
            Assert.Equal(5, middle["count"]);
            Assert.Equal(new[] { 10, 9 }, Listed(middle).Select(row => (int)row["vertex"]).ToArray());
            Assert.Equal(4, middle["nextOffset"]);
            Assert.Equal(5, last["count"]);
            Assert.Equal(new[] { 8 }, Listed(last).Select(row => (int)row["vertex"]).ToArray());
            Assert.False(last.ContainsKey("nextOffset"));
        }

        [Fact]
        public void PagingDoesNotChangeTheCountOrTheFarthestVertex()
        {
            int handle = Mixed();

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                handle,
                Skin(),
                ComposedEditFixture.Given("offset", 2),
                ComposedEditFixture.Given("limit", 1)));

            Assert.Equal(3, value["count"]);
            Assert.Equal(2f, (float)value["maxDepth"], 5);
            Assert.Equal(9, value["maxVertex"]);
            Assert.Equal(new[] { 8 }, Listed(value).Select(row => (int)row["vertex"]).ToArray());
        }

        [Fact]
        public void LeavingOutTheCopyIsRefused()
        {
            int handle = Mixed();

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(handle, Skin()));
            IDictionary<string, object> envelope = _fixture.Call(
                ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("indices", Skin()),
                    ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 })));

            Assert.Equal(3, found["count"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void LeavingOutTheVerticesIsRefused()
        {
            int handle = Mixed();

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(handle, Skin()));
            IDictionary<string, object> envelope = _fixture.Call(
                ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("basePmxHandle", (long)handle),
                    ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 })));

            Assert.Equal(3, found["count"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void LeavingOutTheSurfaceIsRefused()
        {
            int handle = Mixed();

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(handle, Skin()));
            IDictionary<string, object> envelope = _fixture.Call(
                ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("basePmxHandle", (long)handle),
                    ComposedEditFixture.Given("indices", Skin())));

            Assert.Equal(3, found["count"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AVertexOrASurfaceOutsideTheModelIsRefused()
        {
            int handle = Mixed();

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(handle, Skin()));
            IDictionary<string, object> vertexPast = Find(handle, new object[] { 8, 14 });
            IDictionary<string, object> vertexNegative = Find(handle, new object[] { -1 });
            IDictionary<string, object> surfacePast = _fixture.Call(
                ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("basePmxHandle", (long)handle),
                    ComposedEditFixture.Given("indices", Skin()),
                    ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 3 })));

            Assert.Equal(3, found["count"]);
            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(vertexPast));
            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(vertexNegative));
            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(surfacePast));
        }

        [Fact]
        public void ASurfaceWithoutFacesIsRefused()
        {
            int handle = Mixed();
            _fixture.Model.Material.Add(new FakeMaterial("空"));
            _held.Material.Add(new FakeMaterial("空"));

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(handle, Skin()));
            IDictionary<string, object> envelope = _fixture.Call(
                ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("basePmxHandle", (long)handle),
                    ComposedEditFixture.Given("indices", Skin()),
                    ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 1 })));

            Assert.Equal(3, found["count"]);
            Assert.Equal(ToolEnvelope.NotApplicable, ComposedEditFixture.Code(envelope));
        }

        [Theory]
        [InlineData("vertex")]
        [InlineData("face")]
        [InlineData("material")]
        public void ACopyWhoseCountsDifferFromTheModelIsRefused(string differing)
        {
            int same = Mixed();
            IDictionary<string, object> found = ComposedEditFixture.Value(Find(same, Skin()));

            FakePmx copy = FakeEditorState.Duplicate(_fixture.Model);
            switch (differing)
            {
                case "vertex":
                    copy.Vertex.Add(new FakeVertex(5f, 5f, 5f));
                    break;

                case "face":
                    ((FakeMaterial)copy.Material[0]).Faces.RemoveAt(1);
                    break;

                default:
                    copy.Material.Add(new FakeMaterial("余分"));
                    break;
            }

            int handle = _fixture.Handles.Issue(typeof(IPXPmx).FullName, copy, () => { });
            IDictionary<string, object> envelope = Find(handle, Skin());

            Assert.Equal(3, found["count"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void ALimitBelowOneAndANegativeOffsetAreRefused()
        {
            int handle = Mixed();

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(
                handle,
                Skin(),
                ComposedEditFixture.Given("offset", 0),
                ComposedEditFixture.Given("limit", 1)));
            IDictionary<string, object> noLimit = Find(
                handle, Skin(), ComposedEditFixture.Given("limit", 0));
            IDictionary<string, object> backward = Find(
                handle, Skin(), ComposedEditFixture.Given("offset", -1));

            Assert.Equal(3, found["count"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(noLimit));
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(backward));
        }

        [Fact]
        public void InsideIsJudgedByTheCopySurfaceAndDepthByTheSurfaceNow()
        {
            V3[] skin =
            {
                P(0f, 0.1f, 0.2f),
                P(0.5f, 0.1f, 0.2f),
                P(4f, 0.1f, 0.2f),
                P(1.5f, 0.1f, 0.2f),
            };
            int handle = Pair(skin, skin, 4f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(handle, Skin4()));

            IDictionary<string, object>[] listed = Listed(value);
            Assert.Equal(2, value["count"]);
            Assert.Equal(3f, (float)value["maxDepth"], 5);
            Assert.Equal(8, value["maxVertex"]);
            Assert.Equal(new[] { 8, 9 }, listed.Select(row => (int)row["vertex"]).ToArray());
            Assert.Equal(3f, (float)listed[0]["depth"], 5);
            Assert.Equal(2.5f, (float)listed[1]["depth"], 5);
        }

        private static object[] Skin4()
        {
            return new object[] { 8, 9, 10, 11 };
        }

        private static object[] Skin()
        {
            return new object[] { 8, 9, 10, 11, 12, 13 };
        }

        private int Mixed()
        {
            return Pair(
                new[]
                {
                    P(0.1f, 0.2f, 0.3f),
                    P(-0.2f, 0f, 0.1f),
                    P(0f, -0.5f, 0.2f),
                    P(0.3f, 0.3f, 0.3f),
                    P(4f, 0f, 0f),
                    P(3f, 0f, 0f),
                },
                new[]
                {
                    P(0.1f, 1.5f, 0.3f),
                    P(-3f, 0f, 0.1f),
                    P(0f, -0.5f, -1.75f),
                    P(0.4f, 0.3f, 0.3f),
                    P(5f, 0f, 0f),
                    P(0f, 0f, 0f),
                });
        }

        private IDictionary<string, object> Find(
            int handle, object indices, params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>(given)
            {
                ComposedEditFixture.Given("basePmxHandle", (long)handle),
                ComposedEditFixture.Given("indices", indices),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
            };

            return _fixture.Call(ToolName, ComposedEditFixture.Arguments(all.ToArray()));
        }

        private int Pair(V3[] baseline, V3[] posed, float surfaceShift = 0f)
        {
            _held = new FakePmx();
            Build(_held, baseline, 0f);
            Build(_fixture.Model, posed, surfaceShift);

            return _fixture.Handles.Issue(typeof(IPXPmx).FullName, _held, () => { });
        }

        private static void Build(FakePmx model, V3[] skin, float surfaceShift)
        {
            IPXVertex[] corners = new IPXVertex[FirstSkin];
            for (int at = 0; at < FirstSkin; at++)
            {
                float x = ((at & 1) == 0 ? -1f : 1f) + surfaceShift;
                float y = (at & 2) == 0 ? -1f : 1f;
                float z = (at & 4) == 0 ? -1f : 1f;
                corners[at] = new FakeVertex(x, y, z);
                model.Vertex.Add(corners[at]);
            }

            foreach (V3 position in skin)
            {
                model.Vertex.Add(new FakeVertex(position.X, position.Y, position.Z));
            }

            int[][] quads =
            {
                new[] { 0, 2, 6, 4 },
                new[] { 1, 3, 7, 5 },
                new[] { 0, 1, 5, 4 },
                new[] { 2, 3, 7, 6 },
                new[] { 0, 1, 3, 2 },
                new[] { 4, 5, 7, 6 },
            };
            FakeMaterial cube = new FakeMaterial("衣装");
            foreach (int[] quad in quads)
            {
                cube.Faces.Add(new FakeFace(corners[quad[0]], corners[quad[1]], corners[quad[2]]));
                cube.Faces.Add(new FakeFace(corners[quad[0]], corners[quad[2]], corners[quad[3]]));
            }

            model.Material.Add(cube);
        }

        private static V3 P(float x, float y, float z)
        {
            return new V3(x, y, z);
        }

        private static IDictionary<string, object>[] Listed(IDictionary<string, object> value)
        {
            return ((object[])value["vertices"]).Cast<IDictionary<string, object>>().ToArray();
        }
    }
}
