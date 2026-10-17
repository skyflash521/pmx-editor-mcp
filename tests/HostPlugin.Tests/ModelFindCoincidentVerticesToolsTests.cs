using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelFindCoincidentVerticesToolsTests : IDisposable
    {
        private const string ToolName = "model_find_coincident_vertices";

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        private FakePmx _held;

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void VerticesThatWereTogetherInTheCopyAndAreApartNowAreGroupedAndTheSpreadIsTheirPresentDistance()
        {
            int handle = Pair(
                new[] { P(0f, 0f, 0f), P(0f, 0f, 0f), P(5f, 0f, 0f), P(5f, 0f, 0f) },
                new[] { P(0f, 0f, 0f), P(3f, 0f, 0f), P(5f, 0f, 0f), P(5f, 4f, 0f) });

            IDictionary<string, object> value = ComposedEditFixture.Value(FindWithCopy(
                handle, 0.0, ComposedEditFixture.Given("all", true)));

            Assert.Equal(2, value["count"]);
            Assert.Equal(4f, (float)value["maxSpread"], 5);
            IDictionary<string, object>[] groups = Groups(value);
            Assert.Equal(new[] { 2, 3 }, Vertices(groups[0]));
            Assert.Equal(4f, (float)groups[0]["spread"], 5);
            Assert.Equal(new[] { 0, 1 }, Vertices(groups[1]));
            Assert.Equal(3f, (float)groups[1]["spread"], 5);
        }

        [Fact]
        public void WithoutTheCopyTheVerticesAreGroupedAtTheirPresentPositions()
        {
            int handle = Pair(
                new[] { P(0f, 0f, 0f), P(0f, 0f, 0f), P(5f, 0f, 0f), P(5f, 0f, 0f) },
                new[] { P(0f, 0f, 0f), P(3f, 0f, 0f), P(5f, 0f, 0f), P(5f, 4f, 0f) });

            IDictionary<string, object> withCopy = ComposedEditFixture.Value(FindWithCopy(
                handle, 0.0, ComposedEditFixture.Given("all", true)));
            IDictionary<string, object> without = ComposedEditFixture.Value(Find(
                0.0, ComposedEditFixture.Given("all", true)));

            Assert.Equal(2, withCopy["count"]);
            Assert.Equal(0, without["count"]);
            Assert.Empty(Groups(without));
        }

        [Fact]
        public void WithoutTheCopyVerticesNearEachOtherNowAreGroupedWithTheirDistanceAsTheSpread()
        {
            Build(
                _fixture.Model,
                new[] { P(0f, 0f, 0f), P(0.25f, 0f, 0f), P(9f, 0f, 0f) });

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                0.5, ComposedEditFixture.Given("all", true)));

            Assert.Equal(1, value["count"]);
            IDictionary<string, object> group = Groups(value).Single();
            Assert.Equal(new[] { 0, 1 }, Vertices(group));
            Assert.Equal(0.25f, (float)group["spread"], 5);
            Assert.Equal(0.25f, (float)value["maxSpread"], 5);
        }

        [Fact]
        public void WithTheCopyTheGroupsFollowItsPositionsAndNotThePresentOnes()
        {
            int handle = Pair(
                new[] { P(0f, 0f, 0f), P(5f, 0f, 0f) },
                new[] { P(0f, 0f, 0f), P(0f, 0f, 0f) });

            IDictionary<string, object> withCopy = ComposedEditFixture.Value(FindWithCopy(
                handle, 0.5, ComposedEditFixture.Given("all", true)));
            IDictionary<string, object> without = ComposedEditFixture.Value(Find(
                0.5, ComposedEditFixture.Given("all", true)));

            Assert.Equal(0, withCopy["count"]);
            Assert.Equal(1, without["count"]);
        }

        [Fact]
        public void OnlyTheVerticesGivenByIndexAreGrouped()
        {
            Build(
                _fixture.Model,
                new[] { P(0f, 0f, 0f), P(0f, 0f, 0f), P(5f, 0f, 0f), P(5f, 0f, 0f) });

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                0.0, ComposedEditFixture.Given("indices", new object[] { 0, 1, 2 })));

            Assert.Equal(1, value["count"]);
            Assert.Equal(new[] { 0, 1 }, Vertices(Groups(value).Single()));
        }

        [Fact]
        public void TheVerticesOfTheGivenMaterialsAreGroupedAndAllTakesEveryVertex()
        {
            Build(
                _fixture.Model,
                new[]
                {
                    P(0f, 0f, 0f), P(5f, 0f, 0f), P(0f, 0f, 0f), P(9f, 0f, 0f), P(5f, 0f, 0f),
                    P(20f, 0f, 0f), P(20f, 0f, 0f),
                },
                new[] { new[] { 0, 1, 2 } },
                new[] { new[] { 3, 4, 5 } });

            IDictionary<string, object> first = ComposedEditFixture.Value(Find(
                0.0, ComposedEditFixture.Given("materialIndices", new object[] { 0 })));
            IDictionary<string, object> second = ComposedEditFixture.Value(Find(
                0.0, ComposedEditFixture.Given("materialIndices", new object[] { 1 })));
            IDictionary<string, object> both = ComposedEditFixture.Value(Find(
                0.0, ComposedEditFixture.Given("materialIndices", new object[] { 0, 1 })));
            IDictionary<string, object> all = ComposedEditFixture.Value(Find(
                0.0, ComposedEditFixture.Given("all", true)));

            Assert.Equal(1, first["count"]);
            Assert.Equal(new[] { 0, 2 }, Vertices(Groups(first).Single()));
            Assert.Equal(0, second["count"]);
            Assert.Equal(2, both["count"]);
            Assert.Equal(3, all["count"]);
            Assert.Equal(
                new[] { new[] { 0, 2 }, new[] { 1, 4 }, new[] { 5, 6 } },
                Groups(all).Select(Vertices).OrderBy(group => group[0]).ToArray());
        }

        [Fact]
        public void VerticesLinkedOnlyThroughAChainOfNearbyVerticesAreOneGroup()
        {
            Build(
                _fixture.Model,
                new[] { P(0.5f, 0f, 0f), P(0f, 0f, 0f), P(1f, 0f, 0f), P(9f, 0f, 0f) });

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                0.5, ComposedEditFixture.Given("all", true)));

            Assert.Equal(1, value["count"]);
            IDictionary<string, object> group = Groups(value).Single();
            Assert.Equal(new[] { 0, 1, 2 }, Vertices(group));
            Assert.Equal(1f, (float)group["spread"], 5);
        }

        [Fact]
        public void VerticesAtExactlyTheThresholdAreGroupedAndOnesJustBeyondAreNot()
        {
            Build(_fixture.Model, new[] { P(0f, 0f, 0f), P(0.5f, 0f, 0f) });

            IDictionary<string, object> atTheThreshold = ComposedEditFixture.Value(Find(
                0.5, ComposedEditFixture.Given("all", true)));
            IDictionary<string, object> justShort = ComposedEditFixture.Value(Find(
                0.4999, ComposedEditFixture.Given("all", true)));

            Assert.Equal(1, atTheThreshold["count"]);
            Assert.Equal(0, justShort["count"]);
        }

        [Fact]
        public void TheThresholdIsMeasuredAsTheStraightLineBetweenThePositions()
        {
            Build(_fixture.Model, new[] { P(0f, 0f, 0f), P(0.375f, 0.5f, 0f) });

            IDictionary<string, object> grouped = ComposedEditFixture.Value(Find(
                0.625, ComposedEditFixture.Given("all", true)));
            IDictionary<string, object> apart = ComposedEditFixture.Value(Find(
                0.5, ComposedEditFixture.Given("all", true)));

            Assert.Equal(1, grouped["count"]);
            Assert.Equal(0, apart["count"]);
        }

        [Fact]
        public void AThresholdOfZeroGroupsOnlyVerticesAtTheSamePosition()
        {
            Build(
                _fixture.Model,
                new[] { P(0f, 0f, 0f), P(0f, 0f, 0f), P(0.001f, 0f, 0f), P(0f, 0f, 0f) });

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                0.0, ComposedEditFixture.Given("all", true)));

            Assert.Equal(1, value["count"]);
            IDictionary<string, object> group = Groups(value).Single();
            Assert.Equal(new[] { 0, 1, 3 }, Vertices(group));
            Assert.Equal(0f, (float)group["spread"], 5);
        }

        [Fact]
        public void TheSpreadOfAGroupIsTheLargestPresentDistanceBetweenAnyTwoOfItsVertices()
        {
            Build(
                _fixture.Model,
                new[] { P(0f, 0f, 0f), P(1f, 0f, 0f), P(0f, 1f, 0f), P(0f, 0f, 1f) });

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                1.5, ComposedEditFixture.Given("all", true)));

            IDictionary<string, object> group = Groups(value).Single();
            Assert.Equal(new[] { 0, 1, 2, 3 }, Vertices(group));
            Assert.Equal((float)Math.Sqrt(2.0), (float)group["spread"], 5);
        }

        [Fact]
        public void TheSpreadIsMeasuredAtThePresentPositionsWhenTheGroupsComeFromTheCopy()
        {
            int handle = Pair(
                new[] { P(0f, 0f, 0f), P(0f, 0f, 0f), P(0f, 0f, 0f), P(0f, 0f, 0f) },
                new[] { P(0f, 0f, 0f), P(1f, 0f, 0f), P(0f, 1f, 0f), P(0f, 0f, 1f) });

            IDictionary<string, object> value = ComposedEditFixture.Value(FindWithCopy(
                handle, 0.0, ComposedEditFixture.Given("all", true)));

            IDictionary<string, object> group = Groups(value).Single();
            Assert.Equal(new[] { 0, 1, 2, 3 }, Vertices(group));
            Assert.Equal((float)Math.Sqrt(2.0), (float)group["spread"], 5);
        }

        [Fact]
        public void MaxSpreadIsTheLargestSpreadOfTheGroups()
        {
            Build(
                _fixture.Model,
                new[]
                {
                    P(0f, 0f, 0f), P(0.25f, 0f, 0f), P(10f, 0f, 0f), P(10.5f, 0f, 0f), P(20f, 0f, 0f),
                    P(20.125f, 0f, 0f),
                });

            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                0.5, ComposedEditFixture.Given("all", true)));

            Assert.Equal(3, value["count"]);
            Assert.Equal(0.5f, (float)value["maxSpread"], 5);
        }

        [Fact]
        public void TheCountOfGroupsOverTheSpreadLimitLeavesOutOnesAtExactlyTheLimit()
        {
            int handle = Pair(
                new[] { P(0f, 0f, 0f), P(0f, 0f, 0f), P(5f, 0f, 0f), P(5f, 0f, 0f), P(10f, 0f, 0f), P(10f, 0f, 0f) },
                new[] { P(0f, 0f, 0f), P(1f, 0f, 0f), P(5f, 0f, 0f), P(7f, 0f, 0f), P(10f, 0f, 0f), P(13f, 0f, 0f) });

            IDictionary<string, object> over = ComposedEditFixture.Value(FindWithCopy(
                handle,
                0.0,
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("spreadLimit", 2.0)));
            IDictionary<string, object> below = ComposedEditFixture.Value(FindWithCopy(
                handle,
                0.0,
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("spreadLimit", 1.9999)));
            IDictionary<string, object> beyond = ComposedEditFixture.Value(FindWithCopy(
                handle,
                0.0,
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("spreadLimit", 3.0)));

            Assert.Equal(3, over["count"]);
            Assert.Equal(1, over["overLimitCount"]);
            Assert.Equal(2, below["overLimitCount"]);
            Assert.Equal(0, beyond["overLimitCount"]);
        }

        [Fact]
        public void WithoutASpreadLimitThereIsNoOverLimitCount()
        {
            Build(_fixture.Model, new[] { P(0f, 0f, 0f), P(0f, 0f, 0f) });

            IDictionary<string, object> limited = ComposedEditFixture.Value(Find(
                0.0,
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("spreadLimit", 1.0)));
            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                0.0, ComposedEditFixture.Given("all", true)));

            Assert.Equal(0, limited["overLimitCount"]);
            Assert.False(value.ContainsKey("overLimitCount"));
        }

        [Fact]
        public void GroupsAreListedFromTheLargestSpreadAndEqualSpreadsFromTheSmallestVertexNumber()
        {
            int handle = Pair(
                new[]
                {
                    P(0f, 0f, 0f), P(0f, 0f, 0f), P(5f, 0f, 0f), P(5f, 0f, 0f), P(10f, 0f, 0f), P(10f, 0f, 0f),
                    P(15f, 0f, 0f), P(15f, 0f, 0f),
                },
                new[]
                {
                    P(0f, 0f, 0f), P(1f, 0f, 0f), P(5f, 0f, 0f), P(8f, 0f, 0f), P(10f, 0f, 0f),
                    P(11f, 0f, 0f), P(15f, 0f, 0f), P(16f, 0f, 0f),
                });

            IDictionary<string, object> value = ComposedEditFixture.Value(FindWithCopy(
                handle, 0.0, ComposedEditFixture.Given("all", true)));

            Assert.Equal(
                new[] { new[] { 2, 3 }, new[] { 0, 1 }, new[] { 4, 5 }, new[] { 6, 7 } },
                Groups(value).Select(Vertices).ToArray());
        }

        [Fact]
        public void TheListIsReadInGroupsFromTheOffsetAndSaysWhereToGoOn()
        {
            Build(_fixture.Model, Coincident(5));

            IDictionary<string, object> first = ComposedEditFixture.Value(Find(
                0.0, ComposedEditFixture.Given("all", true), ComposedEditFixture.Given("limit", 2)));
            IDictionary<string, object> middle = ComposedEditFixture.Value(Find(
                0.0,
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("offset", 2),
                ComposedEditFixture.Given("limit", 2)));
            IDictionary<string, object> last = ComposedEditFixture.Value(Find(
                0.0, ComposedEditFixture.Given("all", true), ComposedEditFixture.Given("offset", 4)));

            Assert.Equal(5, first["count"]);
            Assert.Equal(new[] { 0, 2 }, Groups(first).Select(Smallest).ToArray());
            Assert.Equal(2, first["nextOffset"]);
            Assert.Equal(5, middle["count"]);
            Assert.Equal(new[] { 4, 6 }, Groups(middle).Select(Smallest).ToArray());
            Assert.Equal(4, middle["nextOffset"]);
            Assert.Equal(5, last["count"]);
            Assert.Equal(new[] { 8 }, Groups(last).Select(Smallest).ToArray());
            Assert.False(last.ContainsKey("nextOffset"));
        }

        [Fact]
        public void PagingDoesNotChangeTheCountOrTheLargestSpread()
        {
            int handle = Pair(
                new[] { P(0f, 0f, 0f), P(0f, 0f, 0f), P(5f, 0f, 0f), P(5f, 0f, 0f) },
                new[] { P(0f, 0f, 0f), P(1f, 0f, 0f), P(10f, 0f, 0f), P(13f, 0f, 0f) });

            IDictionary<string, object> value = ComposedEditFixture.Value(FindWithCopy(
                handle,
                0.0,
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("offset", 1),
                ComposedEditFixture.Given("limit", 1)));

            Assert.Equal(2, value["count"]);
            Assert.Equal(3f, (float)value["maxSpread"], 5);
            Assert.Equal(new[] { 0 }, Groups(value).Select(Smallest).ToArray());
        }

        [Fact]
        public void NothingToGroupGivesNoGroupsAndNoLargestSpread()
        {
            Build(_fixture.Model, new[] { P(0f, 0f, 0f), P(5f, 0f, 0f) });

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(
                5.0, ComposedEditFixture.Given("all", true)));
            IDictionary<string, object> value = ComposedEditFixture.Value(Find(
                1.0, ComposedEditFixture.Given("all", true)));

            Assert.Equal(1, found["count"]);
            Assert.Equal(0, value["count"]);
            Assert.Empty(Groups(value));
            Assert.False(value.ContainsKey("maxSpread"));
            Assert.False(value.ContainsKey("nextOffset"));
        }

        [Fact]
        public void AListTooLongForTheAnswerIsCutAtWhatFitsAndSaysWhereToGoOn()
        {
            Build(_fixture.Model, Coincident(1000));

            IDictionary<string, object> envelope = _fixture.Call(
                ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("threshold", 0.0),
                    ComposedEditFixture.Given("all", true)),
                ResponseBudget.MinimumChars);

            IDictionary<string, object> value = ComposedEditFixture.Value(envelope);
            IDictionary<string, object>[] taken = Groups(value);
            Assert.Equal(1000, value["count"]);
            Assert.InRange(taken.Length, 1, 999);
            Assert.Equal(taken.Length, value["nextOffset"]);
            Assert.Equal(
                Enumerable.Range(0, taken.Length).Select(at => at * 2).ToArray(),
                taken.Select(Smallest).ToArray());
            Assert.Contains(
                ((object[])envelope[ToolEnvelope.WarningsName]).Cast<string>(),
                warning => warning.Contains("件数を減らした"));
        }

        [Fact]
        public void FindingChangesNeitherTheModelNorTheCopyNorTheSelection()
        {
            int handle = Pair(
                new[] { P(0f, 0f, 0f), P(0f, 0f, 0f), P(5f, 0f, 0f) },
                new[] { P(0f, 0f, 0f), P(1f, 0f, 0f), P(5f, 0f, 0f) });
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 2 };
            string before = new ModelShape().Of(_fixture.Model);
            string heldBefore = new ModelShape().Of(_held);

            ComposedEditFixture.Value(FindWithCopy(
                handle, 0.0, ComposedEditFixture.Given("all", true)));

            Assert.Equal(0, _fixture.Commits);
            Assert.Equal(before, new ModelShape().Of(_fixture.Model));
            Assert.Equal(heldBefore, new ModelShape().Of(_held));
            Assert.Equal(new[] { 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Theory]
        [InlineData("vertex")]
        [InlineData("face")]
        [InlineData("bone")]
        public void ACopyWhoseCountsDifferFromTheModelIsRefused(string differing)
        {
            Build(
                _fixture.Model,
                new[] { P(0f, 0f, 0f), P(0f, 0f, 0f), P(0f, 1f, 0f) },
                new[] { new[] { 0, 1, 2 }, new[] { 2, 1, 0 } });
            int same = _fixture.Handles.Issue(
                typeof(IPXPmx).FullName, FakeEditorState.Duplicate(_fixture.Model), () => { });
            IDictionary<string, object> found = ComposedEditFixture.Value(FindWithCopy(
                same, 0.0, ComposedEditFixture.Given("all", true)));

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
            IDictionary<string, object> envelope = FindWithCopy(
                handle, 0.0, ComposedEditFixture.Given("all", true));

            Assert.Equal(1, found["count"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Theory]
        [MemberData(nameof(BadValues))]
        public void ValuesThatAreNotNonNegativeFiniteNumbersAreRefused(string name, object given)
        {
            Build(_fixture.Model, new[] { P(0f, 0f, 0f), P(0f, 0f, 0f) });

            IDictionary<string, object> found = ComposedEditFixture.Value(_fixture.Call(
                ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("threshold", 0.5),
                    ComposedEditFixture.Given("spreadLimit", 0.5),
                    ComposedEditFixture.Given("all", true))));
            List<KeyValuePair<string, object>> arguments = new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("threshold", 0.5),
                ComposedEditFixture.Given("spreadLimit", 0.5),
                ComposedEditFixture.Given("all", true),
            };
            arguments.RemoveAll(argument => argument.Key == name);
            arguments.Add(ComposedEditFixture.Given(name, given));

            IDictionary<string, object> envelope = _fixture.Call(
                ToolName, ComposedEditFixture.Arguments(arguments.ToArray()));

            Assert.Equal(1, found["count"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        public static IEnumerable<object[]> BadValues()
        {
            foreach (string name in new[] { "threshold", "spreadLimit" })
            {
                yield return new object[] { name, -0.1 };
                yield return new object[] { name, double.NaN };
                yield return new object[] { name, double.PositiveInfinity };
                yield return new object[] { name, 1e39 };
                yield return new object[] { name, "0.1" };
                yield return new object[] { name, new object[] { 0.1 } };
            }
        }

        [Fact]
        public void LeavingOutTheThresholdIsRefused()
        {
            Build(_fixture.Model, new[] { P(0f, 0f, 0f), P(0f, 0f, 0f) });

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(
                0.0, ComposedEditFixture.Given("all", true)));
            IDictionary<string, object> envelope = _fixture.Call(
                ToolName,
                ComposedEditFixture.Arguments(ComposedEditFixture.Given("all", true)));

            Assert.Equal(1, found["count"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void PointingAtVerticesInTwoWaysOrInNoWayIsRefused()
        {
            Build(
                _fixture.Model,
                new[] { P(0f, 0f, 0f), P(0f, 0f, 0f), P(0f, 1f, 0f) },
                new[] { new[] { 0, 1, 2 } });

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(
                0.0, ComposedEditFixture.Given("indices", new object[] { 0, 1 })));
            IDictionary<string, object> nothing = Find(0.0);
            IDictionary<string, object> indicesAndAll = Find(
                0.0,
                ComposedEditFixture.Given("indices", new object[] { 0, 1 }),
                ComposedEditFixture.Given("all", true));
            IDictionary<string, object> indicesAndMaterials = Find(
                0.0,
                ComposedEditFixture.Given("indices", new object[] { 0, 1 }),
                ComposedEditFixture.Given("materialIndices", new object[] { 0 }));
            IDictionary<string, object> materialsAndAll = Find(
                0.0,
                ComposedEditFixture.Given("materialIndices", new object[] { 0 }),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(1, found["count"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(nothing));
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(indicesAndAll));
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(indicesAndMaterials));
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(materialsAndAll));
        }

        [Fact]
        public void AVertexOrMaterialOutsideTheModelIsRefused()
        {
            Build(
                _fixture.Model,
                new[] { P(0f, 0f, 0f), P(0f, 0f, 0f), P(0f, 1f, 0f) },
                new[] { new[] { 0, 1, 2 } });

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(
                0.0, ComposedEditFixture.Given("indices", new object[] { 0, 1 })));
            IDictionary<string, object> vertexPast = Find(
                0.0, ComposedEditFixture.Given("indices", new object[] { 0, 3 }));
            IDictionary<string, object> vertexNegative = Find(
                0.0, ComposedEditFixture.Given("indices", new object[] { -1 }));
            IDictionary<string, object> materialPast = Find(
                0.0, ComposedEditFixture.Given("materialIndices", new object[] { 1 }));

            Assert.Equal(1, found["count"]);
            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(vertexPast));
            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(vertexNegative));
            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(materialPast));
        }

        [Fact]
        public void ALimitBelowOneAndANegativeOffsetAreRefused()
        {
            Build(_fixture.Model, new[] { P(0f, 0f, 0f), P(0f, 0f, 0f) });

            IDictionary<string, object> found = ComposedEditFixture.Value(Find(
                0.0,
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given("offset", 0),
                ComposedEditFixture.Given("limit", 1)));
            IDictionary<string, object> noLimit = Find(
                0.0, ComposedEditFixture.Given("all", true), ComposedEditFixture.Given("limit", 0));
            IDictionary<string, object> backward = Find(
                0.0, ComposedEditFixture.Given("all", true), ComposedEditFixture.Given("offset", -1));

            Assert.Equal(1, found["count"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(noLimit));
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(backward));
        }

        private IDictionary<string, object> Find(
            double threshold, params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>(given)
            {
                ComposedEditFixture.Given("threshold", threshold),
            };

            return _fixture.Call(ToolName, ComposedEditFixture.Arguments(all.ToArray()));
        }

        private IDictionary<string, object> FindWithCopy(
            int handle, double threshold, params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>(given)
            {
                ComposedEditFixture.Given("threshold", threshold),
                ComposedEditFixture.Given("basePmxHandle", (long)handle),
            };

            return _fixture.Call(ToolName, ComposedEditFixture.Arguments(all.ToArray()));
        }

        private int Pair(V3[] baseline, V3[] target)
        {
            FakePmx held = new FakePmx();
            Build(held, baseline);
            _held = held;
            Build(_fixture.Model, target);

            return _fixture.Handles.Issue(typeof(IPXPmx).FullName, held, () => { });
        }

        private static void Build(FakePmx model, V3[] positions, params int[][][] materials)
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

        private static void Build(FakePmx model, V3[] positions, int[][] faces)
        {
            Build(model, positions, new[] { faces });
        }

        private static V3 P(float x, float y, float z)
        {
            return new V3(x, y, z);
        }

        private static V3[] Coincident(int pairs)
        {
            List<V3> made = new List<V3>();
            for (int at = 0; at < pairs; at++)
            {
                made.Add(new V3(at * 10f, 0f, 0f));
                made.Add(new V3(at * 10f, 0f, 0f));
            }

            return made.ToArray();
        }

        private static IDictionary<string, object>[] Groups(IDictionary<string, object> value)
        {
            return ((object[])value["groups"]).Cast<IDictionary<string, object>>().ToArray();
        }

        private static int[] Vertices(IDictionary<string, object> group)
        {
            return ((object[])group["vertices"]).Cast<int>().ToArray();
        }

        private static int Smallest(IDictionary<string, object> group)
        {
            return Vertices(group).Min();
        }
    }
}
