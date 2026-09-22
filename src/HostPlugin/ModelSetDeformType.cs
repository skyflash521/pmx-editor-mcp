using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した頂点の変形方式を変え、SDEFの値を整えるツール。
    /// </summary>
    public static class ModelSetDeformType
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_set_deform_type";

        /// <summary>
        /// 指した変形方式へ変える。重みが正の枠を重い順に、方式が使う数まで残し、残した重みの合計が
        /// 1になるようそろえ、余った枠は空にする。正の枠が1つも無ければ、その頂点が指している
        /// ボーンのうち先頭のものへ重み1を振り、ボーンを1つも指していなければ枠を空のままにする。
        /// SDEFの指定は残った枠が2つのときだけ、QDEFの指定は残った枠が1つ以上のときだけ立て、
        /// もう一方の指定は落とす。
        /// </summary>
        public const string Convert = "convert";

        /// <summary>SDEFのC値を、ウェイトの2つのボーンの位置から決め直す。</summary>
        public const string NormalizeSdefC = "normalizeSdefC";

        /// <summary>2つのボーンを保てないSDEFの頂点から、SDEFの指定を落とす。</summary>
        public const string RepairInvalidSdef = "repairInvalidSdef";

        /// <summary>変える先の変形方式を受け取る入力の名前。</summary>
        public const string DeformName = "deform";

        /// <summary>1つのボーンだけで変形する。</summary>
        public const string Bdef1 = "bdef1";

        /// <summary>2つのボーンで変形する。</summary>
        public const string Bdef2 = "bdef2";

        /// <summary>4つのボーンで変形する。</summary>
        public const string Bdef4 = "bdef4";

        /// <summary>2つのボーンと補間の値で変形する。</summary>
        public const string Sdef = "sdef";

        /// <summary>4つのボーンを二重四元数で混ぜて変形する。</summary>
        public const string Qdef = "qdef";

        /// <summary>変えた頂点の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        // SDEFは2つのボーンの間を補間するPMXの変形方式である。
        private const int SdefBones = 2;

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { Convert, NormalizeSdefC, RepairInvalidSdef };
            }
        }

        /// <summary>受け取れる変形方式。スキーマが並べる順。</summary>
        public static IList<string> Deforms
        {
            get
            {
                return new[] { Bdef1, Bdef2, Bdef4, Sdef, Qdef };
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
                DeformName,
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

            string deform;
            if (!ComposedInput.TryChoice(
                    context,
                    DeformName,
                    operation,
                    new[] { Convert },
                    Deforms,
                    out deform,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IList<IPXVertex> picked = chosen.Select(at => model.Vertex[at]).ToList();
            IList<Shape> before = picked.Select(Shape.Of).ToList();
            if (string.Equals(operation, Convert, StringComparison.Ordinal))
            {
                ReferenceCleanup.RepairWeights(model, picked);
            }

            foreach (IPXVertex vertex in picked)
            {
                Written(vertex, operation, deform);
            }

            int changed = 0;
            for (int at = 0; at < picked.Count; at++)
            {
                changed += before[at].Holds(picked[at]) ? 0 : 1;
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ChangedName, changed },
                });
        }

        private static void Written(IPXVertex vertex, string operation, string deform)
        {
            switch (operation)
            {
                case Convert:
                    Converted(vertex, deform);
                    break;

                case NormalizeSdefC:
                    Centred(vertex);
                    break;

                default:
                    Loosened(vertex);
                    break;
            }
        }

        private static void Converted(IPXVertex vertex, string deform)
        {
            IList<KeyValuePair<IPXBone, float>> kept = VertexWeights.Settled(
                VertexWeights.Settled(VertexWeights.Read(vertex)).Take(Slots(deform)).ToList());
            VertexWeights.Write(vertex, kept);
            vertex.SDEF = string.Equals(deform, Sdef, StringComparison.Ordinal)
                && kept.Count == SdefBones;
            vertex.QDEF = string.Equals(deform, Qdef, StringComparison.Ordinal) && kept.Count > 0;
            if (vertex.SDEF)
            {
                Centre(vertex);
            }
        }

        /// <summary>その変形方式が使うボーンの数。</summary>
        private static int Slots(string deform)
        {
            switch (deform)
            {
                case Bdef1:
                    return 1;

                case Bdef2:
                case Sdef:
                    return SdefBones;

                default:
                    return VertexWeights.Slots;
            }
        }

        private static void Centred(IPXVertex vertex)
        {
            if (vertex.SDEF && vertex.Bone1 != null && vertex.Bone2 != null)
            {
                Centre(vertex);
            }
        }

        /// <summary>SDEFの3つの点を、2つのボーンの位置とその重みから決め直す。</summary>
        private static void Centre(IPXVertex vertex)
        {
            vertex.SDEF_C = Vectors.Add(
                Vectors.Scale(vertex.Bone1.Position, vertex.Weight1),
                Vectors.Scale(vertex.Bone2.Position, vertex.Weight2));
            vertex.SDEF_R0 = Vectors.Copied(vertex.Bone1.Position);
            vertex.SDEF_R1 = Vectors.Copied(vertex.Bone2.Position);
        }

        private static void Loosened(IPXVertex vertex)
        {
            if (vertex.SDEF && (vertex.Bone1 == null || vertex.Bone2 == null))
            {
                vertex.SDEF = false;
            }
        }

        private sealed class Shape
        {
            private Shape(IPXVertex vertex)
            {
                _shares = VertexWeights.All(vertex);
                _sdef = vertex.SDEF;
                _qdef = vertex.QDEF;
                _centre = Vectors.Copied(vertex.SDEF_C);
                _first = Vectors.Copied(vertex.SDEF_R0);
                _second = Vectors.Copied(vertex.SDEF_R1);
            }

            private readonly IList<KeyValuePair<IPXBone, float>> _shares;

            private readonly bool _sdef;

            private readonly bool _qdef;

            private readonly V3 _centre;

            private readonly V3 _first;

            private readonly V3 _second;

            public static Shape Of(IPXVertex vertex)
            {
                return new Shape(vertex);
            }

            public bool Holds(IPXVertex vertex)
            {
                return VertexWeights.Same(vertex, _shares)
                    && _sdef == vertex.SDEF
                    && _qdef == vertex.QDEF
                    && Vectors.Same(_centre, vertex.SDEF_C)
                    && Vectors.Same(_first, vertex.SDEF_R0)
                    && Vectors.Same(_second, vertex.SDEF_R1);
            }
        }
    }
}
