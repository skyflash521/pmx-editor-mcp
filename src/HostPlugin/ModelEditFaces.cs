using System;
using System.Collections.Generic;
using System.Linq;
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
            int changed = 0;
            for (int at = 0; at < picked.Count; at++)
            {
                IPXFace left = picked[at].Value;
                if (taken.Contains(left))
                {
                    continue;
                }

                for (int other = at + 1; other < picked.Count; other++)
                {
                    IPXFace right = picked[other].Value;
                    if (taken.Contains(right)
                        || !ReferenceEquals(picked[at].Key, picked[other].Key)
                        || Shared(left, right).Count != SharedCorners)
                    {
                        continue;
                    }

                    Redraw(left, right);
                    taken.Add(left);
                    taken.Add(right);
                    changed += 2;

                    break;
                }
            }

            return Answer(changed, 0, 0);
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

            int walls = 0;
            foreach (KeyValuePair<IPXMaterial, IPXFace> held in picked)
            {
                IPXVertex[] winding = Corners(held.Value);
                for (int at = 0; at < winding.Length; at++)
                {
                    IPXVertex from = winding[at];
                    IPXVertex to = winding[(at + 1) % winding.Length];
                    if (Inside(picked, from, to))
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

        private static bool Inside(
            IList<KeyValuePair<IPXMaterial, IPXFace>> picked, IPXVertex from, IPXVertex to)
        {
            int found = 0;
            foreach (KeyValuePair<IPXMaterial, IPXFace> held in picked)
            {
                IPXVertex[] winding = Corners(held.Value);
                for (int at = 0; at < winding.Length; at++)
                {
                    IPXVertex left = winding[at];
                    IPXVertex right = winding[(at + 1) % winding.Length];
                    if ((ReferenceEquals(left, from) && ReferenceEquals(right, to))
                        || (ReferenceEquals(left, to) && ReferenceEquals(right, from)))
                    {
                        found++;
                    }
                }
            }

            return found > 1;
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
