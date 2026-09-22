using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した材質の面の辺を、UVの上へ線で描いた画像を返すツール。
    /// </summary>
    public static class ModelDrawUvLayout
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_draw_uv_layout";

        /// <summary>描く画像の一辺の画素数。</summary>
        public const int Side = 1024;

        private static readonly Color Ground = Color.White;

        private static readonly Color Frame = Color.FromArgb(160, 160, 160);

        private static readonly Color[] Inks =
        {
            Color.FromArgb(220, 20, 60),
            Color.FromArgb(30, 90, 220),
            Color.FromArgb(20, 160, 60),
            Color.FromArgb(230, 120, 0),
            Color.FromArgb(150, 40, 200),
            Color.FromArgb(0, 150, 160),
        };

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
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.All,
                TargetNames.Element.Selected,
            };
            methods.Add(ToolName, edit.Read(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            IList<int> chosen;
            string code;
            string message;
            if (!TargetInput.TryPositions(
                    context.Params,
                    TargetNames.Element,
                    model.Material.Count,
                    out chosen,
                    out code,
                    out message,
                    context.Screen.Pick(ElementKinds.Material, model.Material.Count)))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            List<V2[]> triangles = new List<V2[]>();
            List<int> owners = new List<int>();
            for (int at = 0; at < chosen.Count; at++)
            {
                foreach (IPXFace face in model.Material[chosen[at]].Faces)
                {
                    V2[] corners = { Uv(face.Vertex1), Uv(face.Vertex2), Uv(face.Vertex3) };
                    if (corners.All(Finite))
                    {
                        triangles.Add(corners);
                        owners.Add(at);
                    }
                }
            }

            object json;
            IList<string> warnings;
            bool packed;
            using (Bitmap image = Draw(triangles, owners))
            {
                packed = ValueShape.TryToJson(
                    typeof(Bitmap),
                    image,
                    ImageTransfer.SoleValueMaxLongSide,
                    out json,
                    out warnings,
                    out code,
                    out message);
            }

            return packed
                ? ComposedEditResult.Complete(json, warnings)
                : ComposedEditResult.Refuse(code, message);
        }

        /// <summary>
        /// 0から1の範囲と、描く点のすべてを収める正方形を画像へ当てて描く。Vは下へ向かって増える。
        /// </summary>
        private static Bitmap Draw(IList<V2[]> triangles, IList<int> owners)
        {
            IEnumerable<V2> points = triangles.SelectMany(t => t);
            double minU = Math.Min(0d, points.Select(p => (double)p.X).DefaultIfEmpty(0d).Min());
            double minV = Math.Min(0d, points.Select(p => (double)p.Y).DefaultIfEmpty(0d).Min());
            double maxU = Math.Max(1d, points.Select(p => (double)p.X).DefaultIfEmpty(1d).Max());
            double maxV = Math.Max(1d, points.Select(p => (double)p.Y).DefaultIfEmpty(1d).Max());
            double scale = (Side - 1) / Math.Max(maxU - minU, maxV - minV);
            Func<float, float, PointF> place =
                (u, v) => new PointF((float)((u - minU) * scale), (float)((v - minV) * scale));

            Bitmap image = new Bitmap(Side, Side);
            try
            {
                using (Graphics graphics = Graphics.FromImage(image))
                using (Pen frame = new Pen(Frame))
                {
                    graphics.Clear(Ground);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    PointF low = place(0f, 0f);
                    PointF high = place(1f, 1f);
                    graphics.DrawRectangle(frame, low.X, low.Y, high.X - low.X, high.Y - low.Y);

                    Pen[] pens = Inks.Select(ink => new Pen(ink)).ToArray();
                    try
                    {
                        for (int at = 0; at < triangles.Count; at++)
                        {
                            graphics.DrawPolygon(
                                pens[owners[at] % pens.Length],
                                triangles[at].Select(p => place(p.X, p.Y)).ToArray());
                        }
                    }
                    finally
                    {
                        foreach (Pen pen in pens)
                        {
                            pen.Dispose();
                        }
                    }
                }
            }
            catch
            {
                image.Dispose();
                throw;
            }

            return image;
        }

        private static V2 Uv(IPXVertex vertex)
        {
            return vertex == null ? null : vertex.UV;
        }

        private static bool Finite(V2 uv)
        {
            return uv != null
                && !float.IsNaN(uv.X) && !float.IsInfinity(uv.X)
                && !float.IsNaN(uv.Y) && !float.IsInfinity(uv.Y);
        }
    }
}
