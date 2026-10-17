using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditVerticesSweepBandTests : IDisposable
    {
        private const string SweepBand = "sweepBand";

        private const string RootIndices = "rootIndices";

        private const string LayerTolerance = "layerTolerance";

        private const string Offsets = "offsets";

        private const string FixedEnds = "fixedEnds";

        private const string Changed = "changed";

        private const int Digits = 4;

        private const int FoldLayers = 7;

        private const int FoldAt = 3;

        private const int StraightLayers = 5;

        private const int StretchedLayers = 4;

        private const int BentLayers = 10;

        private const int SegmentLayers = 3;

        private const float HalfWidth = 0.5f;

        private const float Width = 2f * HalfWidth;

        private const double Tolerance = 0.01;

        private const double FirstT = 1.5;

        private const double SecondT = 4.0;

        private const double SecondLayerShare = (2.0 - FirstT) / (SecondT - FirstT);

        private const double ThirdLayerShare = (3.0 - FirstT) / (SecondT - FirstT);

        private const double Slope = 0.75;

        private const double SlopeCos = 0.8;

        private const double SlopeSin = 0.6;

        private const float StretchedRail = 1.04f;

        private static readonly V3 FirstMove = new V3(0.3f, 0.2f, -0.4f);

        private static readonly V3 SecondMove = new V3(-0.2f, 0.4f, 0.1f);

        private static readonly V3 WholeMove = new V3(0.5f, -1f, 2f);

        private static readonly int[] Roots = { 0, 1 };

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void EachLayerMovesByTheOffsetsInterpolatedAtItsDistanceAlongTheBandAndHeldBeyondTheEnds()
        {
            Fold();
            V3[] before = Positions();

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(
                FoldLayers * 2, Tolerance, FoldOffsets(), 1)));

            V3[] moves =
            {
                FirstMove,
                Between(FirstMove, SecondMove, SecondLayerShare),
                Between(FirstMove, SecondMove, ThirdLayerShare),
                SecondMove,
                SecondMove,
            };
            Assert.Equal(moves.Length * 2, value[Changed]);
            Assert.Equal(1, _fixture.Commits);
            for (int at = 0; at < moves.Length; at++)
            {
                AssertNear(Plus(Middle(before, at + 1), moves[at]), Middle(Positions(), at + 1));
            }
        }

        [Fact]
        public void EachLayerKeepsItsCrossSectionWhenTheCenterlineIsBent()
        {
            Fold();

            ComposedEditFixture.Value(Run(Full(FoldLayers * 2, Tolerance, FoldOffsets(), 1)));

            V3[] after = Positions();
            for (int layer = 1; layer < FoldLayers - 1; layer++)
            {
                V3 middle = Middle(after, layer);
                Assert.Equal(Width, Distance(after[layer * 2], after[layer * 2 + 1]), Digits);
                Assert.Equal(HalfWidth, Distance(after[layer * 2], middle), Digits);
                Assert.Equal(HalfWidth, Distance(after[layer * 2 + 1], middle), Digits);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void TheLayersAtBothEndsThatFixedEndsCountsStayWhereTheyAre(int fixedEnds)
        {
            Fold();
            V3[] before = Positions();

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(
                FoldLayers * 2, Tolerance, FoldOffsets(), fixedEnds)));

            Assert.Equal((FoldLayers - 2 * fixedEnds) * 2, value[Changed]);
            for (int layer = 0; layer < FoldLayers; layer++)
            {
                bool fixedLayer = layer < fixedEnds || layer >= FoldLayers - fixedEnds;
                for (int at = layer * 2; at < layer * 2 + 2; at++)
                {
                    if (fixedLayer)
                    {
                        AssertSame(before[at], at);
                    }
                    else
                    {
                        Assert.False(Same(before[at], at));
                    }
                }
            }
        }

        [Fact]
        public void ABandMovedAsAWholeIsOnlyTranslatedAndItsCrossSectionIsNotTurned()
        {
            Straight();
            _fixture.Model.Vertex.Add(new FakeVertex(9f, 9f, 9f));
            V3[] before = Positions();

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(
                StraightLayers * 2, Tolerance, WholeOffsets(), 0)));

            Assert.Equal(StraightLayers * 2, value[Changed]);
            for (int at = 0; at < StraightLayers * 2; at++)
            {
                AssertNear(Plus(before[at], WholeMove), _fixture.Model.Vertex[at].Position);
            }

            AssertSame(before[StraightLayers * 2], StraightLayers * 2);
        }

        [Fact]
        public void AStraightBandWhoseCenterlineIsTiltedTurnsEachCrossSectionWithTheTangent()
        {
            Straight();
            double reach = StraightLayers - 1;

            ComposedEditFixture.Value(Run(Full(
                StraightLayers * 2,
                Tolerance,
                new object[] { Offset(0.0, 0f, 0f, 0f), Offset(reach, 0f, (float)(Slope * reach), 0f) },
                0)));

            for (int layer = 0; layer < StraightLayers; layer++)
            {
                V3 centre = new V3(layer, (float)(Slope * layer), 0f);
                float across = (float)(HalfWidth * SlopeCos);
                float along = (float)(HalfWidth * SlopeSin);
                AssertNear(
                    new V3(centre.X - along, centre.Y + across, 0f),
                    _fixture.Model.Vertex[layer * 2].Position);
                AssertNear(
                    new V3(centre.X + along, centre.Y - across, 0f),
                    _fixture.Model.Vertex[layer * 2 + 1].Position);
            }
        }

        [Fact]
        public void ACenterlineBentThroughThreeDirectionsCarriesTheCrossSectionByTheMinimalTurnFromLayerToLayer()
        {
            Straight(BentLayers);
            V3[] corners =
            {
                new V3(SegmentLayers, 0f, 0f),
                new V3(SegmentLayers, SegmentLayers, 0f),
                new V3(SegmentLayers, SegmentLayers, SegmentLayers),
            };
            V3[] directions = { new V3(1f, 0f, 0f), new V3(0f, 1f, 0f), new V3(0f, 0f, 1f) };
            V3 acrossAlongY = new V3(-HalfWidth, 0f, 0f);
            V3[] across = { new V3(0f, HalfWidth, 0f), acrossAlongY, acrossAlongY };
            List<object> offsets = new List<object> { Offset(0.0, 0f, 0f, 0f) };
            for (int corner = 0; corner < corners.Length; corner++)
            {
                V3 reached = corners[corner];
                int layer = (corner + 1) * SegmentLayers;
                offsets.Add(Offset(layer, reached.X - layer, reached.Y, reached.Z));
            }

            ComposedEditFixture.Value(Run(Full(BentLayers * 2, Tolerance, offsets.ToArray(), 0)));

            for (int segment = 0; segment < directions.Length; segment++)
            {
                V3 start = segment == 0 ? new V3(0f, 0f, 0f) : corners[segment - 1];
                for (int step = 1; step < SegmentLayers; step++)
                {
                    int layer = segment * SegmentLayers + step;
                    V3 centre = Plus(start, Scaled(directions[segment], step));
                    AssertNear(Plus(centre, across[segment]), _fixture.Model.Vertex[layer * 2].Position);
                    AssertNear(Plus(centre, Scaled(across[segment], -1f)), _fixture.Model.Vertex[layer * 2 + 1].Position);
                }
            }
        }

        [Theory]
        [InlineData(0.15, true, 4)]
        [InlineData(0.01, false, 5)]
        public void VerticesWhoseDistancesDifferByAtMostTheLayerToleranceShareOneLayer(
            double tolerance, bool sharedLayer, int changed)
        {
            Stretched();
            V3[] before = Positions();
            int upperLast = (StretchedLayers - 1) * 2;
            int lowerLast = upperLast + 1;

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(
                StretchedLayers * 2, tolerance, WholeOffsets(), 1)));

            Assert.Equal(changed, value[Changed]);
            AssertSame(before[lowerLast], lowerLast);
            if (sharedLayer)
            {
                AssertSame(before[upperLast], upperLast);
            }
            else
            {
                AssertNear(Plus(before[upperLast], WholeMove), _fixture.Model.Vertex[upperLast].Position);
            }
        }

        [Theory]
        [InlineData(RootIndices)]
        [InlineData(LayerTolerance)]
        [InlineData(Offsets)]
        public void LeavingOutARequiredArgumentIsRefused(string name)
        {
            V3[] before = Accepted();

            IDictionary<string, object> envelope = Run(Without(Valid(), name));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(-0.1)]
        [InlineData("wide")]
        public void ALayerToleranceThatIsNotANonNegativeNumberIsRefused(object given)
        {
            V3[] before = Accepted();

            IDictionary<string, object> envelope = Run(With(Valid(), LayerTolerance, given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(LayerTolerance, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData("one")]
        public void AFixedEndsThatIsNotANonNegativeWholeNumberIsRefused(object given)
        {
            V3[] before = Accepted();

            IDictionary<string, object> envelope = Run(With(Valid(), FixedEnds, given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(FixedEnds, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void OffsetsThatAreNotANonEmptyListOfDistanceAndMovePairsAreRefused(int variant)
        {
            V3[] before = Accepted();
            object[] wrong =
            {
                new object[0],
                new object[] { 1 },
                new object[] { new Dictionary<string, object> { { "t", 1.0 } } },
                new object[] { new Dictionary<string, object> { { "move", new object[] { 0.0, 0.0, 0.0 } } } },
                new object[] { new Dictionary<string, object> { { "t", 1.0 }, { "move", new object[] { 0.0, 0.0 } } } },
                new object[]
                {
                    new Dictionary<string, object> { { "t", 1.0 }, { "move", new object[] { 0.0, "up", 0.0 } } },
                },
            };

            IDictionary<string, object> envelope = Run(With(Valid(), Offsets, wrong[variant]));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Offsets, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Fact]
        public void OffsetsWhoseDistancesDoNotAscendAreRefused()
        {
            V3[] before = Accepted();

            IDictionary<string, object> envelope = Run(With(
                Valid(),
                Offsets,
                new object[] { Offset(4.0, 0f, 1f, 0f), Offset(1.0, 0f, 0f, 0f) }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Offsets, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Fact]
        public void ARootThatIsNotAmongTheBandVerticesIsRefused()
        {
            V3[] before = Accepted();

            IDictionary<string, object> envelope = Run(With(
                Valid(),
                "indices",
                Enumerable.Range(1, StraightLayers * 2 - 1).Cast<object>().ToArray()));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(RootIndices, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Fact]
        public void ABandVertexNoFaceJoinsToTheRestIsRefused()
        {
            Accepted();
            _fixture.Model.Vertex.Add(new FakeVertex(9f, 9f, 9f));
            V3[] before = Positions();

            IDictionary<string, object> envelope = Run(With(
                Valid(),
                "indices",
                Enumerable.Range(0, StraightLayers * 2 + 1).Cast<object>().ToArray()));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(RootIndices)]
        [InlineData(LayerTolerance)]
        [InlineData(Offsets)]
        [InlineData(FixedEnds)]
        public void TheSweepArgumentsArePassedOnlyToSweepingTheBand(string name)
        {
            V3[] before = Accepted();
            object given = name == RootIndices
                ? new object[] { 0, 1 }
                : name == LayerTolerance ? (object)Tolerance : name == FixedEnds ? (object)1 : WholeOffsets();

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

        private static object[] FoldOffsets()
        {
            return new object[]
            {
                Offset(FirstT, FirstMove.X, FirstMove.Y, FirstMove.Z),
                Offset(SecondT, SecondMove.X, SecondMove.Y, SecondMove.Z),
            };
        }

        private static object[] WholeOffsets()
        {
            return new object[]
            {
                Offset(0.0, WholeMove.X, WholeMove.Y, WholeMove.Z),
                Offset(10.0, WholeMove.X, WholeMove.Y, WholeMove.Z),
            };
        }

        private static object Offset(double t, float x, float y, float z)
        {
            return new Dictionary<string, object>
            {
                { "t", t },
                { "move", new object[] { (double)x, (double)y, (double)z } },
            };
        }

        private void Fold()
        {
            List<V3> upper = new List<V3>();
            List<V3> lower = new List<V3>();
            for (int layer = 0; layer < FoldLayers; layer++)
            {
                float x = Math.Min(layer, FoldAt);
                float z = Math.Max(layer - FoldAt, 0);
                upper.Add(new V3(x, HalfWidth, z));
                lower.Add(new V3(x, -HalfWidth, z));
            }

            Ladder(upper, lower);
        }

        private void Straight(int layers = StraightLayers)
        {
            List<V3> upper = new List<V3>();
            List<V3> lower = new List<V3>();
            for (int layer = 0; layer < layers; layer++)
            {
                upper.Add(new V3(layer, HalfWidth, 0f));
                lower.Add(new V3(layer, -HalfWidth, 0f));
            }

            Ladder(upper, lower);
        }

        private void Stretched()
        {
            List<V3> upper = new List<V3>();
            List<V3> lower = new List<V3>();
            for (int layer = 0; layer < StretchedLayers; layer++)
            {
                upper.Add(new V3(layer, HalfWidth, 0f));
                lower.Add(new V3(StretchedRail * layer, -HalfWidth, 0f));
            }

            Ladder(upper, lower);
        }

        private void Ladder(IList<V3> upper, IList<V3> lower)
        {
            FakeMaterial material = new FakeMaterial("材質");
            for (int layer = 0; layer < upper.Count; layer++)
            {
                _fixture.Model.Vertex.Add(new FakeVertex(upper[layer].X, upper[layer].Y, upper[layer].Z));
                _fixture.Model.Vertex.Add(new FakeVertex(lower[layer].X, lower[layer].Y, lower[layer].Z));
            }

            for (int layer = 0; layer + 1 < upper.Count; layer++)
            {
                IPXVertex[] at = _fixture.Model.Vertex.Skip(layer * 2).Take(4).ToArray();
                material.Faces.Add(new FakeFace(at[0], at[1], at[2]));
                material.Faces.Add(new FakeFace(at[1], at[3], at[2]));
            }

            _fixture.Model.Material.Add(material);
        }

        private V3[] Accepted()
        {
            Straight();
            ComposedEditFixture.Value(Run(Valid()));

            return Positions();
        }

        private static List<KeyValuePair<string, object>> Valid()
        {
            return Full(StraightLayers * 2, Tolerance, WholeOffsets(), 1);
        }

        private static List<KeyValuePair<string, object>> Full(
            int vertexCount, double tolerance, object[] offsets, int fixedEnds)
        {
            return new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", SweepBand),
                ComposedEditFixture.Given("indices", Enumerable.Range(0, vertexCount).Cast<object>().ToArray()),
                ComposedEditFixture.Given(RootIndices, Roots.Cast<object>().ToArray()),
                ComposedEditFixture.Given(LayerTolerance, tolerance),
                ComposedEditFixture.Given(Offsets, offsets),
                ComposedEditFixture.Given(FixedEnds, fixedEnds),
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

        private static V3 Middle(V3[] positions, int layer)
        {
            V3 first = positions[layer * 2];
            V3 second = positions[layer * 2 + 1];

            return new V3((first.X + second.X) / 2f, (first.Y + second.Y) / 2f, (first.Z + second.Z) / 2f);
        }

        private static V3 Plus(V3 given, V3 move)
        {
            return new V3(given.X + move.X, given.Y + move.Y, given.Z + move.Z);
        }

        private static V3 Scaled(V3 given, float factor)
        {
            return new V3(given.X * factor, given.Y * factor, given.Z * factor);
        }

        private static V3 Between(V3 from, V3 to, double share)
        {
            return new V3(
                (float)(from.X + (to.X - from.X) * share),
                (float)(from.Y + (to.Y - from.Y) * share),
                (float)(from.Z + (to.Z - from.Z) * share));
        }

        private static double Distance(V3 first, V3 second)
        {
            double x = first.X - second.X;
            double y = first.Y - second.Y;
            double z = first.Z - second.Z;

            return Math.Sqrt(x * x + y * y + z * z);
        }

        private static void AssertNear(V3 wanted, V3 found)
        {
            Assert.Equal(wanted.X, found.X, Digits);
            Assert.Equal(wanted.Y, found.Y, Digits);
            Assert.Equal(wanted.Z, found.Z, Digits);
        }

        private bool Same(V3 wanted, int index)
        {
            V3 found = _fixture.Model.Vertex[index].Position;

            return wanted.X == found.X && wanted.Y == found.Y && wanted.Z == found.Z;
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
