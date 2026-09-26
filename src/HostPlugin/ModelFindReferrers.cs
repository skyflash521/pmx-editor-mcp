using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace PmxEditorMcp
{
    public static class ModelFindReferrers
    {
        public const string ToolName = "model_find_referrers";

        public const string DetailName = "detail";

        public const string Count = "count";

        public const string CountPerTarget = "countPerTarget";

        public const string Indices = "indices";

        public const string ReferrerKindName = "referrerKind";

        public const string MinWeightName = "minWeight";

        public const string LimitName = "limit";

        public const string OffsetName = "offset";

        public const string ReferrersName = "referrers";

        public const string KindName = "kind";

        public const string CountName = "count";

        public const string IndexName = "index";

        public const string TargetsName = "targets";

        public const string ReferrerIndicesName = "referrerIndices";

        public const string RunsName = "runs";

        public const string ReferrerRunsName = "referrerRuns";

        public const string TotalName = "total";

        public const string NextOffsetName = "nextOffset";

        private static readonly JavaScriptSerializer Sizer = new JavaScriptSerializer();

        public static IList<string> Details
        {
            get { return new[] { Count, CountPerTarget, Indices }; }
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

            List<string> known = new List<string>(ElementScope.Names)
            {
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.All,
                TargetNames.Element.Selected,
                DetailName,
                ReferrerKindName,
                MinWeightName,
                OffsetName,
                LimitName,
                RunsName,
            };
            methods.Add(ToolName, edit.Read(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            ElementKind kind;
            IList<object> owners;
            IList<int> targets;
            string detail;
            string referrerKind;
            float minWeight;
            int offset;
            int limit;
            bool runs;
            string code;
            string message;
            if (!TryTargetKind(context, out code, out message)
                || !ElementScope.TryTake(context, pmx, out kind, out owners, out code, out message)
                || !ElementScope.TryPositions(
                    context, pmx, kind, owners[0], out targets, out code, out message)
                || !TryDetail(context, out detail, out code, out message)
                || !TryReferrerKind(context, kind, detail, out referrerKind, out code, out message)
                || !TryMinWeight(context, kind, out minWeight, out code, out message)
                || !TryPaging(context, detail, out offset, out limit, out code, out message)
                || !TryRuns(context, detail, out runs, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            if (string.Equals(detail, Count, StringComparison.Ordinal))
            {
                return ComposedEditResult.Complete(
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        {
                            ReferrersName,
                            Rows(ReferenceEdges.Union(pmx, kind.Name, targets, minWeight))
                        },
                    });
            }

            return string.Equals(detail, CountPerTarget, StringComparison.Ordinal)
                ? PerTarget(context, pmx, kind, targets, minWeight, offset, limit)
                : Places(context, pmx, kind, targets, referrerKind, minWeight, runs, offset, limit);
        }

        private static ComposedEditResult PerTarget(
            McpMethodContext context,
            object pmx,
            ElementKind kind,
            IList<int> targets,
            float minWeight,
            int offset,
            int limit)
        {
            IList<int> asked = Asked(targets, offset, limit);
            IList<ReferrerSets> found = ReferenceEdges.PerTarget(pmx, kind.Name, asked, minWeight);
            IList<object> rows = Enumerable.Range(0, asked.Count)
                .Select(at => (object)new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { IndexName, asked[at] },
                    { ReferrersName, Rows(found[at]) },
                })
                .ToList();

            return Cut(context, rows, TargetsName, targets.Count, targets.Count, offset);
        }

        private static ComposedEditResult Places(
            McpMethodContext context,
            object pmx,
            ElementKind kind,
            IList<int> targets,
            string referrerKind,
            float minWeight,
            bool runs,
            int offset,
            int limit)
        {
            IList<int> found = ReferenceEdges.Union(pmx, kind.Name, targets, minWeight)
                .Of(referrerKind);
            IList<object> all = runs ? PositionRuns.Joined(found) : found.Cast<object>().ToList();

            return Cut(
                context,
                Asked(all, offset, limit),
                runs ? ReferrerRunsName : ReferrerIndicesName,
                all.Count,
                all.Count,
                offset);
        }

        private static IList<T> Asked<T>(IList<T> all, int offset, int limit)
        {
            return all.Skip(offset).Take(limit).ToList();
        }

        private static ComposedEditResult Cut(
            McpMethodContext context,
            IList<object> taken,
            string name,
            int total,
            int pointed,
            int offset)
        {
            Page<object> page;
            if (!Paging.TryTake(
                taken,
                0,
                Math.Max(1, taken.Count),
                ResponseSize.ValueChars(context.BudgetChars),
                held => Sizer.Serialize(Valued(name, total, pointed, offset, held)).Length,
                out page))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.ResponseTooLarge, "値の枠に1件も収まらない。");
            }

            return ComposedEditResult.Complete(
                Valued(name, total, pointed, offset, page.Items), page.Warnings);
        }

        private static IDictionary<string, object> Valued(
            string name, int total, int pointed, int offset, IList<object> taken)
        {
            int next = offset + taken.Count;
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { TotalName, total },
                { name, taken.ToArray() },
            };
            if (next < pointed)
            {
                value.Add(NextOffsetName, next);
            }

            return value;
        }

        private static object[] Rows(ReferrerSets found)
        {
            return found.Kinds
                .Select(kind => (object)new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { KindName, kind },
                    { CountName, found.Of(kind).Count },
                })
                .ToArray();
        }

        private static bool TryTargetKind(
            McpMethodContext context, out string code, out string message)
        {
            code = null;
            ElementKind kind;
            object given;
            context.Params.TryGetValue(ElementKinds.KindName, out given);
            if (ElementKinds.TryResolve(given, out kind, out message)
                && ReferenceEdges.TargetKinds.Contains(kind.Name, StringComparer.Ordinal))
            {
                return true;
            }

            code = ToolEnvelope.InvalidArgument;
            message = ElementKinds.KindName + " は次のどれかでなければならない: "
                + string.Join("・", ReferenceEdges.TargetKinds.ToArray());

            return false;
        }

        private static bool TryDetail(
            McpMethodContext context, out string detail, out string code, out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = null;
            object given;
            if (!context.Params.TryGetValue(DetailName, out given) || given == null)
            {
                detail = Count;
                code = null;

                return true;
            }

            detail = given as string;
            if (detail != null && Details.Contains(detail, StringComparer.Ordinal))
            {
                code = null;

                return true;
            }

            detail = null;
            message = DetailName + " は次のどれかでなければならない: "
                + string.Join("・", Details.ToArray());

            return false;
        }

        private static bool TryReferrerKind(
            McpMethodContext context,
            ElementKind kind,
            string detail,
            out string referrerKind,
            out string code,
            out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = null;
            referrerKind = null;
            object given;
            bool named = context.Params.TryGetValue(ReferrerKindName, out given);
            if (!string.Equals(detail, Indices, StringComparison.Ordinal))
            {
                if (!named)
                {
                    code = null;

                    return true;
                }

                message = ReferrerKindName + " を渡せるのは " + DetailName + " が "
                    + Indices + " のときだけである。";

                return false;
            }

            IList<string> pointing = ReferenceEdges.Into(kind.Name)
                .Select(edge => edge.ReferrerKind)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            referrerKind = given as string;
            if (referrerKind != null && pointing.Contains(referrerKind, StringComparer.Ordinal))
            {
                code = null;

                return true;
            }

            referrerKind = null;
            message = ReferrerKindName + " は次のどれかでなければならない: "
                + string.Join("・", pointing.ToArray());

            return false;
        }

        private static bool TryMinWeight(
            McpMethodContext context,
            ElementKind kind,
            out float minWeight,
            out string code,
            out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = null;
            minWeight = 0f;
            object given;
            if (!context.Params.TryGetValue(MinWeightName, out given) || given == null)
            {
                code = null;

                return true;
            }

            if (!string.Equals(kind.Name, ElementKinds.Bone, StringComparison.Ordinal))
            {
                message = MinWeightName + " を渡せるのは " + ElementKinds.KindName + " が "
                    + ElementKinds.Bone + " のときだけである。";

                return false;
            }

            if (!ValueInput.IsNumber(given))
            {
                message = MinWeightName + " は数でなければならない。";

                return false;
            }

            double taken = Convert.ToDouble(given, CultureInfo.InvariantCulture);
            if (taken < 0d || taken > 1d)
            {
                message = MinWeightName + " は0以上1以下の数である。";

                return false;
            }

            minWeight = (float)taken;
            code = null;

            return true;
        }

        private static bool TryRuns(
            McpMethodContext context,
            string detail,
            out bool runs,
            out string code,
            out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = null;
            runs = false;
            object given;
            if (!context.Params.TryGetValue(RunsName, out given))
            {
                code = null;

                return true;
            }

            if (!string.Equals(detail, Indices, StringComparison.Ordinal))
            {
                message = RunsName + " を渡せるのは " + DetailName + " が " + Indices + " のときだけである。";

                return false;
            }

            if (!(given is bool))
            {
                message = RunsName + " は真か偽でなければならない。";

                return false;
            }

            runs = (bool)given;
            code = null;

            return true;
        }

        private static bool TryPaging(
            McpMethodContext context,
            string detail,
            out int offset,
            out int limit,
            out string code,
            out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = null;
            offset = 0;
            limit = int.MaxValue;
            bool cuts = !string.Equals(detail, Count, StringComparison.Ordinal);
            foreach (string name in new[] { OffsetName, LimitName })
            {
                if (!cuts && context.Params.ContainsKey(name))
                {
                    message = name + " を渡せるのは " + DetailName + " が "
                        + CountPerTarget + "・" + Indices + " のときだけである。";

                    return false;
                }
            }

            if (cuts
                && (!TryNumber(context, OffsetName, 0, ref offset, out message)
                    || !TryNumber(context, LimitName, 1, ref limit, out message)))
            {
                return false;
            }

            code = null;

            return true;
        }

        private static bool TryNumber(
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
    }
}
