using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    public static class ModelValidatePmx
    {
        public const string ToolName = "model_validate_pmx";

        public const string FoundName = "found";

        public const string RunsName = "runs";

        public const string RunsTotalName = "runsTotal";

        public const string OffsetName = "offset";

        public const string LimitName = "limit";

        public const string NextOffsetName = "nextOffset";

        public const string UnnormalizedWeightsName = "unnormalizedWeights";

        public const string HiddenMorphsInExpressionFrameName = "hiddenMorphsInExpressionFrame";

        public static IList<string> Locatable
        {
            get
            {
                return PmxStateCheck.Locatable
                    .Concat(new[] { UnnormalizedWeightsName, HiddenMorphsInExpressionFrameName })
                    .ToList();
            }
        }

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

            methods.Add(ToolName, edit.Read(new List<string> { RunsName, OffsetName, LimitName }, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            string asked;
            int offset;
            int limit;
            string code;
            string message;
            if (!TryRuns(context, out asked, out offset, out limit, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IPXPmx model = (IPXPmx)pmx;
            List<StateItem> items = PmxStateCheck.Of(model).ToList();
            IList<IPXMorph> hidden = HiddenExpressionMorphs.Of(model);
            IList<int> unnormalized = Enumerable.Range(0, model.Vertex.Count)
                .Where(at => !VertexWeights.IsSound(model.Vertex[at]))
                .ToList();
            items.Add(new StateItem(UnnormalizedWeightsName, unnormalized.Count, unnormalized));
            IList<int> hiddenPlaces = PositionsOf(model.Morph, hidden);
            items.Add(new StateItem(HiddenMorphsInExpressionFrameName, hiddenPlaces.Count, hiddenPlaces));

            Dictionary<string, object> found = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (StateItem item in items)
            {
                found.Add(item.Name, item.Count);
            }

            found[FoundName] = items.Sum(item => item.Count);
            if (asked == null)
            {
                return ComposedEditResult.Complete(found);
            }

            IDictionary<string, IList<int>> places = items
                .Where(item => item.Positions != null)
                .ToDictionary(item => item.Name, item => item.Positions, StringComparer.Ordinal);
            IList<object> all = PositionRuns.Joined(places[asked]);
            Page<object> page;
            if (!Paging.TryTake(
                all,
                offset,
                limit,
                ResponseSize.ValueChars(context.BudgetChars),
                taken => ResponseSize.Serializer.Serialize(Located(found, all.Count, offset, taken)).Length,
                out page))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.ResponseTooLarge, "値の枠に1件も収まらない。");
            }

            return ComposedEditResult.Complete(
                Located(found, all.Count, offset, page.Items), page.Warnings);
        }

        private static IDictionary<string, object> Located(
            IDictionary<string, object> found, int total, int offset, IList<object> taken)
        {
            Dictionary<string, object> value = new Dictionary<string, object>(
                found, StringComparer.Ordinal)
            {
                { RunsTotalName, total },
                { RunsName, taken.ToArray() },
            };
            if (offset + taken.Count < total)
            {
                value.Add(NextOffsetName, offset + taken.Count);
            }

            return value;
        }

        private static bool TryRuns(
            McpMethodContext context,
            out string asked,
            out int offset,
            out int limit,
            out string code,
            out string message)
        {
            asked = null;
            offset = 0;
            limit = int.MaxValue;
            if (!context.Params.ContainsKey(RunsName))
            {
                foreach (string name in new[] { OffsetName, LimitName })
                {
                    if (context.Params.ContainsKey(name))
                    {
                        code = ToolEnvelope.InvalidArgument;
                        message = name + " を渡せるのは " + RunsName + " を渡したときだけである。";

                        return false;
                    }
                }

                code = null;
                message = null;

                return true;
            }

            if (!ComposedInput.TryChoice(context, RunsName, Locatable, out asked, out code, out message))
            {
                return false;
            }

            if (!ComposedInput.TryNumber(context, OffsetName, 0, ref offset, out message)
                || !ComposedInput.TryNumber(context, LimitName, 1, ref limit, out message))
            {
                code = ToolEnvelope.InvalidArgument;

                return false;
            }

            return true;
        }

        private static IList<int> PositionsOf(IList<IPXMorph> all, IList<IPXMorph> chosen)
        {
            return Enumerable.Range(0, all.Count)
                .Where(at => chosen.Any(morph => ReferenceEquals(morph, all[at])))
                .ToList();
        }
    }
}
