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

        /// <summary>足した頂点の位置を返す項目の名前。</summary>
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
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            float threshold;
            string axis;
            if (!TryThreshold(context, operation, out threshold, out code, out message)
                || !TryAxis(context, operation, out axis, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IList<IPXVertex> picked = chosen.Select(at => model.Vertex[at]).ToList();
            switch (operation)
            {
                case Weld:
                    return Joined(model, new[] { picked });

                case WeldNear:
                    return Joined(model, Clustered(picked, threshold));

                case Align:
                    return Aligned(picked, axis);

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

            return Answer(moved.Count, moved.Count, faces, new int[0]);
        }

        private static IEnumerable<IList<IPXVertex>> Clustered(
            IList<IPXVertex> picked, float threshold)
        {
            List<IList<IPXVertex>> groups = new List<IList<IPXVertex>>();
            HashSet<IPXVertex> taken =
                new HashSet<IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXVertex vertex in picked)
            {
                if (!taken.Add(vertex))
                {
                    continue;
                }

                List<IPXVertex> group = new List<IPXVertex> { vertex };
                foreach (IPXVertex other in picked)
                {
                    if (taken.Contains(other)
                        || Vectors.Distance(vertex.Position, other.Position) > threshold)
                    {
                        continue;
                    }

                    taken.Add(other);
                    group.Add(other);
                }

                groups.Add(group);
            }

            return groups;
        }

        private static ComposedEditResult Aligned(IList<IPXVertex> picked, string axis)
        {
            if (picked.Count == 0)
            {
                return Answer(0, 0, 0, new int[0]);
            }

            float middle = picked.Select(v => Component(v.Position, axis)).Sum() / picked.Count;
            foreach (IPXVertex vertex in picked)
            {
                vertex.Position = Written(vertex.Position, axis, middle);
            }

            return Answer(picked.Count, 0, 0, new int[0]);
        }

        private static ComposedEditResult Copied(
            IPXPmx model, IList<IPXVertex> picked, string axis)
        {
            List<int> added = new List<int>();
            foreach (IPXVertex vertex in picked)
            {
                IPXVertex made = (IPXVertex)vertex.Clone();
                Flip(made, axis);
                model.Vertex.Add(made);
                added.Add(model.Vertex.Count - 1);
            }

            return Answer(picked.Count, 0, 0, added);
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

            return Answer(picked.Count, 0, 0, new int[0]);
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

        private static ComposedEditResult Answer(
            int changed, int removed, int faces, IList<int> added)
        {
            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ChangedName, changed },
                    { RemovedName, removed },
                    { RemovedFacesName, faces },
                    { AddedName, added.Cast<object>().ToArray() },
                });
        }

        private static bool TryThreshold(
            McpMethodContext context,
            string operation,
            out float threshold,
            out string code,
            out string message)
        {
            threshold = 0f;
            code = ToolEnvelope.InvalidArgument;
            message = null;
            object given;
            bool pointed = context.Params.TryGetValue(ThresholdName, out given);
            if (!string.Equals(operation, WeldNear, StringComparison.Ordinal))
            {
                if (pointed)
                {
                    message = ThresholdName + " を渡せるのは " + WeldNear + " のときだけである。";

                    return false;
                }

                code = null;

                return true;
            }

            if (!pointed || !ValueInput.TrySingle(given, out threshold))
            {
                message = ThresholdName + " は " + WeldNear + " のときに渡す、有限の数である。";

                return false;
            }

            if (threshold < 0f)
            {
                message = ThresholdName + " は0以上でなければならない。";

                return false;
            }

            code = null;

            return true;
        }

        private static bool TryAxis(
            McpMethodContext context,
            string operation,
            out string axis,
            out string code,
            out string message)
        {
            axis = null;
            code = ToolEnvelope.InvalidArgument;
            message = null;
            object given;
            bool pointed = context.Params.TryGetValue(AxisName, out given);
            bool wanted = string.Equals(operation, Align, StringComparison.Ordinal)
                || string.Equals(operation, MirrorCopy, StringComparison.Ordinal)
                || string.Equals(operation, MirrorModel, StringComparison.Ordinal);
            if (!wanted)
            {
                if (pointed)
                {
                    message = AxisName + " を渡せるのは " + Align + "・" + MirrorCopy + "・"
                        + MirrorModel + " のときだけである。";

                    return false;
                }

                code = null;

                return true;
            }

            axis = given as string;
            if (axis == null || !Axes.Contains(axis, StringComparer.Ordinal))
            {
                axis = null;
                message = AxisName + " は次のどれかでなければならない: "
                    + string.Join("・", Axes.ToArray());

                return false;
            }

            code = null;

            return true;
        }
    }
}
