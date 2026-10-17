using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditVerticesPushOutTests : IDisposable
    {
        private const string PushOutOfSurface = "pushOutOfSurface";

        private const string SurfaceMaterialIndices = "surfaceMaterialIndices";

        private const string Margin = "margin";

        private const string SpreadRadius = "spreadRadius";

        private const string Changed = "changed";

        private const string Remaining = "remaining";

        private const float Tolerance = 1e-4f;

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void TwoLayersSunkIntoAClosedSurfaceMoveByTheSameAmountToOutsideTheMargin(
            bool inwardNormals, bool reversedWinding)
        {
            Cube(inwardNormals, reversedWinding);
            IPXVertex upper = Vertex(0.2f, 0.9f, 0.1f);
            IPXVertex lower = Vertex(0.2f, 0.8f, 0.1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Push(
                new[] { upper, lower },
                ComposedEditFixture.Given(Margin, 0.05),
                ComposedEditFixture.Given(SpreadRadius, 100.0)));

            Assert.Equal(2, value[Changed]);
            Assert.Equal(0, value[Remaining]);
            Assert.True(At(upper).Y >= 1.05f - Tolerance);
            Assert.True(At(lower).Y >= 1.05f - Tolerance);
            Assert.Equal(At(upper).Y - 0.9f, At(lower).Y - 0.8f, 4);
            Assert.Equal(0.1f, At(upper).Y - At(lower).Y, 4);
            Assert.Equal(0.2f, At(upper).X, 4);
            Assert.Equal(0.1f, At(upper).Z, 4);
            Assert.Equal(0.2f, At(lower).X, 4);
            Assert.Equal(0.1f, At(lower).Z, 4);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void AnInsideVertexLeavesAlongTheLineToTheNearestPointOfTheSurface(
            bool inwardNormals, bool reversedWinding)
        {
            Cube(inwardNormals, reversedWinding);
            IPXVertex towardX = Vertex(0.9f, 0.1f, 0.2f);
            IPXVertex towardNegativeZ = Vertex(-0.2f, 0.3f, -0.95f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Push(
                new[] { towardX, towardNegativeZ },
                ComposedEditFixture.Given(Margin, 0.1)));

            Assert.Equal(2, value[Changed]);
            Assert.Equal(0, value[Remaining]);
            Assert.True(At(towardX).X >= 1.1f - Tolerance);
            Assert.Equal(0.1f, At(towardX).Y, 4);
            Assert.Equal(0.2f, At(towardX).Z, 4);
            Assert.True(At(towardNegativeZ).Z <= -1.1f + Tolerance);
            Assert.Equal(-0.2f, At(towardNegativeZ).X, 4);
            Assert.Equal(0.3f, At(towardNegativeZ).Y, 4);
        }

        [Fact]
        public void AnOutsideVertexCloserThanTheMarginLeavesAlongTheLineFromTheNearestPoint()
        {
            Cube(false, false);
            IPXVertex near = Vertex(1.02f, 0.1f, 0.2f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Push(
                new[] { near },
                ComposedEditFixture.Given(Margin, 0.1)));

            Assert.Equal(1, value[Changed]);
            Assert.Equal(0, value[Remaining]);
            Assert.True(At(near).X >= 1.1f - Tolerance);
            Assert.Equal(0.1f, At(near).Y, 4);
            Assert.Equal(0.2f, At(near).Z, 4);
        }

        [Fact]
        public void WithoutTheMarginAnInsideVertexLeavesToTheSurfaceOrBeyond()
        {
            Cube(false, false);
            IPXVertex inside = Vertex(0.9f, 0.1f, 0.2f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Push(new[] { inside }));

            Assert.Equal(1, value[Changed]);
            Assert.Equal(0, value[Remaining]);
            Assert.True(At(inside).X >= 1f - Tolerance);
        }

        [Fact]
        public void VerticesExactlyAtTheMarginAndBeyondItStayWhereTheyAre()
        {
            Cube(false, false);
            IPXVertex atMargin = Vertex(1.25f, 0.125f, 0.25f);
            IPXVertex beyond = Vertex(1.5f, 0.125f, 0.25f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Push(
                new[] { atMargin, beyond },
                ComposedEditFixture.Given(Margin, 0.25),
                ComposedEditFixture.Given(SpreadRadius, 1.0)));

            Assert.Equal(0, value[Changed]);
            Assert.Equal(0, value[Remaining]);
            AssertAt(1.25f, 0.125f, 0.25f, atMargin);
            AssertAt(1.5f, 0.125f, 0.25f, beyond);
        }

        [Fact]
        public void TheVerticesThatWereNotPickedStayEvenInsideTheSurfaceAndWithinTheSpread()
        {
            Cube(false, false);
            IPXVertex picked = Vertex(0.2f, 0.9f, 0.1f);
            IPXVertex left = Vertex(0.2f, 0.9f, 0.1f);
            IPXVertex leftDeeper = Vertex(0.2f, 0.8f, 0.1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Push(
                new[] { picked },
                ComposedEditFixture.Given(Margin, 0.05),
                ComposedEditFixture.Given(SpreadRadius, 100.0)));

            Assert.Equal(1, value[Changed]);
            Assert.Equal(0, value[Remaining]);
            Assert.True(At(picked).Y >= 1.05f - Tolerance);
            AssertAt(0.2f, 0.9f, 0.1f, left);
            AssertAt(0.2f, 0.8f, 0.1f, leftDeeper);
        }

        [Fact]
        public void TheSpreadMovesThePickedVerticesWithinTheRadiusByLessAndLeavesTheOnesBeyondIt()
        {
            Cube(false, false);
            IPXVertex inside = Vertex(0.2f, 0.9f, 0.1f);
            IPXVertex within = Vertex(0.2f, 1.5f, 0.1f);
            IPXVertex beyond = Vertex(0.2f, 3f, 0.1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Push(
                new[] { inside, within, beyond },
                ComposedEditFixture.Given(Margin, 0.05),
                ComposedEditFixture.Given(SpreadRadius, 1.0)));

            float pushedInside = At(inside).Y - 0.9f;
            float pushedWithin = At(within).Y - 1.5f;
            Assert.Equal(2, value[Changed]);
            Assert.Equal(0, value[Remaining]);
            Assert.True(At(inside).Y >= 1.05f - Tolerance);
            Assert.True(pushedWithin > Tolerance);
            Assert.True(pushedWithin < pushedInside - Tolerance);
            Assert.Equal(0.2f, At(within).X, 4);
            Assert.Equal(0.1f, At(within).Z, 4);
            AssertAt(0.2f, 3f, 0.1f, beyond);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WithoutTheSpreadOnlyTheVerticesThatNeedToMoveMove(bool zeroGiven)
        {
            Cube(false, false);
            IPXVertex inside = Vertex(0.2f, 0.9f, 0.1f);
            IPXVertex neighbour = Vertex(0.2f, 1.5f, 0.1f);
            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given(Margin, 0.05),
            };
            if (zeroGiven)
            {
                given.Add(ComposedEditFixture.Given(SpreadRadius, 0.0));
            }

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Push(new[] { inside, neighbour }, given.ToArray()));

            Assert.Equal(1, value[Changed]);
            Assert.Equal(0, value[Remaining]);
            Assert.True(At(inside).Y >= 1.05f - Tolerance);
            AssertAt(0.2f, 1.5f, 0.1f, neighbour);
        }

        [Fact]
        public void EveryPickedVertexLeavesTheSurfaceEvenWhereTheSpreadFallsOff()
        {
            Cube(false, false);
            IPXVertex[] column =
            {
                Vertex(0f, 0.9f, 0f),
                Vertex(0f, 0.5f, 0f),
                Vertex(0f, 0.1f, 0f),
            };

            IDictionary<string, object> value = ComposedEditFixture.Value(Push(
                column,
                ComposedEditFixture.Given(Margin, 0.05),
                ComposedEditFixture.Given(SpreadRadius, 0.5)));

            Assert.Equal(3, value[Changed]);
            Assert.Equal(0, value[Remaining]);
            foreach (IPXVertex vertex in column)
            {
                Assert.True(At(vertex).Y >= 1.05f - Tolerance);
            }
        }

        [Fact]
        public void APickedVertexThatTheSpreadCarriesIntoTheSurfaceIsCountedAsRemaining()
        {
            Cube(false, false);
            IPXVertex inside = Vertex(0f, 0.5f, 0f);
            IPXVertex outside = Vertex(0f, -1.1f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Push(
                new[] { inside, outside },
                ComposedEditFixture.Given(Margin, 0.05),
                ComposedEditFixture.Given(SpreadRadius, 4.0)));

            Assert.Equal(2, value[Changed]);
            Assert.True(At(inside).Y >= 1.05f - Tolerance);
            Assert.True(At(outside).Y > -1f);
            Assert.Equal(1, value[Remaining]);
        }

        [Fact]
        public void AShallowVertexBeyondTheRadiusIsPushedOnlyByItsOwnDepth()
        {
            Cube(false, false);
            IPXVertex deep = Vertex(0.9f, 0.1f, 0.2f);
            IPXVertex shallow = Vertex(-0.99f, 0.1f, 0.2f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Push(
                new[] { deep, shallow },
                ComposedEditFixture.Given(SpreadRadius, 1.0)));

            Assert.Equal(2, value[Changed]);
            Assert.Equal(0, value[Remaining]);
            Assert.True(At(deep).X >= 1f - Tolerance);
            Assert.Equal(-1.0f, At(shallow).X, 4);
        }

        [Fact]
        public void TwoVerticesOnEitherSideOfTheRadiusEdgeAtTheSameDepthMoveByTheSameAmount()
        {
            Cube(false, false);
            IPXVertex centre = Vertex(0f, 0.9f, 0f);
            IPXVertex nearby = Vertex(-0.3f, 1.2f, 0f);
            IPXVertex inner = Vertex(0.495f, 0.9f, 0f);
            IPXVertex outer = Vertex(0.505f, 0.9f, 0f);

            ComposedEditFixture.Value(Push(
                new[] { centre, nearby, inner, outer },
                ComposedEditFixture.Given(Margin, 0.05),
                ComposedEditFixture.Given(SpreadRadius, 0.5)));

            Assert.Equal(At(inner).Y - 0.9f, At(outer).Y - 0.9f, 3);
        }

        [Fact]
        public void AThinBoxWhereTheNeighboursPushAgainstEachOtherDoesNotThrowTheFarVertexAway()
        {
            Box(false, false, 0f, 0.1f);
            IPXVertex up = Vertex(0f, 0.06f, 0f);
            IPXVertex down = Vertex(0f, 0.0399f, 0.02f);
            IPXVertex side = Vertex(0.96f, 0.05f, 0f);

            ComposedEditFixture.Value(Push(
                new[] { up, down, side },
                ComposedEditFixture.Given(SpreadRadius, 1.0)));

            Assert.InRange(At(side).X, 1f - Tolerance, 1.1f);
        }

        [Fact]
        public void AVertexOnTheSurfaceHasNoDirectionToMoveInAndIsCountedAsRemaining()
        {
            Cube(false, false);
            IPXVertex onSurface = Vertex(0.2f, 1f, 0.1f);
            IPXVertex inside = Vertex(0.9f, 0.1f, 0.2f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Push(
                new[] { onSurface, inside },
                ComposedEditFixture.Given(Margin, 0.1)));

            Assert.Equal(1, value[Changed]);
            Assert.Equal(1, value[Remaining]);
            AssertAt(0.2f, 1f, 0.1f, onSurface);
            Assert.True(At(inside).X >= 1.1f - Tolerance);
        }

        [Fact]
        public void WithoutTheSurfaceMaterialsTheOperationIsRefused()
        {
            Cube(false, false);
            IPXVertex inside = Vertex(0.9f, 0.1f, 0.2f);
            Succeeds();

            IDictionary<string, object> envelope = _fixture.Call(
                ModelEditVertices.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", PushOutOfSurface),
                    ComposedEditFixture.Given("indices", Indices(inside))));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(SurfaceMaterialIndices, ComposedEditFixture.Message(envelope));
            AssertAt(0.9f, 0.1f, 0.2f, inside);
        }

        [Fact]
        public void AMaterialOutsideTheListIsRefused()
        {
            Cube(false, false);
            IPXVertex inside = Vertex(0.9f, 0.1f, 0.2f);
            Succeeds();

            IDictionary<string, object> envelope = _fixture.Call(
                ModelEditVertices.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", PushOutOfSurface),
                    ComposedEditFixture.Given("indices", Indices(inside)),
                    ComposedEditFixture.Given(SurfaceMaterialIndices, new object[] { 5 })));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            Assert.Contains(SurfaceMaterialIndices, ComposedEditFixture.Message(envelope));
            AssertAt(0.9f, 0.1f, 0.2f, inside);
        }

        [Theory]
        [MemberData(nameof(BadNumbers))]
        public void AMarginThatIsNotANonNegativeFiniteNumberIsRefused(object given)
        {
            Cube(false, false);
            IPXVertex inside = Vertex(0.9f, 0.1f, 0.2f);
            Succeeds(ComposedEditFixture.Given(Margin, 0.0));

            IDictionary<string, object> envelope = Push(
                new[] { inside }, ComposedEditFixture.Given(Margin, given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Margin, ComposedEditFixture.Message(envelope));
            AssertAt(0.9f, 0.1f, 0.2f, inside);
        }

        [Theory]
        [MemberData(nameof(BadNumbers))]
        public void ASpreadRadiusThatIsNotANonNegativeFiniteNumberIsRefused(object given)
        {
            Cube(false, false);
            IPXVertex inside = Vertex(0.9f, 0.1f, 0.2f);
            Succeeds(ComposedEditFixture.Given(SpreadRadius, 0.0));

            IDictionary<string, object> envelope = Push(
                new[] { inside }, ComposedEditFixture.Given(SpreadRadius, given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(SpreadRadius, ComposedEditFixture.Message(envelope));
            AssertAt(0.9f, 0.1f, 0.2f, inside);
        }

        public static IEnumerable<object[]> BadNumbers()
        {
            yield return new object[] { -0.1 };
            yield return new object[] { "0.1" };
            yield return new object[] { new object[] { 0.1 } };
            yield return new object[] { double.PositiveInfinity };
            yield return new object[] { double.NaN };
        }

        [Theory]
        [InlineData(Margin)]
        [InlineData(SpreadRadius)]
        public void TheMarginAndTheSpreadRadiusArePassedOnlyToPushingOut(string name)
        {
            Cube(false, false);
            IPXVertex inside = Vertex(0.9f, 0.1f, 0.2f);
            Succeeds(ComposedEditFixture.Given(name, 0.0));

            IDictionary<string, object> envelope = _fixture.Call(
                ModelEditVertices.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", ModelEditVertices.Align),
                    ComposedEditFixture.Given("indices", Indices(inside)),
                    ComposedEditFixture.Given("axis", "y"),
                    ComposedEditFixture.Given(name, 0.0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
            AssertAt(0.9f, 0.1f, 0.2f, inside);
        }

        private void Succeeds(params KeyValuePair<string, object>[] given)
        {
            IPXVertex probe = Vertex(0.2f, 0.9f, 0.1f);
            ComposedEditFixture.Value(Push(new[] { probe }, given));
        }

        private IDictionary<string, object> Push(
            IList<IPXVertex> picked, params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", PushOutOfSurface),
                ComposedEditFixture.Given("indices", Indices(picked.ToArray())),
                ComposedEditFixture.Given(SurfaceMaterialIndices, new object[] { 0 }),
            };
            all.AddRange(given);

            return _fixture.Call(
                ModelEditVertices.ToolName, ComposedEditFixture.Arguments(all.ToArray()));
        }

        private object[] Indices(params IPXVertex[] picked)
        {
            return picked
                .Select(vertex => (object)_fixture.Model.Vertex.IndexOf(_fixture.Now(vertex)))
                .ToArray();
        }

        private V3 At(IPXVertex given)
        {
            return _fixture.Now(given).Position;
        }

        private void AssertAt(float x, float y, float z, IPXVertex given)
        {
            Assert.Equal(x, At(given).X);
            Assert.Equal(y, At(given).Y);
            Assert.Equal(z, At(given).Z);
        }

        private void Cube(bool inwardNormals, bool reversedWinding)
        {
            Box(inwardNormals, reversedWinding, -1f, 1f);
        }

        private void Box(bool inwardNormals, bool reversedWinding, float bottom, float top)
        {
            IPXVertex[] corners = new IPXVertex[8];
            for (int at = 0; at < 8; at++)
            {
                float x = (at & 1) == 0 ? -1f : 1f;
                float y = (at & 2) == 0 ? bottom : top;
                float z = (at & 4) == 0 ? -1f : 1f;
                corners[at] = Vertex(x, y, z);
                float facing = (at & 2) == 0 ? -1f : 1f;
                corners[at].Normal = inwardNormals ? new V3(-x, -facing, -z) : new V3(x, facing, z);
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
            FakeMaterial cube = new FakeMaterial("箱");
            foreach (int[] quad in quads)
            {
                cube.Faces.Add(Face(corners, reversedWinding, quad[0], quad[1], quad[2]));
                cube.Faces.Add(Face(corners, reversedWinding, quad[0], quad[2], quad[3]));
            }

            _fixture.Model.Material.Add(cube);
        }

        private static FakeFace Face(IPXVertex[] corners, bool reversed, int a, int b, int c)
        {
            return reversed
                ? new FakeFace(corners[a], corners[c], corners[b])
                : new FakeFace(corners[a], corners[b], corners[c]);
        }

        private IPXVertex Vertex(float x, float y, float z)
        {
            FakeVertex made = new FakeVertex(x, y, z);
            _fixture.Model.Vertex.Add(made);

            return made;
        }
    }
}
