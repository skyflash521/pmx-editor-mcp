using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// スキーマ正本が載せるツールのすべてが、実機へ投げる検査のうち呼び先まで届くもののどれかに
    /// 覆われることを確かめる。覆われないものは、覆えないツールの正本へ理由ごと載っていなければ
    /// ならない——載っていないものも、載っているのに覆われるようになったものも、正本が実際と
    /// 食い違っているので落とす。覆いに数える検査は、生成器が組むE2E事例と、受入シナリオのうち
    /// 成功を期待するツールの段の2つである。呼ぶ行が呼び出しの記録以外の効果を宣言するツールは、
    /// 宣言するどの行についても、その効果を確かめる判定を持つ事例があるときだけ覆われたと数える
    /// ——同じ名前を共有する行は多重定義の呼び分けで、どれを通ったかは名前からは言えない。
    /// </summary>
    public static class ToolCoverageGate
    {
        /// <summary>覆われないツールがあれば <see cref="InvalidOperationException"/>。</summary>
        public static void Require(
            ToolSchemaTable schemas,
            ToolMap map,
            IDictionary<string, string> toolsByRow,
            IList<E2eCase> cases,
            ISet<string> succeeding,
            UncoveredToolTable uncoveredTools)
        {
            if (schemas == null)
            {
                throw new ArgumentNullException(nameof(schemas));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (toolsByRow == null)
            {
                throw new ArgumentNullException(nameof(toolsByRow));
            }

            if (cases == null)
            {
                throw new ArgumentNullException(nameof(cases));
            }

            if (succeeding == null)
            {
                throw new ArgumentNullException(nameof(succeeding));
            }

            if (uncoveredTools == null)
            {
                throw new ArgumentNullException(nameof(uncoveredTools));
            }

            HashSet<string> arrived = new HashSet<string>(
                cases.Where(c => Reached(c.Expectation)).Select(c => c.Tool),
                StringComparer.Ordinal);
            HashSet<string> examined = new HashSet<string>(
                cases
                    .Where(c => c.Checks != null
                        && !string.IsNullOrEmpty(c.RowKey)
                        && Succeeds(c.Expectation))
                    .Select(c => Checked(c.RowKey, c.Checks)),
                StringComparer.Ordinal);
            IDictionary<string, IList<ISet<string>>> declaring = DeclaringRows(map, toolsByRow);

            Dictionary<string, UncoveredReason> found =
                new Dictionary<string, UncoveredReason>(StringComparer.Ordinal);
            foreach (string tool in schemas.Tools.Select(t => t.Tool)
                .Distinct(StringComparer.Ordinal))
            {
                if (!arrived.Contains(tool) && !succeeding.Contains(tool))
                {
                    found.Add(tool, UncoveredReason.NoCase);
                }
                else if (!Examined(tool, examined, declaring))
                {
                    found.Add(tool, UncoveredReason.NoEffectCheck);
                }
            }

            Dictionary<string, UncoveredReason> listed = uncoveredTools.Tools.ToDictionary(
                t => t.Tool, t => t.Reason, StringComparer.Ordinal);
            Require(
                "実機の検査に覆われないのに、覆えないツールの正本に無いものがある",
                found.Keys.Where(t => !listed.ContainsKey(t)));
            Require(
                "覆えないツールの正本に載っているのに、実機の検査に覆われているものがある",
                listed.Keys.Where(t => !found.ContainsKey(t)));
            Require(
                "覆えない理由が正本と違うものがある",
                found.Where(f => listed.ContainsKey(f.Key) && listed[f.Key] != f.Value)
                    .Select(f => f.Key + "(正本: " + listed[f.Key] + " / 導いた: " + f.Value + ")"));
        }

        private static void Require(string what, IEnumerable<string> tools)
        {
            string[] found = tools.OrderBy(t => t, StringComparer.Ordinal).ToArray();
            if (found.Length == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                what + "(" + found.Length + "件): " + string.Join("・", found.Take(20))
                    + (found.Length > 20 ? "・ほか" : string.Empty));
        }

        /// <summary>
        /// そのツールが呼ぶ行のすべてに、その行が宣言した効果のどれかを確かめる事例が在るか。
        /// </summary>
        private static bool Examined(
            string tool, ISet<string> examined, IDictionary<string, IList<ISet<string>>> declaring)
        {
            IList<ISet<string>> rows;

            return !declaring.TryGetValue(tool, out rows)
                || rows.All(effects => effects.Any(examined.Contains));
        }

        /// <summary>
        /// ツールの名前から、そのツールが呼ぶ行のうち効果を宣言するもの1つずつについて、その行と
        /// 宣言した効果の組の並びへ。
        /// </summary>
        private static IDictionary<string, IList<ISet<string>>> DeclaringRows(
            ToolMap map, IDictionary<string, string> toolsByRow)
        {
            Dictionary<string, IList<ISet<string>>> declaring =
                new Dictionary<string, IList<ISet<string>>>(StringComparer.Ordinal);
            foreach (ToolMapRow row in map.Rows)
            {
                string tool;
                ISet<string> effects = Declared(row);
                if (effects.Count == 0 || !toolsByRow.TryGetValue(row.SignatureKey, out tool))
                {
                    continue;
                }

                IList<ISet<string>> rows;
                if (!declaring.TryGetValue(tool, out rows))
                {
                    rows = new List<ISet<string>>();
                    declaring.Add(tool, rows);
                }

                rows.Add(effects);
            }

            return declaring;
        }

        private static ISet<string> Declared(ToolMapRow row)
        {
            return new HashSet<string>(
                (row.Postcondition ?? new Postcondition[0])
                    .Where(p => p.Kind != EffectCheckKind.CallLogOnly)
                    .Select(p => Checked(row.SignatureKey, p.EffectId)),
                StringComparer.Ordinal);
        }

        private static string Checked(string rowKey, string effectId)
        {
            return rowKey + "\n" + effectId;
        }

        private static bool Succeeds(E2eExpectation expectation)
        {
            return expectation != E2eExpectation.Refusal && expectation != E2eExpectation.Denied;
        }

        /// <summary>
        /// その結末が、呼び先まで届いたことを示すか。入口で断られることを見る検査は、断り方を
        /// 確かめてはいるが、そのツールの振る舞いには一度も入っていないので届いていない。
        /// </summary>
        private static bool Reached(E2eExpectation expectation)
        {
            return expectation != E2eExpectation.Refusal;
        }
    }
}
