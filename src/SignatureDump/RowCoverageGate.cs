using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表の行が、実機へ投げる検査のどれかに覆われることを確かめる。行は自分のツールで
    /// 覆われるか、値を埋め込む先のツールで覆われるか、イベントを取り出すツールで覆われるか、
    /// 受け手へ至る道としてその先の型のツールで覆われる。共通契約が受け持つ行だけは、ツールに
    /// ならずホストの流れの中で呼ばれるので、割当をもって覆われたものとする。
    /// </summary>
    public static class RowCoverageGate
    {
        /// <summary>覆われない行があれば <see cref="InvalidOperationException"/>。</summary>
        public static void Require(
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolsByRow,
            IDictionary<string, ComposedTool> composedTools,
            CommonAssignmentTable assignments,
            TypeRoleTable roles,
            ISet<string> traversed,
            IList<E2eCase> cases)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (toolsByRow == null)
            {
                throw new ArgumentNullException(nameof(toolsByRow));
            }

            if (composedTools == null)
            {
                throw new ArgumentNullException(nameof(composedTools));
            }

            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            if (traversed == null)
            {
                throw new ArgumentNullException(nameof(traversed));
            }

            if (cases == null)
            {
                throw new ArgumentNullException(nameof(cases));
            }

            HashSet<string> examined = new HashSet<string>(
                cases.Select(c => c.Tool), StringComparer.Ordinal);
            HashSet<string> assigned = new HashSet<string>(
                assignments.Assignments.Select(a => a.SignatureKey), StringComparer.Ordinal);
            IDictionary<string, TypeRoleRecord> byType = roles.Types.ToDictionary(
                t => TypeDefinitionName.OfElement(t.TypeName), t => t, StringComparer.Ordinal);
            IDictionary<string, ISet<string>> byOwner = ToolsByOwner(toolsByRow, signatures);
            string[] uncovered = map.Rows
                .Where(r => !assigned.Contains(r.SignatureKey))
                .Where(r => !Covered(
                    r, signatures, toolsByRow, composedTools, byType, byOwner, traversed,
                    examined))
                .Select(r => r.SignatureKey)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();
            if (uncovered.Length == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "検査に覆われない行がある(" + uncovered.Length + "件): "
                    + string.Join("・", uncovered.Take(20))
                    + (uncovered.Length > 20 ? "・ほか" : string.Empty));
        }

        /// <summary>型の名前から、その型が宣言する行のツールの名前へ。</summary>
        private static IDictionary<string, ISet<string>> ToolsByOwner(
            IDictionary<string, string> toolsByRow,
            IDictionary<string, SignatureRecord> signatures)
        {
            Dictionary<string, ISet<string>> byOwner =
                new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> dispatched in toolsByRow)
            {
                SignatureRecord signature;
                if (!signatures.TryGetValue(dispatched.Key, out signature))
                {
                    continue;
                }

                string owner = TypeDefinitionName.OfElement(signature.DeclaringType);
                ISet<string> named;
                if (!byOwner.TryGetValue(owner, out named))
                {
                    named = new HashSet<string>(StringComparer.Ordinal);
                    byOwner.Add(owner, named);
                }

                named.Add(dispatched.Value);
            }

            return byOwner;
        }

        private static bool Covered(
            ToolMapRow row,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolsByRow,
            IDictionary<string, ComposedTool> composedTools,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, ISet<string>> byOwner,
            ISet<string> traversed,
            ISet<string> examined)
        {
            return Covering(row, signatures, toolsByRow, composedTools, byType, byOwner, traversed)
                .Any(examined.Contains);
        }

        /// <summary>行を覆うツールの名前。どれも検査を持たなければ覆われていない。</summary>
        private static IEnumerable<string> Covering(
            ToolMapRow row,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolsByRow,
            IDictionary<string, ComposedTool> composedTools,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, ISet<string>> byOwner,
            ISet<string> traversed)
        {
            string dispatched;
            if (toolsByRow.TryGetValue(row.SignatureKey, out dispatched))
            {
                yield return dispatched;
            }

            foreach (string embedded in row.EmbeddedIn ?? new string[0])
            {
                yield return embedded;
            }

            SignatureRecord signature;
            if (!signatures.TryGetValue(row.SignatureKey, out signature))
            {
                yield break;
            }

            if (row.EventType != null || IsEventArgument(signature, byType))
            {
                foreach (string branching in composedTools
                    .Where(t => t.Value.Branching)
                    .Select(t => t.Key))
                {
                    yield return branching;
                }

                yield break;
            }

            if (!traversed.Contains(row.SignatureKey))
            {
                yield break;
            }

            foreach (string reaching in Reaching(signature, byType, byOwner))
            {
                yield return reaching;
            }
        }

        /// <summary>その行がイベント引数型の項目か。</summary>
        private static bool IsEventArgument(
            SignatureRecord signature, IDictionary<string, TypeRoleRecord> byType)
        {
            TypeRoleRecord owner;

            return byType.TryGetValue(
                    TypeDefinitionName.OfElement(signature.DeclaringType), out owner)
                && owner.Role == TypeRole.EventArgs;
        }

        /// <summary>その行が指す型のツールの名前。独立したツールを持つ役割の型だけが持つ。</summary>
        private static IEnumerable<string> Reaching(
            SignatureRecord signature,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, ISet<string>> byOwner)
        {
            string value = TypeDefinitionName.OfElement(
                ValueTypeName.Contained(signature.ValueType));
            TypeRoleRecord reached;
            if (!byType.TryGetValue(value, out reached)
                || !TypeRoleRecord.HasIndependentTool(reached.Role))
            {
                return new string[0];
            }

            ISet<string> named;
            bool aggregated = reached.Group != CapabilityOwner.None
                && !string.IsNullOrEmpty(reached.ElementNoun)
                && !string.IsNullOrEmpty(reached.ElementNounPlural);

            return (aggregated
                    ? AggregationToolRule.Of(reached).Concat(ElementToolRule.Of(reached))
                    : new string[0])
                .Concat(byOwner.TryGetValue(value, out named) ? named : new string[0]);
        }
    }
}
