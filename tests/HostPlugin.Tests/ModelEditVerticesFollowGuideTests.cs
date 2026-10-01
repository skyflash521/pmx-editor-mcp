using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditVerticesFollowGuideTests : IDisposable
    {
        private const string FollowGuide = "followGuide";

        private const string GuideIndices = "guideIndices";

        private const string Mode = "mode";

        private const string Rigid = "rigid";

        private const string Interpolate = "interpolate";

        private const string BasePmxHandle = "basePmxHandle";

        private const string Changed = "changed";

        private const string QuarterTurnAboutY = "quarterTurnAboutY";

        private const string ThirdTurnAboutDiagonal = "thirdTurnAboutDiagonal";

        private const int Digits = 4;

        private const int FirstPicked = 7;

        private const int SecondPicked = 8;

        private const int GuideA = 0;

        private const int GuideB = 1;

        private const int OffAxis = 2;

        private const int NearA = 3;

        private const int NearB = 4;

        private const int OnA = 5;

        private static readonly V3[] Corners =
        {
            new V3(1f, 0f, 0f), new V3(0f, 1f, 0f), new V3(0f, 0f, 1f), new V3(1f, 1f, 1f),
        };

        private static readonly V3[] Row =
        {
            new V3(5f, 5f, 5f), new V3(6f, 5f, 5f), new V3(7f, 5f, 5f),
        };

        private static readonly V3 FirstPoint = new V3(3f, -1f, 2f);

        private static readonly V3 SecondPoint = new V3(-2f, 4f, 1f);

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        private FakePmx _held;

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Theory]
        [InlineData(QuarterTurnAboutY, 3, 4f, 2f, -4f, 3f, 7f, 1f)]
        [InlineData(QuarterTurnAboutY, 4, 4f, 2f, -4f, 3f, 7f, 1f)]
        [InlineData(ThirdTurnAboutDiagonal, 3, 4f, 6f, -2f, 3f, 1f, 3f)]
        [InlineData(ThirdTurnAboutDiagonal, 4, 4f, 6f, -2f, 3f, 1f, 3f)]
        public void RigidCarriesThePickedVerticesByTheRotationAndTranslationThatMovedTheGuides(
            string turn, int guideCount, float x1, float y1, float z1, float x2, float y2, float z2)
        {
            int handle = Turned(turn);

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(
                handle,
                Rigid,
                new[] { FirstPicked, SecondPicked },
                Enumerable.Range(0, guideCount).ToArray())));

            Assert.Equal(2, value[Changed]);
            Assert.Equal(1, _fixture.Commits);
            AssertAt(FirstPicked, x1, y1, z1);
            AssertAt(SecondPicked, x2, y2, z2);
        }

        [Fact]
        public void RigidFitsGuidesThatWereAlsoStretchedByTheirRotationAndTranslationAlone()
        {
            V3[] plane =
            {
                new V3(1f, 0f, 1f), new V3(-1f, 0f, 1f), new V3(-1f, 0f, -1f), new V3(1f, 0f, -1f),
            };
            V3[] baseline = plane.Concat(new[] { FirstPoint }).ToArray();
            V3[] current = plane
                .Select(point => Moved(QuarterTurnAboutY, new V3(point.X * 1.5f, point.Y * 1.5f, point.Z * 1.5f)))
                .Concat(new[] { FirstPoint })
                .ToArray();
            int handle = Scene(baseline, current);

            ComposedEditFixture.Value(Run(Full(
                handle, Rigid, new[] { 4 }, new[] { 0, 1, 2, 3 })));

            AssertAt(4, 4f, 2f, -4f);
        }

        [Fact]
        public void RigidPlacesAPickedVertexFromItsPositionInTheCopyNotFromWhereItIsNow()
        {
            V3[] baseline = Corners.Concat(Row).Concat(new[] { FirstPoint }).ToArray();
            V3[] current = Corners.Select(point => Moved(QuarterTurnAboutY, point))
                .Concat(Row)
                .Concat(new[] { new V3(9f, 9f, 9f) })
                .ToArray();
            int handle = Scene(baseline, current);

            ComposedEditFixture.Value(Run(Full(
                handle, Rigid, new[] { 7 }, new[] { 0, 1, 2, 3 })));

            AssertAt(7, 4f, 2f, -4f);
        }

        [Fact]
        public void RigidLeavesTheGuidesAndTheVerticesThatWereNotPickedWhereTheyAre()
        {
            int handle = Turned(QuarterTurnAboutY);
            V3[] before = Positions();

            ComposedEditFixture.Value(Run(Full(
                handle, Rigid, new[] { FirstPicked }, new[] { 0, 1, 2, 3 })));

            for (int at = 0; at < before.Length; at++)
            {
                if (at != FirstPicked)
                {
                    AssertSame(before[at], at);
                }
            }

            AssertAt(FirstPicked, 4f, 2f, -4f);
        }

        [Theory]
        [InlineData(Rigid)]
        [InlineData(Interpolate)]
        public void GuidesThatDidNotMoveLeaveThePickedVerticesWhereTheyAre(string mode)
        {
            V3[] baseline = Corners.Concat(new[] { FirstPoint, SecondPoint }).ToArray();
            int handle = Scene(baseline, baseline);

            ComposedEditFixture.Value(Run(Full(
                handle, mode, new[] { 4, 5 }, new[] { 0, 1, 2, 3 })));

            AssertAt(4, FirstPoint.X, FirstPoint.Y, FirstPoint.Z);
            AssertAt(5, SecondPoint.X, SecondPoint.Y, SecondPoint.Z);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void RigidRefusesGuidesThatDoNotFixTheRotation(int variant)
        {
            int handle = Turned(QuarterTurnAboutY);
            ComposedEditFixture.Value(Run(Full(
                handle, Rigid, new[] { FirstPicked }, new[] { 0, 1, 2, 3 })));
            V3[] before = Positions();
            int[] guides = variant == 1 ? new[] { 0 } : variant == 2 ? new[] { 0, 1 } : new[] { 4, 5, 6 };

            IDictionary<string, object> envelope = Run(Full(handle, Rigid, new[] { SecondPicked }, guides));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(GuideIndices, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Fact]
        public void InterpolateWeighsEachGuideDisplacementByTheInverseSquareOfTheDistance()
        {
            int handle = Pulled();

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(
                handle, Interpolate, new[] { NearA, OffAxis }, new[] { GuideA, GuideB })));

            Assert.Equal(2, value[Changed]);
            Assert.Equal(1, _fixture.Commits);
            AssertAt(NearA, 1f, 9f, 2f);
            AssertAt(OffAxis, 0f, 3f + 410f / 66f, 4f + 500f / 66f);
        }

        [Fact]
        public void InterpolateGivesAVertexNextToAGuideAlmostExactlyThatGuidesDisplacement()
        {
            int handle = Pulled();

            ComposedEditFixture.Value(Run(Full(
                handle, Interpolate, new[] { NearB }, new[] { GuideA, GuideB })));

            AssertAt(NearB, 3.99f, 0f, 20f, 3);
        }

        [Fact]
        public void InterpolateGivesAVertexAtTheSamePositionAsAGuideExactlyThatGuidesDisplacement()
        {
            int handle = Pulled();

            ComposedEditFixture.Value(Run(Full(
                handle, Interpolate, new[] { OnA }, new[] { GuideA, GuideB })));

            AssertAt(OnA, 0f, 10f, 0f);
        }

        [Fact]
        public void InterpolateWithOneGuideMovesEveryPickedVertexByThatGuidesDisplacement()
        {
            V3[] baseline =
            {
                new V3(0f, 0f, 0f), new V3(1f, 0f, 0f), new V3(10f, 10f, 10f), new V3(-3f, 2f, 5f),
            };
            V3[] current = (V3[])baseline.Clone();
            current[0] = new V3(1f, 2f, 3f);
            int handle = Scene(baseline, current);

            ComposedEditFixture.Value(Run(Full(
                handle, Interpolate, new[] { 1, 2, 3 }, new[] { 0 })));

            AssertAt(1, 2f, 2f, 3f);
            AssertAt(2, 11f, 12f, 13f);
            AssertAt(3, -2f, 4f, 8f);
        }

        [Fact]
        public void InterpolatePlacesAPickedVertexFromItsPositionInTheCopyNotFromWhereItIsNow()
        {
            V3[] baseline = PulledBase();
            V3[] current = PulledCurrent();
            current[NearA] = new V3(7f, 7f, 7f);
            int handle = Scene(baseline, current);

            ComposedEditFixture.Value(Run(Full(
                handle, Interpolate, new[] { NearA }, new[] { GuideA, GuideB })));

            AssertAt(NearA, 1f, 9f, 2f);
        }

        [Fact]
        public void InterpolateLeavesTheGuidesAndTheVerticesThatWereNotPickedWhereTheyAre()
        {
            int handle = Pulled();
            V3[] before = Positions();

            ComposedEditFixture.Value(Run(Full(
                handle, Interpolate, new[] { NearA }, new[] { GuideA, GuideB })));

            for (int at = 0; at < before.Length; at++)
            {
                if (at != NearA)
                {
                    AssertSame(before[at], at);
                }
            }

            AssertAt(NearA, 1f, 9f, 2f);
        }

        [Theory]
        [InlineData(Rigid)]
        [InlineData(Interpolate)]
        public void FollowingTheGuideChangesNeitherTheCopyNorItsCounts(string mode)
        {
            int handle = Turned(QuarterTurnAboutY);
            V3[] baseline = _held.Vertex.Select(vertex => vertex.Position).ToArray();

            ComposedEditFixture.Value(Run(Full(
                handle, mode, new[] { FirstPicked }, new[] { 0, 1, 2, 3 })));

            Assert.Equal(baseline.Length, _held.Vertex.Count);
            Assert.Single(((FakeMaterial)_held.Material[0]).Faces);
            Assert.Empty(_held.Bone);
            for (int at = 0; at < baseline.Length; at++)
            {
                Assert.Equal(baseline[at].X, _held.Vertex[at].Position.X);
                Assert.Equal(baseline[at].Y, _held.Vertex[at].Position.Y);
                Assert.Equal(baseline[at].Z, _held.Vertex[at].Position.Z);
            }
        }

        [Theory]
        [InlineData(Rigid)]
        [InlineData(Interpolate)]
        public void LeavingOutTheGuidesIsRefused(string mode)
        {
            int handle = Turned(QuarterTurnAboutY);
            ComposedEditFixture.Value(Run(Full(
                handle, mode, new[] { FirstPicked }, new[] { 0, 1, 2, 3 })));
            V3[] before = Positions();

            IDictionary<string, object> envelope = Run(Without(
                Full(handle, mode, new[] { SecondPicked }, new[] { 0, 1, 2, 3 }), GuideIndices));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(GuideIndices, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(Rigid, 0)]
        [InlineData(Interpolate, 0)]
        [InlineData(Rigid, 1)]
        [InlineData(Interpolate, 2)]
        [InlineData(Interpolate, 3)]
        public void GuidesThatAreNotANonEmptyListOfPositionsAreRefused(string mode, int variant)
        {
            int handle = Turned(QuarterTurnAboutY);
            ComposedEditFixture.Value(Run(Full(
                handle, mode, new[] { FirstPicked }, new[] { 0, 1, 2, 3 })));
            V3[] before = Positions();
            object given = variant == 0
                ? new object[0]
                : variant == 1 ? (object)1 : variant == 2 ? (object)"0" : new object[] { "a" };

            IDictionary<string, object> envelope = Run(With(
                Full(handle, mode, new[] { SecondPicked }, new[] { 0, 1, 2, 3 }), GuideIndices, given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(GuideIndices, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(Rigid, 99)]
        [InlineData(Interpolate, 99)]
        [InlineData(Rigid, 9)]
        [InlineData(Interpolate, -1)]
        public void AGuideOutsideTheVerticesIsRefused(string mode, int outside)
        {
            int handle = Turned(QuarterTurnAboutY);
            ComposedEditFixture.Value(Run(Full(
                handle, mode, new[] { FirstPicked }, new[] { 0, 1, 2, 3 })));
            V3[] before = Positions();

            IDictionary<string, object> envelope = Run(Full(
                handle, mode, new[] { SecondPicked }, new[] { 0, 1, 2, outside }));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            Assert.Contains(GuideIndices, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(Rigid)]
        [InlineData(Interpolate)]
        public void AGuideThatIsAlsoPickedIsRefused(string mode)
        {
            int handle = Turned(QuarterTurnAboutY);
            ComposedEditFixture.Value(Run(Full(
                handle, mode, new[] { FirstPicked }, new[] { 0, 1, 2, 3 })));
            V3[] before = Positions();

            IDictionary<string, object> envelope = Run(Full(
                handle, mode, new[] { FirstPicked, SecondPicked }, new[] { 0, 1, 2, SecondPicked }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(GuideIndices, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData("diagonal")]
        [InlineData("")]
        [InlineData("Rigid")]
        [InlineData(1.0)]
        public void AModeThatIsNotOneOfTheTwoIsRefused(object given)
        {
            int handle = Turned(QuarterTurnAboutY);
            ComposedEditFixture.Value(Run(Full(
                handle, Rigid, new[] { FirstPicked }, new[] { 0, 1, 2, 3 })));
            V3[] before = Positions();

            IDictionary<string, object> envelope = Run(With(
                Full(handle, Rigid, new[] { SecondPicked }, new[] { 0, 1, 2, 3 }), Mode, given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Mode, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Fact]
        public void LeavingOutTheModeIsRefused()
        {
            int handle = Turned(QuarterTurnAboutY);
            ComposedEditFixture.Value(Run(Full(
                handle, Rigid, new[] { FirstPicked }, new[] { 0, 1, 2, 3 })));
            V3[] before = Positions();

            IDictionary<string, object> envelope = Run(Without(
                Full(handle, Rigid, new[] { SecondPicked }, new[] { 0, 1, 2, 3 }), Mode));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Mode, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(Rigid)]
        [InlineData(Interpolate)]
        public void LeavingOutTheCopyIsRefused(string mode)
        {
            int handle = Turned(QuarterTurnAboutY);
            ComposedEditFixture.Value(Run(Full(
                handle, mode, new[] { FirstPicked }, new[] { 0, 1, 2, 3 })));
            V3[] before = Positions();

            IDictionary<string, object> envelope = Run(Without(
                Full(handle, mode, new[] { SecondPicked }, new[] { 0, 1, 2, 3 }), BasePmxHandle));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(BasePmxHandle, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Fact]
        public void ACopyHandleThatIsNotHeldIsRefused()
        {
            int handle = Turned(QuarterTurnAboutY);
            ComposedEditFixture.Value(Run(Full(
                handle, Rigid, new[] { FirstPicked }, new[] { 0, 1, 2, 3 })));
            V3[] before = Positions();

            IDictionary<string, object> envelope = Run(Full(
                handle + 1000, Rigid, new[] { SecondPicked }, new[] { 0, 1, 2, 3 }));

            Assert.Equal(ToolEnvelope.InvalidHandle, ComposedEditFixture.Code(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData("vertex", Rigid)]
        [InlineData("face", Rigid)]
        [InlineData("bone", Rigid)]
        [InlineData("vertex", Interpolate)]
        [InlineData("face", Interpolate)]
        [InlineData("bone", Interpolate)]
        public void ACopyWhoseCountsDifferFromTheModelIsRefused(string differing, string mode)
        {
            int same = Turned(QuarterTurnAboutY);
            ComposedEditFixture.Value(Run(Full(
                same, mode, new[] { FirstPicked }, new[] { 0, 1, 2, 3 })));
            V3[] before = Positions();

            FakePmx copy = FakeEditorState.Duplicate(_fixture.Model);
            switch (differing)
            {
                case "vertex":
                    copy.Vertex.Add(new FakeVertex(5f, 5f, 5f));
                    break;

                case "face":
                    ((FakeMaterial)copy.Material[0]).Faces.RemoveAt(0);
                    break;

                default:
                    copy.Bone.Add(new FakeBone("ボーン"));
                    break;
            }

            int handle = _fixture.Handles.Issue(typeof(IPXPmx).FullName, copy, () => { });
            IDictionary<string, object> envelope = Run(Full(
                handle, mode, new[] { SecondPicked }, new[] { 0, 1, 2, 3 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(GuideIndices)]
        [InlineData(Mode)]
        [InlineData(BasePmxHandle)]
        public void TheGuideArgumentsArePassedOnlyToFollowingTheGuide(string name)
        {
            int handle = Turned(QuarterTurnAboutY);
            ComposedEditFixture.Value(Run(Full(
                handle, Rigid, new[] { FirstPicked }, new[] { 0, 1, 2, 3 })));
            V3[] before = Positions();
            object given = name == GuideIndices
                ? new object[] { 0, 1, 2, 3 }
                : name == Mode ? (object)Rigid : (long)handle;

            IDictionary<string, object> envelope = _fixture.Call(
                ModelEditVertices.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", ModelEditVertices.Align),
                    ComposedEditFixture.Given("indices", new object[] { SecondPicked }),
                    ComposedEditFixture.Given("axis", "y"),
                    ComposedEditFixture.Given(name, given)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        private static V3[] PulledBase()
        {
            return new[]
            {
                new V3(0f, 0f, 0f), new V3(4f, 0f, 0f), new V3(0f, 3f, 4f), new V3(1f, 0f, 0f),
                new V3(3.99f, 0f, 0f), new V3(0f, 0f, 0f),
            };
        }

        private static V3[] PulledCurrent()
        {
            V3[] current = PulledBase();
            current[GuideA] = new V3(0f, 10f, 0f);
            current[GuideB] = new V3(4f, 0f, 20f);

            return current;
        }

        private int Pulled()
        {
            return Scene(PulledBase(), PulledCurrent());
        }

        private int Turned(string turn)
        {
            V3[] baseline = Corners.Concat(Row).Concat(new[] { FirstPoint, SecondPoint }).ToArray();
            V3[] current = baseline.Select((point, at) => at < FirstPicked ? Moved(turn, point) : point).ToArray();

            return Scene(baseline, current);
        }

        private static V3 Moved(string turn, V3 point)
        {
            V3 turned = turn == QuarterTurnAboutY
                ? new V3(point.Z, point.Y, -point.X)
                : new V3(point.Z, point.X, point.Y);

            return new V3(turned.X + 2f, turned.Y + 3f, turned.Z - 1f);
        }

        private int Scene(V3[] baseline, V3[] current)
        {
            FakePmx held = new FakePmx();
            Fill(held, baseline);
            _held = held;
            Fill(_fixture.Model, current);

            return _fixture.Handles.Issue(typeof(IPXPmx).FullName, held, () => { });
        }

        private static void Fill(FakePmx model, V3[] positions)
        {
            foreach (V3 position in positions)
            {
                model.Vertex.Add(new FakeVertex(position.X, position.Y, position.Z));
            }

            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(model.Vertex[0], model.Vertex[1], model.Vertex[2]));
            model.Material.Add(material);
        }

        private static List<KeyValuePair<string, object>> Full(
            int handle, string mode, int[] picked, int[] guides)
        {
            return new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", FollowGuide),
                ComposedEditFixture.Given("indices", picked.Cast<object>().ToArray()),
                ComposedEditFixture.Given(GuideIndices, guides.Cast<object>().ToArray()),
                ComposedEditFixture.Given(Mode, mode),
                ComposedEditFixture.Given(BasePmxHandle, (long)handle),
            };
        }

        private static List<KeyValuePair<string, object>> Without(
            List<KeyValuePair<string, object>> given, string name)
        {
            return given.Where(pair => pair.Key != name).ToList();
        }

        private static List<KeyValuePair<string, object>> With(
            List<KeyValuePair<string, object>> given, string name, object value)
        {
            List<KeyValuePair<string, object>> made = Without(given, name);
            made.Add(ComposedEditFixture.Given(name, value));

            return made;
        }

        private IDictionary<string, object> Run(List<KeyValuePair<string, object>> given)
        {
            return _fixture.Call(
                ModelEditVertices.ToolName, ComposedEditFixture.Arguments(given.ToArray()));
        }

        private V3[] Positions()
        {
            return _fixture.Model.Vertex.Select(vertex => vertex.Position).ToArray();
        }

        private void AssertAt(int index, float x, float y, float z, int digits = Digits)
        {
            V3 found = _fixture.Model.Vertex[index].Position;
            Assert.Equal(x, found.X, digits);
            Assert.Equal(y, found.Y, digits);
            Assert.Equal(z, found.Z, digits);
        }

        private void AssertSame(V3 wanted, int index)
        {
            V3 found = _fixture.Model.Vertex[index].Position;
            Assert.Equal(wanted.X, found.X);
            Assert.Equal(wanted.Y, found.Y);
            Assert.Equal(wanted.Z, found.Z);
        }

        private void AssertUnchanged(V3[] before)
        {
            Assert.Equal(before.Length, _fixture.Model.Vertex.Count);
            for (int at = 0; at < before.Length; at++)
            {
                AssertSame(before[at], at);
            }
        }
    }
}
