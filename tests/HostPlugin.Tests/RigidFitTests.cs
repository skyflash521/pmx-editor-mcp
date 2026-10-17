using System.Linq;
using Xunit;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp.Tests
{
    public sealed class RigidFitTests
    {
        private const int Digits = 9;

        private static readonly Vec[] Tetrahedron =
        {
            new Vec(1, 0, 0), new Vec(0, 2, 0), new Vec(0, 0, 3), new Vec(1, 1, 1),
        };

        [Fact]
        public void APureRotationAndTranslationIsRecoveredExactly()
        {
            Vec[] to = Tetrahedron.Select(point => new Vec(point.Z + 2, point.Y + 3, -point.X - 1)).ToArray();

            RigidFit fit = RigidFit.Of(Tetrahedron, to);

            Assert.NotNull(fit);
            for (int at = 0; at < Tetrahedron.Length; at++)
            {
                AssertAt(to[at], fit.Point(Tetrahedron[at]));
            }
        }

        [Fact]
        public void TargetsThatAreAMirrorImageStillGetARotationNotAReflection()
        {
            Vec[] to = Tetrahedron.Select(point => new Vec(-point.X, point.Y, point.Z)).ToArray();

            RigidFit fit = RigidFit.Of(Tetrahedron, to);

            Assert.NotNull(fit);
            Vec x = fit.Direction(new Vec(1, 0, 0));
            Vec y = fit.Direction(new Vec(0, 1, 0));
            Vec z = fit.Direction(new Vec(0, 0, 1));
            Assert.Equal(1d, x.Cross(y).Dot(z), Digits);
            Assert.Equal(1d, x.Length, Digits);
            Assert.Equal(0d, x.Dot(y), Digits);
        }

        [Fact]
        public void CoplanarPointsWithRepeatedSingularValuesAreFitByTheRotationThatCarriesThem()
        {
            Vec[] square = { new Vec(1, 1, 0), new Vec(-1, 1, 0), new Vec(-1, -1, 0), new Vec(1, -1, 0) };
            Vec[] to = square.Select(point => new Vec(-point.Y + 5, point.X, 7)).ToArray();

            RigidFit fit = RigidFit.Of(square, to);

            Assert.NotNull(fit);
            for (int at = 0; at < square.Length; at++)
            {
                AssertAt(to[at], fit.Point(square[at]));
            }

            AssertAt(new Vec(0, 0, 1), fit.Direction(new Vec(0, 0, 1)));
        }

        [Fact]
        public void AMirroredSetOfCoplanarPointsStillGetsARotation()
        {
            Vec[] triangle = { new Vec(0, 0, 0), new Vec(2, 0, 0), new Vec(0, 1, 0) };
            Vec[] to = triangle.Select(point => new Vec(-point.X, point.Y, 0)).ToArray();

            RigidFit fit = RigidFit.Of(triangle, to);

            Assert.NotNull(fit);
            Vec x = fit.Direction(new Vec(1, 0, 0));
            Vec y = fit.Direction(new Vec(0, 1, 0));
            Vec z = fit.Direction(new Vec(0, 0, 1));
            Assert.Equal(1d, x.Cross(y).Dot(z), Digits);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void FewerThanThreePointsDoNotFixTheRotation(int count)
        {
            Vec[] from = Tetrahedron.Take(count).ToArray();

            Assert.Null(RigidFit.Of(from, from));
        }

        [Fact]
        public void PointsOnALineInEitherSetDoNotFixTheRotation()
        {
            Vec[] line = { new Vec(0, 0, 0), new Vec(1, 1, 1), new Vec(3, 3, 3) };
            Vec[] triangle = { new Vec(0, 0, 0), new Vec(1, 0, 0), new Vec(0, 1, 0) };

            Assert.Null(RigidFit.Of(line, line));
            Assert.Null(RigidFit.Of(line, triangle));
            Assert.Null(RigidFit.Of(triangle, line));
        }

        [Fact]
        public void PointsThatAreNotFiniteDoNotFixTheRotation()
        {
            Vec[] from = Tetrahedron.ToArray();
            Vec[] to = Tetrahedron.ToArray();
            to[0] = new Vec(double.NaN, 0, 0);

            Assert.Null(RigidFit.Of(from, to));
        }

        [Theory]
        [InlineData(2e-4)]
        [InlineData(5e-4)]
        [InlineData(1e-3)]
        [InlineData(1e-2)]
        public void NearlyCollinearPointsAreFitByTheKnownRotation(double thickness)
        {
            Vec[] from = Thin(thickness);
            Vec[] to = from.Select(Turned).ToArray();

            RigidFit fit = RigidFit.Of(from, to);

            Assert.NotNull(fit);
            for (int at = 0; at < from.Length; at++)
            {
                AssertAt(to[at], fit.Point(from[at]), 7);
            }

            AssertAt(Turned(new Vec(1, 0, 0)) - Turned(new Vec(0, 0, 0)), fit.Direction(new Vec(1, 0, 0)), 7);
            AssertAt(Turned(new Vec(0, 1, 0)) - Turned(new Vec(0, 0, 0)), fit.Direction(new Vec(0, 1, 0)), 7);
        }

        [Theory]
        [InlineData(1e-5)]
        [InlineData(1e-6)]
        [InlineData(1e-7)]
        [InlineData(1e-10)]
        public void PointsThinnerThanTheRotationCanBeToldFromTheirLengthAreRefused(double thickness)
        {
            Assert.Null(RigidFit.Of(Thin(thickness), Thin(thickness).Select(Turned).ToArray()));
        }

        private static Vec[] Thin(double thickness)
        {
            double[] along = { 0, 0.25, 0.5, 0.75, 1 };
            double[][] across = { new[] { 1d, 0d }, new[] { -1d, 1d }, new[] { 0d, -1d }, new[] { 0.5, 0.5 }, new[] { -1d, -1d } };

            return along
                .Select((t, at) => new Vec(10 + t, 5 + (thickness * across[at][0]), 3 + (thickness * across[at][1])))
                .ToArray();
        }

        private static Vec Turned(Vec point)
        {
            double cosine = System.Math.Cos(0.7);
            double sine = System.Math.Sin(0.7);
            Vec axis = new Vec(1d / 3, 2d / 3, 2d / 3);
            Vec turned = (point * cosine) + (axis.Cross(point) * sine) + (axis * (axis.Dot(point) * (1 - cosine)));

            return turned + new Vec(1, -2, 3);
        }

        private static void AssertAt(Vec wanted, Vec found)
        {
            AssertAt(wanted, found, Digits);
        }

        private static void AssertAt(Vec wanted, Vec found, int digits)
        {
            Assert.Equal(wanted.X, found.X, digits);
            Assert.Equal(wanted.Y, found.Y, digits);
            Assert.Equal(wanted.Z, found.Z, digits);
        }
    }
}
