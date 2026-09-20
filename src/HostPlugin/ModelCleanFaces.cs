using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した材質から、不正な面と重なった面を落とすツール。3つの頂点が揃っていない面を不正とし、
    /// 同じ3頂点を指す面が2つ以上あるとき、最初の1つだけを残す。
    /// </summary>
    public static class ModelCleanFaces
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_clean_faces";

        /// <summary>3つの頂点が揃っていない面を落とす。</summary>
        public const string Invalid = "invalid";

        /// <summary>モデル全体で同じ3頂点を指す面を、最初の1つだけ残して落とす。</summary>
        public const string Duplicate = "duplicate";

        /// <summary>材質ごとに同じ3頂点を指す面を、最初の1つだけ残して落とす。</summary>
        public const string DuplicateInMaterial = "duplicateInMaterial";

        /// <summary>落とした面の数を返す項目の名前。</summary>
        public const string RemovedName = "removed";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get { return new[] { Invalid, Duplicate, DuplicateInMaterial }; }
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
                    model.Material.Count,
                    out chosen,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IDictionary<IPXVertex, int> places = Places(model);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            int removed = 0;
            foreach (int at in chosen)
            {
                IPXMaterial material = model.Material[at];
                if (string.Equals(operation, DuplicateInMaterial, StringComparison.Ordinal))
                {
                    seen = new HashSet<string>(StringComparer.Ordinal);
                }

                removed += Sweep(material, operation, seen, places);
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { RemovedName, removed },
                });
        }

        private static int Sweep(
            IPXMaterial material,
            string operation,
            ISet<string> seen,
            IDictionary<IPXVertex, int> places)
        {
            List<int> dropped = new List<int>();
            for (int at = 0; at < material.Faces.Count; at++)
            {
                if (Drops(material.Faces[at], operation, seen, places))
                {
                    dropped.Add(at);
                }
            }

            for (int at = dropped.Count - 1; at >= 0; at--)
            {
                material.Faces.RemoveAt(dropped[at]);
            }

            return dropped.Count;
        }

        private static bool Drops(
            IPXFace face,
            string operation,
            ISet<string> seen,
            IDictionary<IPXVertex, int> places)
        {
            if (string.Equals(operation, Invalid, StringComparison.Ordinal))
            {
                return !ReferenceCleanup.IsSoundFace(face);
            }

            return !seen.Add(Key(face, places));
        }

        private static string Key(IPXFace face, IDictionary<IPXVertex, int> places)
        {
            return string.Join(
                "-",
                new[] { face.Vertex1, face.Vertex2, face.Vertex3 }
                    .Select(v => At(places, v))
                    .OrderBy(a => a)
                    .Select(a => a.ToString(CultureInfo.InvariantCulture))
                    .ToArray());
        }

        private static int At(IDictionary<IPXVertex, int> places, IPXVertex vertex)
        {
            int place;

            return vertex != null && places.TryGetValue(vertex, out place) ? place : -1;
        }

        private static IDictionary<IPXVertex, int> Places(IPXPmx model)
        {
            Dictionary<IPXVertex, int> places =
                new Dictionary<IPXVertex, int>(ReferenceComparer<IPXVertex>.Instance);
            for (int at = 0; at < model.Vertex.Count; at++)
            {
                places[model.Vertex[at]] = at;
            }

            return places;
        }
    }
}
