using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した頂点の法線の平均化・面法線化・正規化・反転を行うツール。
    /// </summary>
    public static class ModelEditNormals
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_normals";

        /// <summary>指した頂点の法線を、その平均へそろえる。</summary>
        public const string Average = "average";

        /// <summary>しきい値より近い頂点どうしで法線を平均する。</summary>
        public const string AverageNear = "averageNear";

        /// <summary>その頂点を使う面の法線の平均を、頂点の法線にする。</summary>
        public const string FromFaces = "fromFaces";

        /// <summary>法線の長さを1にそろえる。</summary>
        public const string Normalize = "normalize";

        /// <summary>法線の向きを逆にする。</summary>
        public const string Flip = "flip";

        /// <summary>平均する距離のしきい値を受け取る入力の名前。</summary>
        public const string ThresholdName = "threshold";

        /// <summary>変えた頂点の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { Average, AverageNear, FromFaces, Normalize, Flip };
            }
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
            if (!ComposedInput.TryFloat(
                    context,
                    ThresholdName,
                    operation,
                    new[] { AverageNear },
                    0f,
                    ComposedInput.NoCeiling,
                    out threshold,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IList<IPXVertex> picked = chosen.Select(at => model.Vertex[at]).ToList();
            IList<V3> before = picked.Select(vertex => Vectors.Copied(vertex.Normal)).ToList();
            switch (operation)
            {
                case Average:
                    Aim(picked, Shared(picked));
                    break;

                case AverageNear:
                    foreach (IList<IPXVertex> group in VertexClusters.Near(picked, threshold))
                    {
                        Aim(group, Shared(group));
                    }

                    break;

                case FromFaces:
                    FromTheFaces(model, picked);
                    break;

                case Normalize:
                    foreach (IPXVertex vertex in picked)
                    {
                        vertex.Normal = Vectors.Normalized(vertex.Normal);
                    }

                    break;

                default:
                    foreach (IPXVertex vertex in picked)
                    {
                        vertex.Normal = Vectors.Scale(vertex.Normal, -1f);
                    }

                    break;
            }

            int changed = 0;
            for (int at = 0; at < picked.Count; at++)
            {
                changed += Vectors.Same(before[at], picked[at].Normal) ? 0 : 1;
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ChangedName, changed },
                });
        }

        private static V3 Shared(IEnumerable<IPXVertex> picked)
        {
            return Vectors.NormalizedSum(picked.Select(vertex => vertex.Normal));
        }

        private static void Aim(IEnumerable<IPXVertex> picked, V3 direction)
        {
            foreach (IPXVertex vertex in picked)
            {
                vertex.Normal = new V3(direction.X, direction.Y, direction.Z);
            }
        }

        private static void FromTheFaces(IPXPmx model, IList<IPXVertex> picked)
        {
            Dictionary<IPXVertex, IList<V3>> facing =
                new Dictionary<IPXVertex, IList<V3>>(ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXVertex vertex in picked)
            {
                facing[vertex] = new List<V3>();
            }

            foreach (IPXMaterial material in model.Material)
            {
                foreach (IPXFace face in material.Faces)
                {
                    if (!ReferenceCleanup.IsSoundFace(face))
                    {
                        continue;
                    }

                    V3 made = Vectors.PerpendicularTo(
                        face.Vertex1.Position, face.Vertex2.Position, face.Vertex3.Position);
                    foreach (IPXVertex corner in
                        new[] { face.Vertex1, face.Vertex2, face.Vertex3 })
                    {
                        IList<V3> held;
                        if (facing.TryGetValue(corner, out held))
                        {
                            held.Add(made);
                        }
                    }
                }
            }

            foreach (IPXVertex vertex in picked)
            {
                V3 made = Vectors.NormalizedSum(facing[vertex]);
                if (Vectors.HasLength(made))
                {
                    vertex.Normal = made;
                }
            }
        }
    }
}
