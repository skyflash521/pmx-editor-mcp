using System;
using System.Collections.Generic;
using System.Linq;
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

        [Fact]
        public void TheDistributionCountsTheVerticesWithinEachThresholdInTheOrderGiven()
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(0f, 0.05f, 0f);
            Vertex(0f, 0.2f, 0f);
            Vertex(0f, 0.2f, 0.1f);
            Vertex(0f, -0.15f, 0f);
            Vertex(0f, 0.8f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 4, 5, 6, 7, 8 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("distanceThresholds", new object[] { 0.25, 0.1, 1.0, 0.0 })));

            object[] distribution = (object[])value["distribution"];
            Assert.Equal(4, distribution.Length);
            AssertLimit(0.25f, 4, distribution[0]);
            AssertLimit(0.1f, 1, distribution[1]);
            AssertLimit(1f, 5, distribution[2]);
            AssertLimit(0f, 0, distribution[3]);
        }

        [Fact]
        public void AVertexExactlyAtAThresholdIsCountedWithinIt()
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(0f, 0.2f, 0f);
            Vertex(0f, 0f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 4, 5 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("distanceThresholds", new object[] { 0.2, 0.0 })));

            object[] distribution = (object[])value["distribution"];
            AssertLimit(0.2f, 2, distribution[0]);
            AssertLimit(0f, 1, distribution[1]);
        }

        [Fact]
        public void TheDistributionIsKeptPerBoxToo()
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(0f, 0.05f, 0f);
            Vertex(3f, 0.3f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 4, 5 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("distanceThresholds", new object[] { 0.1 }),
                ComposedEditFixture.Given(
                    "boxes",
                    new object[]
                    {
                        Box("minX", -1.0, "maxX", 1.0, "minY", -1.0, "maxY", 1.0, "minZ", -1.0, "maxZ", 1.0),
                        Box("minX", 2.0, "maxX", 4.0, "minY", -1.0, "maxY", 1.0, "minZ", -1.0, "maxZ", 1.0),
                    })));

            object[] boxes = (object[])value["boxes"];
            AssertLimit(0.1f, 1, ((object[])((IDictionary<string, object>)boxes[0])["distribution"])[0]);
            AssertLimit(0.1f, 0, ((object[])((IDictionary<string, object>)boxes[1])["distribution"])[0]);
        }

        [Fact]
        public void VerticesFartherThanTheDistanceLimitAreLeftOutOfTheDistribution()
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(0f, 0.05f, 0f);
            Vertex(0f, 5f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 4, 5 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("distanceLimit", 1.0),
                ComposedEditFixture.Given("distanceThresholds", new object[] { 10.0 })));

            AssertLimit(10f, 1, ((object[])value["distribution"])[0]);
        }

        [Fact]
        public void WithoutThresholdsThereIsNoDistribution()
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(0f, 0.05f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", new object[] { 4 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 })));

            Assert.False(value.ContainsKey("distribution"));
        }

        [Theory]
        [MemberData(nameof(BadThresholds))]
        public void ThresholdsThatAreNotNonNegativeFiniteNumbersAreRefused(object given)
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(0f, 0.05f, 0f);

            IDictionary<string, object> envelope = Find(
                ComposedEditFixture.Given("indices", new object[] { 4 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("distanceThresholds", given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        public static IEnumerable<object[]> BadThresholds()
        {
            yield return new object[] { new object[0] };
            yield return new object[] { new object[] { -0.1 } };
            yield return new object[] { new object[] { "0.1" } };
            yield return new object[] { new object[] { double.PositiveInfinity } };
            yield return new object[] { 0.1 };
        }

        [Fact]
        public void ProjectingMovesTheVerticesOntoTheNearestPointOfTheSurface()
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(0.2f, 0.5f, 0.1f);
            Vertex(-0.4f, -0.3f, 0.2f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Project(
                ComposedEditFixture.Given("indices", new object[] { 4, 5 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 })));

            Assert.Equal(2, value["changed"]);
            AssertAt(0.2f, 0f, 0.1f, _fixture.Model.Vertex[4]);
            AssertAt(-0.4f, 0f, 0.2f, _fixture.Model.Vertex[5]);
        }

        [Fact]
        public void TheOffsetIsMeasuredAlongTheFrontOfTheSurfaceFromBothSides()
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(0.2f, 0.5f, 0.1f);
            Vertex(-0.4f, -0.3f, 0.2f);

            Project(
                ComposedEditFixture.Given("indices", new object[] { 4, 5 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("surfaceOffset", 0.01));

            AssertAt(0.2f, 0.01f, 0.1f, _fixture.Model.Vertex[4]);
            AssertAt(-0.4f, 0.01f, 0.2f, _fixture.Model.Vertex[5]);
        }

        [Theory]
        [InlineData(2f, 0.25f)]
        [InlineData(0f, -0.25f)]
        public void TheOffsetDoesNotDependOnTheLengthOfTheNormalsOfTheSurface(
            float normalLength, float expectedHeight)
        {
            Floor(new V3(0f, normalLength, 0f));
            Vertex(0.2f, 0.5f, 0.1f);

            Project(
                ComposedEditFixture.Given("indices", new object[] { 4 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("surfaceOffset", 0.25));

            AssertAt(0.2f, expectedHeight, 0.1f, _fixture.Model.Vertex[4]);
        }

        [Fact]
        public void ANegativeOffsetSinksTheVerticesBehindTheSurface()
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(0.2f, 0.5f, 0.1f);

            Project(
                ComposedEditFixture.Given("indices", new object[] { 4 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("surfaceOffset", -0.02));

            AssertAt(0.2f, -0.02f, 0.1f, _fixture.Model.Vertex[4]);
        }

        [Fact]
        public void VerticesFartherThanTheDistanceLimitStayWhereTheyAre()
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(0.2f, 0.5f, 0.1f);
            Vertex(0.2f, 5f, 0.1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Project(
                ComposedEditFixture.Given("indices", new object[] { 4, 5 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("distanceLimit", 1.0)));

            Assert.Equal(1, value["changed"]);
            AssertAt(0.2f, 0f, 0.1f, _fixture.Model.Vertex[4]);
            AssertAt(0.2f, 5f, 0.1f, _fixture.Model.Vertex[5]);
        }

        [Fact]
        public void ProjectingWithoutTheSurfaceMaterialsIsRefused()
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(0.2f, 0.5f, 0.1f);

            IDictionary<string, object> envelope = Project(
                ComposedEditFixture.Given("indices", new object[] { 4 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Equal(0.5f, _fixture.Model.Vertex[4].Position.Y);
        }

        [Fact]
        public void TheSurfaceMaterialsArePassedOnlyToProjecting()
        {
            Floor(new V3(0f, 1f, 0f));
            Vertex(0.2f, 0.5f, 0.1f);

            IDictionary<string, object> envelope = _fixture.Call(
                ModelEditVertices.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", ModelEditVertices.Align),
                    ComposedEditFixture.Given("indices", new object[] { 4 }),
                    ComposedEditFixture.Given("axis", "y"),
                    ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 })));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void ByNormalTheInsideOfAClosedSurfaceWithInwardNormalsIsTheFront()
        {
            Cube(true);
            Vertex(0.5f, 0.1f, 0.2f);
            Vertex(3f, 0.1f, 0.2f);

            IDictionary<string, object> value = FindBy("normal", 8, 9);

            Assert.Equal(1, value["frontCount"]);
            Assert.Equal(1, value["backCount"]);
            Assert.Equal(-2f, (float)value["minDistance"], 5);
            Assert.Equal(9, value["minVertex"]);
            Assert.Equal(0.5f, (float)value["maxDistance"], 5);
            Assert.Equal(8, value["maxVertex"]);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ByRayParityTheInsideOfAClosedSurfaceIsTheBackWhicheverWayTheNormalsPoint(
            bool inward)
        {
            Cube(inward);
            Vertex(0.5f, 0.1f, 0.2f);
            Vertex(3f, 0.1f, 0.2f);

            IDictionary<string, object> value = FindBy("rayParity", 8, 9);

            Assert.Equal(1, value["frontCount"]);
            Assert.Equal(1, value["backCount"]);
            Assert.Equal(-0.5f, (float)value["minDistance"], 5);
            Assert.Equal(8, value["minVertex"]);
            Assert.Equal(2f, (float)value["maxDistance"], 5);
            Assert.Equal(9, value["maxVertex"]);
        }

        [Fact]
        public void ByNormalAnOpenSurfaceWithDownwardNormalsHasItsUpperSideAsTheBack()
        {
            Floor(new V3(0f, -1f, 0f));
            Vertex(0.2f, 0.5f, 0.1f);
            Vertex(0.2f, -0.3f, 0.1f);

            IDictionary<string, object> value = FindBy("normal", 4, 5);

            Assert.Equal(-0.5f, (float)value["minDistance"], 5);
            Assert.Equal(4, value["minVertex"]);
            Assert.Equal(0.3f, (float)value["maxDistance"], 5);
            Assert.Equal(5, value["maxVertex"]);
        }

        [Fact]
        public void ByRayParityAnOpenSurfaceHasItsSideReachedByTwoOfTheThreeRaysAsTheBack()
        {
            Floor(new V3(0f, -1f, 0f));
            Vertex(0.2f, 0.5f, 0.1f);
            Vertex(0.2f, -0.3f, 0.1f);

            IDictionary<string, object> value = FindBy("rayParity", 4, 5);

            Assert.Equal(-0.3f, (float)value["minDistance"], 5);
            Assert.Equal(5, value["minVertex"]);
            Assert.Equal(0.5f, (float)value["maxDistance"], 5);
            Assert.Equal(4, value["maxVertex"]);
        }

        [Theory]
        [InlineData("inside")]
        [InlineData("")]
        [InlineData(1.0)]
        public void ASideRuleThatIsNotOneOfTheTwoIsRefused(object given)
        {
            Cube(false);
            Vertex(0.5f, 0.1f, 0.2f);

            IDictionary<string, object> found = FindBy("rayParity", 8);
            IDictionary<string, object> envelope = Find(
                ComposedEditFixture.Given("indices", new object[] { 8 }),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("sideBy", given));

            Assert.Equal(1, found["count"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        private IDictionary<string, object> FindBy(string sideBy, params int[] indices)
        {
            return ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("indices", indices.Cast<object>().ToArray()),
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("sideBy", sideBy)));
        }

        private void Cube(bool inward)
        {
            IPXVertex[] corners = new IPXVertex[8];
            for (int at = 0; at < 8; at++)
            {
                float x = (at & 1) == 0 ? -1f : 1f;
                float y = (at & 2) == 0 ? -1f : 1f;
                float z = (at & 4) == 0 ? -1f : 1f;
                corners[at] = Vertex(x, y, z);
                corners[at].Normal = inward ? new V3(-x, -y, -z) : new V3(x, y, z);
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
                cube.Faces.Add(new FakeFace(corners[quad[0]], corners[quad[1]], corners[quad[2]]));
                cube.Faces.Add(new FakeFace(corners[quad[0]], corners[quad[2]], corners[quad[3]]));
            }

            _fixture.Model.Material.Add(cube);
        }

        private IDictionary<string, object> Project(params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", ModelEditVertices.ProjectOntoSurface),
            };
            all.AddRange(given);

            return _fixture.Call(
                ModelEditVertices.ToolName, ComposedEditFixture.Arguments(all.ToArray()));
        }

        private static void AssertAt(float x, float y, float z, IPXVertex given)
        {
            Assert.Equal(x, given.Position.X, 5);
            Assert.Equal(y, given.Position.Y, 5);
            Assert.Equal(z, given.Position.Z, 5);
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

        private static void AssertLimit(float limit, int count, object given)
        {
            IDictionary<string, object> row = (IDictionary<string, object>)given;
            Assert.Equal(limit, (float)row["limit"], 5);
            Assert.Equal(count, row["count"]);
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
