// 頂点が持つボーンと重みの4つの枠を、まとめて読み書きする。

using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    public static class VertexWeights
    {
        /// <summary>1つの頂点が指せるボーンの数。</summary>
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

        /// <summary>渡された順に枠へ書き、余った枠を空にする。5つめからは捨てる。</summary>
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
        /// なるようそろえる。足し合わせも合計も倍精度で行うので、単精度で持てるどの重みを何本並べても
        /// 潰れない。正の枠が1つも残らなければ先頭のボーンへ重み1を振り、ボーンの入った枠が1つも
        /// 無ければ空を返す。
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
