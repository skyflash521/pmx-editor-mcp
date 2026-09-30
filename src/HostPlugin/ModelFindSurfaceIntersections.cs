using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using PEPlugin.Pmx;
using Vec = PmxEditorMcp.ModelFindSurfaceDistances.Vec;

namespace PmxEditorMcp
{
    public static class ModelFindSurfaceIntersections
    {
        public const string ToolName = "model_find_surface_intersections";

        public const string OtherSurfaceMaterialIndicesName = "otherSurfaceMaterialIndices";

        public const string OffsetName = "offset";

        public const string LimitName = "limit";

        public const string CountName = "count";

        public const string FaceCountName = "faceCount";

        public const string OtherFaceCountName = "otherFaceCount";

        public const string TotalLengthName = "totalLength";

        public const string MaxDepthName = "maxDepth";

        public const string PairsName = "pairs";

        public const string MaterialName = "material";

        public const string FaceName = "face";

        public const string OtherMaterialName = "otherMaterial";

        public const string OtherFaceName = "otherFace";

        public const string DepthName = "depth";

        public const string LengthName = "length";

        public const string StartName = "start";

        public const string EndName = "end";

        public const string NextOffsetName = "nextOffset";

        private static readonly JavaScriptSerializer Sizer = new JavaScriptSerializer();

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
                ModelFindSurfaceDistances.SurfaceMaterialIndicesName,
                OtherSurfaceMaterialIndicesName,
                OffsetName,
                LimitName,
            };
            methods.Add(ToolName, edit.Read(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            List<int> sides;
            List<int> others;
            string code;
            string message;
            if (!ModelFindSurfaceDistances.TryMaterials(
                    context,
                    model,
                    ModelFindSurfaceDistances.SurfaceMaterialIndicesName,
                    out sides,
                    out code,
                    out message)
                || !ModelFindSurfaceDistances.TryMaterials(
                    context, model, OtherSurfaceMaterialIndicesName, out others, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            int shared = sides.Intersect(others).DefaultIfEmpty(-1).First();
            if (shared >= 0)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    "同じ材質を両側へ指せない: " + shared);
            }

            int offset = 0;
            int limit = int.MaxValue;
            if (!ModelFindBoneWeights.TryNumber(context, OffsetName, 0, ref offset, out message)
                || !ModelFindBoneWeights.TryNumber(context, LimitName, 1, ref limit, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            List<Facet> first = Facets(model, sides);
            List<Facet> second = Facets(model, others);
            if (first.Count == 0 || second.Count == 0)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    (first.Count == 0
                        ? ModelFindSurfaceDistances.SurfaceMaterialIndicesName
                        : OtherSurfaceMaterialIndicesName)
                        + " の材質に面積のある面が1つも無い。");
            }

            List<Crossing> found = Crossings(first, second);
            found.Sort(Deepest);
            object[] all = found.Select(crossing => (object)Row(crossing)).ToArray();
            Page<object> page;
            if (!Paging.TryTake(
                    all,
                    offset,
                    limit,
                    ResponseSize.ValueChars(context.BudgetChars),
                    taken => Sizer.Serialize(Valued(found, offset, taken)).Length,
                    out page))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.ResponseTooLarge, "値の枠に1件も収まらない。");
            }

