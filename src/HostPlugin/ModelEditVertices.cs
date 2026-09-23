using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した頂点の結合・近距離の統合・位置合わせ・鏡像を行うツール。頂点をまとめる操作では、
    /// 3つの頂点が揃わなくなった面を落とす。
    /// </summary>
    public static class ModelEditVertices
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_vertices";

        /// <summary>指した頂点を1つへまとめ、面の参照を付け替える。</summary>
        public const string Weld = "weld";

        /// <summary>指した頂点のうち、しきい値より近いものどうしをまとめる。</summary>
        public const string WeldNear = "weldNear";

        /// <summary>指した頂点の、指した軸の値を、指した頂点の平均へそろえる。</summary>
        public const string Align = "align";

        /// <summary>指した頂点を、指した軸の鏡像として複製する。</summary>
        public const string MirrorCopy = "mirrorCopy";

        /// <summary>指した頂点を、指した軸の鏡像へ移す。</summary>
        public const string MirrorModel = "mirrorModel";

        /// <summary>まとめる距離のしきい値を受け取る入力の名前。</summary>
        public const string ThresholdName = "threshold";

        /// <summary>軸を受け取る入力の名前。</summary>
        public const string AxisName = "axis";

        /// <summary>X軸。</summary>
        public const string AxisX = "x";

        /// <summary>Y軸。</summary>
        public const string AxisY = "y";

        /// <summary>Z軸。</summary>
        public const string AxisZ = "z";

        /// <summary>変えた頂点の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>消えた頂点の数を返す項目の名前。</summary>
        public const string RemovedName = "removed";

        /// <summary>3つの頂点が揃わなくなって落ちた面の数を返す項目の名前。</summary>
        public const string RemovedFacesName = "removedFaces";

        /// <summary>
        /// 足した頂点の位置を、先頭と件数の組で返す項目の名前。足した頂点は並びの末尾に連なる。
        /// 足していなければ件数は0で、先頭は頂点の数になる。
        /// </summary>
        public const string AddedName = "added";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get { return new[] { Weld, WeldNear, Align, MirrorCopy, MirrorModel }; }
        }

        /// <summary>受け取れる軸。スキーマが並べる順。</summary>
        public static IList<string> Axes
        {
            get { return new[] { AxisX, AxisY, AxisZ }; }
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
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.All,
                TargetNames.Element.Selected,
                ThresholdName,
                AxisName,
            };
            methods.Add(ToolName, edit.Method(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            string operation;
            string code;
            string message;
            IList<int> chosen;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message)
                || !TargetInput.TryPositions(
                    context.Params,
                    TargetNames.Element,
                    model.Vertex.Count,
                    out chosen,
                    out code,
                    out message,
                    context.Screen.Pick(ElementKinds.Vertex, model.Vertex.Count)))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            float threshold;
            string axis;
            if (!ComposedInput.TryFloat(
                    context,
                    ThresholdName,
                    operation,
                    new[] { WeldNear },
                    0f,
                    ComposedInput.NoCeiling,
                    out threshold,
                    out code,
                    out message)
                || !ComposedInput.TryChoice(
                    context,
                    AxisName,
                    operation,
                    new[] { Align, MirrorCopy, MirrorModel },
                    Axes,
                    out axis,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IList<IPXVertex> picked = chosen.Select(at => model.Vertex[at]).ToList();
            switch (operation)
            {
                case Weld:
                    return Joined(model, new[] { picked });

                case WeldNear:
                    return Joined(model, VertexClusters.Near(picked, threshold));

                case Align:
                    return Aligned(model, picked, axis);

                case MirrorCopy:
                    return Copied(model, picked, axis);

                default:
                    return Mirrored(model, picked, axis);
            }
        }

        private static ComposedEditResult Joined(
            IPXPmx model, IEnumerable<IList<IPXVertex>> groups)
        {
            Dictionary<IPXVertex, IPXVertex> moved =
                new Dictionary<IPXVertex, IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
            foreach (IList<IPXVertex> group in groups)
            {
                foreach (IPXVertex dropped in group.Skip(1))
                {
                    moved[dropped] = group[0];
                }
            }

            ReferenceCleanup.Repoint(model, moved);
            foreach (IPXVertex dropped in moved.Keys)
            {
                model.Vertex.Remove(dropped);
            }

            int faces = ReferenceCleanup.DropUnsoundFaces(model);

            return Answer(moved.Count, moved.Count, faces, None(model));
        }

        private static ComposedEditResult Aligned(
            IPXPmx model, IList<IPXVertex> picked, string axis)
        {
            if (picked.Count == 0)
            {
                return Answer(0, 0, 0, None(model));
            }

            float middle = picked.Select(v => Component(v.Position, axis)).Sum() / picked.Count;
            foreach (IPXVertex vertex in picked)
            {
                vertex.Position = Written(vertex.Position, axis, middle);
            }

            return Answer(picked.Count, 0, 0, None(model), new[] { ElementKinds.Vertex });
        }

        private static ComposedEditResult Copied(
            IPXPmx model, IList<IPXVertex> picked, string axis)
        {
            int start = model.Vertex.Count;
            foreach (IPXVertex vertex in picked)
            {
                IPXVertex made = (IPXVertex)vertex.Clone();
                Flip(made, axis);
                model.Vertex.Add(made);
            }

            return Answer(picked.Count, 0, 0, PositionRuns.Of(start, picked.Count));
        }

        private static ComposedEditResult Mirrored(
            IPXPmx model, IList<IPXVertex> picked, string axis)
        {
            foreach (IPXVertex vertex in picked)
            {
                Flip(vertex, axis);
            }

            HashSet<IPXVertex> flipped =
                new HashSet<IPXVertex>(picked, ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXMaterial material in model.Material)
            {
                foreach (IPXFace face in material.Faces)
                {
                    if (!flipped.Contains(face.Vertex1)
                        || !flipped.Contains(face.Vertex2)
                        || !flipped.Contains(face.Vertex3))
                    {
                        continue;
                    }

                    IPXVertex second = face.Vertex2;
                    face.Vertex2 = face.Vertex3;
                    face.Vertex3 = second;
                }
            }

            return Answer(picked.Count, 0, 0, None(model));
        }

        private static void Flip(IPXVertex vertex, string axis)
        {
            vertex.Position = Written(
                vertex.Position, axis, -Component(vertex.Position, axis));
            vertex.Normal = Written(vertex.Normal, axis, -Component(vertex.Normal, axis));
        }

        private static float Component(V3 given, string axis)
        {
            switch (axis)
            {
                case AxisX:
                    return given.X;

                case AxisY:
                    return given.Y;

                default:
                    return given.Z;
            }
        }

        private static V3 Written(V3 given, string axis, float value)
        {
            switch (axis)
            {
                case AxisX:
                    return new V3(value, given.Y, given.Z);

                case AxisY:
                    return new V3(given.X, value, given.Z);

                default:
                    return new V3(given.X, given.Y, value);
            }
        }

        /// <summary>
        /// 結末を作る。<paramref name="rewritten"/> は並びを変えずに中身だけを書き換えた種類で、
        /// 並びを変えうる操作では null。
        /// </summary>
        private static ComposedEditResult Answer(
            int changed,
            int removed,
            int faces,
            IDictionary<string, object> added,
            IList<string> rewritten = null)
        {
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ChangedName, changed },
                { RemovedName, removed },
                { RemovedFacesName, faces },
                { AddedName, added },
            };

            return rewritten == null
                ? ComposedEditResult.Complete(value)
                : ComposedEditResult.CompleteRewriting(value, rewritten);
        }

        private static IDictionary<string, object> None(IPXPmx model)
        {
            return PositionRuns.Of(model.Vertex.Count, 0);
        }

    }
}
