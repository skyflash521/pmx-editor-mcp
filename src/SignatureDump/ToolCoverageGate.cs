using System;
using System.Collections.Generic;

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
            throw new NotImplementedException();
        }
    }
}
