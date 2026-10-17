using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    internal static class MirrorPartners
    {
        public const double Tolerance = 0.001;

        private const string Mark = "M-";

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

        public static V3 Across(V3 given, string axis)
        {
            int along = AxisIndex(axis);

            return new V3(
                along == 0 ? -given.X : given.X,
                along == 1 ? -given.Y : given.Y,
                along == 2 ? -given.Z : given.Z);
        }

        /// <summary>
        /// 名前の左右を入れ替える。<paramref name="marking"/> が真のとき、左右を持たない名前には
        /// 写しと分かる印を頭へ足す。
        /// </summary>
        public static string Flipped(string name, bool marking)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            char[] letters = name.ToCharArray();
            int first = 0;
            int last = letters.Length - 1;
            while (first <= last && letters[first] == '_')
            {
                first++;
            }

            while (last >= first && letters[last] == '_')
            {
                last--;
            }

            if (first > last)
            {
                return marking ? Mark + name : name;
            }

            if (letters[first] == '左' || letters[first] == '右')
            {
                letters[first] = letters[first] == '左' ? '右' : '左';
            }
            else if (letters[last] == '左' || letters[last] == '右')
            {
                letters[last] = letters[last] == '左' ? '右' : '左';
            }
            else
            {
                return marking ? Mark + name : name;
            }

            return new string(letters);
        }

        /// <summary>左右を入れ替えた名前。左右を持たない名前では空を返す。</summary>
        public static string OtherSide(string name)
        {
            string turned = Flipped(name, false);

            return string.Equals(turned, name, StringComparison.Ordinal) ? null : turned;
        }

        /// <summary>名前の左右を入れ替えた名前のボーン。左右を持たない名前か、その名前のボーンが無ければ空を返す。</summary>
        public static IPXBone OfBone(IPXPmx model, IPXBone bone)
        {
            string turned = OtherSide(bone.Name);

            return turned == null
                ? null
                : model.Bone.FirstOrDefault(
                    held => string.Equals(held.Name, turned, StringComparison.Ordinal));
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
