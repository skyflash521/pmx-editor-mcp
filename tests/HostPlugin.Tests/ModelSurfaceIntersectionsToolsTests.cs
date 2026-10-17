using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelSurfaceIntersectionsToolsTests : IDisposable
    {
        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TwoCrossingFacesAreCountedWithTheSegmentWhereTheyMeet()
        {
            Triangle(0, -2, -2, 0, 2, -2, 0, 0, 2, 0);
            Triangle(1, 0, -1, -1, 0, -1, 1, 0, 1, 0);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(A(0), B(1)));

            Assert.Equal(1, value["count"]);
            Assert.Equal(1, value["faceCount"]);
            Assert.Equal(1, value["otherFaceCount"]);
            Assert.Equal(2f, (float)value["totalLength"], 5);
            IDictionary<string, object> pair = Pairs(value).Single();
            Assert.Equal(0, pair["material"]);
            Assert.Equal(0, pair["face"]);
            Assert.Equal(1, pair["otherMaterial"]);
            Assert.Equal(0, pair["otherFace"]);
            Assert.Equal(2f, (float)pair["length"], 5);
            AssertPoint(0f, -1f, 0f, pair["start"]);
            AssertPoint(0f, 1f, 0f, pair["end"]);
        }

        [Fact]
        public void TheDepthOfAPairIsTheShortestMoveThatBringsOneFaceToOneSideOfThePlaneOfTheOther()
        {
            Triangle(0, -2, -2, 0, 2, -2, 0, 0, 2, 0);
            Triangle(1, 0, -1, -1, 0, -1, 1, 0, 1, 0);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(A(0), B(1)));

            Assert.Equal(1f, (float)Pairs(value).Single()["depth"], 5);
            Assert.Equal(1f, (float)value["maxDepth"], 5);
        }

        [Fact]
        public void TheSideOfTheMoveThatIsShorterSetsTheDepth()
        {
            Triangle(0, -2, -2, 0, 2, -2, 0, 0, 2, 0);
            Triangle(1, 0, -1, -0.25f, 0, -1, 3, 0, 1, 0);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(A(0), B(1)));

            Assert.Equal(0.25f, (float)Pairs(value).Single()["depth"], 5);
        }

        [Fact]
        public void FacesThatDoNotMeetAreNotCounted()
        {
            Triangle(0, -2, -2, 0, 2, -2, 0, 0, 2, 0);
            Triangle(1, 0, -1, 1, 0, -1, 3, 0, 1, 2);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(A(0), B(1)));

            Assert.Equal(0, value["count"]);
            Assert.Equal(0, value["faceCount"]);
            Assert.Equal(0, value["otherFaceCount"]);
            Assert.Equal(0f, (float)value["totalLength"], 5);
            Assert.Empty(Pairs(value));
            Assert.False(value.ContainsKey("maxDepth"));
        }

        [Fact]
        public void FacesThatOnlyTouchAtAPointAreNotCounted()
        {
            Triangle(0, -2, -2, 0, 2, -2, 0, 0, 2, 0);
            Triangle(1, 0, 0, 0, 0, 1, 1, 0, -1, 1);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(A(0), B(1)));

            Assert.Equal(0, value["count"]);
        }

        [Fact]
        public void FacesThatShareAVertexAreNotCounted()
        {
            IPXVertex shared = Vertex(0f, 0f, 0f);
            Face(0, shared, Vertex(2f, 0f, 0f), Vertex(0f, 2f, 0f));
            Face(1, shared, Vertex(0.5f, 0.5f, 1f), Vertex(0.5f, 0.5f, -1f));

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(A(0), B(1)));

            Assert.Equal(0, value["count"]);
        }

        [Fact]
        public void FacesThatShareAPositionThroughVerticesOfTheirOwnAreNotCounted()
        {
            Face(0, Vertex(0f, 0f, 0f), Vertex(2f, 0f, 0f), Vertex(0f, 2f, 0f));
            Face(1, Vertex(0f, 0f, 0f), Vertex(0.5f, 0.5f, 1f), Vertex(0.5f, 0.5f, -1f));

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(A(0), B(1)));

            Assert.Equal(0, value["count"]);
        }

        [Fact]
        public void FacesThatMeetAlongAnEdgeOfSplitVerticesAreNotCounted()
        {
            Triangle(0, 0, 0, 0, 1, 0, 0, 0, 1, 0);
            Triangle(1, 0, 0, 0, 1, 0, 0, 0, 0, 1);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(A(0), B(1)));

            Assert.Equal(0, value["count"]);
        }

        [Fact]
        public void AFaceWhoseEdgeLiesInsideAnotherFaceIsNotCounted()
        {
            Triangle(0, -2, -2, 0, 2, -2, 0, 0, 2, 0);
            Triangle(1, -1, 0, 0, 1, 0, 0, 0, 0, 1);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(A(0), B(1)));

            Assert.Equal(0, value["count"]);
        }

        [Fact]
        public void FacesThatLieInTheSamePlaneAreNotCounted()
        {
            Triangle(0, -2, -2, 0, 2, -2, 0, 0, 2, 0);
            Triangle(1, -1, -1, 0, 1, -1, 0, 0, 1, 0);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(A(0), B(1)));

            Assert.Equal(0, value["count"]);
        }

        [Fact]
        public void FacesWithoutAreaAreLeftOut()
        {
            Triangle(0, -2, -2, 0, 2, -2, 0, 0, 2, 0);
            Triangle(0, 0, 0, -1, 0, 0, 1, 0, 0, 1);
            Triangle(1, 0, -1, -1, 0, -1, 1, 0, 1, 0);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(A(0), B(1)));

            Assert.Equal(1, value["count"]);
        }

        [Fact]
        public void EveryMaterialOfEitherSideTakesPart()
        {
            Triangle(0, -2, -2, 0, 2, -2, 0, 0, 2, 0);
            Triangle(1, -2, -2, 5, 2, -2, 5, 0, 2, 5);
            Triangle(2, 0, -1, -1, 0, -1, 1, 0, 1, 0);
            Triangle(3, 0, -1, 4, 0, -1, 6, 0, 1, 5);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0, 1 }),
                ComposedEditFixture.Given("otherSurfaceMaterialIndices", new object[] { 2, 3 })));

            Assert.Equal(2, value["count"]);
            IList<IDictionary<string, object>> pairs = Pairs(value);
            Assert.Equal(new[] { 0, 1 }, pairs.Select(p => (int)p["material"]).OrderBy(x => x).ToArray());
            Assert.Equal(new[] { 2, 3 }, pairs.Select(p => (int)p["otherMaterial"]).OrderBy(x => x).ToArray());
        }

        [Fact]
        public void PairsComeDeepestFirstAndCanBePaged()
        {
            Triangle(0, -20, -20, 0, 20, -20, 0, 0, 20, 0);
            Triangle(1, 0, -1, -1, 0, -1, 1, 0, 1, 0);
            Triangle(1, 5, -2, -2, 5, -2, 2, 5, 2, 0);
            Triangle(1, -5, -0.5f, -0.5f, -5, -0.5f, 0.5f, -5, 0.5f, 0);

            IDictionary<string, object> first = ComposedEditFixture.Value(Find(
                A(0), B(1), ComposedEditFixture.Given("limit", 2)));

            Assert.Equal(3, first["count"]);
            Assert.Equal(1, first["faceCount"]);
            Assert.Equal(3, first["otherFaceCount"]);
            Assert.Equal(new[] { 1, 0 }, Pairs(first).Select(p => (int)p["otherFace"]).ToArray());
            Assert.Equal(2, first["nextOffset"]);
            Assert.Equal(2f, (float)first["maxDepth"], 5);

            IDictionary<string, object> rest = ComposedEditFixture.Value(Find(
                A(0), B(1), ComposedEditFixture.Given("offset", 2)));

            Assert.Equal(new[] { 2 }, Pairs(rest).Select(p => (int)p["otherFace"]).ToArray());
            Assert.False(rest.ContainsKey("nextOffset"));
            Assert.Equal(3, rest["count"]);
        }

        [Fact]
        public void PairsOfTheSameDepthComeInTheOrderOfTheirPositions()
        {
            Triangle(0, -20, -20, 0, 20, -20, 0, 0, 20, 0);
            for (int x = 5; x >= 0; x--)
            {
                Triangle(1, x, -1, -1, x, -1, 1, x, 1, 0);
            }

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(A(0), B(1)));

            Assert.Equal(6, value["count"]);
            Assert.Equal(
                new[] { 0, 1, 2, 3, 4, 5 },
                Pairs(value).Select(p => (int)p["otherFace"]).ToArray());
        }

        [Fact]
        public void TheEndWithTheSmallerXIsTheStartEvenWhenItsYIsLarger()
        {
            Triangle(0, -20, -20, 0, 20, -20, 0, 0, 20, 0);
            Triangle(1, -1, 1, -1, -1, 1, 1, 1, -1, 0);

            IDictionary<string, object> pair = Pairs(ComposedEditFixture.Value(Find(A(0), B(1)))).Single();

            AssertPoint(-1f, 1f, 0f, pair["start"]);
            AssertPoint(1f, -1f, 0f, pair["end"]);
            IDictionary<string, object> swapped = Pairs(ComposedEditFixture.Value(Find(A(1), B(0)))).Single();
            AssertPoint(-1f, 1f, 0f, swapped["start"]);
            AssertPoint(1f, -1f, 0f, swapped["end"]);
        }

        [Fact]
        public void WhenXAndYAreEqualTheEndWithTheSmallerZIsTheStart()
        {
            Triangle(0, -2, 0, -2, 2, 0, -2, 0, 0, 2);
            Triangle(1, 0, -1, -1, 0, 1, -1, 0, 0, 1.5f);

            IDictionary<string, object> pair = Pairs(ComposedEditFixture.Value(Find(A(0), B(1)))).Single();

            AssertPoint(0f, 0f, -1f, pair["start"]);
            AssertPoint(0f, 0f, 1.5f, pair["end"]);
        }

        [Fact]
        public void EveryCrossingPairOfARandomSoupIsFoundAndNoOtherPair()
        {
            Random random = new Random(20260930);
            List<float[]> first = Soup(random, 0, 40);
            List<float[]> second = Soup(random, 1, 40);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(A(0), B(1)));

            HashSet<string> found = new HashSet<string>(
                Pairs(value).Select(p => p["face"] + ":" + p["otherFace"]));
            HashSet<string> expected = new HashSet<string>();
            for (int a = 0; a < first.Count; a++)
            {
                for (int b = 0; b < second.Count; b++)
                {
                    if (Crosses(first[a], second[b]) || Crosses(second[b], first[a]))
                    {
                        expected.Add(a + ":" + b);
                    }
                }
            }

            Assert.True(expected.Count > 5, "題材が交差を十分に含まない: " + expected.Count);
            Assert.Equal(expected.OrderBy(x => x).ToArray(), found.OrderBy(x => x).ToArray());
            Assert.Equal(expected.Count, value["count"]);
        }

        [Fact]
        public void TheModelIsReadWithoutBeingChanged()
        {
            Triangle(0, -2, -2, 0, 2, -2, 0, 0, 2, 0);
            Triangle(1, 0, -1, -1, 0, -1, 1, 0, 1, 0);

            Find(A(0), B(1));

            Assert.Equal(0, _fixture.Commits);
        }

        [Fact]
        public void ASideWithoutMaterialsIsRefused()
        {
            Triangle(0, -2, -2, 0, 2, -2, 0, 0, 2, 0);
            Triangle(1, 0, -1, -1, 0, -1, 1, 0, 1, 0);

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ComposedEditFixture.Code(Find(A(0))));
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ComposedEditFixture.Code(Find(B(1))));
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ComposedEditFixture.Code(Find(
                    ComposedEditFixture.Given("surfaceMaterialIndices", new object[0]), B(1))));
        }

        [Fact]
        public void AMaterialOutsideTheListIsRefused()
        {
            Triangle(0, -2, -2, 0, 2, -2, 0, 0, 2, 0);

            IDictionary<string, object> envelope = Find(A(0), B(5));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AMaterialOnBothSidesIsRefused()
        {
            Triangle(0, -2, -2, 0, 2, -2, 0, 0, 2, 0);
            Triangle(1, 0, -1, -1, 0, -1, 1, 0, 1, 0);

            IDictionary<string, object> envelope = Find(
                ComposedEditFixture.Given("surfaceMaterialIndices", new object[] { 0, 1 }), B(1));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void ASideWhoseFacesAllLackAreaIsRefused()
        {
            Triangle(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            Triangle(1, 0, -1, -1, 0, -1, 1, 0, 1, 0);

            IDictionary<string, object> envelope = Find(A(0), B(1));

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void ALimitBelowOneIsRefused()
        {
            Triangle(0, -2, -2, 0, 2, -2, 0, 0, 2, 0);
            Triangle(1, 0, -1, -1, 0, -1, 1, 0, 1, 0);

            IDictionary<string, object> envelope = Find(
                A(0), B(1), ComposedEditFixture.Given("limit", 0));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        private static bool Crosses(float[] from, float[] against)
        {
            for (int edge = 0; edge < 3; edge++)
            {
                int next = (edge + 1) % 3;
                if (Pierces(
                    Corner(from, edge), Corner(from, next), Corner(against, 0), Corner(against, 1), Corner(against, 2)))
                {
                    return true;
                }
            }

            return false;
        }

        private static double[] Corner(float[] triangle, int at)
        {
            return new double[] { triangle[at * 3], triangle[(at * 3) + 1], triangle[(at * 3) + 2] };
        }

        private static bool Pierces(double[] from, double[] to, double[] a, double[] b, double[] c)
        {
            double[] direction = Minus(to, from);
            double[] ab = Minus(b, a);
            double[] ac = Minus(c, a);
            double[] p = Cross(direction, ac);
            double determinant = Dot(ab, p);
            if (determinant == 0)
            {
                return false;
            }

            double[] t = Minus(from, a);
            double u = Dot(t, p) / determinant;
            if (u <= 0 || u >= 1)
            {
                return false;
            }

            double[] q = Cross(t, ab);
            double v = Dot(direction, q) / determinant;
            if (v <= 0 || u + v >= 1)
            {
                return false;
            }

            double along = Dot(ac, q) / determinant;

            return along > 0 && along < 1;
        }

        private static double[] Minus(double[] left, double[] right)
        {
            return new[] { left[0] - right[0], left[1] - right[1], left[2] - right[2] };
        }

        private static double[] Cross(double[] left, double[] right)
        {
            return new[]
            {
                (left[1] * right[2]) - (left[2] * right[1]),
                (left[2] * right[0]) - (left[0] * right[2]),
                (left[0] * right[1]) - (left[1] * right[0]),
            };
        }

        private static double Dot(double[] left, double[] right)
        {
            return (left[0] * right[0]) + (left[1] * right[1]) + (left[2] * right[2]);
        }

        private List<float[]> Soup(Random random, int material, int count)
        {
            List<float[]> made = new List<float[]>();
            for (int at = 0; at < count; at++)
            {
                float[] triangle = new float[9];
                float cx = (float)((random.NextDouble() * 10) - 5);
                float cy = (float)((random.NextDouble() * 10) - 5);
                float cz = (float)((random.NextDouble() * 10) - 5);
                for (int corner = 0; corner < 3; corner++)
                {
                    triangle[corner * 3] = cx + (float)((random.NextDouble() * 5) - 2.5);
                    triangle[(corner * 3) + 1] = cy + (float)((random.NextDouble() * 5) - 2.5);
                    triangle[(corner * 3) + 2] = cz + (float)((random.NextDouble() * 5) - 2.5);
                }

                Triangle(material, triangle);
                made.Add(triangle);
            }

            return made;
        }

        private KeyValuePair<string, object> A(params int[] materials)
        {
            return ComposedEditFixture.Given(
                "surfaceMaterialIndices", materials.Select(m => (object)m).ToArray());
        }

        private KeyValuePair<string, object> B(params int[] materials)
        {
            return ComposedEditFixture.Given(
                "otherSurfaceMaterialIndices", materials.Select(m => (object)m).ToArray());
        }

        private IDictionary<string, object> Find(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelFindSurfaceIntersections.ToolName, ComposedEditFixture.Arguments(given));
        }

        private static IList<IDictionary<string, object>> Pairs(IDictionary<string, object> value)
        {
            return ((object[])value["pairs"]).Cast<IDictionary<string, object>>().ToList();
        }

        private static void AssertPoint(float x, float y, float z, object given)
        {
            object[] point = (object[])given;
            Assert.Equal(x, (float)point[0], 5);
            Assert.Equal(y, (float)point[1], 5);
            Assert.Equal(z, (float)point[2], 5);
        }

        private void Triangle(int material, params float[] corners)
        {
            Face(
                material,
                Vertex(corners[0], corners[1], corners[2]),
                Vertex(corners[3], corners[4], corners[5]),
                Vertex(corners[6], corners[7], corners[8]));
        }

        private void Face(int material, IPXVertex first, IPXVertex second, IPXVertex third)
        {
            while (_fixture.Model.Material.Count <= material)
            {
                _fixture.Model.Material.Add(new FakeMaterial("材質" + _fixture.Model.Material.Count));
            }

            ((FakeMaterial)_fixture.Model.Material[material]).Faces.Add(new FakeFace(first, second, third));
        }

        private IPXVertex Vertex(float x, float y, float z)
        {
            FakeVertex made = new FakeVertex(x, y, z);
            _fixture.Model.Vertex.Add(made);

            return made;
        }
    }
}
