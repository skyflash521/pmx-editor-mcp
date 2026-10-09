using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class SharedInputRefusalTests : IDisposable
    {
        private const string Offset = "offset";

        private const string Limit = "limit";

        private const string Indices = "indices";

        private const string All = "all";

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        public static IEnumerable<object[]> Counted()
        {
            foreach (string tool in new[]
            {
                ModelFindBoneWeights.ToolName,
                ModelFindMaterialVertices.ToolName,
                ModelFindReferrers.ToolName,
                ModelValidatePmx.ToolName,
            })
            {
                yield return new object[] { tool, Offset, -1, "offset は 0 以上の整数でなければならない。" };
                yield return new object[] { tool, Limit, 0, "limit は 1 以上の整数でなければならない。" };
                yield return new object[] { tool, Limit, "many", "limit は 1 以上の整数でなければならない。" };
                yield return new object[] { tool, Limit, 1.5, "limit は 1 以上の整数でなければならない。" };
                yield return new object[]
                {
                    tool, Limit, (long)int.MaxValue + 1, "limit は 1 以上の整数でなければならない。",
                };
            }
        }

        public static IEnumerable<object[]> Spots()
        {
            foreach (string[] each in new[]
            {
                new[] { ModelEditVertices.ToolName, "from" },
                new[] { ModelEditVertices.ToolName, "to" },
                new[] { ModelEditVertices.ToolName, "across" },
                new[] { ModelPlaceElements.ToolName, ModelPlaceElements.PositionName },
                new[] { ModelPlaceElements.ToolName, ModelPlaceElements.OffsetName },
                new[] { ModelPlaceElements.ToolName, ModelPlaceElements.RotationName },
                new[] { ModelPlaceElements.ToolName, ModelPlaceElements.ScaleName },
                new[] { ModelEditUv.ToolName, ModelEditUv.DirectionName },
            })
            {
                string tool = each[0];
                string name = each[1];
                yield return new object[] { tool, name, new object[] { 1.0, 0.0 }, name + " は3つの数の並びでなければならない。" };
                yield return new object[] { tool, name, "right", name + " は3つの数の並びでなければならない。" };
                yield return new object[]
                {
                    tool, name, new object[] { 1.0, "up", 0.0 }, name + " は3つの有限の数の並びでなければならない。",
                };
                yield return new object[]
                {
                    tool, name, new object[] { 1.0, double.NaN, 0.0 }, name + " は3つの有限の数の並びでなければならない。",
                };
            }
        }

        [Theory]
        [MemberData(nameof(Counted))]
        public void ACountOutsideTheRangeIsRefusedWithTheSameWordsInEveryTool(
            string tool, string name, object given, string said)
        {
            Model();

            IDictionary<string, object> envelope = _fixture.Call(
                tool, ComposedEditFixture.Arguments(CountedArguments(tool, name, given)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Equal(said, ComposedEditFixture.Message(envelope));
        }

        [Theory]
        [MemberData(nameof(Spots))]
        public void AValueThatIsNotThreeFiniteNumbersIsRefusedWithTheSameWordsInEveryTool(
            string tool, string name, object given, string said)
        {
            Model();

            IDictionary<string, object> envelope = _fixture.Call(
                tool, ComposedEditFixture.Arguments(SpotArguments(tool, name, given)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Equal(said, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void LeavingOutTheDirectionToProjectFromIsRefusedAsNotThreeNumbers()
        {
            Model();

            IDictionary<string, object> envelope = _fixture.Call(
                ModelEditUv.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given(ComposedOperation.OperationName, ModelEditUv.ProjectFromView),
                    ComposedEditFixture.Given(All, true)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Equal(
                ModelEditUv.DirectionName + " は3つの数の並びでなければならない。",
                ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void ADirectionToProjectFromWithoutLengthIsRefusedWithTheSharedWords()
        {
            Model();

            IDictionary<string, object> envelope = _fixture.Call(
                ModelEditUv.ToolName,
                ComposedEditFixture.Arguments(SpotArguments(
                    ModelEditUv.ToolName, ModelEditUv.DirectionName, new object[] { 0.0, 0.0, 0.0 })));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Equal(
                ModelEditUv.DirectionName + " は長さを持たなければならない。",
                ComposedEditFixture.Message(envelope));
        }

        private static KeyValuePair<string, object>[] CountedArguments(string tool, string name, object given)
        {
            List<KeyValuePair<string, object>> made = new List<KeyValuePair<string, object>>();
            switch (tool)
            {
                case ModelFindReferrers.ToolName:
                    made.Add(ComposedEditFixture.Given(ElementKinds.KindName, ElementKinds.Bone));
                    made.Add(ComposedEditFixture.Given(TargetNames.Element.All, true));
                    made.Add(ComposedEditFixture.Given("detail", "countPerTarget"));
                    break;

                case ModelValidatePmx.ToolName:
                    made.Add(ComposedEditFixture.Given(ModelValidatePmx.RunsName, PmxStateCheck.BadFacesName));
                    break;

                default:
                    made.Add(ComposedEditFixture.Given(All, true));
                    break;
            }

            made.Add(ComposedEditFixture.Given(name, given));

            return made.ToArray();
        }

        private static KeyValuePair<string, object>[] SpotArguments(string tool, string name, object given)
        {
            List<KeyValuePair<string, object>> made = new List<KeyValuePair<string, object>>();
            switch (tool)
            {
                case ModelEditVertices.ToolName:
                    made.Add(ComposedEditFixture.Given(ComposedOperation.OperationName, "lateralScale"));
                    made.Add(ComposedEditFixture.Given(Indices, new object[] { 0 }));
                    made.Add(ComposedEditFixture.Given("from", Triple(0.0, 0.0, 0.0)));
                    made.Add(ComposedEditFixture.Given("to", Triple(10.0, 0.0, 0.0)));
                    made.Add(ComposedEditFixture.Given("across", Triple(0.0, 1.0, 0.0)));
                    made.Add(ComposedEditFixture.Given(
                        "knots",
                        new object[]
                        {
                            new Dictionary<string, object>(StringComparer.Ordinal) { { "s", 0.5 }, { "scale", 2.0 } },
                        }));
                    break;

                case ModelPlaceElements.ToolName:
                    made.Add(ComposedEditFixture.Given(ComposedOperation.OperationName, PlacedBy(name)));
                    made.Add(ComposedEditFixture.Given(
                        ModelPlaceElements.TargetsName,
                        new object[]
                        {
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                { ModelPlaceElements.KindName, ElementKinds.Vertex },
                                { TargetNames.Element.All, true },
                            },
                        }));
                    if (name == ModelPlaceElements.PositionName)
                    {
                        made.Add(ComposedEditFixture.Given(ModelPlaceElements.AxesName, ModelPlaceElements.AllAxes));
                    }

                    break;

                default:
                    made.Add(ComposedEditFixture.Given(ComposedOperation.OperationName, ModelEditUv.ProjectFromView));
                    made.Add(ComposedEditFixture.Given(All, true));
                    break;
            }

            made.RemoveAll(pair => pair.Key == name);
            made.Add(ComposedEditFixture.Given(name, given));

            return made.ToArray();
        }

        private static string PlacedBy(string name)
        {
            switch (name)
            {
                case ModelPlaceElements.PositionName:
                    return ModelPlaceElements.AlignTo;

                case ModelPlaceElements.OffsetName:
                    return ModelPlaceElements.TranslateBy;

                case ModelPlaceElements.RotationName:
                    return ModelPlaceElements.RotateBy;

                default:
                    return ModelPlaceElements.ScaleBy;
            }
        }

        private static object Triple(double x, double y, double z)
        {
            return new object[] { x, y, z };
        }

        private void Model()
        {
            _fixture.Model.Vertex.Add(new FakeVertex(1f, 2f, 3f));
            _fixture.Model.Bone.Add(new FakeBone("センター"));
        }
    }
}
