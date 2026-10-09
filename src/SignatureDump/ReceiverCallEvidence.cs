using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 呼び出しの受け手をハンドルで要るツールへ、そのハンドルを得るまでに順に呼ぶツールの列を
    /// 与える。列は根——受け手を渡さずに呼べるツール——から始まり、次の段は前の段が出したハンドルを
    /// 受け取る。段数に上限は置かない。同じ相手へ届く列が2つ以上あるときは、段数が少ないものを
    /// 採り、段数が並ぶときはツールの名前を先頭から綴りの順で比べて先のものを採る。
    /// </summary>
    public static class ReceiverCallEvidence
    {
        /// <summary>
        /// 型の名前から、その型の実体を1つ得るまでに順に呼ぶツールの列へ。派生型へ至る列は、その
        /// 型が継承・実装する型の名前からも引ける。根から辿り着けない型は持たない。
        /// </summary>
        public static IDictionary<string, IList<string>> ByType(
            InventoryRecord inventory,
            ToolMap map,
            IDictionary<string, string> toolsByRow,
            ToolSchemaTable schemas)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (toolsByRow == null)
            {
                throw new ArgumentNullException(nameof(toolsByRow));
            }

            if (schemas == null)
            {
                throw new ArgumentNullException(nameof(schemas));
            }

            IDictionary<string, ISet<string>> bases = Bases(inventory);
            IDictionary<string, IList<string>> made = Made(
                bases, Steps(inventory, map, toolsByRow, schemas));

            Dictionary<string, IList<string>> answer =
                new Dictionary<string, IList<string>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, IList<string>> one in made)
            {
                foreach (string named in Named(bases, one.Key))
                {
                    IList<string> held;
                    if (!answer.TryGetValue(named, out held) || Better(one.Value, held))
                    {
                        answer[named] = one.Value;
                    }
                }
            }

            return answer;
        }

        /// <summary>
        /// 受け手をハンドルで要るツールの名前から、そのハンドルを得るまでに順に呼ぶツールの列へ。
        /// 自分の行を持たない集約のツールも同じ列で引ける。受け手を要さないツールと、受け手へ
        /// 至る列の無いツールは持たない——ただし <paramref name="aimed"/> が挙げるツールは、受け手を
        /// 渡さずに呼べても列を持つ。<paramref name="roles"/> は担当群を解いた型役割表である。
        /// </summary>
        public static IDictionary<string, IList<string>> ByTool(
            InventoryRecord inventory,
            ToolMap map,
            TypeRoleTable roles,
            IDictionary<string, string> toolsByRow,
            ToolSchemaTable schemas,
            ISet<string> aimed)
        {
            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            if (aimed == null)
            {
                throw new ArgumentNullException(nameof(aimed));
            }

            IDictionary<string, IList<string>> byType =
                ByType(inventory, map, toolsByRow, schemas);
            IDictionary<string, string> receivers = Receivers(inventory, roles, toolsByRow);

            Dictionary<string, IList<string>> byTool =
                new Dictionary<string, IList<string>>(StringComparer.Ordinal);
            foreach (ToolSchema schema in schemas.Tools)
            {
                string receiver;
                IList<string> path;
                if ((E2eCaseBuilder.Unchosen(schema) != null
                        && !aimed.Contains(schema.Tool, StringComparer.Ordinal))
                    || !receivers.TryGetValue(schema.Tool, out receiver)
                    || !byType.TryGetValue(receiver, out path)
                    || path.Contains(schema.Tool, StringComparer.Ordinal))
                {
                    continue;
                }

                byTool[schema.Tool] = path;
            }

            return byTool;
        }

        /// <summary>
        /// ツールの名前から、そのツールがハンドルで受け取る型の名前へ。項目を集めるツールが
        /// 受け取るのは、並べる要素そのものである。
        /// </summary>
        private static IDictionary<string, string> Receivers(
            InventoryRecord inventory,
            TypeRoleTable roles,
            IDictionary<string, string> toolsByRow)
        {
            IDictionary<string, SignatureRecord> signatures = inventory.Signatures
                .ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);
            Dictionary<string, string> receivers =
                new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> named in toolsByRow)
            {
                SignatureRecord signature;
                if (signatures.TryGetValue(named.Key, out signature))
                {
                    receivers[named.Value] = TypeDefinitionName.Of(signature.DeclaringType);
                }
            }

            foreach (TypeRoleRecord role in roles.Types)
            {
                foreach (string tool in Aggregating(role))
                {
                    if (!receivers.ContainsKey(tool))
                    {
                        receivers[tool] = TypeDefinitionName.Of(role.TypeName);
                    }
                }
            }

            return receivers;
        }

        /// <summary>その役割の型の項目を集めるツールの名前。名前を決められない役割では空。</summary>
        private static IEnumerable<string> Aggregating(TypeRoleRecord role)
        {
            return role.Group != CapabilityOwner.None
                && !string.IsNullOrEmpty(role.ElementNoun)
                && !string.IsNullOrEmpty(role.ElementNounPlural)
                ? AggregationToolRule.Of(role)
                : new string[0];
        }

        /// <summary>ハンドルを1つ出す呼び出し1件。受け手を要らない呼び出しでは受け手が null。</summary>
        private sealed class Step
        {
            public Step(string tool, string receiver, string made)
            {
                Tool = tool;
                Receiver = receiver;
                Made = made;
            }

            public string Tool { get; }

            /// <summary>渡す受け手の型の名前。受け手を渡さずに呼べるなら null。</summary>
            public string Receiver { get; }

            public string Made { get; }
        }

        /// <summary>
        /// ハンドルを出すと述べる行のうち、受け手のほかに渡すものを持たない呼び出し。渡すものを
        /// 持たないかはスキーマで見る。
        /// </summary>
        private static IList<Step> Steps(
            InventoryRecord inventory,
            ToolMap map,
            IDictionary<string, string> toolsByRow,
            ToolSchemaTable schemas)
        {
            IDictionary<string, SignatureRecord> signatures = inventory.Signatures
                .ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);
            List<Step> steps = new List<Step>();
            foreach (ToolMapRow row in map.Rows.Where(Issues))
            {
                string tool;
                SignatureRecord signature;
                if (!toolsByRow.TryGetValue(row.SignatureKey, out tool)
                    || !signatures.TryGetValue(row.SignatureKey, out signature))
                {
                    continue;
                }

                ToolSchema schema = schemas.Tools.FirstOrDefault(
                    t => string.Equals(t.Tool, tool, StringComparison.Ordinal));
                if (schema == null)
                {
                    continue;
                }

                if (E2eCaseBuilder.Unchosen(schema) != null)
                {
                    steps.Add(new Step(
                        tool, null, TypeDefinitionName.Of(signature.ValueType)));
                }
                else if (E2eCaseBuilder.Handed(schema))
                {
                    steps.Add(new Step(
                        tool,
                        TypeDefinitionName.Of(signature.DeclaringType),
                        TypeDefinitionName.Of(signature.ValueType)));
                }
            }

            return steps;
        }

        /// <summary>その行が、新しいハンドルを出すと述べているか。</summary>
        private static bool Issues(ToolMapRow row)
        {
            return row.Postcondition != null
                && row.Postcondition.Any(p => p.EffectType == EffectType.HandleCreated);
        }

        /// <summary>出る型の名前から、そこへ届く最も良い列へ。</summary>
        private static IDictionary<string, IList<string>> Made(
            IDictionary<string, ISet<string>> bases, IList<Step> steps)
        {
            Dictionary<string, IList<string>> made =
                new Dictionary<string, IList<string>>(StringComparer.Ordinal);
            bool moved = true;
            while (moved)
            {
                moved = false;
                foreach (Step step in steps)
                {
                    IList<string> path = Extended(bases, made, step);
                    IList<string> held;
                    if (path == null
                        || (made.TryGetValue(step.Made, out held) && !Better(path, held)))
                    {
                        continue;
                    }

                    made[step.Made] = path;
                    moved = true;
                }
            }

            return made;
        }

        /// <summary>その段まで延ばした列。前の段が揃わない、または同じツールを二度通るなら null。</summary>
        private static IList<string> Extended(
            IDictionary<string, ISet<string>> bases,
            IDictionary<string, IList<string>> made,
            Step step)
        {
            if (step.Receiver == null)
            {
                return new[] { step.Tool };
            }

            IList<string> held = Held(bases, made, step.Receiver);

            return held == null || held.Contains(step.Tool, StringComparer.Ordinal)
                ? null
                : held.Concat(new[] { step.Tool }).ToArray();
        }

        /// <summary>その型の受け手に渡せる実体へ届く、最も良い列。代入互換で合わせる。</summary>
        private static IList<string> Held(
            IDictionary<string, ISet<string>> bases,
            IDictionary<string, IList<string>> made,
            string receiver)
        {
            IList<string> best = null;
            foreach (KeyValuePair<string, IList<string>> one in made)
            {
                if (Named(bases, one.Key).Contains(receiver, StringComparer.Ordinal)
                    && (best == null || Better(one.Value, best)))
                {
                    best = one.Value;
                }
            }

            return best;
        }

        /// <summary>その型の実体を渡せる受け手の型の名前。自分自身と、継承・実装する型。</summary>
        private static IEnumerable<string> Named(
            IDictionary<string, ISet<string>> bases, string typeName)
        {
            ISet<string> held;

            return new[] { typeName }.Concat(
                bases.TryGetValue(typeName, out held) ? held : new string[0]);
        }

        /// <summary>型の名前から、その型が継承・実装する型の名前へ。どちらも綴りを揃える。</summary>
        private static IDictionary<string, ISet<string>> Bases(InventoryRecord inventory)
        {
            Dictionary<string, ISet<string>> bases =
                new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
            foreach (TypeRecord type in inventory.Types.Concat(inventory.ReferencedTypes))
            {
                bases[TypeDefinitionName.Of(type.Name)] = new HashSet<string>(
                    (type.BaseTypes ?? new string[0]).Select(TypeDefinitionName.Of),
                    StringComparer.Ordinal);
            }

            return bases;
        }

        /// <summary>一方の列がもう一方より良いか。段数が少ない方を採り、並べば綴りの順で決める。</summary>
        private static bool Better(IList<string> one, IList<string> other)
        {
            if (one.Count != other.Count)
            {
                return one.Count < other.Count;
            }

            for (int at = 0; at < one.Count; at++)
            {
                int order = string.CompareOrdinal(one[at], other[at]);
                if (order != 0)
                {
                    return order < 0;
                }
            }

            return false;
        }
    }
}
