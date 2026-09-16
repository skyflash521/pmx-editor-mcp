using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// スキーマ正本が載せるツールのすべてが、実機へ投げる検査のうち呼び先まで届くもののどれかに
    /// 覆われることを確かめる。覆いに数える検査は、生成器が組むE2E事例と、受入シナリオのうち
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
            ISet<string> succeeding)
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

            HashSet<string> arrived = new HashSet<string>(
                cases.Where(c => Reached(c.Expectation)).Select(c => c.Tool),
                StringComparer.Ordinal);
            HashSet<string> examined = new HashSet<string>(
                cases
                    .Where(c => c.Checks != null && !string.IsNullOrEmpty(c.RowKey))
                    .Select(c => c.RowKey),
                StringComparer.Ordinal);
            IDictionary<string, ISet<string>> declaring = DeclaringRows(map, toolsByRow);

            string[] uncovered = schemas.Tools
                .Select(t => t.Tool)
                .Where(t => !Covered(t, arrived, succeeding, examined, declaring))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToArray();
            if (uncovered.Length == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "実機の検査に覆われないツールがある(" + uncovered.Length + "件): "
                    + string.Join("・", uncovered.Take(20))
                    + (uncovered.Length > 20 ? "・ほか" : string.Empty));
        }

        /// <summary>そのツールが、呼び先まで届く検査と、宣言した効果の判定を持つか。</summary>
        private static bool Covered(
            string tool,
            ISet<string> arrived,
            ISet<string> succeeding,
            ISet<string> examined,
            IDictionary<string, ISet<string>> declaring)
        {
            if (!arrived.Contains(tool) && !succeeding.Contains(tool))
            {
                return false;
            }

            ISet<string> declared;

            return !declaring.TryGetValue(tool, out declared) || declared.All(examined.Contains);
        }

        /// <summary>ツールの名前から、そのツールが呼ぶ行のうち効果を宣言するものの行キーへ。</summary>
        private static IDictionary<string, ISet<string>> DeclaringRows(
            ToolMap map, IDictionary<string, string> toolsByRow)
        {
            Dictionary<string, ISet<string>> declaring =
                new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
            foreach (ToolMapRow row in map.Rows.Where(Declares))
            {
                string tool;
                if (!toolsByRow.TryGetValue(row.SignatureKey, out tool))
                {
                    continue;
                }

                ISet<string> keys;
                if (!declaring.TryGetValue(tool, out keys))
                {
                    keys = new HashSet<string>(StringComparer.Ordinal);
                    declaring.Add(tool, keys);
                }

                keys.Add(row.SignatureKey);
            }

            return declaring;
        }

        /// <summary>その行が、呼び出しの記録だけでは済まない効果を宣言しているか。</summary>
        private static bool Declares(ToolMapRow row)
        {
            return row.Postcondition != null
                && row.Postcondition.Any(p => p.Kind != EffectCheckKind.CallLogOnly);
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
