using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 1つのツールへ集めた行と呼び分けの対応。引数の名前が同じで型だけが違う行は名前で見分けられ
    /// ないので、分岐を選ぶ項目の値にその引数の綴りを採り、ここで結び付ける。
    /// </summary>
    public static class BranchRowRule
    {
        /// <summary>
        /// 行キーから呼び分けへ引く表。分岐を選ぶ項目を持たないツールと、行が1件のツールは1件も
        /// 持たない——後者が選ぶ項目で分かれるのは行ではなく、要素の具象の型である。結び付け
        /// られなければ <see cref="InvalidOperationException"/>。
        /// </summary>
        public static IDictionary<string, SchemaBranch> Resolve(
            ToolSchema schema,
            IList<SignatureRecord> rows,
            IDictionary<string, string> shapesByType)
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            if (shapesByType == null)
            {
                throw new ArgumentNullException(nameof(shapesByType));
            }

            Dictionary<string, SchemaBranch> byRow =
                new Dictionary<string, SchemaBranch>(StringComparer.Ordinal);
            if (rows.Count < 2 || !schema.Branches.Any(b => b.SelectorName != null))
            {
                return byRow;
            }

            if (schema.Branches.Any(b => b.SelectorName == null))
            {
                throw new InvalidOperationException(
                    "分岐を選ぶ項目を持たない呼び分けがある: " + schema.Tool);
            }

            string argument = Distinguishing(schema.Tool, rows, shapesByType);
            IDictionary<string, SchemaBranch> byValue = ByValue(schema);
            foreach (SignatureRecord row in rows)
            {
                string shape = SdkShapeEvidence.ShapeOf(
                    row.Parameters.First(
                        p => string.Equals(p.Name, argument, StringComparison.Ordinal)).TypeName,
                    shapesByType);
                SchemaBranch branch;
                if (shape == null || !byValue.TryGetValue(shape, out branch))
                {
                    throw new InvalidOperationException(
                        "行の綴りを選ぶ呼び分けが無い: " + schema.Tool + "(" + row.Key + ")");
                }

                byRow.Add(row.Key, branch);
            }

            if (byRow.Values.Distinct().Count() != schema.Branches.Count)
            {
                throw new InvalidOperationException(
                    "呼び分けと行が一対一で対応しない: " + schema.Tool);
            }

            return byRow;
        }

        /// <summary>選ぶ値から呼び分けへ。同じ値を選ぶ呼び分けが2つあれば例外。</summary>
        private static IDictionary<string, SchemaBranch> ByValue(ToolSchema schema)
        {
            Dictionary<string, SchemaBranch> byValue =
                new Dictionary<string, SchemaBranch>(StringComparer.Ordinal);
            foreach (SchemaBranch branch in schema.Branches)
            {
                string value = branch.SelectorValue as string;
                if (value == null)
                {
                    throw new InvalidOperationException(
                        "分岐を選ぶ値が綴りでない: " + schema.Tool + "(" + branch.Branch + ")");
                }

                if (byValue.ContainsKey(value))
                {
                    throw new InvalidOperationException(
                        "同じ値を選ぶ呼び分けが2つある: " + schema.Tool + "(" + value + ")");
                }

                byValue.Add(value, branch);
            }

            return byValue;
        }

        /// <summary>
        /// 行どうしを分ける引数の名前。どの行も同じ名前で持ち、綴りが行ごとに違う引数が1つだけ
        /// あることを求める——2つ以上あると、1つの値ではどの行かが決まらない。
        /// </summary>
        private static string Distinguishing(
            string tool, IList<SignatureRecord> rows, IDictionary<string, string> shapesByType)
        {
            string[] shared = rows[0].Parameters
                .Select(p => p.Name)
                .Where(n => rows.All(r => r.Parameters.Any(
                    p => string.Equals(p.Name, n, StringComparison.Ordinal))))
                .ToArray();
            string[] apart = shared
                .Where(n => rows
                    .Select(r => SdkShapeEvidence.ShapeOf(
                        r.Parameters.First(
                            p => string.Equals(p.Name, n, StringComparison.Ordinal)).TypeName,
                        shapesByType))
                    .Distinct(StringComparer.Ordinal)
                    .Count() > 1)
                .ToArray();
            if (apart.Length != 1)
            {
                throw new InvalidOperationException(
                    "行を分ける引数が1つに決まらない: " + tool
                        + "(" + (apart.Length == 0 ? "無し" : string.Join("・", apart)) + ")");
            }

            return apart[0];
        }
    }
}
