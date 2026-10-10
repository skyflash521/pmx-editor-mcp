using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelSurfaceSectionsToolsTests : IDisposable
    {
        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void ABoxAndASlantedSlabAreCutIntoTheirOutlines()
        {
            BoxAndSlab();

            object[] sections = Sections(new object[]
            {
                Material(0), Material(1),
            }, 0.25, 0.5, 0.75);

            Assert.Equal(6, sections.Length);
            for (int at = 0; at < 3; at++)
            {
                IDictionary<string, object> line = Line(sections[at], 0);
                Assert.Equal(0, Convert.ToInt32(Section(sections[at])["set"]));
                Assert.Equal(true, line["closed"]);
                Assert.Equal(8f, (float)line["length"], 4);
                Assert.Single((object[])Section(sections[at])["lines"]);
            }

            AssertPoints(Line(sections[3], 0), new[] { -1f, 1f }, new[] { -0.5f, 1f }, new[] { 1f, 1f });
            AssertPoints(Line(sections[4], 0), new[] { -1f, 2f }, new[] { 0f, 2f }, new[] { 1f, 2f });
            AssertPoints(Line(sections[5], 0), new[] { -1f, 3f }, new[] { 0.5f, 3f }, new[] { 1f, 3f });
            Assert.Equal(false, Line(sections[3], 0)["closed"]);
        }

        [Fact]
        public void APositionOutsideTheSurfaceHasNoLines()
        {
            BoxAndSlab();

            object[] sections = Sections(new object[] { Material(1) }, 1.5, -0.25, 0.5);

            Assert.Empty((object[])Section(sections[0])["lines"]);
            Assert.Empty((object[])Section(sections[1])["lines"]);
            Assert.Single((object[])Section(sections[2])["lines"]);
        }

        [Fact]
        public void VertexSetsCutOnlyTheFacesWhoseCornersAreAllInTheSet()
        {
            BoxAndSlab();

            object[] sections = Sections(new object[]
            {
                Vertices(0, 1, 2, 3, 4, 5, 6, 7), Vertices(8, 9, 10),
            }, 0.5);

            Assert.Equal(8f, (float)Line(sections[0], 0)["length"], 4);
            AssertPoints(Line(sections[1], 0), new[] { 0f, 2f }, new[] { 1f, 2f });
        }

        [Fact]
        public void TheAcrossDirectionSetsTheSignOfBothCoordinates()
        {
            BoxAndSlab();

            object[] sections = Sections(new object[] { Material(1) }, new object[] { 3.0, 0.0, -2.0 }, 0.5);

            AssertPoints(Line(sections[0], 0), new[] { -1f, -2f }, new[] { 0f, -2f }, new[] { 1f, -2f });
        }

        [Fact]
        public void WithoutAtItCutsAtEachVertexPositionOfEachSet()
        {
            Bipyramid();

            object[] sections = SectionsEverywhere(new object[] { Material(0), Vertices(0, 2, 3, 4, 5) });

            Assert.Equal(5, sections.Length);
            Assert.Equal(new[] { 0, 0, 0, 1, 1 }, sections.Select(s => Convert.ToInt32(Section(s)["set"])));
            Assert.Equal(new[] { 0f, 0.5f, 1f, 0f, 0.5f }, sections.Select(s => (float)Section(s)["s"]));
            Assert.Empty((object[])Section(sections[0])["lines"]);
            AssertPoints(
                Line(sections[1], 0), new[] { -1f, -1f }, new[] { 1f, -1f }, new[] { 1f, 1f }, new[] { -1f, 1f });
            Assert.Equal(true, Line(sections[1], 0)["closed"]);
            Assert.Empty((object[])Section(sections[2])["lines"]);
            Assert.Empty((object[])Section(sections[3])["lines"]);
            AssertPoints(
                Line(sections[4], 0), new[] { -1f, -1f }, new[] { 1f, -1f }, new[] { 1f, 1f }, new[] { -1f, 1f });
        }

        [Fact]
        public void AnEdgeThroughTheVerticesOfASlantedAxisPositionIsTheSection()
        {
            IPXVertex[] v = Place(new[]
            {
                new[] { 0f, 1f, 0f }, new[] { 2f, 0f, 0f }, new[] { 0f, 0f, 0f },
            });
            FakeMaterial triangle = new FakeMaterial("三角形");
            triangle.Faces.Add(new FakeFace(v[0], v[1], v[2]));
            _fixture.Model.Material.Add(triangle);
            Dictionary<string, object> arguments = Arguments(new object[] { Vertices(0, 1, 2) }, (object[])null);
            arguments["to"] = new object[] { 1.0, 2.0, 0.0 };
            arguments["across"] = new object[] { 0.0, 0.0, 1.0 };

            IDictionary<string, object> value = ComposedEditFixture.Value(_fixture.Call(
                ModelFindSurfaceSections.ToolName,
                ComposedEditFixture.Arguments(arguments.Select(p => ComposedEditFixture.Given(p.Key, p.Value)).ToArray())));

            object[] sections = (object[])value["sections"];
            Assert.Equal(2, sections.Length);
            Assert.Equal(0.4f, (float)Section(sections[1])["s"], 4);
            Assert.Equal(Math.Sqrt(5), (float)Line(sections[1], 0)["length"], 4);
            float root = (float)Math.Sqrt(5);
            AssertPoints(Line(sections[1], 0), new[] { 0f, -4f / root }, new[] { 0f, 1f / root });
        }

        [Theory]
        [MemberData(nameof(RefusedArguments))]
        public void MalformedArgumentsAreRefused(string name, object given)
        {
            BoxAndSlab();
            Dictionary<string, object> arguments = Arguments(new object[] { Vertices(0, 1, 2) }, 0.75);
            if (given == null)
            {
                arguments.Remove(name);
            }
            else
            {
                arguments[name] = given;
            }

            IDictionary<string, object> envelope = _fixture.Call(
                ModelFindSurfaceSections.ToolName,
                ComposedEditFixture.Arguments(arguments.Select(p => ComposedEditFixture.Given(p.Key, p.Value)).ToArray()));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        public static IEnumerable<object[]> RefusedArguments()
        {
            yield return new object[] { "sets", new object[0] };
            yield return new object[] { "sets", null };
            yield return new object[] { "sets", new object[] { new Dictionary<string, object>() } };
            yield return new object[]
            {
                "sets",
                new object[]
                {
                    new Dictionary<string, object>
                    {
                        { "indices", new object[] { 0, 1, 2 } }, { "materialIndices", new object[] { 0 } },
                    },
                },
            };
            yield return new object[] { "at", new object[0] };
            yield return new object[] { "from", new object[] { 4.0, 0.0, 0.0 } };
            yield return new object[] { "across", new object[] { 2.0, 0.0, 0.0 } };
            yield return new object[] { "across", new object[] { 0.0, 0.0, 0.0 } };
        }

        [Fact]
        public void ATooLargeResultIsRefused()
        {
            BoxAndSlab();

            IDictionary<string, object> envelope = _fixture.Call(
                ModelFindSurfaceSections.ToolName,
                ComposedEditFixture.Arguments(
                    Arguments(new object[] { Material(0) }, Enumerable.Repeat(0.5, 5000).Cast<object>().ToArray())
                        .Select(p => ComposedEditFixture.Given(p.Key, p.Value)).ToArray()),
                ResponseBudget.MinimumChars);

            Assert.Equal(ToolEnvelope.ResponseTooLarge, ComposedEditFixture.Code(envelope));
        }

        private static Dictionary<string, object> Material(int at)
        {
            return new Dictionary<string, object> { { "materialIndices", new object[] { at } } };
        }

        private static Dictionary<string, object> Vertices(params int[] indices)
        {
            return new Dictionary<string, object> { { "indices", indices.Cast<object>().ToArray() } };
        }

        private static Dictionary<string, object> Arguments(object[] sets, params object[] at)
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "from", new object[] { 0.0, 0.0, 0.0 } },
                { "to", new object[] { 4.0, 0.0, 0.0 } },
                { "across", new object[] { 3.0, 0.0, 2.0 } },
                { "sets", sets },
            };
            if (at != null)
            {
                arguments["at"] = at;
            }

            return arguments;
        }

        private object[] Sections(object[] sets, params double[] at)
        {
            return Sections(sets, new object[] { 3.0, 0.0, 2.0 }, at);
        }

        private object[] Sections(object[] sets, object[] across, params double[] at)
        {
            Dictionary<string, object> arguments = Arguments(sets, at == null ? null : at.Cast<object>().ToArray());
            arguments["across"] = across;
            IDictionary<string, object> value = ComposedEditFixture.Value(_fixture.Call(
                ModelFindSurfaceSections.ToolName,
                ComposedEditFixture.Arguments(arguments.Select(p => ComposedEditFixture.Given(p.Key, p.Value)).ToArray())));

            return (object[])value["sections"];
        }

        private object[] SectionsEverywhere(object[] sets)
        {
            return Sections(sets, new object[] { 3.0, 0.0, 2.0 }, (double[])null);
        }

        private static IDictionary<string, object> Section(object section)
        {
            return (IDictionary<string, object>)section;
        }

        private static IDictionary<string, object> Line(object section, int at)
        {
            return (IDictionary<string, object>)((object[])Section(section)["lines"])[at];
        }

        private static void AssertPoints(IDictionary<string, object> line, params float[][] expected)
        {
            object[] points = (object[])line["points"];
            Assert.Equal(expected.Length, points.Length);
            for (int at = 0; at < expected.Length; at++)
            {
                object[] point = (object[])points[at];
                Assert.Equal(expected[at][0], (float)point[0], 4);
                Assert.Equal(expected[at][1], (float)point[1], 4);
            }
        }

        private void BoxAndSlab()
        {
            float[][] spots =
            {
                new[] { 0f, 0f, -1f }, new[] { 4f, 0f, -1f }, new[] { 4f, 2f, -1f }, new[] { 0f, 2f, -1f },
                new[] { 0f, 0f, 1f }, new[] { 4f, 0f, 1f }, new[] { 4f, 2f, 1f }, new[] { 0f, 2f, 1f },
                new[] { 0f, 0f, -1f }, new[] { 0f, 0f, 1f }, new[] { 4f, 4f, 1f }, new[] { 4f, 4f, -1f },
            };
            IPXVertex[] v = Place(spots);
            FakeMaterial box = new FakeMaterial("箱");
            int[][] faces =
            {
                new[] { 0, 2, 1 }, new[] { 0, 3, 2 }, new[] { 4, 5, 6 }, new[] { 4, 6, 7 },
                new[] { 0, 1, 5 }, new[] { 0, 5, 4 }, new[] { 3, 7, 6 }, new[] { 3, 6, 2 },
                new[] { 0, 4, 7 }, new[] { 0, 7, 3 }, new[] { 1, 2, 6 }, new[] { 1, 6, 5 },
            };
            foreach (int[] face in faces)
            {
                box.Faces.Add(new FakeFace(v[face[0]], v[face[1]], v[face[2]]));
            }

            FakeMaterial slab = new FakeMaterial("板");
            slab.Faces.Add(new FakeFace(v[8], v[9], v[10]));
            slab.Faces.Add(new FakeFace(v[8], v[10], v[11]));
            _fixture.Model.Material.Add(box);
            _fixture.Model.Material.Add(slab);
        }

        private void Bipyramid()
        {
            IPXVertex[] v = Place(new[]
            {
                new[] { 0f, 0f, 0f }, new[] { 4f, 0f, 0f }, new[] { 2f, -1f, -1f },
                new[] { 2f, -1f, 1f }, new[] { 2f, 1f, 1f }, new[] { 2f, 1f, -1f },
            });
            FakeMaterial shape = new FakeMaterial("双角錐");
            int[][] faces =
            {
                new[] { 0, 2, 3 }, new[] { 0, 3, 4 }, new[] { 0, 4, 5 }, new[] { 0, 5, 2 },
                new[] { 1, 3, 2 }, new[] { 1, 4, 3 }, new[] { 1, 5, 4 }, new[] { 1, 2, 5 },
            };
            foreach (int[] face in faces)
            {
                shape.Faces.Add(new FakeFace(v[face[0]], v[face[1]], v[face[2]]));
            }

            _fixture.Model.Material.Add(shape);
        }

        private IPXVertex[] Place(float[][] spots)
        {
            IPXVertex[] made = new IPXVertex[spots.Length];
            for (int at = 0; at < spots.Length; at++)
            {
                made[at] = new FakeVertex(spots[at][0], spots[at][1], spots[at][2]);
                _fixture.Model.Vertex.Add(made[at]);
            }

            return made;
        }
    }
}
