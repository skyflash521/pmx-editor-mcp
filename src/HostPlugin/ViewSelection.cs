// 画面が持つ選択を種類ごとに読み書きし、選ぶ相手をモデルから引く。面の位置はモデル全体の通し番号で、
// 材質の区切りをまたいで数える。

using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using PEPlugin.View;

namespace PmxEditorMcp
{
    public static class ViewSelection
    {
        // 画面のコネクタは面の選択を、面が並べる頂点の位置の並びの中の位置で受け渡す。面 n は 3n から3つ。
        private const int CornersPerFace = 3;

        /// <summary>画面が選べる要素の種類。スキーマが並べる順。</summary>
        public static IList<string> Kinds
        {
            get
            {
                return new[]
                {
                    ElementKinds.Vertex,
                    ElementKinds.Face,
                    ElementKinds.Bone,
                    ElementKinds.Body,
                    ElementKinds.Joint,
                };
            }
        }

        /// <summary>種類を読む。画面が選べない種類なら偽で、断る内容を渡す。</summary>
        public static bool TryKind(
            McpMethodContext context,
            string name,
            out string kind,
            out string code,
            out string message)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            code = ToolEnvelope.InvalidArgument;
            message = null;
            object given;
            context.Params.TryGetValue(name, out given);
            kind = given as string;
            if (kind != null && Kinds.Contains(kind, StringComparer.Ordinal))
            {
                code = null;

                return true;
            }

            kind = null;
            message = name + " は次のどれかでなければならない: "
                + string.Join("・", Kinds.ToArray());

            return false;
        }

        /// <summary>
        /// その種類で、いま画面が選んでいる位置。選んだ順のまま渡し、いまのモデルに無い位置は外す。
        /// </summary>
        public static IList<int> Taken(object view, string kind, int count)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            IEnumerable<int> held = Held((IPXPmxViewConnector)view, kind) ?? new int[0];
            if (string.Equals(kind, ElementKinds.Face, StringComparison.Ordinal))
            {
                held = held.Where(at => at >= 0).Select(at => at / CornersPerFace).Distinct();
            }

            return held.Where(at => at >= 0 && at < count).ToList();
        }

        /// <summary>
        /// その種類の選択を、渡した位置で置き換える。書き込む位置は昇順で、重なりを持たない。
        /// </summary>
        public static void Put(object view, string kind, IEnumerable<int> chosen)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (chosen == null)
            {
                throw new ArgumentNullException(nameof(chosen));
            }

            int[] made = chosen.Distinct().OrderBy(at => at).ToArray();
            IPXPmxViewConnector held = (IPXPmxViewConnector)view;
            switch (kind)
            {
                case ElementKinds.Vertex:
                    held.SetSelectedVertexIndices(made);

                    return;

                case ElementKinds.Face:
                    held.SetSelectedFaceIndices(Spread(made));

                    return;

                case ElementKinds.Bone:
                    held.SetSelectedBoneIndices(made);

                    return;

                case ElementKinds.Body:
                    held.SetSelectedBodyIndices(made);

                    return;

                default:
                    held.SetSelectedJointIndices(made);

                    return;
            }
        }

        /// <summary>その種類の要素の数。</summary>
        public static int Count(IPXPmx model, string kind)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            switch (kind)
            {
                case ElementKinds.Vertex:
                    return model.Vertex.Count;

                case ElementKinds.Face:
                    return model.Material.Sum(material => material.Faces.Count);

                case ElementKinds.Bone:
                    return model.Bone.Count;

                case ElementKinds.Body:
                    return model.Body.Count;

                default:
                    return model.Joint.Count;
            }
        }

        /// <summary>モデルが持つ面を、通し番号の順に並べたもの。</summary>
        public static IList<IPXFace> Faces(IPXPmx model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            return model.Material.SelectMany(material => material.Faces).ToList();
        }

        /// <summary>面ごとの、その面を持つ材質の位置。通し番号の順に並ぶ。</summary>
        public static IList<int> Owners(IPXPmx model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            List<int> made = new List<int>();
            for (int at = 0; at < model.Material.Count; at++)
            {
                made.AddRange(Enumerable.Repeat(at, model.Material[at].Faces.Count));
            }

            return made;
        }

        /// <summary>その面が使う3つの頂点。</summary>
        public static IPXVertex[] Corners(IPXFace face)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return new[] { face.Vertex1, face.Vertex2, face.Vertex3 };
        }

        /// <summary>
        /// 要素ごとの、その要素が置かれている点。面はどこか1点に置かれていないので、3つの頂点の点を
        /// 並べて渡す。通し番号の順に並ぶ。
        /// </summary>
        public static IList<IList<V3>> Spots(IPXPmx model, string kind)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            switch (kind)
            {
                case ElementKinds.Vertex:
                    return model.Vertex.Select(vertex => Alone(vertex.Position)).ToList();

                case ElementKinds.Face:
                    return Faces(model)
                        .Select(face => (IList<V3>)Corners(face)
                            .Select(vertex => vertex.Position).ToList())
                        .ToList();

                case ElementKinds.Bone:
                    return model.Bone.Select(bone => Alone(bone.Position)).ToList();

                case ElementKinds.Body:
                    return model.Body.Select(body => Alone(body.Position)).ToList();

                default:
                    return model.Joint.Select(joint => Alone(joint.Position)).ToList();
            }
        }

        private static int[] Spread(IEnumerable<int> faces)
        {
            return faces
                .SelectMany(at => Enumerable.Range(at * CornersPerFace, CornersPerFace))
                .ToArray();
        }

        private static IList<V3> Alone(V3 spot)
        {
            return new[] { spot };
        }

        private static int[] Held(IPXPmxViewConnector view, string kind)
        {
            switch (kind)
            {
                case ElementKinds.Vertex:
                    return view.GetSelectedVertexIndices();

                case ElementKinds.Face:
                    return view.GetSelectedFaceIndices();

                case ElementKinds.Bone:
                    return view.GetSelectedBoneIndices();

                case ElementKinds.Body:
                    return view.GetSelectedBodyIndices();

                default:
                    return view.GetSelectedJointIndices();
            }
        }
    }
}
