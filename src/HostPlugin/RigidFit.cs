using System;
using System.Collections.Generic;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    internal sealed class RigidFit
    {
        private const int Dimension = 3;

        private static readonly double Tolerance = Dimension * Math.Pow(2, -52);

        private static readonly double Unit = Math.Pow(2, -24);

        private readonly double[,] _rotation;

        private readonly Vec _from;

        private readonly Vec _to;

        private RigidFit(double[,] rotation, Vec from, Vec to)
        {
            _rotation = rotation;
            _from = from;
            _to = to;
        }

        public static RigidFit Of(IList<Vec> from, IList<Vec> to)
        {
            if (from == null)
            {
                throw new ArgumentNullException(nameof(from));
            }

            if (to == null)
            {
                throw new ArgumentNullException(nameof(to));
            }

            if (from.Count != to.Count)
            {
                throw new ArgumentException("組の数がそろっていない。", nameof(to));
            }

            if (from.Count == 0)
            {
                return null;
            }

            Vec fromCentre = Centre(from);
            Vec toCentre = Centre(to);
            double[,] cross = new double[Dimension, Dimension];
            for (int at = 0; at < from.Count; at++)
            {
                Vec source = from[at] - fromCentre;
                Vec target = to[at] - toCentre;
                for (int row = 0; row < Dimension; row++)
                {
                    for (int column = 0; column < Dimension; column++)
                    {
                        cross[row, column] += target.Axis(row) * source.Axis(column);
                    }
                }
            }

            for (int row = 0; row < Dimension; row++)
            {
                for (int column = 0; column < Dimension; column++)
                {
                    if (double.IsNaN(cross[row, column]) || double.IsInfinity(cross[row, column]))
                    {
                        return null;
                    }
                }
            }

            double[,] vectors;
            double[] values;
            Decompose(cross, out values, out vectors);
            int[] order = { 0, 1, 2 };
            Array.Sort(order, (left, right) => values[right].CompareTo(values[left]));
            double largest = values[order[0]];
            double second = values[order[1]];
            if (!(second > 0 && second * Unit >= Tolerance * largest))
            {
                return null;
            }

            Vec firstSource = Column(vectors, order[0]);
            Vec secondSource = Column(vectors, order[1]);
            Vec firstTarget = Column(cross, order[0]) * (1d / largest);
            Vec secondTarget = Column(cross, order[1]);
            secondTarget = secondTarget - (firstTarget * firstTarget.Dot(secondTarget));
            secondTarget = secondTarget * (1d / secondTarget.Length);
            Vec thirdSource = firstSource.Cross(secondSource);
            Vec thirdTarget = firstTarget.Cross(secondTarget);
            double[,] rotation = new double[Dimension, Dimension];
            for (int row = 0; row < Dimension; row++)
            {
                for (int column = 0; column < Dimension; column++)
                {
                    rotation[row, column] =
                        (firstTarget.Axis(row) * firstSource.Axis(column))
                        + (secondTarget.Axis(row) * secondSource.Axis(column))
                        + (thirdTarget.Axis(row) * thirdSource.Axis(column));
                }
            }

            return new RigidFit(rotation, fromCentre, toCentre);
        }

        public Vec Point(Vec at)
        {
            return Direction(at - _from) + _to;
        }

        public Vec Direction(Vec along)
        {
            return Apply(_rotation, along);
        }

        private static Vec Centre(IList<Vec> points)
        {
            Vec sum = default(Vec);
            foreach (Vec point in points)
            {
                sum = sum + point;
            }

            return sum * (1d / points.Count);
        }

        private static Vec Apply(double[,] matrix, Vec by)
        {
            return new Vec(
                (matrix[0, 0] * by.X) + (matrix[0, 1] * by.Y) + (matrix[0, 2] * by.Z),
                (matrix[1, 0] * by.X) + (matrix[1, 1] * by.Y) + (matrix[1, 2] * by.Z),
                (matrix[2, 0] * by.X) + (matrix[2, 1] * by.Y) + (matrix[2, 2] * by.Z));
        }

        private static Vec Column(double[,] matrix, int at)
        {
            return new Vec(matrix[0, at], matrix[1, at], matrix[2, at]);
        }

        private static void Decompose(double[,] product, out double[] values, out double[,] vectors)
        {
            vectors = new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            bool turned = true;
            while (turned)
            {
                turned = false;
                for (int first = 0; first < Dimension - 1; first++)
                {
                    for (int second = first + 1; second < Dimension; second++)
                    {
                        double alpha = Column(product, first).Dot(Column(product, first));
                        double beta = Column(product, second).Dot(Column(product, second));
                        double gamma = Column(product, first).Dot(Column(product, second));
                        if (Math.Min(alpha, beta) <= Tolerance * Tolerance * Math.Max(alpha, beta)
                            || Math.Abs(gamma) <= Tolerance * Math.Sqrt(alpha * beta))
                        {
                            continue;
                        }

                        turned = true;
                        double zeta = (beta - alpha) / (2 * gamma);
                        double tangent = (zeta >= 0 ? 1d : -1d) / (Math.Abs(zeta) + Math.Sqrt((zeta * zeta) + 1));
                        double cosine = 1d / Math.Sqrt((tangent * tangent) + 1);
                        double sine = tangent * cosine;
                        for (int row = 0; row < Dimension; row++)
                        {
                            Turn(product, row, first, second, cosine, sine);
                            Turn(vectors, row, first, second, cosine, sine);
                        }
                    }
                }
            }

            values = new[] { Column(product, 0).Length, Column(product, 1).Length, Column(product, 2).Length };
        }

        private static void Turn(double[,] matrix, int row, int first, int second, double cosine, double sine)
        {
            double onFirst = matrix[row, first];
            double onSecond = matrix[row, second];
            matrix[row, first] = (cosine * onFirst) - (sine * onSecond);
            matrix[row, second] = (sine * onFirst) + (cosine * onSecond);
        }
    }
}