            return ComposedEditResult.Complete(Valued(found, offset, page.Items), page.Warnings);
        }

        private static IDictionary<string, object> Valued(
            List<Crossing> found, int offset, IList<object> taken)
        {
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { CountName, found.Count },
                {
                    FaceCountName,
                    found.Select(c => new KeyValuePair<int, int>(c.Side.Material, c.Side.Face)).Distinct().Count()
                },
                {
                    OtherFaceCountName,
                    found.Select(c => new KeyValuePair<int, int>(c.Other.Material, c.Other.Face)).Distinct().Count()
                },
                { TotalLengthName, (float)found.Sum(c => c.Length) },
            };
            if (found.Count > 0)
            {
                value.Add(MaxDepthName, (float)found.Max(c => c.Depth));
            }

            value.Add(PairsName, taken.ToArray());
            int next = Math.Min(offset, found.Count) + taken.Count;
            if (next < found.Count)
            {
                value.Add(NextOffsetName, next);
            }

            return value;
        }

        private static IDictionary<string, object> Row(Crossing crossing)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { MaterialName, crossing.Side.Material },
                { FaceName, crossing.Side.Face },
                { OtherMaterialName, crossing.Other.Material },
                { OtherFaceName, crossing.Other.Face },
                { DepthName, (float)crossing.Depth },
                { LengthName, (float)crossing.Length },
                { StartName, crossing.Start.Components() },
                { EndName, crossing.End.Components() },
            };
        }

        private static int Deepest(Crossing left, Crossing right)
        {
            int order = right.Depth.CompareTo(left.Depth);
            order = order != 0 ? order : left.Side.Material.CompareTo(right.Side.Material);
            order = order != 0 ? order : left.Side.Face.CompareTo(right.Side.Face);
            order = order != 0 ? order : left.Other.Material.CompareTo(right.Other.Material);

            return order != 0 ? order : left.Other.Face.CompareTo(right.Other.Face);
        }

        private static List<Facet> Facets(IPXPmx model, IEnumerable<int> materials)
        {
            List<Facet> made = new List<Facet>();
            foreach (int material in materials.Distinct())
            {
                IList<IPXFace> faces = model.Material[material].Faces;
                for (int at = 0; at < faces.Count; at++)
                {
                    IPXVertex[] corners = ViewSelection.Corners(faces[at]);
                    if (corners.Any(c => c == null))
                    {
                        continue;
                    }

                    Vec[] spots = corners.Select(c => Vec.Of(c.Position)).ToArray();
                    Vec span = (spots[1] - spots[0]).Cross(spots[2] - spots[0]);
                    if (span.Dot(span) == 0)
                    {
                        continue;
                    }

                    made.Add(new Facet(material, at, corners, spots));
                }
            }

            return made;
        }

        private static List<Crossing> Crossings(List<Facet> first, List<Facet> second)
        {
            FacetTree tree = FacetTree.Of(second);
            List<Crossing> found = new List<Crossing>();
            foreach (Facet side in first)
            {
                tree.Overlapping(side, other =>
                {
                    Crossing crossing;
                    if (Crossing.TryMeet(side, other, out crossing))
                    {
                        found.Add(crossing);
                    }
                });
            }

            return found;
        }

        private sealed class Facet
        {
            public Facet(int material, int face, IPXVertex[] corners, Vec[] spots)
            {
                Material = material;
                Face = face;
                Corners = corners;
                Spots = spots;
                Low = new Vec(
                    spots.Min(s => s.X), spots.Min(s => s.Y), spots.Min(s => s.Z));
                High = new Vec(
                    spots.Max(s => s.X), spots.Max(s => s.Y), spots.Max(s => s.Z));
                Centre = (spots[0] + spots[1] + spots[2]) * (1.0 / 3.0);
                Facing = (spots[1] - spots[0]).Cross(spots[2] - spots[0]);
                Offset = -Facing.Dot(spots[0]);
            }

            public int Material { get; }

            public int Face { get; }

            public IPXVertex[] Corners { get; }

            public Vec[] Spots { get; }

            public Vec Low { get; }

            public Vec High { get; }

            public Vec Centre { get; }

            public Vec Facing { get; }

            public double Offset { get; }

            public bool Shares(Facet other)
            {
                return Spots.Any(c => other.Spots.Any(o => c.X == o.X && c.Y == o.Y && c.Z == o.Z));
            }

            public bool Overlaps(Facet other)
            {
                return Low.X <= other.High.X && other.Low.X <= High.X
                    && Low.Y <= other.High.Y && other.Low.Y <= High.Y
                    && Low.Z <= other.High.Z && other.Low.Z <= High.Z;
            }

            public double[] Sides(Facet other)
            {
                return other.Spots.Select(s => Facing.Dot(s) + Offset).ToArray();
            }
        }

        private sealed class Crossing
        {
            private Crossing(Facet side, Facet other, Vec start, Vec end, double depth)
            {
                Side = side;
                Other = other;
                Start = start;
                End = end;
                Depth = depth;
                Length = (end - start).Length;
            }

            public Facet Side { get; }

            public Facet Other { get; }

            public Vec Start { get; }

            public Vec End { get; }

            public double Depth { get; }

            public double Length { get; }

            public static bool TryMeet(Facet side, Facet other, out Crossing crossing)
            {
                crossing = null;
                if (side.Shares(other))
                {
                    return false;
                }

                double[] ofSide = other.Sides(side);
                double[] ofOther = side.Sides(other);
                if (!Straddles(ofSide) || !Straddles(ofOther))
                {
                    return false;
                }

                Vec line = other.Facing.Cross(side.Facing);
                if (line.Dot(line) == 0)
                {
                    return false;
                }

                Vec sideLow;
                Vec sideHigh;
                Vec otherLow;
                Vec otherHigh;
                Along(side, ofSide, line, out sideLow, out sideHigh);
                Along(other, ofOther, line, out otherLow, out otherHigh);
                Vec start = line.Dot(sideLow) >= line.Dot(otherLow) ? sideLow : otherLow;
                Vec end = line.Dot(sideHigh) <= line.Dot(otherHigh) ? sideHigh : otherHigh;
                if (line.Dot(end) <= line.Dot(start))
                {
                    return false;
                }

                double toSide = other.Facing.Length;
                double toOther = side.Facing.Length;
                double depth = Math.Min(
                    Math.Min(-ofSide.Min() / toSide, ofSide.Max() / toSide),
                    Math.Min(-ofOther.Min() / toOther, ofOther.Max() / toOther));
                crossing = Ordered(side, other, start, end, depth);

                return true;
            }

            private static Crossing Ordered(Facet side, Facet other, Vec start, Vec end, double depth)
            {
                bool swapped = start.X != end.X
                    ? start.X > end.X
                    : start.Y != end.Y ? start.Y > end.Y : start.Z > end.Z;

                return swapped
                    ? new Crossing(side, other, end, start, depth)
                    : new Crossing(side, other, start, end, depth);
            }

            private static bool Straddles(double[] sides)
            {
                return sides.Any(s => s > 0) && sides.Any(s => s < 0);
            }

            private static void Along(
                Facet facet, double[] sides, Vec line, out Vec low, out Vec high)
            {
                List<Vec> met = new List<Vec>();
                for (int at = 0; at < 3; at++)
                {
                    int next = (at + 1) % 3;
                    if (sides[at] == 0)
                    {
                        met.Add(facet.Spots[at]);
                    }

                    if ((sides[at] > 0 && sides[next] < 0) || (sides[at] < 0 && sides[next] > 0))
                    {
                        double along = sides[at] / (sides[at] - sides[next]);
                        met.Add(facet.Spots[at] + ((facet.Spots[next] - facet.Spots[at]) * along));
                    }
                }

                low = met.OrderBy(p => line.Dot(p)).First();
                high = met.OrderByDescending(p => line.Dot(p)).First();
            }
        }

        private sealed class FacetTree
        {
            private const int LeafSize = 4;

            private readonly Vec _low;

            private readonly Vec _high;

            private readonly FacetTree _left;

            private readonly FacetTree _right;

            private readonly List<Facet> _leaf;

            private FacetTree(List<Facet> facets)
            {
                _low = new Vec(
                    facets.Min(f => f.Low.X), facets.Min(f => f.Low.Y), facets.Min(f => f.Low.Z));
                _high = new Vec(
                    facets.Max(f => f.High.X), facets.Max(f => f.High.Y), facets.Max(f => f.High.Z));
                if (facets.Count <= LeafSize)
                {
                    _leaf = facets;

                    return;
                }

                Vec size = _high - _low;
                int axis = size.X >= size.Y && size.X >= size.Z ? 0 : size.Y >= size.Z ? 1 : 2;
                List<Facet> sorted = facets.OrderBy(f => f.Centre.Axis(axis)).ToList();
                int half = sorted.Count / 2;
                _left = new FacetTree(sorted.GetRange(0, half));
                _right = new FacetTree(sorted.GetRange(half, sorted.Count - half));
            }

            public static FacetTree Of(List<Facet> facets)
            {
                return new FacetTree(facets);
            }

            public void Overlapping(Facet probe, Action<Facet> found)
            {
                if (_low.X > probe.High.X || probe.Low.X > _high.X
                    || _low.Y > probe.High.Y || probe.Low.Y > _high.Y
                    || _low.Z > probe.High.Z || probe.Low.Z > _high.Z)
                {
                    return;
                }

                if (_leaf != null)
                {
                    foreach (Facet facet in _leaf)
                    {
                        if (facet.Overlaps(probe))
                        {
                            found(facet);
                        }
                    }

                    return;
                }

                _left.Overlapping(probe, found);
                _right.Overlapping(probe, found);
            }
        }
    }
}
