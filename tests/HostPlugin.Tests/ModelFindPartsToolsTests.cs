using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelFindPartsToolsTests : IDisposable
    {
        private const string ToolName = "model_find_parts";

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void FacesThatShareAVertexAreOnePartAndFacesApartAreAnother()
        {
            Build(
                new[]
                {
                    new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(0f, 1f, 0f), new V3(1f, 1f, 0f),
                    new V3(10f, 0f, 0f), new V3(11f, 0f, 0f), new V3(10f, 1f, 0f),
                },
                new[] { new[] { 0, 1, 2 }, new[] { 1, 3, 2 }, new[] { 4, 5, 6 } });

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(Materials(0)));

            Assert.Equal(2, value["count"]);
            IDictionary<string, object>[] parts = Parts(value);
            Assert.Equal(2, parts.Length);
            Assert.Equal(4, parts[0]["vertexCount"]);
            Assert.Equal(2, parts[0]["faceCount"]);
            AssertRuns(parts[0], new[] { 0, 4 });
            Assert.Equal(3, parts[1]["vertexCount"]);
            Assert.Equal(1, parts[1]["faceCount"]);
            AssertRuns(parts[1], new[] { 4, 3 });
            Assert.False(value.ContainsKey("nextOffset"));
        }

        [Fact]
        public void VerticesAtTheSamePositionJoinPartsWithoutAWeldDistance()
        {
            int[][] faces = { new[] { 0, 1, 2 }, new[] { 3, 4, 5 } };
            Build(TwoTrianglesTouchingAt(new V3(1f, 0f, 0f)), faces);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(Materials(0)));

            Assert.Equal(1, value["count"]);
            IDictionary<string, object> part = Parts(value).Single();
            Assert.Equal(6, part["vertexCount"]);
            Assert.Equal(2, part["faceCount"]);
            AssertRuns(part, new[] { 0, 6 });
        }

        [Fact]
        public void VerticesAtDifferentPositionsStayApartWhenTheWeldDistanceIsZero()
        {
            int[][] faces = { new[] { 0, 1, 2 }, new[] { 3, 4, 5 } };
            Build(TwoTrianglesTouchingAt(new V3(1.5f, 0f, 0f)), faces);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                Materials(0), ComposedEditFixture.Given("weldDistance", 0.0)));

            Assert.Equal(2, value["count"]);
        }

        [Fact]
        public void VerticesAtTheSamePositionJoinPartsWhenTheWeldDistanceIsZero()
        {
            int[][] faces = { new[] { 0, 1, 2 }, new[] { 3, 4, 5 } };
            Build(TwoTrianglesTouchingAt(new V3(1f, 0f, 0f)), faces);

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                Materials(0), ComposedEditFixture.Given("weldDistance", 0.0)));

            Assert.Equal(1, value["count"]);
            Assert.Equal(6, Parts(value).Single()["vertexCount"]);
        }

        [Fact]
        public void VerticesAtExactlyTheWeldDistanceJoinAndOnesJustBeyondDoNot()
        {
            int[][] faces = { new[] { 0, 1, 2 }, new[] { 3, 4, 5 } };
            Build(TwoTrianglesTouchingAt(new V3(1.5f, 0f, 0f)), faces);

            IDictionary<string, object> atTheDistance = ComposedEditFixture.Value(Find(
                Materials(0), ComposedEditFixture.Given("weldDistance", 0.5)));
            IDictionary<string, object> justShort = ComposedEditFixture.Value(Find(
                Materials(0), ComposedEditFixture.Given("weldDistance", 0.4999)));

            Assert.Equal(1, atTheDistance["count"]);
            Assert.Equal(2, justShort["count"]);
        }

        [Fact]
        public void TheWeldDistanceIsMeasuredAsTheStraightLineBetweenThePositions()
        {
            int[][] faces = { new[] { 0, 1, 2 }, new[] { 3, 4, 5 } };
            Build(TwoTrianglesTouchingAt(new V3(1.375f, 0f, 0.5f)), faces);

            IDictionary<string, object> joined = ComposedEditFixture.Value(Find(
                Materials(0), ComposedEditFixture.Given("weldDistance", 0.625)));
            IDictionary<string, object> apart = ComposedEditFixture.Value(Find(
                Materials(0), ComposedEditFixture.Given("weldDistance", 0.5)));

            Assert.Equal(1, joined["count"]);
            Assert.Equal(2, apart["count"]);
        }

        [Fact]
        public void PartsThatAreOnlyLinkedThroughAChainOfNearbyVerticesAreOnePart()
        {
            Build(
                new[]
                {
                    new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(0f, 1f, 0f),
                    new V3(1.5f, 0f, 0f), new V3(2.5f, 0f, 0f), new V3(1.5f, 1f, 0f),
                    new V3(3f, 0f, 0f), new V3(4f, 0f, 0f), new V3(3f, 1f, 0f),
                },
                new[] { new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 } });

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                Materials(0), ComposedEditFixture.Given("weldDistance", 0.5)));

            Assert.Equal(1, value["count"]);
            IDictionary<string, object> part = Parts(value).Single();
            Assert.Equal(9, part["vertexCount"]);
            Assert.Equal(3, part["faceCount"]);
        }

        [Fact]
        public void PartsAreListedFromTheOneWithTheMostVerticesAndEqualCountsFromTheSmallestVertexNumber()
        {
            Build(
                new[]
                {
                    new V3(10f, 0f, 0f), new V3(11f, 0f, 0f), new V3(10f, 1f, 0f),
                    new V3(20f, 0f, 0f), new V3(21f, 0f, 0f), new V3(20f, 1f, 0f),
                    new V3(30f, 0f, 0f), new V3(31f, 0f, 0f), new V3(30f, 1f, 0f), new V3(31f, 1f, 0f),
                },
                new[] { new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, new[] { 7, 9, 8 }, new[] { 0, 1, 2 } });

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(Materials(0)));

            IDictionary<string, object>[] parts = Parts(value);
            Assert.Equal(3, parts.Length);
            AssertRuns(parts[0], new[] { 6, 4 });
            AssertRuns(parts[1], new[] { 0, 3 });
            AssertRuns(parts[2], new[] { 3, 3 });
        }

        [Fact]
        public void VertexRunsGatherTheVerticesOfAPartInAscendingOrderIntoStartAndCount()
        {
            Build(
                new[]
                {
                    new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(0f, 1f, 0f), new V3(50f, 0f, 0f),
                    new V3(51f, 0f, 0f), new V3(50f, 1f, 0f), new V3(1f, 1f, 0f),
                },
                new[] { new[] { 6, 2, 0 }, new[] { 6, 1, 2 }, new[] { 4, 5, 3 } });

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(Materials(0)));

            IDictionary<string, object>[] parts = Parts(value);
            Assert.Equal(2, parts.Length);
            Assert.Equal(4, parts[0]["vertexCount"]);
            AssertRuns(parts[0], new[] { 0, 3, 6, 1 });
            Assert.Equal(3, parts[1]["vertexCount"]);
            AssertRuns(parts[1], new[] { 3, 3 });
        }

        [Fact]
        public void VerticesOfOnePartThatAreNotConsecutiveMakeSeparateRuns()
        {
            Build(
                new[]
                {
                    new V3(0f, 0f, 0f), new V3(100f, 0f, 0f), new V3(1f, 0f, 0f), new V3(101f, 0f, 0f),
                    new V3(0f, 1f, 0f), new V3(100f, 1f, 0f),
                },
                new[] { new[] { 0, 2, 4 }, new[] { 1, 3, 5 } });

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(Materials(0)));

            IDictionary<string, object>[] parts = Parts(value);
            Assert.Equal(2, parts.Length);
            AssertRuns(parts[0], new[] { 0, 1, 2, 1, 4, 1 });
            AssertRuns(parts[1], new[] { 1, 1, 3, 1, 5, 1 });
        }

        [Fact]
        public void EachPartReportsTheSmallestAndLargestPositionOfItsVertices()
        {
            Build(
                new[]
                {
                    new V3(-1f, 2f, 3f), new V3(4f, -5f, 6f), new V3(0f, 0f, -7f),
                    new V3(20f, 20f, 20f), new V3(21f, 22f, 23f), new V3(20f, 25f, 21f),
                },
                new[] { new[] { 0, 1, 2 }, new[] { 3, 4, 5 } });

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(Materials(0)));

            IDictionary<string, object>[] parts = Parts(value);
            Assert.Equal(new object[] { -1f, -5f, -7f }, (object[])parts[0]["min"]);
            Assert.Equal(new object[] { 4f, 2f, 6f }, (object[])parts[0]["max"]);
            Assert.Equal(new object[] { 20f, 20f, 20f }, (object[])parts[1]["min"]);
            Assert.Equal(new object[] { 21f, 25f, 23f }, (object[])parts[1]["max"]);
        }

        [Fact]
        public void AFaceThatUsesOneVertexThriceIsAPartOfOneVertexAndOneFace()
        {
            Build(
                new[] { new V3(1f, 2f, 3f), new V3(10f, 0f, 0f), new V3(11f, 0f, 0f), new V3(10f, 1f, 0f) },
                new[] { new[] { 0, 0, 0 }, new[] { 1, 2, 3 } });

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(Materials(0)));

            IDictionary<string, object>[] parts = Parts(value);
            Assert.Equal(2, parts.Length);
            Assert.Equal(1, parts[1]["vertexCount"]);
            Assert.Equal(1, parts[1]["faceCount"]);
            AssertRuns(parts[1], new[] { 0, 1 });
            Assert.Equal(new object[] { 1f, 2f, 3f }, (object[])parts[1]["min"]);
            Assert.Equal(new object[] { 1f, 2f, 3f }, (object[])parts[1]["max"]);
        }

        [Fact]
        public void OnlyTheFacesOfTheGivenMaterialsAreJoinedAndTheOthersDoNotBridgeParts()
        {
            Build(
                new[]
                {
                    new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(0f, 1f, 0f),
                    new V3(5f, 0f, 0f), new V3(6f, 0f, 0f), new V3(5f, 1f, 0f),
                },
                new[] { new[] { 0, 1, 2 } },
                new[] { new[] { 3, 4, 5 } },
                new[] { new[] { 2, 3, 0 } });

            IDictionary<string, object> both = ComposedEditFixture.Value(Find(Materials(0, 1)));
            IDictionary<string, object> bridged = ComposedEditFixture.Value(Find(Materials(0, 1, 2)));
            IDictionary<string, object> second = ComposedEditFixture.Value(Find(Materials(1)));

            Assert.Equal(2, both["count"]);
            Assert.Equal(1, bridged["count"]);
            Assert.Equal(1, second["count"]);
            IDictionary<string, object> only = Parts(second).Single();
            Assert.Equal(3, only["vertexCount"]);
            Assert.Equal(1, only["faceCount"]);
            AssertRuns(only, new[] { 3, 3 });
        }

        [Fact]
        public void AMaterialWithoutFacesGivesNoParts()
        {
            Build(
                new[] { new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(0f, 1f, 0f) },
                new[] { new[] { 0, 1, 2 } },
                new int[0][]);

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(Materials(0)));
            IDictionary<string, object> value = ComposedEditFixture.Value(Find(Materials(1)));

            Assert.Equal(1, found["count"]);
            Assert.Equal(0, value["count"]);
            Assert.Empty(Parts(value));
            Assert.False(value.ContainsKey("nextOffset"));
        }

        [Fact]
        public void TheListIsReadInPartsFromTheOffsetAndSaysWhereToGoOn()
        {
            Build(Separate(5), Singles(5));

            IDictionary<string, object> first = ComposedEditFixture.Value(Find(
                Materials(0), ComposedEditFixture.Given("limit", 2)));
            IDictionary<string, object> middle = ComposedEditFixture.Value(Find(
                Materials(0),
                ComposedEditFixture.Given("offset", 2),
                ComposedEditFixture.Given("limit", 2)));
            IDictionary<string, object> last = ComposedEditFixture.Value(Find(
                Materials(0), ComposedEditFixture.Given("offset", 4)));

            Assert.Equal(5, first["count"]);
            Assert.Equal(new[] { 0, 3 }, Parts(first).Select(Start).ToArray());
            Assert.Equal(2, first["nextOffset"]);
            Assert.Equal(5, middle["count"]);
            Assert.Equal(new[] { 6, 9 }, Parts(middle).Select(Start).ToArray());
            Assert.Equal(4, middle["nextOffset"]);
            Assert.Equal(5, last["count"]);
            Assert.Equal(new[] { 12 }, Parts(last).Select(Start).ToArray());
            Assert.False(last.ContainsKey("nextOffset"));
        }

        [Fact]
        public void AListTooLongForTheAnswerIsCutAtWhatFitsAndSaysWhereToGoOn()
        {
            Build(Separate(1000), Singles(1000));

            IDictionary<string, object> envelope = _fixture.Call(
                ToolName,
                ComposedEditFixture.Arguments(Materials(0)),
                ResponseBudget.MinimumChars);

            IDictionary<string, object> value = ComposedEditFixture.Value(envelope);
            IDictionary<string, object>[] taken = Parts(value);
            Assert.Equal(1000, value["count"]);
            Assert.InRange(taken.Length, 1, 999);
            Assert.Equal(taken.Length, value["nextOffset"]);
            Assert.Equal(
                Enumerable.Range(0, taken.Length).Select(at => at * 3).ToArray(),
                taken.Select(Start).ToArray());
            Assert.Contains(
                ((object[])envelope[ToolEnvelope.WarningsName]).Cast<string>(),
                warning => warning.Contains("件数を減らした"));
        }

        [Fact]
        public void FindingChangesNeitherTheModelNorTheSelection()
        {
            Build(
                new[] { new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(0f, 1f, 0f) },
                new[] { new[] { 0, 1, 2 } });
            _fixture.Form.SelectedMaterials = new[] { 0 };
            string before = new ModelShape().Of(_fixture.Model);

            ComposedEditFixture.Value(Find(Materials(0)));

            Assert.Equal(0, _fixture.Commits);
            Assert.Equal(before, new ModelShape().Of(_fixture.Model));
            Assert.Equal(new[] { 0 }, _fixture.Form.SelectedMaterials);
        }

        [Theory]
        [MemberData(nameof(BadWeldDistances))]
        public void AWeldDistanceThatIsNotANonNegativeFiniteNumberIsRefused(object given)
        {
            Build(
                new[] { new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(0f, 1f, 0f) },
                new[] { new[] { 0, 1, 2 } });

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(
                Materials(0), ComposedEditFixture.Given("weldDistance", 0.5)));
            IDictionary<string, object> envelope = Find(
                Materials(0), ComposedEditFixture.Given("weldDistance", given));

            Assert.Equal(1, found["count"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        public static IEnumerable<object[]> BadWeldDistances()
        {
            yield return new object[] { -0.1 };
            yield return new object[] { double.NaN };
            yield return new object[] { double.PositiveInfinity };
            yield return new object[] { 1e39 };
            yield return new object[] { "0.1" };
            yield return new object[] { new object[] { 0.1 } };
        }

        [Fact]
        public void LeavingOutTheMaterialsOrGivingAnEmptyListIsRefused()
        {
            Build(
                new[] { new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(0f, 1f, 0f) },
                new[] { new[] { 0, 1, 2 } });

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(Materials(0)));
            IDictionary<string, object> missing = Find();
            IDictionary<string, object> empty = Find(
                ComposedEditFixture.Given("materialIndices", new object[0]));
            IDictionary<string, object> notAList = Find(
                ComposedEditFixture.Given("materialIndices", 0));

            Assert.Equal(1, found["count"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(missing));
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(empty));
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(notAList));
        }

        [Fact]
        public void AMaterialOutsideTheListIsRefused()
        {
            Build(
                new[] { new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(0f, 1f, 0f) },
                new[] { new[] { 0, 1, 2 } });

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(Materials(0)));
            IDictionary<string, object> past = Find(Materials(0, 1));
            IDictionary<string, object> negative = Find(Materials(-1));

            Assert.Equal(1, found["count"]);
            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(past));
            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(negative));
        }

        [Fact]
        public void ALimitBelowOneAndANegativeOffsetAreRefused()
        {
            Build(
                new[] { new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(0f, 1f, 0f) },
                new[] { new[] { 0, 1, 2 } });

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(
                Materials(0),
                ComposedEditFixture.Given("offset", 0),
                ComposedEditFixture.Given("limit", 1)));
            IDictionary<string, object> noLimit = Find(
                Materials(0), ComposedEditFixture.Given("limit", 0));
            IDictionary<string, object> backward = Find(
                Materials(0), ComposedEditFixture.Given("offset", -1));

            Assert.Equal(1, found["count"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(noLimit));
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(backward));
        }

        private IDictionary<string, object> Find(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ToolName, ComposedEditFixture.Arguments(given));
        }

        private static KeyValuePair<string, object> Materials(params int[] indices)
        {
            return ComposedEditFixture.Given(
                "materialIndices", indices.Cast<object>().ToArray());
        }

        private void Build(V3[] positions, params int[][][] materials)
        {
            foreach (V3 position in positions)
            {
                _fixture.Model.Vertex.Add(new FakeVertex(position.X, position.Y, position.Z));
            }

            int number = 0;
            foreach (int[][] faces in materials)
            {
                FakeMaterial material = new FakeMaterial("材質" + number++);
                foreach (int[] face in faces)
                {
                    material.Faces.Add(new FakeFace(
                        _fixture.Model.Vertex[face[0]],
                        _fixture.Model.Vertex[face[1]],
                        _fixture.Model.Vertex[face[2]]));
                }

                _fixture.Model.Material.Add(material);
            }
        }

        private void Build(V3[] positions, int[][] faces)
        {
            Build(positions, new[] { faces });
        }

        private static V3[] TwoTrianglesTouchingAt(V3 second)
        {
            return new[]
            {
                new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(0f, 1f, 0f),
                second, new V3(second.X + 1f, 5f, second.Z), new V3(second.X, 6f, second.Z),
            };
        }

        private static V3[] Separate(int count)
        {
            List<V3> made = new List<V3>();
            for (int at = 0; at < count; at++)
            {
                float x = at * 10f;
                made.Add(new V3(x, 0f, 0f));
                made.Add(new V3(x + 1f, 0f, 0f));
                made.Add(new V3(x, 1f, 0f));
            }

            return made.ToArray();
        }

        private static int[][] Singles(int count)
        {
            int[][] made = new int[count][];
            for (int at = 0; at < count; at++)
            {
                made[at] = new[] { at * 3, at * 3 + 1, at * 3 + 2 };
            }

            return made;
        }

        private static IDictionary<string, object>[] Parts(IDictionary<string, object> value)
        {
            return ((object[])value["parts"]).Cast<IDictionary<string, object>>().ToArray();
        }

        private static int Start(IDictionary<string, object> part)
        {
            return (int)((IDictionary<string, object>)((object[])part["vertexRuns"])[0])["start"];
        }

        private static void AssertRuns(IDictionary<string, object> part, int[] startAndCountPairs)
        {
            object[] runs = (object[])part["vertexRuns"];
            Assert.Equal(startAndCountPairs.Length / 2, runs.Length);
            for (int at = 0; at < runs.Length; at++)
            {
                IDictionary<string, object> run = (IDictionary<string, object>)runs[at];
                Assert.Equal(startAndCountPairs[at * 2], run["start"]);
                Assert.Equal(startAndCountPairs[at * 2 + 1], run["count"]);
            }
        }
    }
}
