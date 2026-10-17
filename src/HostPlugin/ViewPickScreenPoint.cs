// ComposedScreen に載る合成ツール。

using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using PEPlugin.View;

namespace PmxEditorMcp
{
    public static class ViewPickScreenPoint
    {
        public const string ToolName = "view_pick_screen_point";

        public const string XName = "x";

        public const string YName = "y";

        public const string ImageWidthName = "imageWidth";

        public const string ImageHeightName = "imageHeight";

        public const string ScreenName = "screen";

        public const string MaterialIndicesName = "materialIndices";

        public const string LimitName = "limit";

        public const string CountName = "count";

        public const string HitsName = "hits";

        public const string MaterialName = "material";

        public const string FaceName = "face";

        public const string VerticesName = "vertices";

        public const string PointName = "point";

        public const string DistanceName = "distance";

        public const string FrontFacingName = "frontFacing";

        public const string VertexName = "vertex";

        public const string VertexDistanceName = "vertexDistance";

        public const string VertexScreenName = "vertexScreen";

        private const int Screens = 4;

        private const double Edge = 1e-9;

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

            List<string> known = new List<string>
            {
                XName, YName, ImageWidthName, ImageHeightName, ScreenName, MaterialIndicesName, LimitName,
            };
            methods.Add(
                ToolName,
                screen.Method(
                    known,
                    ScreenNeeds.View | ScreenNeeds.Pmx | ScreenNeeds.ReadsPmx,
                    ScreenRefreshKind.None,
                    Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, ScreenParts parts)
        {
            IPXPmx model = (IPXPmx)parts.Pmx;
            Request request;
            string code;
            string message;
            if (!TryRequest(context, out request, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            List<int> materials;
            if (context.Params.ContainsKey(MaterialIndicesName))
            {
                if (!ModelFindSurfaceDistances.TryMaterials(
                        context, model, MaterialIndicesName, out materials, out code, out message))
                {
                    return ComposedEditResult.Refuse(code, message);
                }
            }
            else
            {
                materials = Enumerable.Range(0, model.Material.Count).ToList();
            }

            IPXPmxViewConnector view = (IPXPmxViewConnector)parts.View;
            double[,] viewMatrix = Cells(view.GetViewMatrix(request.Screen));
            double[,] projection = Cells(view.GetProjectionMatrix(request.Screen));
            double[,] both = Times(viewMatrix, projection);
            double[,] back;
            if (!TryInverse(both, out back))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable, "ビューの行列に逆行列が無く、画素を視線へ戻せない。");
            }

            double ndcX = ((request.X / request.Width) * 2d) - 1d;
            double ndcY = 1d - ((request.Y / request.Height) * 2d);
            double[] near = Unproject(back, ndcX, ndcY, 0d);
            double[] far = Unproject(back, ndcX, ndcY, 1d);
            if (near == null || far == null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable, "ビューの行列で画素を視線へ戻せない。");
            }

            double[] along = Minus(far, near);
            List<Crossing> found = Cross(model, materials, near, along);
            found.Sort((left, right) =>
            {
                int order = left.T.CompareTo(right.T);

                return order != 0 ? order : left.Face.CompareTo(right.Face);
            });

            double length = Math.Sqrt(Dot(along, along));
            List<object> hits = found
                .Take(request.Limit)
                .Select(crossing => Describe(model, both, request, near, along, length, crossing))
                .ToList();

            return ComposedEditResult.Complete(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { CountName, hits.Count },
                { HitsName, hits.ToArray() },
            });
        }

        private static object Describe(
            IPXPmx model,
            double[,] both,
            Request request,
            double[] near,
            double[] along,
            double length,
            Crossing crossing)
        {
            double[] point = Plus(near, Scaled(along, crossing.T));
            int closest = 0;
            double gap = double.PositiveInfinity;
            for (int at = 0; at < 3; at++)
            {
                double[] gapVector = Minus(point, crossing.Corners[at]);
                double squared = Dot(gapVector, gapVector);
                if (squared < gap)
                {
                    gap = squared;
                    closest = at;
                }
            }

            IPXVertex[] vertices = ViewSelection.Corners(
                model.Material[crossing.Material].Faces[crossing.FaceInMaterial]);
            double[] onScreen = Project(both, crossing.Corners[closest]);

            Dictionary<string, object> made = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { MaterialName, crossing.Material },
                { FaceName, crossing.Face },
                { VerticesName, vertices.Select(v => (object)model.Vertex.IndexOf(v)).ToArray() },
                { PointName, new object[] { (float)point[0], (float)point[1], (float)point[2] } },
                { DistanceName, (float)(crossing.T * length) },
                { FrontFacingName, crossing.FrontFacing },
                { VertexName, model.Vertex.IndexOf(vertices[closest]) },
                { VertexDistanceName, (float)Math.Sqrt(gap) },
            };
            if (onScreen != null)
            {
                made.Add(
                    VertexScreenName,
                    new object[]
                    {
                        (float)(((onScreen[0] + 1d) / 2d) * request.Width),
                        (float)(((1d - onScreen[1]) / 2d) * request.Height),
                    });
            }

