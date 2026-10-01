using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    internal static class MirrorPartners
    {
        public const double Tolerance = 0.001;

        public static int AxisIndex(string axis)
        {
            return string.Equals(axis, ModelEditVertices.AxisX, StringComparison.Ordinal)
                ? 0
                : string.Equals(axis, ModelEditVertices.AxisY, StringComparison.Ordinal) ? 1 : 2;
        }

        public static Vec Flip(Vec given, int axis)
        {
            return new Vec(
                axis == 0 ? -given.X : given.X,
                axis == 1 ? -given.Y : given.Y,
                axis == 2 ? -given.Z : given.Z);
        }

        public static Vec Level(Vec given, int axis)
        {
            return new Vec(
                axis == 0 ? 0d : given.X,
                axis == 1 ? 0d : given.Y,
                axis == 2 ? 0d : given.Z);
        }

        public static IDictionary<int, int> OfVertices(IPXPmx based, int axis, IEnumerable<int> picked)
        {
            int[] asked = picked.Distinct().ToArray();
            Vec[] spots = ModelCompareShape.Positions(based);
            IList<int>[] found = SurfaceGeometry.Near(
                spots, asked.Select(at => Flip(spots[at], axis)).ToList(), Tolerance);
            IList<int>[] around = null;
            Dictionary<int, int> partners = new Dictionary<int, int>();
            for (int at = 0; at < asked.Length; at++)
            {
                if (found[at].Count == 0)
                {
                    continue;
                }

                if (found[at].Count > 1 && around == null)
                {
                    around = SurfaceGeometry.Neighbours(based, Enumerable.Range(0, based.Material.Count));
                }

                partners[asked[at]] = found[at].Count == 1
                    ? found[at][0]
                    : Surrounded(asked[at], found[at], spots, around, axis);
            }

            return partners;
        }

        private static int Surrounded(
            int vertex, IList<int> candidates, Vec[] spots, IList<int>[] around, int axis)
        {
            Vec[] wanted = around[vertex].Select(near => Flip(spots[near], axis)).ToArray();

            return candidates
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Gaps = wanted
                        .Select(spot => around[candidate]
                            .Select(near => (spots[near] - spot).Length)
                            .DefaultIfEmpty(double.PositiveInfinity)
                            .Min())
                        .ToArray(),
                })
                .OrderBy(held => held.Gaps.Count(gap => gap > Tolerance))
                .ThenBy(held => held.Gaps.Sum())
                .ThenBy(held => held.Candidate == vertex ? 0 : 1)
                .ThenBy(held => held.Candidate)
                .First()
                .Candidate;
        }
    }
}
