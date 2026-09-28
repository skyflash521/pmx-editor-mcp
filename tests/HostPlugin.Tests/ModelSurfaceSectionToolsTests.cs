using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelSurfaceSectionToolsTests : IDisposable
    {
        private static readonly double RidgeLength = 2 * Math.Sqrt(2);

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void CuttingABentSurfaceReturnsTheLineAlongItAndItsLength()
        {
            Ridge(0f, false);

            IDictionary<string, object> value = ComposedEditFixture.Value(CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 })));

            object[] lines = (object[])value["lines"];
            Assert.Single(lines);
            IDictionary<string, object> line = (IDictionary<string, object>)lines[0];
            Assert.Equal(false, line["closed"]);
            Assert.Equal(RidgeLength, (float)line["length"], 4);
            object[] points = (object[])line["points"];
            AssertEnds(points, 0f, 0f, 0f, 0f, 0f, 2f);
            Assert.Contains(points, p => Near(p, 0f, 1f, 1f));
        }

        [Fact]
        public void CuttingATubeReturnsAClosedLine()
        {
            Tube();

            IDictionary<string, object> value = ComposedEditFixture.Value(CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 })));

            object[] lines = (object[])value["lines"];
            Assert.Single(lines);
            IDictionary<string, object> line = (IDictionary<string, object>)lines[0];
            Assert.Equal(true, line["closed"]);
            Assert.Equal(8f, (float)line["length"], 4);
        }

        [Fact]
        public void DoubleSidedFacesAndSplitVerticesStillMakeOneLine()
        {
            Ridge(0f, true);

            IDictionary<string, object> value = ComposedEditFixture.Value(CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 })));

            object[] lines = (object[])value["lines"];
            Assert.Single(lines);
            Assert.Equal(RidgeLength, (float)((IDictionary<string, object>)lines[0])["length"], 4);
        }

        [Fact]
        public void ALinePassingThroughAVertexOnThePlaneStaysOneLine()
        {
            IPXVertex on = Vertex(0f, 0f, 0f);
            IPXVertex first = Vertex(-1f, 1f, 0f);
            IPXVertex second = Vertex(1f, 2f, 0f);
            IPXVertex third = Vertex(-1f, 3f, 0f);
            FakeMaterial fan = Roof();
            fan.Faces.Add(new FakeFace(on, first, second));
            fan.Faces.Add(new FakeFace(on, second, third));

            IDictionary<string, object> value = ComposedEditFixture.Value(CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 })));

            object[] lines = (object[])value["lines"];
            Assert.Single(lines);
            IDictionary<string, object> line = (IDictionary<string, object>)lines[0];
            Assert.Equal(4f, (float)line["length"], 4);
            AssertEnds((object[])line["points"], 0f, 1.5f, 0f, 0f, 2.5f, 0f);
        }

        [Fact]
        public void AnEdgeLyingOnThePlaneIsPartOfTheLine()
        {
            FakeMaterial face = Roof();
            face.Faces.Add(new FakeFace(Vertex(0f, 0f, 0f), Vertex(0f, 0f, 1f), Vertex(1f, 0f, 0f)));

            IDictionary<string, object> value = ComposedEditFixture.Value(CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 })));

            object[] lines = (object[])value["lines"];
            Assert.Single(lines);
            Assert.Equal(1f, (float)((IDictionary<string, object>)lines[0])["length"], 4);
        }

        [Fact]
        public void AnEdgeLyingOnASlantedPlaneIsPartOfTheLine()
        {
            FakeMaterial face = Roof();
            face.Faces.Add(new FakeFace(Vertex(3f, 0f, -1f), Vertex(0f, 3f, -2f), Vertex(0f, 0f, -2f)));

            IDictionary<string, object> value = ComposedEditFixture.Value(Cut(
                ComposedEditFixture.Given("planePoint", new object[] { 0.0, 0.0, 0.0 }),
                ComposedEditFixture.Given("planeNormal", new object[] { 1.0, 2.0, 3.0 }),
                ComposedEditFixture.Given("materialIndices", new object[] { 0 })));

            object[] lines = (object[])value["lines"];
            Assert.Single(lines);
            Assert.Equal(Math.Sqrt(19), (float)((IDictionary<string, object>)lines[0])["length"], 4);
        }

        [Theory]
        [InlineData(1000.0009765625f, 999.9990234375f, 1000.00048828125f, 0.6871842709)]
        [InlineData(1000.00018310546875f, 999.99981689453125f, 1000.0001220703125f, 0.6082762530)]
        public void VerticesOffThePlaneByAFewStepsOfTheirPrecisionAreStillCut(
            float first, float second, float third, double length)
        {
            FakeMaterial face = Roof();
            face.Faces.Add(new FakeFace(Vertex(first, 0f, 0f), Vertex(second, 1f, 0f), Vertex(third, 0f, 1f)));

            IDictionary<string, object> value = ComposedEditFixture.Value(Cut(
                ComposedEditFixture.Given("planePoint", new object[] { 1000.0, 0.0, 0.0 }),
                ComposedEditFixture.Given("planeNormal", new object[] { 1.0, 0.0, 0.0 }),
                ComposedEditFixture.Given("materialIndices", new object[] { 0 })));

            object[] lines = (object[])value["lines"];
            Assert.Single(lines);
            Assert.Equal(length, (float)((IDictionary<string, object>)lines[0])["length"], 4);
        }

        [Fact]
        public void ASpacingTooFineForTheResponseIsRefusedWithoutPlacingThePoints()
        {
            Ridge(0f, false);

            IDictionary<string, object> envelope = CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("spacing", 1e-20));

            Assert.Equal(ToolEnvelope.ResponseTooLarge, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void TheLineBetweenTwoPointsOfAClosedLineTakesTheShorterSide()
        {
            Tube();

            IDictionary<string, object> value = ComposedEditFixture.Value(CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("from", new object[] { 0.0, -1.0, -0.5 }),
                ComposedEditFixture.Given("to", new object[] { 0.0, 1.0, -0.5 })));

            Assert.Equal(3f, (float)value["length"], 4);
            Assert.Equal(8f, (float)value["lineLength"], 4);
            Assert.Equal(true, value["closed"]);
            object[] points = (object[])value["points"];
            AssertEnds(points, 0f, -1f, -0.5f, 0f, 1f, -0.5f);
            Assert.Contains(points, p => Near(p, 0f, -1f, -1f));
        }

        [Fact]
        public void APointToPassThroughChoosesTheSideOfAClosedLine()
        {
            Tube();

            IDictionary<string, object> value = ComposedEditFixture.Value(CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("from", new object[] { 0.0, -1.0, -0.5 }),
                ComposedEditFixture.Given("to", new object[] { 0.0, 1.0, -0.5 }),
                ComposedEditFixture.Given("via", new object[] { 0.0, 0.0, 1.2 })));

            Assert.Equal(5f, (float)value["length"], 4);
            Assert.Contains((object[])value["points"], p => Near(p, 0f, 1f, 1f));
        }

        [Fact]
        public void PointsOffTheLineAreTakenToTheirNearestPointsOnIt()
        {
            Ridge(0f, false);

            IDictionary<string, object> value = ComposedEditFixture.Value(CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("from", new object[] { 0.3, 1.0, 1.0 }),
                ComposedEditFixture.Given("to", new object[] { 0.0, -0.1, 2.0 })));

            Assert.Equal(RidgeLength / 2, (float)value["length"], 4);
            Assert.Equal(0.3f, (float)value["fromGap"], 4);
            Assert.Equal(0.1f, (float)value["toGap"], 4);
            AssertEnds((object[])value["points"], 0f, 1f, 1f, 0f, 0f, 2f);
        }

        [Fact]
        public void SpacingThinsThePointsWithoutChangingTheLength()
        {
            Ridge(0f, false);

            IDictionary<string, object> value = ComposedEditFixture.Value(CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("spacing", 0.5)));

            IDictionary<string, object> line = (IDictionary<string, object>)((object[])value["lines"])[0];
            Assert.Equal(RidgeLength, (float)line["length"], 4);
            object[] points = (object[])line["points"];
            Assert.Equal(7, points.Length);
            AssertEnds(points, 0f, 0f, 0f, 0f, 0f, 2f);
            Assert.True(Near(points[1], 0f, (float)(0.5 / Math.Sqrt(2)), (float)(0.5 / Math.Sqrt(2))));
        }

        [Fact]
        public void EndsOnDifferentLinesAreRefused()
        {
            Ridge(0f, false);
            Ridge(10f, false);

            IDictionary<string, object> envelope = CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("from", new object[] { 0.0, 0.0, 0.0 }),
                ComposedEditFixture.Given("to", new object[] { 0.0, 0.0, 12.0 }));

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void APlaneMissingTheFacesIsRefused()
        {
            Ridge(0f, false);

            IDictionary<string, object> envelope = Cut(
                ComposedEditFixture.Given("planePoint", new object[] { 5.0, 0.0, 0.0 }),
                ComposedEditFixture.Given("planeNormal", new object[] { 1.0, 0.0, 0.0 }),
                ComposedEditFixture.Given("materialIndices", new object[] { 0 }));

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void TheModelAndTheSelectionAreLeftAsTheyWere()
        {
            Ridge(0f, false);
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0 };

            ComposedEditFixture.Value(CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 })));

            Assert.Equal(new[] { 0 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(0, _fixture.Commits);
        }

        [Fact]
        public void ALineTooLargeForTheResponseIsRefused()
        {
            Ridge(0f, false);

            IDictionary<string, object> envelope = _fixture.Call(
                ModelFindSurfaceSection.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("planePoint", new object[] { 0.0, 0.0, 0.0 }),
                    ComposedEditFixture.Given("planeNormal", new object[] { 1.0, 0.0, 0.0 }),
                    ComposedEditFixture.Given("materialIndices", new object[] { 0 }),
                    ComposedEditFixture.Given("spacing", 0.0001)),
                ResponseBudget.MinimumChars);

            Assert.Equal(ToolEnvelope.ResponseTooLarge, ComposedEditFixture.Code(envelope));
        }

        [Theory]
        [MemberData(nameof(MalformedArguments))]
        public void MalformedArgumentsAreRefused(string name, object given)
        {
            Ridge(0f, false);
            Dictionary<string, object> arguments = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "planePoint", new object[] { 0.0, 0.0, 0.0 } },
                { "planeNormal", new object[] { 1.0, 0.0, 0.0 } },
                { "materialIndices", new object[] { 0 } },
            };
            if (given == null)
            {
                arguments.Remove(name);
            }
            else
            {
                arguments[name] = given;
            }

            IDictionary<string, object> envelope = _fixture.Call(
                ModelFindSurfaceSection.ToolName, ComposedEditFixture.Arguments(Pairs(arguments)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void OnlyOneEndIsRefused()
        {
            Ridge(0f, false);

            IDictionary<string, object> envelope = CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("from", new object[] { 0.0, 0.0, 0.0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void APointToPassThroughWithoutEndsIsRefused()
        {
            Ridge(0f, false);

            IDictionary<string, object> envelope = CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("via", new object[] { 0.0, 0.0, 0.0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AMaterialOutsideTheModelIsRefused()
        {
            Ridge(0f, false);

            IDictionary<string, object> envelope = CutAcross(
                ComposedEditFixture.Given("materialIndices", new object[] { 4 }));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
        }

        public static IEnumerable<object[]> MalformedArguments()
        {
            yield return new object[] { "planePoint", null };
            yield return new object[] { "planeNormal", null };
            yield return new object[] { "planeNormal", new object[] { 0.0, 0.0, 0.0 } };
            yield return new object[] { "materialIndices", null };
            yield return new object[] { "materialIndices", new object[0] };
            yield return new object[] { "spacing", 0.0 };
            yield return new object[] { "spacing", "a" };
        }

        private IDictionary<string, object> Cut(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ModelFindSurfaceSection.ToolName, ComposedEditFixture.Arguments(given));
        }

        private IDictionary<string, object> CutAcross(params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("planePoint", new object[] { 0.0, 0.0, 0.0 }),
                ComposedEditFixture.Given("planeNormal", new object[] { 2.0, 0.0, 0.0 }),
            };
            all.AddRange(given);

            return Cut(all.ToArray());
        }

        private static KeyValuePair<string, object>[] Pairs(IDictionary<string, object> arguments)
        {
            List<KeyValuePair<string, object>> pairs = new List<KeyValuePair<string, object>>();
            foreach (KeyValuePair<string, object> pair in arguments)
            {
                pairs.Add(ComposedEditFixture.Given(pair.Key, pair.Value));
            }

            return pairs.ToArray();
        }

        private void Ridge(float offset, bool seamed)
        {
            FakeMaterial roof = Roof();
            IPXVertex[] up = Strip(new[] { new[] { 0f, offset }, new[] { 1f, offset + 1f } });
            IPXVertex[] down = Strip(new[] { new[] { 1f, offset + 1f }, new[] { 0f, offset + 2f } });
            if (!seamed)
            {
                down[0] = up[2];
                down[1] = up[3];
            }

            Quads(roof, up, seamed);
            Quads(roof, down, seamed);
        }

        private void Tube()
        {
            FakeMaterial tube = Roof();
            IPXVertex[] ring = Strip(new[]
            {
                new[] { -1f, -1f },
                new[] { 1f, -1f },
                new[] { 1f, 1f },
                new[] { -1f, 1f },
                new[] { -1f, -1f },
            });
            ring[8] = ring[0];
            ring[9] = ring[1];
            Quads(tube, ring, false);
        }

        private FakeMaterial Roof()
        {
            if (_fixture.Model.Material.Count == 0)
            {
                _fixture.Model.Material.Add(new FakeMaterial("面"));
            }

            return (FakeMaterial)_fixture.Model.Material[0];
        }

        private IPXVertex[] Strip(float[][] profile)
        {
            IPXVertex[] made = new IPXVertex[profile.Length * 2];
            for (int at = 0; at < profile.Length; at++)
            {
                made[at * 2] = Vertex(-1f, profile[at][0], profile[at][1]);
                made[(at * 2) + 1] = Vertex(1f, profile[at][0], profile[at][1]);
            }

            return made;
        }

        private static void Quads(FakeMaterial material, IPXVertex[] strip, bool doubled)
        {
            for (int at = 0; at + 3 < strip.Length; at += 2)
            {
                material.Faces.Add(new FakeFace(strip[at], strip[at + 1], strip[at + 3]));
                material.Faces.Add(new FakeFace(strip[at], strip[at + 3], strip[at + 2]));
                if (doubled)
                {
                    material.Faces.Add(new FakeFace(strip[at], strip[at + 3], strip[at + 1]));
                    material.Faces.Add(new FakeFace(strip[at], strip[at + 2], strip[at + 3]));
                }
            }
        }

        private static void AssertEnds(object[] points, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            bool forward = Near(points[0], x0, y0, z0) && Near(points[points.Length - 1], x1, y1, z1);
            bool backward = Near(points[0], x1, y1, z1) && Near(points[points.Length - 1], x0, y0, z0);
            Assert.True(forward || backward);
        }

        private static bool Near(object given, float x, float y, float z)
        {
            object[] point = (object[])given;

            return Math.Abs((float)point[0] - x) < 1e-4
                && Math.Abs((float)point[1] - y) < 1e-4
                && Math.Abs((float)point[2] - z) < 1e-4;
        }

        private IPXVertex Vertex(float x, float y, float z)
        {
            FakeVertex made = new FakeVertex(x, y, z);
            _fixture.Model.Vertex.Add(made);

            return made;
        }
    }
}
