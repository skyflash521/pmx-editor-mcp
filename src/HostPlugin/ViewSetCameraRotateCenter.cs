using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using PEPlugin.View;

namespace PmxEditorMcp
{
    /// <summary>
    /// 視点の回転の中心を、指した要素から決めるツール。
    /// </summary>
    public static class ViewSetCameraRotateCenter
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "view_set_camera_rotate_center";

        /// <summary>選んだ頂点の重心を中心にする。</summary>
        public const string Vertices = "vertices";

        /// <summary>選んだボーンの重心を中心にする。</summary>
        public const string Bones = "bones";

        /// <summary>選んだ面の重心を中心にする。</summary>
        public const string Face = "face";

        public const string FaceFront = "faceFront";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { Vertices, Bones, Face, FaceFront };
            }
        }

        private const float Away = 10f;

        /// <summary>決めた中心の座標を返す項目の名前。</summary>
        public const string CentreName = "centre";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedScreen screen)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            List<string> known = new List<string> { ComposedOperation.OperationName };
            methods.Add(
                ToolName,
                screen.Method(
                    known, ScreenNeeds.View | ScreenNeeds.Pmx, ScreenRefreshKind.Drawn, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, ScreenParts parts)
        {
            IPXPmx model = (IPXPmx)parts.Pmx;
            string operation;
            string code;
            string message;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            string kind = Kind(operation);
            IList<V3> held = Spots(
                model,
                kind,
                ViewSelection.Taken(parts.View, kind, ViewSelection.Count(model, kind)));
            if (held.Count == 0)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "画面で " + kind + " を1つも選んでいないので、回転の中心を決められない。");
            }

            V3 middle = Vectors.Middle(held);
            IPXPmxViewConnector view = (IPXPmxViewConnector)parts.View;
            view.CameraRotateCenter = middle;
            if (string.Equals(operation, FaceFront, StringComparison.Ordinal))
            {
                Faced(view, model, middle);
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { CentreName, new object[] { middle.X, middle.Y, middle.Z } },
                });
        }

        /// <summary>
        /// 重心を取る点。面を指した操作では、選んだ面が使う頂点の点で、2つ以上の面が同じくする頂点は
        /// その面の数だけ数える。
        /// </summary>
        private static IList<V3> Spots(IPXPmx model, string kind, IList<int> held)
        {
            if (string.Equals(kind, ElementKinds.Vertex, StringComparison.Ordinal))
            {
                return held.Select(at => model.Vertex[at].Position).ToList();
            }

            if (string.Equals(kind, ElementKinds.Bone, StringComparison.Ordinal))
            {
                return held.Select(at => model.Bone[at].Position).ToList();
            }

            IList<IPXFace> faces = ViewSelection.Faces(model);

            return held.SelectMany(at => ViewSelection.Corners(faces[at]))
                .Where(vertex => vertex != null)
                .Select(vertex => vertex.Position)
                .ToList();
        }

        /// <summary>その操作が重心を取る要素の種類。</summary>
        private static string Kind(string operation)
        {
            switch (operation)
            {
                case Vertices:
                    return ElementKinds.Vertex;

                case Bones:
                    return ElementKinds.Bone;

                default:
                    return ElementKinds.Face;
            }
        }

        private static void Faced(IPXPmxViewConnector view, IPXPmx model, V3 middle)
        {
            IList<IPXFace> faces = ViewSelection.Faces(model);
            IList<V3> facings = ViewSelection
                .Taken(view, ElementKinds.Face, faces.Count)
                .Select(at => Normal(faces[at]))
                .ToList();
            V3 facing = Vectors.NormalizedSum(facings);
            if (!Vectors.HasLength(facing))
            {
                facing = new V3(0f, 0f, -1f);
            }

            view.SetCameraView(
                middle,
                Vectors.Add(middle, Vectors.Scale(facing, Away)),
                new V3(0f, 1f, 0f));
        }

        private static V3 Normal(IPXFace face)
        {
            return Vectors.PerpendicularTo(
                face.Vertex1.Position, face.Vertex2.Position, face.Vertex3.Position);
        }
    }
}
