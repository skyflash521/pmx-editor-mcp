using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表の行が、実機へ投げる検査のうち振る舞いを確かめるもののどれかに覆われることを
    /// 確かめる。行は自分のツールで
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

            // 覆いに数えるのは、呼び先まで届いたことが結末から分かる検査だけである。呼び先が在る
            // ことしか見ていない検査と、入口で断られることを見る検査は、行の振る舞いを一度も
            // 確かめないまま通るので数えない。
            HashSet<string> arrivedTools = new HashSet<string>(
                cases.Where(c => Reached(c.Expectation)).Select(c => c.Tool),
                StringComparer.Ordinal);

            // 同じ名前のツールを複数の行が持つことがあり、そのときツールの名前だけでは、検査が
            // どの行を通ったのかを言えない。自分の名前のツールを持つ行は、その行を名指しした
            // 検査で数える。
            HashSet<string> arrived = new HashSet<string>(
                cases
                    .Where(c => Reached(c.Expectation) && !string.IsNullOrEmpty(c.RowKey))
                    .Select(c => c.RowKey),
                StringComparer.Ordinal);

            // 効果を宣言する行は、その宣言を確かめた検査でだけ覆われる。呼び先まで届いただけの
            // 検査は、宣言した効果が起きたかどうかを一度も見ていない。
            HashSet<string> verified = new HashSet<string>(
                cases
                    .Where(c => c.Checks != null && !string.IsNullOrEmpty(c.RowKey))
                    .Select(c => c.RowKey),
                StringComparer.Ordinal);
            HashSet<string> assigned = new HashSet<string>(
                assignments.Assignments.Select(a => a.SignatureKey), StringComparer.Ordinal);
            IDictionary<string, TypeRoleRecord> byType = roles.Types.ToDictionary(
                t => TypeDefinitionName.OfElement(t.TypeName), t => t, StringComparer.Ordinal);
            IDictionary<string, ISet<string>> byOwner = ToolsByOwner(toolsByRow, signatures);
            // 覆うツールを持つ行が届かせられないなら、そのツールへ埋め込まれた行にも届く道が無い。
            // 覆うツールがどれもそうである行は、理由を述べた行と同じく判定から外す——理由を写せば
            // 写した先は写した時点で固まり、元の行が届くようになっても戻らない。
            HashSet<string> beyond = new HashSet<string>(
                toolsByRow
                    .Where(t => map.Rows.Any(r => Excused(r)
                        && string.Equals(r.SignatureKey, t.Key, StringComparison.Ordinal)))
                    .Select(t => t.Value),
                StringComparer.Ordinal);
            ToolMapRow[] judged = map.Rows
                .Where(r => !assigned.Contains(r.SignatureKey) && !Excused(r))
                .Where(r => toolsByRow.ContainsKey(r.SignatureKey)
                    || !Beyond(
                        r, signatures, toolsByRow, composedTools, byType, byOwner, traversed,
                        beyond))
                .ToArray();
            foreach (ToolMapRow row in judged)
            {
                RequireReachedOrExplained(row, toolsByRow, arrived, arrivedTools);
            }

            string[] uncovered = judged
                .Where(r => !Covered(
                    r, signatures, toolsByRow, composedTools, byType, byOwner, traversed,
                    arrivedTools, arrived, verified))
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

        /// <summary>
        /// 届かせられない理由を述べた行か。理由を述べた行は、共通契約が受け持つ行と同じく覆いの
        /// 判定から外す——届かせる手立ての無い行へ検査を求めても、増えるのは通らない要求だけである。
        /// </summary>
        private static bool Excused(ToolMapRow row)
        {
            return row.Basis.IndexOf(
                E2eCaseBuilder.UnreachableReason, StringComparison.Ordinal) >= 0;
        }

        /// <summary>その行を覆えるツールが1つ以上あり、そのどれもが届かせられないか。</summary>
        private static bool Beyond(
            ToolMapRow row,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolsByRow,
            IDictionary<string, ComposedTool> composedTools,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, ISet<string>> byOwner,
            ISet<string> traversed,
            ISet<string> beyond)
        {
            string[] covering = Covering(
                row, signatures, toolsByRow, composedTools, byType, byOwner, traversed).ToArray();

            return covering.Length != 0 && covering.All(beyond.Contains);
        }

        /// <summary>
        /// 自分の名前のツールを持つ行は、呼び先まで届く検査を持つか、届かせられない理由を述べる。
        /// 理由を書かせるのは、届かせていない行が届かせられない行に紛れないようにするためである
        /// ——紛れると、値を足せば届く行が足されないまま残る。多重定義が同じ名前を共有する行だけは
        /// 名指しできないので、その名前の検査で見る。
        /// </summary>
        private static void RequireReachedOrExplained(
            ToolMapRow row,
            IDictionary<string, string> toolsByRow,
            ISet<string> arrived,
            ISet<string> reachedTools)
        {
            string dispatched;
            if (!toolsByRow.TryGetValue(row.SignatureKey, out dispatched))
            {
                return;
            }

            bool alone = toolsByRow.Count(t => string.Equals(
                t.Value, dispatched, StringComparison.Ordinal)) == 1;
            if (alone
                ? arrived.Contains(row.SignatureKey)
                : reachedTools.Contains(dispatched))
            {
                return;
            }

            throw new InvalidOperationException(
                "呼び先まで届く検査も、届かせられない理由も持たない行がある: " + row.SignatureKey
                    + "(根拠へ「" + E2eCaseBuilder.UnreachableReason + "」を含む一文を書く)");
        }

        /// <summary>
        /// その結末が、呼び先まで届いたことを示すか。呼び先が在ることしか見ない検査は、引数の
        /// 不足で断られた応答も通すので届いていない。入口で断られることを見る検査も、断り方を
        /// 確かめてはいるが、その行の振る舞いには一度も入っていないので届いていない。
        /// </summary>
        private static bool Reached(E2eExpectation expectation)
        {
            return expectation != E2eExpectation.Dispatched
                && expectation != E2eExpectation.Refusal;
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
            ISet<string> arrivedTools,
            ISet<string> arrived,
            ISet<string> checkedRows)
        {
            string dispatched;
            if (toolsByRow.TryGetValue(row.SignatureKey, out dispatched))
            {
                // 効果を宣言する行は、その宣言を確かめた検査だけで数える。
                ISet<string> covering = Declared(row) ? checkedRows : arrived;

                // 自分の名前のツールを持つ行は、その行を名指しした検査で数える。多重定義が同じ
                // 名前を共有する行だけは名指しできないので、その名前を持つどの行かが数えられて
                // いればよい——どちらの呼び分けを通ったかは名前から言えないが、同じツールを
                // 通っている。
                string[] sharing = toolsByRow
                    .Where(t => string.Equals(t.Value, dispatched, StringComparison.Ordinal))
                    .Select(t => t.Key)
                    .ToArray();

                return sharing.Length == 1
                    ? covering.Contains(row.SignatureKey)
                    : sharing.Any(covering.Contains);
            }

            return Covering(row, signatures, toolsByRow, composedTools, byType, byOwner, traversed)
                .Any(arrivedTools.Contains);
        }

        /// <summary>その行が、呼び出しの記録だけでは済まない効果を宣言しているか。</summary>
        private static bool Declared(ToolMapRow row)
        {
            return row.Postcondition != null
                && row.Postcondition.Any(p => p.Kind != EffectCheckKind.CallLogOnly);
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