            return made;
        }

        private static List<Crossing> Cross(
            IPXPmx model, IList<int> materials, double[] origin, double[] along)
        {
            HashSet<int> wanted = new HashSet<int>(materials);
            List<Crossing> found = new List<Crossing>();
            int face = 0;
            for (int material = 0; material < model.Material.Count; material++)
            {
                IPXMaterial held = model.Material[material];
                bool taken = wanted.Contains(material);
                for (int inside = 0; inside < held.Faces.Count; inside++, face++)
                {
                    if (!taken)
                    {
                        continue;
                    }

                    IPXVertex[] corners = ViewSelection.Corners(held.Faces[inside]);
                    if (corners.Any(c => c == null))
                    {
                        continue;
                    }

                    double[][] spots = corners
                        .Select(c => new double[] { c.Position.X, c.Position.Y, c.Position.Z })
                        .ToArray();
                    double t;
                    bool front;
                    if (Meets(spots, origin, along, out t, out front) && (front || held.BothDraw))
                    {
                        found.Add(new Crossing(material, inside, face, spots, t, front));
                    }
                }
            }

            return found;
        }

        private static bool Meets(
            double[][] spots, double[] origin, double[] along, out double t, out bool front)
        {
            t = 0d;
            front = false;
            double[] first = Minus(spots[1], spots[0]);
            double[] second = Minus(spots[2], spots[0]);
            double[] normal = Cross(first, second);
            double[] pvec = Cross(along, second);
            double det = Dot(first, pvec);
            double scale = Math.Sqrt(Dot(along, along) * Dot(first, first) * Dot(second, second));
            if (!(Math.Abs(det) > scale * 1e-12))
            {
                return false;
            }

            double[] start = Minus(origin, spots[0]);
            double u = Dot(start, pvec) / det;
            if (u < -Edge || u > 1d + Edge)
            {
                return false;
            }

            double[] qvec = Cross(start, first);
            double v = Dot(along, qvec) / det;
            if (v < -Edge || u + v > 1d + Edge)
            {
                return false;
            }

            t = Dot(second, qvec) / det;
            front = Dot(along, normal) < 0d;

            return t >= 0d && t <= 1d;
        }

        private static bool TryRequest(McpMethodContext context, out Request request, out string message)
        {
            request = null;
            float x;
            float y;
            int width;
            int height;
            int screen = 0;
            int limit = 1;
            if (!Real(context, XName, out x, out message)
                || !Real(context, YName, out y, out message)
                || !Count(context, ImageWidthName, 1, out width, out message)
                || !Count(context, ImageHeightName, 1, out height, out message)
                || !ComposedInput.TryNumber(context, ScreenName, 0, ref screen, out message)
                || !ComposedInput.TryNumber(context, LimitName, 1, ref limit, out message))
            {
                return false;
            }

            if (screen >= Screens)
            {
                message = ScreenName + " は0以上" + Screens + "未満の整数でなければならない。";

                return false;
            }

            if (x < 0 || x > width || y < 0 || y > height)
            {
                message = XName + " と " + YName + " は画像の中(0 以上、" + ImageWidthName + " と "
                    + ImageHeightName + " 以下)を指さなければならない。";

                return false;
            }

            request = new Request(x, y, width, height, screen, limit);
            message = null;

            return true;
        }

        private static bool Real(McpMethodContext context, string name, out float number, out string message)
        {
            number = 0f;
            message = null;
            object given;
            if (context.Params.TryGetValue(name, out given) && ValueInput.TrySingle(given, out number))
            {
                return true;
            }

            message = name + " は有限の数でなければならない。";

            return false;
        }

        private static bool Count(
            McpMethodContext context,
            string name,
            int least,
            out int number,
            out string message)
        {
            number = 0;
            message = null;
            object given;
            if (!context.Params.TryGetValue(name, out given))
            {
                message = name + " を渡さなければならない。";

                return false;
            }

            if (ValueInput.TryIndex(given, out number) && number >= least)
            {
                return true;
            }

            message = name + " は " + least + " 以上の整数でなければならない。";

            return false;
        }

        private static double[,] Cells(M given)
        {
            return new double[,]
            {
                { given.M11, given.M12, given.M13, given.M14 },
                { given.M21, given.M22, given.M23, given.M24 },
                { given.M31, given.M32, given.M33, given.M34 },
                { given.M41, given.M42, given.M43, given.M44 },
            };
        }

        private static double[,] Times(double[,] left, double[,] right)
        {
            double[,] made = new double[4, 4];
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    for (int at = 0; at < 4; at++)
                    {
                        made[row, column] += left[row, at] * right[at, column];
                    }
                }
            }

            return made;
        }

        private static bool TryInverse(double[,] given, out double[,] inverse)
        {
            double[,] work = new double[4, 8];
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    work[row, column] = given[row, column];
                }

                work[row, 4 + row] = 1d;
            }

            inverse = null;
            for (int at = 0; at < 4; at++)
            {
                int pivot = at;
                for (int row = at + 1; row < 4; row++)
                {
                    if (Math.Abs(work[row, at]) > Math.Abs(work[pivot, at]))
                    {
                        pivot = row;
                    }
                }

                if (!(Math.Abs(work[pivot, at]) > 1e-300))
                {
                    return false;
                }

                for (int column = 0; column < 8; column++)
                {
                    double swap = work[at, column];
                    work[at, column] = work[pivot, column];
                    work[pivot, column] = swap;
                }

                double divisor = work[at, at];
                for (int column = 0; column < 8; column++)
                {
                    work[at, column] /= divisor;
                }

                for (int row = 0; row < 4; row++)
                {
                    if (row == at)
                    {
                        continue;
                    }

                    double factor = work[row, at];
                    for (int column = 0; column < 8; column++)
                    {
                        work[row, column] -= factor * work[at, column];
                    }
                }
            }

            inverse = new double[4, 4];
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    inverse[row, column] = work[row, 4 + column];
                    if (double.IsNaN(inverse[row, column]) || double.IsInfinity(inverse[row, column]))
                    {
                        inverse = null;

                        return false;
                    }
                }
            }

            return true;
        }

        private static double[] Unproject(double[,] matrix, double x, double y, double z)
        {
            double[] made = new double[4];
            double[] given = { x, y, z, 1d };
            for (int column = 0; column < 4; column++)
            {
                for (int row = 0; row < 4; row++)
                {
                    made[column] += given[row] * matrix[row, column];
                }
            }

            if (!(Math.Abs(made[3]) > 1e-300))
            {
                return null;
            }

            return new[] { made[0] / made[3], made[1] / made[3], made[2] / made[3] };
        }

        private static double[] Project(double[,] both, double[] point)
        {
            double[] made = new double[4];
            double[] given = { point[0], point[1], point[2], 1d };
            for (int column = 0; column < 4; column++)
            {
                for (int row = 0; row < 4; row++)
                {
                    made[column] += given[row] * both[row, column];
                }
            }

            if (!(made[3] > 0d) || made[2] < 0d)
            {
                return null;
            }

            return new[] { made[0] / made[3], made[1] / made[3] };
        }

        private static double[] Minus(double[] left, double[] right)
        {
            return new[] { left[0] - right[0], left[1] - right[1], left[2] - right[2] };
        }

        private static double[] Plus(double[] left, double[] right)
        {
            return new[] { left[0] + right[0], left[1] + right[1], left[2] + right[2] };
        }

        private static double[] Scaled(double[] given, double by)
        {
            return new[] { given[0] * by, given[1] * by, given[2] * by };
        }

        private static double Dot(double[] left, double[] right)
        {
            return (left[0] * right[0]) + (left[1] * right[1]) + (left[2] * right[2]);
        }

        private static double[] Cross(double[] left, double[] right)
        {
            return new[]
            {
                (left[1] * right[2]) - (left[2] * right[1]),
                (left[2] * right[0]) - (left[0] * right[2]),
                (left[0] * right[1]) - (left[1] * right[0]),
            };
        }

        private sealed class Request
        {
            public Request(double x, double y, double width, double height, int screen, int limit)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Screen = screen;
                Limit = limit;
            }

            public double X { get; }

            public double Y { get; }

            public double Width { get; }

            public double Height { get; }

            public int Screen { get; }

            public int Limit { get; }
        }

        private sealed class Crossing
        {
            public Crossing(
                int material, int faceInMaterial, int face, double[][] corners, double t, bool frontFacing)
            {
                Material = material;
                FaceInMaterial = faceInMaterial;
                Face = face;
                Corners = corners;
                T = t;
                FrontFacing = frontFacing;
            }

            public int Material { get; }

            public int FaceInMaterial { get; }

            public int Face { get; }

            public double[][] Corners { get; }

            public double T { get; }

            public bool FrontFacing { get; }
        }
    }
}
