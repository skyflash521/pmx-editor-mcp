using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    public static class ModelFindBoneWeights
    {
        public const string ToolName = "model_find_bone_weights";

        public const string OffsetName = "offset";

        public const string LimitName = "limit";

        public const string CountName = "count";

        public const string TotalName = "total";

        public const string BonesName = "bones";

        public const string IndexName = "index";

        public const string NameName = "name";

        public const string VertexCountName = "vertexCount";

        public const string WeightSumName = "weightSum";

        public const string WeightMaxName = "weightMax";

        public const string NextOffsetName = "nextOffset";

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

            List<string> known = new List<string>(ModelFindVertexBounds.VertexPointing)
            {
                ModelFindVertexBounds.MaterialIndicesName,
                OffsetName,
                LimitName,
            };
            methods.Add(ToolName, edit.Read(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            IEnumerable<int> chosen;
            string code;
            string message;
            int offset = 0;
            int limit = int.MaxValue;
            if (!ModelFindVertexBounds.TryChosen(context, model, out chosen, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            if (!TryNumber(context, OffsetName, 0, ref offset, out message)
                || !TryNumber(context, LimitName, 1, ref limit, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            IList<int> vertices = chosen.ToList();
            object[] all = Aggregate(model, vertices)
                .Select(row => (object)Row(model, row.Key, row.Value))
                .ToArray();
            Page<object> page;
            if (!Paging.TryTake(
                    all,
                    offset,
                    limit,
                    ResponseSize.ValueChars(context.BudgetChars),
                    taken => ResponseSize.Serializer.Serialize(Valued(vertices.Count, all.Length, offset, taken)).Length,
                    out page))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.ResponseTooLarge, "値の枠に1件も収まらない。");
            }

            return ComposedEditResult.Complete(
                Valued(vertices.Count, all.Length, offset, page.Items), page.Warnings);
        }

        private static IList<KeyValuePair<int, Tally>> Aggregate(IPXPmx model, IList<int> vertices)
        {
            Dictionary<IPXBone, int> placed = new Dictionary<IPXBone, int>(
                ReferenceComparer<IPXBone>.Instance);
            for (int at = 0; at < model.Bone.Count; at++)
            {
                if (model.Bone[at] != null && !placed.ContainsKey(model.Bone[at]))
                {
                    placed.Add(model.Bone[at], at);
                }
            }

            SortedDictionary<int, Tally> tallies = new SortedDictionary<int, Tally>();
            foreach (int at in vertices)
            {
                Dictionary<int, double> perVertex = new Dictionary<int, double>();
                foreach (KeyValuePair<IPXBone, float> share in VertexWeights.Read(model.Vertex[at]))
                {
                    int bone;
                    if (share.Value > 0f && placed.TryGetValue(share.Key, out bone))
                    {
                        double held;
                        perVertex.TryGetValue(bone, out held);
                        perVertex[bone] = held + share.Value;
                    }
                }

                foreach (KeyValuePair<int, double> weight in perVertex)
                {
                    Tally tally;
                    if (!tallies.TryGetValue(weight.Key, out tally))
                    {
                        tally = new Tally();
                        tallies.Add(weight.Key, tally);
                    }

                    tally.VertexCount++;
                    tally.Sum += weight.Value;
                    tally.Max = Math.Max(tally.Max, weight.Value);
                }
            }

            return tallies.OrderByDescending(pair => pair.Value.Sum).ToList();
        }

        private static IDictionary<string, object> Row(IPXPmx model, int bone, Tally tally)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { IndexName, bone },
                { NameName, model.Bone[bone].Name },
                { VertexCountName, tally.VertexCount },
                { WeightSumName, (float)tally.Sum },
                { WeightMaxName, (float)tally.Max },
            };
        }

        private static IDictionary<string, object> Valued(
            int count, int total, int offset, IList<object> taken)
        {
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { CountName, count },
                { TotalName, total },
                { BonesName, taken.ToArray() },
            };
            int next = Math.Min(offset, total) + taken.Count;
            if (next < total)
            {
                value.Add(NextOffsetName, next);
            }

            return value;
        }

        internal static bool TryNumber(
            McpMethodContext context, string name, int least, ref int taken, out string message)
        {
            message = null;
            object given;
            if (!context.Params.TryGetValue(name, out given) || given == null)
            {
                return true;
            }

            long number;
            if (!ValueInput.TryInteger(given, out number)
                || number < least
                || number > int.MaxValue)
            {
                message = name + " は " + least.ToString(CultureInfo.InvariantCulture)
                    + " 以上の整数でなければならない。";

                return false;
            }

            taken = (int)number;

            return true;
        }

        private sealed class Tally
        {
            public int VertexCount;

            public double Sum;

            public double Max;
        }
    }
}
