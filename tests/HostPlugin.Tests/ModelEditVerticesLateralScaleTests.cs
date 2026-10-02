using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditVerticesLateralScaleTests : IDisposable
    {
        private const string LateralScale = "lateralScale";

        private const string From = "from";

        private const string To = "to";

        private const string Across = "across";

        private const string Knots = "knots";

        private const string Changed = "changed";

        private const int Digits = 4;

        private const float AxisLength = 10f;

        private const float AcrossOffset = 1.5f;

        private const float OtherOffset = -2f;

        private const int AcceptedPairs = 2;

        private const double FirstS = 0.2;

        private const double MiddleS = 0.5;

        private const double LastS = 0.8;

        private const double FirstScale = 2.0;

        private const double MiddleScale = 0.5;

        private const double LastScale = 3.0;

        private static readonly V3 AxisFrom = new V3(0f, 0f, 0f);

        private static readonly V3 AxisTo = new V3(AxisLength, 0f, 0f);

        private static readonly V3 AcrossY = new V3(0f, 1f, 0f);

        private static readonly V3 SlantFrom = new V3(1f, -1f, 2f);

        private static readonly V3 SlantAxis = new V3(6f, 3f, 6f);

        private static readonly V3 SlantAcross = new V3(2f, -2f, -1f);

        private static readonly V3 SlantAcrossUnit = new V3(2f / 3f, -2f / 3f, -1f / 3f);

        private static readonly V3 SlantOther = new V3(1f / 3f, 2f / 3f, -2f / 3f);

        private static readonly V3 SkewedAcross = new V3(3f, 2f, 1f);

        private static readonly V3 SkewedPerpendicular = new V3(0f, 2f, 1f);

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Theory]
        [InlineData(FirstS, FirstScale)]
        [InlineData(MiddleS, MiddleScale)]
        [InlineData(LastS, LastScale)]
        public void TheAcrossWidthAtAKnotIsTheKnotScaleAndNothingElseMoves(double s, double scale)
        {
            Place(s);

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Valid(2)));

            Assert.Equal(2, value[Changed]);
            Assert.Equal(1, _fixture.Commits);
            AssertScaled(0, s, scale);
        }

        [Fact]
        public void AVertexThatIsNotAmongTheIndicesStaysWhereItIs()
        {
            Place(MiddleS);
            _fixture.Model.Vertex.Add(new FakeVertex((float)(AxisLength * FirstS), AcrossOffset, OtherOffset));
            V3[] before = Positions();

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Valid(2)));

            Assert.Equal(2, value[Changed]);
            AssertScaled(0, MiddleS, MiddleScale);
            AssertSame(before[2], 2);
        }

        [Fact]
        public void BetweenTwoKnotsTheScaleStaysStrictlyBetweenTheirScalesAndChangesMonotonicallyWithThePosition()
        {
            double[] falling = { 0.25, 0.3, 0.35, 0.4, 0.45 };
            double[] rising = { 0.55, 0.6, 0.65, 0.7, 0.75 };
            Place(falling.Concat(rising).ToArray());

            ComposedEditFixture.Value(Run(Valid((falling.Length + rising.Length) * 2)));

            double[] after = Enumerable.Range(0, falling.Length + rising.Length)
                .Select(pair => Positions()[pair * 2].Y / (double)AcrossOffset)
                .ToArray();
            AssertBetweenAndMonotone(after.Take(falling.Length).ToArray(), FirstScale, MiddleScale);
            AssertBetweenAndMonotone(after.Skip(falling.Length).ToArray(), MiddleScale, LastScale);
        }

        [Theory]
        [InlineData(-0.3, FirstScale)]
        [InlineData(0.0, FirstScale)]
        [InlineData(1.0, LastScale)]
        [InlineData(1.4, LastScale)]
        public void BeyondTheEndsOfTheKnotsTheScaleOfTheNearerEndKnotHolds(double s, double scale)
        {
            Place(s);

            ComposedEditFixture.Value(Run(Valid(2)));

            AssertScaled(0, s, scale);
        }

        [Fact]
        public void APointOnTheAxisLineStaysWhereItIs()
        {
            double[] onAxis = { FirstS, MiddleS, -0.3 };
            foreach (double s in onAxis)
            {
                _fixture.Model.Vertex.Add(new FakeVertex((float)(AxisLength * s), 0f, 0f));
            }

            Place(MiddleS);
            V3[] before = Positions();

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Valid(onAxis.Length + 2)));

            Assert.Equal(2, value[Changed]);
            for (int at = 0; at < onAxis.Length; at++)
            {
                AssertSame(before[at], at);
            }

            AssertScaled(0, MiddleS, MiddleScale, onAxis.Length);
        }

        [Theory]
        [InlineData(FirstS, FirstScale)]
        [InlineData(MiddleS, MiddleScale)]
        [InlineData(1.4, LastScale)]
        public void AnAxisThatIsNotParallelToACoordinateAxisScalesOnlyTheAcrossComponent(double s, double scale)
        {
            _fixture.Model.Vertex.Add(Slanted(s, AcrossOffset, OtherOffset));
            _fixture.Model.Vertex.Add(Slanted(s, -AcrossOffset, OtherOffset));

            ComposedEditFixture.Value(Run(Full(
                2, SlantFrom, Plus(SlantFrom, SlantAxis), SlantAcross, Table())));

            V3[] after = Positions();
            AssertNear(SlantedPlace(s, AcrossOffset * scale, OtherOffset), after[0]);
            AssertNear(SlantedPlace(s, -AcrossOffset * scale, OtherOffset), after[1]);
        }

        [Fact]
        public void AnAcrossThatIsNotPerpendicularToTheAxisScalesTheComponentPerpendicularToIt()
        {
            Place(FirstS);
            V3 at = Positions()[0];
            double share = Dot(at, SkewedPerpendicular) / Dot(SkewedPerpendicular, SkewedPerpendicular);
            V3 wanted = Plus(at, Scaled(SkewedPerpendicular, (float)(share * (FirstScale - 1.0))));

            ComposedEditFixture.Value(Run(Full(
                2, AxisFrom, AxisTo, SkewedAcross, Table())));

            AssertNear(wanted, Positions()[0]);
            Assert.Equal(0.4, wanted.Y - at.Y, Digits);
            Assert.Equal(0.2, wanted.Z - at.Z, Digits);
            Assert.Equal(at.X, Positions()[0].X, Digits);
        }

        [Theory]
        [InlineData(1.0, 0.0, 0.0)]
        [InlineData(-2.0, 0.0, 0.0)]
        [InlineData(0.0, 0.0, 0.0)]
        public void AnAcrossParallelToTheAxisOrWithoutADirectionIsRefused(double x, double y, double z)
        {
            V3[] before = Accepted();

            IDictionary<string, object> envelope = Run(With(Valid(), Across, Point(x, y, z)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Across, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Fact]
        public void AnAxisWhoseTwoEndsAreTheSamePointIsRefused()
        {
            V3[] before = Accepted();

            IDictionary<string, object> envelope = Run(With(Valid(), To, Point(AxisFrom)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(From, ComposedEditFixture.Message(envelope));
            Assert.Contains(To, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(From)]
        [InlineData(To)]
        [InlineData(Across)]
        [InlineData(Knots)]
        public void LeavingOutARequiredArgumentIsRefused(string name)
        {
            V3[] before = Accepted();

            IDictionary<string, object> envelope = Run(Without(Valid(), name));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(From, 0)]
        [InlineData(From, 1)]
        [InlineData(From, 2)]
        [InlineData(From, 3)]
        [InlineData(To, 0)]
        [InlineData(To, 1)]
        [InlineData(To, 2)]
        [InlineData(To, 3)]
        [InlineData(Across, 0)]
        [InlineData(Across, 1)]
        [InlineData(Across, 2)]
        [InlineData(Across, 3)]
        public void APointThatIsNotAListOfThreeFiniteNumbersIsRefused(string name, int variant)
        {
            V3[] before = Accepted();
            object[] wrong =
            {
                new object[] { 1.0, 0.0 },
                new object[] { 1.0, "up", 0.0 },
                "right",
                new object[] { 1.0, double.NaN, 0.0 },
            };

            IDictionary<string, object> envelope = Run(With(Valid(), name, wrong[variant]));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        public void KnotsThatAreNotANonEmptyAscendingListOfPositionAndFiniteScalePairsAreRefused(int variant)
        {
            V3[] before = Accepted();
            object[] wrong =
            {
                new object[0],
                1,
                new object[] { 1 },
                new object[] { new Dictionary<string, object> { { "scale", 1.0 } } },
                new object[] { new Dictionary<string, object> { { "s", 0.5 } } },
                new object[] { Knot("a", 1.0) },
                new object[] { Knot(0.5, "wide") },
                new object[] { Knot(0.5, double.NaN) },
                new object[] { Knot(0.5, double.PositiveInfinity) },
                new object[] { Knot(double.NaN, 1.0) },
                new object[] { Knot(LastS, LastScale), Knot(FirstS, FirstScale) },
            };

            IDictionary<string, object> envelope = Run(With(Valid(), Knots, wrong[variant]));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Knots, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(From)]
        [InlineData(To)]
        [InlineData(Across)]
        [InlineData(Knots)]
        public void TheLateralScaleArgumentsArePassedOnlyToScalingLaterally(string name)
        {
            V3[] before = Accepted();
            object given = name == Knots ? Table() : Point(1.0, 0.0, 0.0);

            IDictionary<string, object> envelope = _fixture.Call(
                ModelEditVertices.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", ModelEditVertices.Align),
                    ComposedEditFixture.Given("indices", new object[] { 0 }),
                    ComposedEditFixture.Given("axis", "y"),
                    ComposedEditFixture.Given(name, given)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        private static object[] Table()
        {
            return new object[]
            {
                Knot(FirstS, FirstScale),
                Knot(MiddleS, MiddleScale),
                Knot(LastS, LastScale),
            };
        }

        private static object Knot(object s, object scale)
        {
            return new Dictionary<string, object> { { "s", s }, { "scale", scale } };
        }

        private static object Point(V3 given)
        {
            return Point(given.X, given.Y, given.Z);
        }

        private static object Point(double x, double y, double z)
        {
            return new object[] { x, y, z };
        }

        private void Place(params double[] positions)
        {
            foreach (double s in positions)
            {
                float x = (float)(AxisLength * s);
                _fixture.Model.Vertex.Add(new FakeVertex(x, AcrossOffset, OtherOffset));
                _fixture.Model.Vertex.Add(new FakeVertex(x, -AcrossOffset, OtherOffset));
            }
        }

        private static FakeVertex Slanted(double s, float across, float other)
        {
            V3 place = SlantedPlace(s, across, other);

            return new FakeVertex(place.X, place.Y, place.Z);
        }

        private static V3 SlantedPlace(double s, double across, double other)
        {
            return new V3(
                (float)(SlantFrom.X + s * SlantAxis.X + across * SlantAcrossUnit.X + other * SlantOther.X),
                (float)(SlantFrom.Y + s * SlantAxis.Y + across * SlantAcrossUnit.Y + other * SlantOther.Y),
                (float)(SlantFrom.Z + s * SlantAxis.Z + across * SlantAcrossUnit.Z + other * SlantOther.Z));
        }

        private V3[] Accepted()
        {
            Place(FirstS, MiddleS);
            ComposedEditFixture.Value(Run(Valid()));

            return Positions();
        }

        private static List<KeyValuePair<string, object>> Valid(int vertexCount = AcceptedPairs * 2)
        {
            return Full(vertexCount, AxisFrom, AxisTo, AcrossY, Table());
        }

        private static List<KeyValuePair<string, object>> Full(
            int vertexCount, V3 from, V3 to, V3 across, object[] knots)
        {
            return new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", LateralScale),
                ComposedEditFixture.Given("indices", Enumerable.Range(0, vertexCount).Cast<object>().ToArray()),
                ComposedEditFixture.Given(From, Point(from)),
                ComposedEditFixture.Given(To, Point(to)),
                ComposedEditFixture.Given(Across, Point(across)),
                ComposedEditFixture.Given(Knots, knots),
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

        private static V3 Plus(V3 given, V3 move)
        {
            return new V3(given.X + move.X, given.Y + move.Y, given.Z + move.Z);
        }

        private static V3 Scaled(V3 given, float factor)
        {
            return new V3(given.X * factor, given.Y * factor, given.Z * factor);
        }

        private static double Dot(V3 first, V3 second)
        {
            return (double)first.X * second.X + (double)first.Y * second.Y + (double)first.Z * second.Z;
        }

        private static void AssertNear(V3 wanted, V3 found)
        {
            Assert.Equal(wanted.X, found.X, Digits);
            Assert.Equal(wanted.Y, found.Y, Digits);
            Assert.Equal(wanted.Z, found.Z, Digits);
        }

        private void AssertScaled(int pair, double s, double scale, int first = 0)
        {
            V3[] after = Positions();
            float x = (float)(AxisLength * s);
            float width = (float)(AcrossOffset * scale);
            AssertNear(new V3(x, width, OtherOffset), after[first + pair * 2]);
            AssertNear(new V3(x, -width, OtherOffset), after[first + pair * 2 + 1]);
        }

        private static void AssertBetweenAndMonotone(double[] scales, double start, double end)
        {
            double low = Math.Min(start, end);
            double high = Math.Max(start, end);
            for (int at = 0; at < scales.Length; at++)
            {
                Assert.InRange(scales[at], low + 0.001, high - 0.001);
                if (at > 0)
                {
                    Assert.Equal(Math.Sign(end - start), Math.Sign(scales[at] - scales[at - 1]));
                }
            }
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
