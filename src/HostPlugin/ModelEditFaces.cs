using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した面の向き・対角線・押し出し・共有頂点の分離を行うツール。
    /// </summary>
    public static class ModelEditFaces
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_faces";

        /// <summary>面の表と裏を入れ替える。</summary>
        public const string Flip = "flip";

        /// <summary>辺を共有する2つの面が作る四角の対角線を引き直す。</summary>
        public const string SwapDiagonal = "swapDiagonal";

        /// <summary>
        /// 面を法線の向きへ押し出し、側面を作る。指した面をひとまとまりとして扱い、使っている頂点は
        /// 1つにつき1つだけ複製する。側面を張るのは、指した面のうち1つにしか使われていない辺だけで、
        /// 辺の向きは元の面の巻き順のまま、辺の始まりと終わりと、それぞれを押し出した先で2つの面を作る。
        /// </summary>
        public const string Extrude = "extrude";

        /// <summary>指した面が他の面と共有する頂点を複製して分ける。</summary>
        public const string SeparateSharedVertices = "separateSharedVertices";

        /// <summary>押し出す長さを受け取る入力の名前。</summary>
        public const string DistanceName = "distance";

        /// <summary>変えた面の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>足した頂点の数を返す項目の名前。</summary>
        public const string AddedVerticesName = "addedVertices";

        /// <summary>足した面の数を返す項目の名前。</summary>
        public const string AddedFacesName = "addedFaces";

        private const int SharedCorners = 2;

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get { return new[] { Flip, SwapDiagonal, Extrude, SeparateSharedVertices }; }
        }

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            List<string> known = new List<string>
            {
                ComposedOperation.OperationName,
                TargetNames.Parent.Indices,
                TargetNames.Parent.Range,
                TargetNames.Parent.All,
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.All,
                TargetNames.Element.Selected,
                DistanceName,
            };
            methods.Add(ToolName, edit.Method(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            string operation;
            string code;
            string message;
            IList<int> parents;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message)
                || !TargetInput.TryPositions(
                    context.Params,
                    TargetNames.Parent,
                    model.Material.Count,
                    out parents,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            float distance;
            if (!ComposedInput.TryFloat(
                    context,
                    DistanceName,
                    operation,
                    new[] { Extrude },
                    ComposedInput.NoFloor,
                    ComposedInput.NoCeiling,
                    out distance,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IList<int> counts = model.Material.Select(m => m.Faces.Count).ToList();
            List<KeyValuePair<IPXMaterial, IPXFace>> picked =
                new List<KeyValuePair<IPXMaterial, IPXFace>>();
            foreach (int at in parents)
            {
                IPXMaterial material = model.Material[at];
                IList<int> chosen;
                if (!ElementScope.TryFacePositions(
                    context, counts, at, out chosen, out code, out message))
                {
                    return ComposedEditResult.Refuse(code, message);
                }

                picked.AddRange(chosen.Select(
                    face => new KeyValuePair<IPXMaterial, IPXFace>(material, material.Faces[face])));
            }

            switch (operation)
            {
                case Flip:
                    return Flipped(picked);

                case SwapDiagonal:
                    return Swapped(picked);

                case Extrude:
                    return Raised(model, picked, distance);

                default:
                    return Separated(model, picked);
            }
        }

        private static ComposedEditResult Flipped(
            IList<KeyValuePair<IPXMaterial, IPXFace>> picked)
        {
            foreach (KeyValuePair<IPXMaterial, IPXFace> held in picked)
            {
                IPXVertex second = held.Value.Vertex2;
                held.Value.Vertex2 = held.Value.Vertex3;
                held.Value.Vertex3 = second;
            }

            return Answer(picked.Count, 0, 0);
        }

        private static ComposedEditResult Swapped(
            IList<KeyValuePair<IPXMaterial, IPXFace>> picked)
        {
            HashSet<IPXFace> taken = new HashSet<IPXFace>(ReferenceComparer<IPXFace>.Instance);
            Dictionary<object, List<int>> touching = Touching(picked);
            Dictionary<Pair, List<int>> sides = Sides(picked);
            int changed = 0;
            for (int at = 0; at < picked.Count; at++)
            {
                IPXFace left = picked[at].Value;
                if (taken.Contains(left))
                {
                    continue;
                }

                int partner = Candidates(left, touching, sides)
                    .Where(other => other > at)
                    .Distinct()
                    .OrderBy(other => other)
                    .Where(other => !taken.Contains(picked[other].Value)
                        && ReferenceEquals(picked[at].Key, picked[other].Key)
                        && Shared(left, picked[other].Value).Count == SharedCorners)
                    .DefaultIfEmpty(-1)
                    .First();
                if (partner < 0)
                {
                    continue;
                }

                IPXFace right = picked[partner].Value;
                Redraw(left, right);
                taken.Add(left);
                taken.Add(right);
                changed += 2;
            }

            return Answer(changed, 0, 0);
        }

        /// <summary>
        /// 角を2つ共有しうる面の位置。角がすべて異なる面では、自分の角の組を持つ面に限られる。
        /// 同じ頂点を2度角に持つ面は、角を1つ共有するだけの相手とも2つと数えるので、角ごとに引く。
        /// </summary>
        private static IEnumerable<int> Candidates(
            IPXFace face, Dictionary<object, List<int>> touching, Dictionary<Pair, List<int>> sides)
        {
            object[] corners = Corners(face).Select(Keyed).ToArray();
            bool distinct = !ReferenceEquals(corners[0], corners[1])
                && !ReferenceEquals(corners[1], corners[2])
                && !ReferenceEquals(corners[0], corners[2]);
            if (!distinct)
            {
                return corners.SelectMany(corner => touching[corner]);
            }

            return Pairs(corners).SelectMany(pair => sides[pair]);
        }

        /// <summary>角の組ごとの、その2つの頂点を角に持つ面の位置。位置は昇順に並ぶ。</summary>
        private static Dictionary<Pair, List<int>> Sides(
            IList<KeyValuePair<IPXMaterial, IPXFace>> picked)
        {
            Dictionary<Pair, List<int>> sides = new Dictionary<Pair, List<int>>();
            for (int at = 0; at < picked.Count; at++)
            {
                foreach (Pair pair in Pairs(Corners(picked[at].Value).Select(Keyed).ToArray()).Distinct())
                {
                    List<int> held;
                    if (!sides.TryGetValue(pair, out held))
                    {
                        held = new List<int>();
                        sides.Add(pair, held);
                    }

                    held.Add(at);
                }
            }

            return sides;
        }

        private static IEnumerable<Pair> Pairs(object[] corners)
        {
            yield return new Pair(corners[0], corners[1]);
            yield return new Pair(corners[1], corners[2]);
            yield return new Pair(corners[0], corners[2]);
        }

        /// <summary>向きを問わない2つの頂点の組。同じ実体かどうかだけで比べる。</summary>
        private struct Pair : IEquatable<Pair>
        {
            private readonly object _one;

            private readonly object _other;

            public Pair(object one, object other)
            {
                _one = one;
                _other = other;
            }

            public bool Equals(Pair pair)
            {
                return (ReferenceEquals(_one, pair._one) && ReferenceEquals(_other, pair._other))
                    || (ReferenceEquals(_one, pair._other) && ReferenceEquals(_other, pair._one));
            }

            public override bool Equals(object other)
            {
                return other is Pair && Equals((Pair)other);
            }

            public override int GetHashCode()
            {
                return RuntimeHelpers.GetHashCode(_one) ^ RuntimeHelpers.GetHashCode(_other);
            }
        }

        /// <summary>角が指さない頂点を、表の鍵として表す目印。</summary>
        private static readonly object NoCorner = new object();

        private static object Keyed(IPXVertex corner)
        {
            return (object)corner ?? NoCorner;
        }

        /// <summary>頂点ごとの、その頂点を角に持つ面の位置。位置は昇順に並ぶ。</summary>
        private static Dictionary<object, List<int>> Touching(
            IList<KeyValuePair<IPXMaterial, IPXFace>> picked)
        {
            Dictionary<object, List<int>> touching =
                new Dictionary<object, List<int>>(ReferenceComparer<object>.Instance);
            for (int at = 0; at < picked.Count; at++)
            {
                foreach (IPXVertex corner in Corners(picked[at].Value))
                {
                    List<int> held;
                    if (!touching.TryGetValue(Keyed(corner), out held))
                    {
                        held = new List<int>();
                        touching.Add(Keyed(corner), held);
                    }

                    held.Add(at);
                }
            }

            return touching;
        }

        private static void Redraw(IPXFace left, IPXFace right)
        {
            IList<IPXVertex> shared = Shared(left, right);
            IPXVertex ours = Corners(left).Single(v => !shared.Any(s => ReferenceEquals(s, v)));
            IPXVertex theirs = Corners(right).Single(v => !shared.Any(s => ReferenceEquals(s, v)));
            IPXVertex[] winding = Corners(left);
            int at = Array.FindIndex(winding, v => ReferenceEquals(v, ours));
            IPXVertex before = winding[(at + winding.Length - 1) % winding.Length];
            IPXVertex after = winding[(at + 1) % winding.Length];

            left.Vertex1 = before;
            left.Vertex2 = ours;
            left.Vertex3 = theirs;
            right.Vertex1 = ours;
            right.Vertex2 = after;
            right.Vertex3 = theirs;
        }

        private static ComposedEditResult Raised(
            IPXPmx model, IList<KeyValuePair<IPXMaterial, IPXFace>> picked, float distance)
        {
            Dictionary<IPXVertex, V3> pushed =
                new Dictionary<IPXVertex, V3>(ReferenceComparer<IPXVertex>.Instance);
            foreach (KeyValuePair<IPXMaterial, IPXFace> held in picked)
            {
                V3 normal = Normal(held.Value);
                foreach (IPXVertex corner in Corners(held.Value))
                {
                    V3 sum;
                    pushed[corner] = pushed.TryGetValue(corner, out sum)
                        ? Vectors.Add(sum, normal)
                        : normal;
                }
            }

            Dictionary<IPXVertex, IPXVertex> raised =
                new Dictionary<IPXVertex, IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
            foreach (KeyValuePair<IPXVertex, V3> lifted in pushed)
            {
                IPXVertex made = (IPXVertex)lifted.Key.Clone();
                made.Position = Vectors.Add(
                    lifted.Key.Position,
                    Vectors.Scale(Vectors.Normalized(lifted.Value), distance));
                model.Vertex.Add(made);
                raised[lifted.Key] = made;
            }

            Dictionary<object, Dictionary<object, int>> edges = Edges(picked);
            int walls = 0;
            foreach (KeyValuePair<IPXMaterial, IPXFace> held in picked)
            {
                IPXVertex[] winding = Corners(held.Value);
                for (int at = 0; at < winding.Length; at++)
                {
                    IPXVertex from = winding[at];
                    IPXVertex to = winding[(at + 1) % winding.Length];
                    if (Inside(edges, from, to))
                    {
                        continue;
                    }

                    held.Key.Faces.Add(Wall(held.Value, from, to, raised[to]));
                    held.Key.Faces.Add(Wall(held.Value, from, raised[to], raised[from]));
                    walls += 2;
                }
            }

            foreach (KeyValuePair<IPXMaterial, IPXFace> held in picked)
            {
                held.Value.Vertex1 = raised[held.Value.Vertex1];
                held.Value.Vertex2 = raised[held.Value.Vertex2];
                held.Value.Vertex3 = raised[held.Value.Vertex3];
            }

            return Answer(picked.Count, raised.Count, walls);
        }

        /// <summary>選んだ面の辺ごとの、向きを付けたまま数えた出現の数。</summary>
        private static Dictionary<object, Dictionary<object, int>> Edges(
            IList<KeyValuePair<IPXMaterial, IPXFace>> picked)
        {
            Dictionary<object, Dictionary<object, int>> edges =
                new Dictionary<object, Dictionary<object, int>>(
                    ReferenceComparer<object>.Instance);
            foreach (KeyValuePair<IPXMaterial, IPXFace> held in picked)
            {
                IPXVertex[] winding = Corners(held.Value);
                for (int at = 0; at < winding.Length; at++)
                {
                    IPXVertex left = winding[at];
                    IPXVertex right = winding[(at + 1) % winding.Length];
                    Dictionary<object, int> onward;
                    if (!edges.TryGetValue(Keyed(left), out onward))
                    {
                        onward = new Dictionary<object, int>(ReferenceComparer<object>.Instance);
                        edges.Add(Keyed(left), onward);
                    }

                    int count;
                    onward.TryGetValue(Keyed(right), out count);
                    onward[Keyed(right)] = count + 1;
                }
            }

            return edges;
        }

        /// <summary>その辺を、選んだ面のうち2つ以上が向きを問わず持つか。</summary>
        private static bool Inside(
            Dictionary<object, Dictionary<object, int>> edges, IPXVertex from, IPXVertex to)
        {
            int found = Counted(edges, from, to)
                + (ReferenceEquals(from, to) ? 0 : Counted(edges, to, from));

            return found > 1;
        }

        private static int Counted(
            Dictionary<object, Dictionary<object, int>> edges, IPXVertex from, IPXVertex to)
        {
            Dictionary<object, int> onward;
            int count;

            return edges.TryGetValue(Keyed(from), out onward)
                && onward.TryGetValue(Keyed(to), out count)
                ? count
                : 0;
        }

        private static ComposedEditResult Separated(
            IPXPmx model, IList<KeyValuePair<IPXMaterial, IPXFace>> picked)
        {
            HashSet<IPXFace> chosen = new HashSet<IPXFace>(
                picked.Select(p => p.Value), ReferenceComparer<IPXFace>.Instance);
            HashSet<IPXVertex> elsewhere =
                new HashSet<IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXMaterial material in model.Material)
            {
                foreach (IPXFace face in material.Faces.Where(f => !chosen.Contains(f)))
                {
                    foreach (IPXVertex corner in Corners(face))
                    {
                        elsewhere.Add(corner);
                    }
                }
            }

            Dictionary<IPXVertex, IPXVertex> apart =
                new Dictionary<IPXVertex, IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXFace face in chosen)
            {
                foreach (IPXVertex corner in Corners(face))
                {
                    if (!elsewhere.Contains(corner) || apart.ContainsKey(corner))
                    {
                        continue;
                    }

                    IPXVertex made = (IPXVertex)corner.Clone();
                    model.Vertex.Add(made);
                    apart[corner] = made;
                }
            }

            foreach (IPXFace face in chosen)
            {
                face.Vertex1 = Instead(apart, face.Vertex1);
                face.Vertex2 = Instead(apart, face.Vertex2);
                face.Vertex3 = Instead(apart, face.Vertex3);
            }

            return Answer(chosen.Count, apart.Count, 0);
        }

        private static IPXVertex Instead(
            IDictionary<IPXVertex, IPXVertex> apart, IPXVertex corner)
        {
            IPXVertex made;

            return apart.TryGetValue(corner, out made) ? made : corner;
        }

        private static V3 Normal(IPXFace face)
        {
            return Vectors.PerpendicularTo(
                face.Vertex1.Position, face.Vertex2.Position, face.Vertex3.Position);
        }

        private static IPXVertex[] Corners(IPXFace face)
        {
            return new[] { face.Vertex1, face.Vertex2, face.Vertex3 };
        }

        private static IList<IPXVertex> Shared(IPXFace left, IPXFace right)
        {
            IPXVertex[] theirs = Corners(right);

            return Corners(left).Where(v => theirs.Any(t => ReferenceEquals(t, v))).ToList();
        }

        private static ComposedEditResult Answer(int changed, int vertices, int faces)
        {
            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ChangedName, changed },
                    { AddedVerticesName, vertices },
                    { AddedFacesName, faces },
                });
        }

        private static IPXFace Wall(
            IPXFace from, IPXVertex first, IPXVertex second, IPXVertex third)
        {
            // 面を作る口はSDKの並びに無いので、元の面を写して指し替える。
            IPXFace made = (IPXFace)from.Clone();
            made.Vertex1 = first;
            made.Vertex2 = second;
            made.Vertex3 = third;

            return made;
        }
    }
}
