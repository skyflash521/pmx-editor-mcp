// 頂点が持つボーンと重みの4つの枠を、まとめて読み書きする。

using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public static class VertexWeights
    {
        public const int Slots = 4;

        /// <summary>ボーンの入っている枠を、枠の並びの順で読む。</summary>
        public static IList<KeyValuePair<IPXBone, float>> Read(IPXVertex vertex)
        {
            return All(vertex).Where(share => share.Key != null).ToList();
        }

        /// <summary>4つの枠を、ボーンの入っていない枠も含めて枠の並びの順で読む。</summary>
        public static IList<KeyValuePair<IPXBone, float>> All(IPXVertex vertex)
        {
            if (vertex == null)
            {
                throw new ArgumentNullException(nameof(vertex));
            }

            IPXBone[] bones = { vertex.Bone1, vertex.Bone2, vertex.Bone3, vertex.Bone4 };
            float[] shares = { vertex.Weight1, vertex.Weight2, vertex.Weight3, vertex.Weight4 };

            return Enumerable.Range(0, Slots)
                .Select(at => new KeyValuePair<IPXBone, float>(bones[at], shares[at]))
                .ToList();
        }

        /// <summary>
        /// 渡された順に枠へ書き、余った枠を空にする。5つめからは捨てる。SDEFの頂点へいまの2本のボーンを
        /// 入れ替えた順で渡したときは、いまの順のまま書く。
        /// </summary>
        public static void Write(
            IPXVertex vertex, IList<KeyValuePair<IPXBone, float>> shares)
        {
            if (vertex == null)
            {
                throw new ArgumentNullException(nameof(vertex));
            }

            if (shares == null)
            {
                throw new ArgumentNullException(nameof(shares));
            }

            if (vertex.SDEF
                && shares.Count == 2
                && ReferenceEquals(shares[0].Key, vertex.Bone2)
                && ReferenceEquals(shares[1].Key, vertex.Bone1))
            {
                shares = new[] { shares[1], shares[0] };
            }

            vertex.Bone1 = BoneAt(shares, 0);
            vertex.Bone2 = BoneAt(shares, 1);
            vertex.Bone3 = BoneAt(shares, 2);
            vertex.Bone4 = BoneAt(shares, 3);
            vertex.Weight1 = ShareAt(shares, 0);
            vertex.Weight2 = ShareAt(shares, 1);
            vertex.Weight3 = ShareAt(shares, 2);
            vertex.Weight4 = ShareAt(shares, 3);
        }

        /// <summary>
        /// 同じボーンへの重みを足し合わせ、重みが正の枠だけを重い順に4つまで残して、その合計が1に
        /// なるようそろえる。正の枠が1つも残らなければ先頭のボーンへ重み1を振り、ボーンの入った枠が
        /// 1つも無ければ空を返す。
        /// </summary>
        public static IList<KeyValuePair<IPXBone, float>> Settled(
            IEnumerable<KeyValuePair<IPXBone, float>> shares)
        {
            IList<IPXBone> order = Ordered(shares);
            IDictionary<IPXBone, double> total = Totalled(shares);
            IList<IPXBone> kept = order
                .Where(bone => total[bone] > 0d)
                .OrderByDescending(bone => total[bone])
                .Take(Slots)
                .ToList();
            if (kept.Count == 0)
            {
                return order.Count == 0
                    ? new List<KeyValuePair<IPXBone, float>>()
                    : new List<KeyValuePair<IPXBone, float>>
                    {
                        new KeyValuePair<IPXBone, float>(order[0], 1f),
                    };
            }

            double whole = kept.Sum(bone => total[bone]);

            return kept
                .Select(bone => new KeyValuePair<IPXBone, float>(
                    bone, (float)(total[bone] / whole)))
                .ToList();
        }

        /// <summary>
        /// 重みが0でない枠が、そろえ直した並びと同じボーンと重みを同じ順に持つか。重みが0の枠はボーンを
        /// 問わず空とみなし、SDEFの頂点は枠の順を問わない。
        /// </summary>
        public static bool IsSound(IPXVertex vertex)
        {
            List<KeyValuePair<IPXBone, float>> weighted = All(vertex).Where(share => share.Value != 0f).ToList();
            IList<KeyValuePair<IPXBone, float>> settled = Settled(Read(vertex));
            if (weighted.Count != settled.Count)
            {
                return false;
            }

            if (vertex.SDEF)
            {
                return settled.All(share => weighted.Any(
                    held => ReferenceEquals(held.Key, share.Key) && held.Value == share.Value));
            }

            return Enumerable.Range(0, settled.Count).All(
                at => ReferenceEquals(weighted[at].Key, settled[at].Key) && weighted[at].Value == settled[at].Value);
        }

        /// <summary>その頂点の4つの枠が、渡された並びと同じボーンと重みを同じ順に持つか。</summary>
        public static bool Same(IPXVertex vertex, IList<KeyValuePair<IPXBone, float>> shares)
        {
            if (shares == null)
            {
                throw new ArgumentNullException(nameof(shares));
            }

            IList<KeyValuePair<IPXBone, float>> held = All(vertex);
            if (held.Count != shares.Count)
            {
                return false;
            }

            for (int at = 0; at < held.Count; at++)
            {
                if (!ReferenceEquals(held[at].Key, shares[at].Key)
                    || held[at].Value != shares[at].Value)
                {
                    return false;
                }
            }

            return true;
        }

        public static void MirrorSdef(IPXVertex vertex, IPXVertex source, Func<V3, V3> mirror)
        {
            if (vertex == null)
            {
                throw new ArgumentNullException(nameof(vertex));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (mirror == null)
            {
                throw new ArgumentNullException(nameof(mirror));
            }

            vertex.SDEF_C = mirror(source.SDEF_C);
            vertex.SDEF_R0 = mirror(source.SDEF_R0);
            vertex.SDEF_R1 = mirror(source.SDEF_R1);
            ProjectSdefCenter(vertex);
        }

        /// <summary>
        /// SDEF の頂点の C を、いまの頂点の位置から2つのボーンを結ぶ軸へ下ろした足に置く。SDEF でない頂点、
        /// ボーンが欠けた頂点、2つのボーンが同じ位置の頂点は変えない。
        /// </summary>
        public static void ProjectSdefCenter(IPXVertex vertex)
        {
            if (vertex == null)
            {
                throw new ArgumentNullException(nameof(vertex));
            }

            if (!vertex.SDEF || vertex.Bone1 == null || vertex.Bone2 == null)
            {
                return;
            }

            V3 from = vertex.Bone1.Position;
            V3 along = Vectors.Toward(vertex.Bone2.Position, from);
            if (Vectors.HasLength(along))
            {
                vertex.SDEF_C = Vectors.Add(
                    from,
                    Vectors.Scale(along, Vectors.Dot(along, Vectors.Apart(vertex.Position, from, 1f))));
            }
        }

        /// <summary><paramref name="bones"/> のどれかを SDEF の2つのボーンに持つ頂点の C を置き直す。</summary>
        public static void ProjectSdefCenters(IEnumerable<IPXVertex> vertices, ICollection<IPXBone> bones)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            if (bones == null)
            {
                throw new ArgumentNullException(nameof(bones));
            }

            if (bones.Count == 0)
            {
                return;
            }

            foreach (IPXVertex vertex in vertices)
            {
                if (vertex.SDEF
                    && ((vertex.Bone1 != null && bones.Contains(vertex.Bone1))
                        || (vertex.Bone2 != null && bones.Contains(vertex.Bone2))))
                {
                    ProjectSdefCenter(vertex);
                }
            }
        }

        /// <summary>ボーンの入った枠を、先に現れた順に重なりなく並べる。</summary>
        private static IList<IPXBone> Ordered(IEnumerable<KeyValuePair<IPXBone, float>> shares)
        {
            if (shares == null)
            {
                throw new ArgumentNullException(nameof(shares));
            }

            List<IPXBone> order = new List<IPXBone>();
            HashSet<IPXBone> met = new HashSet<IPXBone>(ReferenceComparer<IPXBone>.Instance);
            foreach (KeyValuePair<IPXBone, float> share in shares)
            {
                if (share.Key != null && met.Add(share.Key))
                {
                    order.Add(share.Key);
                }
            }

            return order;
        }

        /// <summary>ボーンごとの重みの合計。正でない重みは足さない。</summary>
        private static IDictionary<IPXBone, double> Totalled(
            IEnumerable<KeyValuePair<IPXBone, float>> shares)
        {
            Dictionary<IPXBone, double> total =
                new Dictionary<IPXBone, double>(ReferenceComparer<IPXBone>.Instance);
            foreach (KeyValuePair<IPXBone, float> share in shares)
            {
                if (share.Key == null)
                {
                    continue;
                }

                if (!total.ContainsKey(share.Key))
                {
                    total[share.Key] = 0d;
                }

                if (share.Value > 0f)
                {
                    total[share.Key] += share.Value;
                }
            }

            return total;
        }

        private static IPXBone BoneAt(IList<KeyValuePair<IPXBone, float>> shares, int at)
        {
            return at < shares.Count ? shares[at].Key : null;
        }

        private static float ShareAt(IList<KeyValuePair<IPXBone, float>> shares, int at)
        {
            return at < shares.Count ? shares[at].Value : 0f;
        }
    }
}
