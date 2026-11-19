using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    public static class ModelSetPassGroups
    {
        public const string ToolName = "model_set_pass_groups";

        public const string PassGroupName = "passGroup";

        public const string PassGroupsName = "passGroups";

        public const string ChangedName = "changed";

        private const int Groups = 16;

        /// <summary>受け取れる種類。スキーマが並べる順。</summary>
        public static IList<string> Kinds
        {
            get
            {
                return new[] { ElementKinds.Body, ElementKinds.SoftBody };
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

            List<string> known = new List<string>
            {
                ElementKinds.KindName,
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.Ranges,
                TargetNames.Element.All,
                TargetNames.Element.Selected,
                PassGroupName,
                PassGroupsName,
            };
            methods.Add(ToolName, edit.Method(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            string code;
            string message;
            string kind;
            if (!TryKind(context, out kind, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            bool soft = string.Equals(kind, ElementKinds.SoftBody, StringComparison.Ordinal);
            int count = soft ? model.SoftBody.Count : model.Body.Count;
            IList<int> chosen;
            IList<bool[]> wanted;
            if (!TargetInput.TryPositions(
                    context.Params,
                    TargetNames.Element,
                    count,
                    out chosen,
                    out code,
                    out message,
                    context.Screen.Pick(kind, count)))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            if (!TryFlags(context, chosen.Count, out wanted, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            int changed = 0;
            for (int at = 0; at < chosen.Count; at++)
            {
                bool[] held = soft ? model.SoftBody[chosen[at]].PassGroup : model.Body[chosen[at]].PassGroup;
                if (held.SequenceEqual(wanted[at]))
                {
                    continue;
                }

                for (int group = 0; group < Groups; group++)
                {
                    held[group] = wanted[at][group];
                }

                changed++;
            }

            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ChangedName, changed },
            };

            return soft
                ? ComposedEditResult.Complete(value)
                : ComposedEditResult.CompleteRewriting(value, new[] { ElementKinds.Body });
        }

        private static bool TryKind(McpMethodContext context, out string kind, out string message)
        {
            message = null;
            object given;
            if (!context.Params.TryGetValue(ElementKinds.KindName, out given) || given == null)
            {
                kind = ElementKinds.Body;

                return true;
            }

            kind = given as string;
            if (kind != null && Kinds.Contains(kind, StringComparer.Ordinal))
            {
                return true;
            }

            message = ElementKinds.KindName + " は " + string.Join("・", Kinds.ToArray()) + " のどちらかでなければならない。";

            return false;
        }

        /// <summary>対象の順に、書く非衝突グループを1つずつ並べて渡す。</summary>
        private static bool TryFlags(
            McpMethodContext context, int targets, out IList<bool[]> wanted, out string message)
        {
            wanted = null;
            message = null;
            object one;
            object many;
            bool single = context.Params.TryGetValue(PassGroupName, out one);
            bool several = context.Params.TryGetValue(PassGroupsName, out many);
            if (single == several)
            {
                message = PassGroupName + " と " + PassGroupsName + " のどちらか一方を渡す。";

                return false;
            }

            string shape = " は、16個の真偽の並びでなければならない。";
            if (single)
            {
                bool[] flags = Flags(one);
                if (flags == null)
                {
                    message = PassGroupName + shape;

                    return false;
                }

                wanted = Enumerable.Repeat(flags, targets).ToList();

                return true;
            }

            object[] items = many as object[];
            List<bool[]> taken = items == null ? null : items.Select(Flags).ToList();
            if (taken == null || taken.Any(flags => flags == null))
            {
                message = PassGroupsName + " は、16個の真偽の並びを並べた並びでなければならない。";

                return false;
            }

            if (taken.Count != targets)
            {
                message = PassGroupsName + " の件数は、指した対象の数 " + targets + " に揃える。";

                return false;
            }

            wanted = taken;

            return true;
        }

        private static bool[] Flags(object given)
        {
            object[] items = given as object[];
            if (items == null || items.Length != Groups || !items.All(item => item is bool))
            {
                return null;
            }

            return items.Select(item => (bool)item).ToArray();
        }
    }
}
