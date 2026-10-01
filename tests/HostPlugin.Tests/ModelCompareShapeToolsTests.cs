using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelCompareShapeToolsTests : IDisposable
    {
        private const string ToolName = "model_compare_shape";

        private static readonly V3[] QuadBase =
        {
            new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(0f, 1f, 0f), new V3(1f, 1f, 0f),
        };

        private static readonly int[][] QuadFaces = { new[] { 0, 1, 2 }, new[] { 1, 3, 2 } };

        private static readonly V3[] HingeBase =
        {
            new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(0f, 1f, 0f), new V3(0f, -1f, 0f),
            new V3(-1f, -1f, 0f),
        };

        private static readonly int[][] HingeFaces =
        {
            new[] { 0, 1, 2 }, new[] { 1, 0, 3 }, new[] { 0, 4, 3 },
        };

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        private FakePmx _held;

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheLargestStretchIsReportedWithTheVerticesOfItsEdge()
        {
            int handle = Pair(QuadBase, Moved(QuadBase, 3, 2f, 1f, 0f), QuadFaces);

            IDictionary<string, object> value = ComposedEditFixture.Value(Compare(
                handle, ComposedEditFixture.Given("all", true)));

            Assert.Equal(5, value["edgeCount"]);
            Assert.Equal(2, value["faceCount"]);
            Assert.Equal(1f, (float)value["minStretch"], 5);
            Assert.Equal(2f, (float)value["maxStretch"], 5);
            Assert.Equal(new object[] { 2, 3 }, (object[])value["maxStretchVertices"]);
            object[] points = (object[])value["maxStretchPoints"];
            AssertPoint(0f, 1f, 0f, points[0]);
            AssertPoint(2f, 1f, 0f, points[1]);
        }

        [Fact]
        public void AnEdgeThatShrankGivesTheSmallestRatio()
        {
            int handle = Pair(QuadBase, Moved(QuadBase, 3, 0.5f, 1f, 0f), QuadFaces);

            IDictionary<string, object> value = ComposedEditFixture.Value(Compare(
                handle, ComposedEditFixture.Given("all", true)));

            Assert.Equal(0.5f, (float)value["minStretch"], 5);
            Assert.Equal((float)Math.Sqrt(1.25), (float)value["maxStretch"], 5);
            Assert.Equal(new object[] { 1, 3 }, (object[])value["maxStretchVertices"]);
        }

        [Fact]
        public void TheStretchDistributionCountsTheEdgesAtLeastAsLongAsEachThresholdInTheOrderGiven()
        {
            int handle = Pair(QuadBase, Moved(QuadBase, 3, 2f, 1f, 0f), QuadFaces);

            IDictionary<string, object> value = ComposedEditFixture.Value(Compare(
                handle,
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("stretchThresholds", new object[] { 1.5, 3.0, 1.2, 2.0 })));

            object[] distribution = (object[])value["stretchDistribution"];
            Assert.Equal(4, distribution.Length);
            AssertLimit(1.5f, 1, distribution[0]);
            AssertLimit(3f, 0, distribution[1]);
            AssertLimit(1.2f, 2, distribution[2]);
            AssertLimit(2f, 1, distribution[3]);
        }

        [Fact]
        public void AnEdgeSharedByTwoFacesIsCountedOnce()
        {
            int handle = Pair(QuadBase, QuadBase, QuadFaces);

            IDictionary<string, object> value = ComposedEditFixture.Value(Compare(
                handle,
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("stretchThresholds", new object[] { 1.0 })));

            Assert.Equal(5, value["edgeCount"]);
            AssertLimit(1f, 5, ((object[])value["stretchDistribution"])[0]);
            Assert.Equal(0f, (float)value["maxBend"], 5);
            Assert.Equal(0, value["flippedCount"]);
        }

        [Fact]
        public void TheLargestChangeOfTheAngleBetweenNeighbouringFacesIsReportedWithTheFacesAndTheirPoints()
        {
            int handle = Pair(HingeBase, Moved(HingeBase, 3, 0f, -1f, 1f), HingeFaces);

            IDictionary<string, object> value = ComposedEditFixture.Value(Compare(
                handle, ComposedEditFixture.Given("all", true)));

            Assert.Equal(45f, (float)value["maxBend"], 3);
            object[] faces = (object[])value["maxBendFaces"];
            Assert.Equal(2, faces.Length);
            AssertFace(0, 0, new[] { new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(0f, 1f, 0f) }, faces[0]);
            AssertFace(0, 1, new[] { new V3(1f, 0f, 0f), new V3(0f, 0f, 0f), new V3(0f, -1f, 1f) }, faces[1]);
            Assert.Equal(0, value["flippedCount"]);
        }

        [Fact]
        public void TheBendDistributionCountsTheFacePairsOverEachThresholdInDegreesInTheOrderGiven()
        {
            int handle = Pair(HingeBase, Moved(HingeBase, 3, 0f, -1f, 1f), HingeFaces);

            IDictionary<string, object> value = ComposedEditFixture.Value(Compare(
                handle,
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("bendThresholds", new object[] { 40.0, 30.0, 50.0 })));

            Assert.Equal(2, value["facePairCount"]);
            object[] distribution = (object[])value["bendDistribution"];
            Assert.Equal(3, distribution.Length);
            AssertLimit(40f, 1, distribution[0]);
            AssertLimit(30f, 2, distribution[1]);
            AssertLimit(50f, 0, distribution[2]);
        }

        [Fact]
        public void AFoldThatWasAlreadyInTheCopyIsNotABend()
        {
            V3[] folded = Moved(HingeBase, 3, 0f, -1f, 1f);
            int handle = Pair(folded, folded, HingeFaces);

            IDictionary<string, object> value = ComposedEditFixture.Value(Compare(
                handle,
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("bendThresholds", new object[] { 1.0 })));

            Assert.Equal(0f, (float)value["maxBend"], 3);
            AssertLimit(1f, 0, ((object[])value["bendDistribution"])[0]);
            Assert.Equal(0, value["flippedCount"]);
        }

        [Fact]
        public void TheBendIsTheChangeFromTheAngleTheCopyAlreadyHad()
        {
            int handle = Pair(
                Moved(HingeBase, 3, 0f, -1f, 1f), Moved(HingeBase, 3, 0f, 0f, 1f), HingeFaces);

            IDictionary<string, object> value = ComposedEditFixture.Value(Compare(
                handle, ComposedEditFixture.Given("all", true)));

            Assert.Equal(45f, (float)value["maxBend"], 3);
            object[] faces = (object[])value["maxBendFaces"];
            Assert.Equal(0, ((IDictionary<string, object>)faces[0])["face"]);
            Assert.Equal(1, ((IDictionary<string, object>)faces[1])["face"]);
        }

        [Fact]
        public void AFaceTurnedOverIsCountedAsFlipped()
        {
            int handle = Pair(HingeBase, Moved(HingeBase, 3, 0f, 1f, 0f), HingeFaces);

            IDictionary<string, object> value = ComposedEditFixture.Value(Compare(
                handle, ComposedEditFixture.Given("all", true)));

            Assert.Equal(2, value["flippedCount"]);
            Assert.Equal(180f, (float)value["maxBend"], 3);
        }

        [Fact]
        public void OnlyTheFacesOfTheGivenMaterialsAreMeasured()
        {
            V3[] baseline = QuadBase.Concat(Shift(QuadBase, 10f)).ToArray();
            int handle = Pair(
                baseline,
                Moved(baseline, 3, 2f, 1f, 0f),
                QuadFaces,
                new[] { new[] { 4, 5, 6 }, new[] { 5, 7, 6 } });

            IDictionary<string, object> value = ComposedEditFixture.Value(Compare(
                handle, ComposedEditFixture.Given("materialIndices", new object[] { 1 })));

            Assert.Equal(2, value["faceCount"]);
            Assert.Equal(5, value["edgeCount"]);
            Assert.Equal(1f, (float)value["maxStretch"], 5);
        }

        [Fact]
        public void OnlyTheFacesWhoseThreeVerticesAreAllGivenAreMeasured()
        {
            int handle = Pair(QuadBase, Moved(QuadBase, 3, 2f, 1f, 0f), QuadFaces);

            IDictionary<string, object> value = ComposedEditFixture.Value(Compare(
                handle, ComposedEditFixture.Given("indices", new object[] { 0, 1, 2 })));

            Assert.Equal(1, value["faceCount"]);
            Assert.Equal(3, value["edgeCount"]);
            Assert.Equal(1f, (float)value["maxStretch"], 5);
        }

        [Fact]
        public void WithoutThresholdsThereAreNoDistributions()
        {
            int handle = Pair(QuadBase, QuadBase, QuadFaces);

            IDictionary<string, object> value = ComposedEditFixture.Value(Compare(
                handle, ComposedEditFixture.Given("all", true)));

            Assert.False(value.ContainsKey("stretchDistribution"));
            Assert.False(value.ContainsKey("bendDistribution"));
        }

        [Fact]
        public void ComparingChangesNeitherTheModelNorTheCopy()
        {
            int handle = Pair(QuadBase, Moved(QuadBase, 3, 2f, 1f, 0f), QuadFaces);

            ComposedEditFixture.Value(Compare(handle, ComposedEditFixture.Given("all", true)));

            Assert.Equal(0, _fixture.Commits);
            Assert.Equal(2f, _fixture.Model.Vertex[3].Position.X, 5);
            Assert.Equal(4, _held.Vertex.Count);
            Assert.Equal(2, ((FakeMaterial)_held.Material[0]).Faces.Count);
            for (int at = 0; at < 4; at++)
            {
                AssertPoint(
                    QuadBase[at].X, QuadBase[at].Y, QuadBase[at].Z, Position(_held.Vertex[at]));
            }
        }

        [Theory]
        [InlineData("vertex")]
        [InlineData("face")]
        [InlineData("bone")]
        public void ACopyWhoseCountsDifferFromTheModelIsRefused(string differing)
        {
            int same = Pair(QuadBase, QuadBase, QuadFaces);
            IDictionary<string, object> found = ComposedEditFixture.Value(Compare(
                same, ComposedEditFixture.Given("all", true)));

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
                    copy.Bone.Add(new FakeBone("ボーン"));
                    break;
            }

            int handle = _fixture.Handles.Issue(typeof(IPXPmx).FullName, copy, () => { });
            IDictionary<string, object> envelope = Compare(
                handle, ComposedEditFixture.Given("all", true));

            Assert.Equal(2, found["faceCount"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void LeavingOutTheCopyIsRefused()
        {
            int handle = Pair(QuadBase, QuadBase, QuadFaces);

            IDictionary<string, object> found = ComposedEditFixture.Value(Compare(
                handle, ComposedEditFixture.Given("all", true)));
            IDictionary<string, object> envelope = _fixture.Call(
                ToolName,
                ComposedEditFixture.Arguments(ComposedEditFixture.Given("all", true)));

            Assert.Equal(2, found["faceCount"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Theory]
        [MemberData(nameof(BadThresholds))]
        public void ThresholdsThatAreNotNonNegativeFiniteNumbersAreRefused(string name, object given)
        {
            int handle = Pair(QuadBase, QuadBase, QuadFaces);

            IDictionary<string, object> found = ComposedEditFixture.Value(Compare(
                handle, ComposedEditFixture.Given("all", true)));
            IDictionary<string, object> envelope = Compare(
                handle,
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(name, given));

            Assert.Equal(2, found["faceCount"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        public static IEnumerable<object[]> BadThresholds()
        {
            foreach (string name in new[] { "stretchThresholds", "bendThresholds" })
            {
                yield return new object[] { name, new object[0] };
                yield return new object[] { name, new object[] { -0.1 } };
                yield return new object[] { name, new object[] { "0.1" } };
                yield return new object[] { name, new object[] { double.PositiveInfinity } };
                yield return new object[] { name, new object[] { 1e39 } };
                yield return new object[] { name, 0.1 };
            }
        }

        private IDictionary<string, object> Compare(
            int handle, params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>(given)
            {
                ComposedEditFixture.Given("basePmxHandle", (long)handle),
            };

            return _fixture.Call(ToolName, ComposedEditFixture.Arguments(all.ToArray()));
        }

        private int Pair(V3[] baseline, V3[] target, params int[][][] materials)
        {
            FakePmx held = new FakePmx();
            Build(held, baseline, materials);
            _held = held;
            Build(_fixture.Model, target, materials);

            return _fixture.Handles.Issue(typeof(IPXPmx).FullName, held, () => { });
        }

        private static void Build(FakePmx model, V3[] positions, int[][][] materials)
        {
            foreach (V3 position in positions)
            {
                model.Vertex.Add(new FakeVertex(position.X, position.Y, position.Z));
            }

            int number = 0;
            foreach (int[][] faces in materials)
            {
                FakeMaterial material = new FakeMaterial("材質" + number++);
                foreach (int[] face in faces)
                {
                    material.Faces.Add(new FakeFace(
                        model.Vertex[face[0]], model.Vertex[face[1]], model.Vertex[face[2]]));
                }

                model.Material.Add(material);
            }
        }

        private static object Position(IPXVertex vertex)
        {
            return new object[] { vertex.Position.X, vertex.Position.Y, vertex.Position.Z };
        }

        private static V3[] Moved(V3[] source, int at, float x, float y, float z)
        {
            V3[] made = (V3[])source.Clone();
            made[at] = new V3(x, y, z);

            return made;
        }

        private static V3[] Shift(V3[] source, float along)
        {
            return source.Select(point => new V3(point.X + along, point.Y, point.Z)).ToArray();
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

        private static void AssertFace(int material, int face, V3[] points, object given)
        {
            IDictionary<string, object> row = (IDictionary<string, object>)given;
            Assert.Equal(material, row["material"]);
            Assert.Equal(face, row["face"]);
            object[] shown = (object[])row["points"];
            Assert.Equal(3, shown.Length);
            for (int at = 0; at < 3; at++)
            {
                AssertPoint(points[at].X, points[at].Y, points[at].Z, shown[at]);
            }
        }
    }
}
