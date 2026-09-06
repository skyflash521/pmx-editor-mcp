using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// スキーマ正本を能力対応表と仕様書の表と照合し、あわせて仕様書の2つの表どうしが対応することを
    /// 確かめる。
    /// </summary>
    public static class ToolSchemaGate
    {
        /// <summary>一覧が何件返すかを受け取る入力の名前。</summary>
        private const string LimitName = "limit";

        /// <summary>一覧が切り出す前の総数を返す項目の名前。</summary>
        private const string TotalName = "total";

        /// <summary>食い違いがあれば <see cref="InvalidOperationException"/>。</summary>
        public static void Require(
            ToolSchemaTable schemas,
            ToolMap map,
            ISet<string> spellings,
            IDictionary<string, int> lengths,
            IDictionary<string, ComposedTool> composedTools)
        {
            if (schemas == null)
            {
                throw new ArgumentNullException(nameof(schemas));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (spellings == null)
            {
                throw new ArgumentNullException(nameof(spellings));
            }

            if (lengths == null)
            {
                throw new ArgumentNullException(nameof(lengths));
            }

            if (composedTools == null)
            {
                throw new ArgumentNullException(nameof(composedTools));
            }

            RequireSameSpellings(spellings, lengths);
            RequireSameBranching(schemas, composedTools);
            RequireOnePollingTool(schemas);
            RequireSamePayloads(schemas, map);
            foreach (ToolSchema schema in schemas.Tools)
            {
                RequireShapes(schema, spellings);
                RequireDerivedListingLimits(schema);
            }
        }

        /// <summary>
        /// 在る合成ツールの形について、イベントの分岐の有無が仕様書の分岐の欄と合うことを求める。
        /// 欄は形を求めるかどうかを決めるので、形と照らさないと書き換えだけで検査を外せる。
        /// </summary>
        private static void RequireSameBranching(
            ToolSchemaTable schemas, IDictionary<string, ComposedTool> composedTools)
        {
            foreach (ToolSchema schema in schemas.Tools.OrderBy(t => t.Tool, StringComparer.Ordinal))
            {
                ComposedTool composed;
                if (!composedTools.TryGetValue(schema.Tool, out composed)
                    || composed.Branching == (schema.Payloads != null))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    "分岐の欄が入出力の形と合わない: " + schema.Tool);
            }
        }

        /// <summary>イベントの分岐を持つツールが、表に1つだけであることを求める。</summary>
        private static void RequireOnePollingTool(ToolSchemaTable schemas)
        {
            string second = schemas.Tools.Where(t => t.Payloads != null)
                .Select(t => t.Tool).OrderBy(t => t, StringComparer.Ordinal)
                .Skip(1).FirstOrDefault();
            if (second != null)
            {
                throw new InvalidOperationException(
                    "イベントの分岐を持つツールが2つ以上ある: " + second);
            }
        }

        /// <summary>
        /// イベントの取り出しが持つ分岐が、能力対応表のイベント行と一致することを求める。
        /// </summary>
        private static void RequireSamePayloads(ToolSchemaTable schemas, ToolMap map)
        {
            HashSet<string> branches = new HashSet<string>(
                map.Rows.Where(r => r.EventType != null).Select(r => r.EventType),
                StringComparer.Ordinal);
            HashSet<string> described = new HashSet<string>(
                schemas.Tools.Where(t => t.Payloads != null)
                    .SelectMany(t => t.Payloads).Select(p => p.Type),
                StringComparer.Ordinal);

            string missing = branches.Except(described, StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException("イベント行の分岐の形が無い: " + missing);
            }

            string extra = described.Except(branches, StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (extra != null)
            {
                throw new InvalidOperationException(
                    "能力対応表にイベント行の無い分岐がある: " + extra);
            }
        }

        /// <summary>
        /// 想定文字数の表が、綴りの表と過不足なく対応することを求める。綴りを増やして想定文字数を
        /// 足し忘れると、その綴りを使う一覧が現れるまで表に出ない。
        /// </summary>
        private static void RequireSameSpellings(
            ISet<string> spellings, IDictionary<string, int> lengths)
        {
            string missing = spellings.Except(lengths.Keys, StringComparer.Ordinal)
                .OrderBy(s => s, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException("想定文字数を持たない綴りがある: " + missing);
            }

            string extra = lengths.Keys.Except(spellings, StringComparer.Ordinal)
                .OrderBy(s => s, StringComparer.Ordinal).FirstOrDefault();
            if (extra != null)
            {
                throw new InvalidOperationException("綴りの表に無い想定文字数がある: " + extra);
            }
        }

        /// <summary>
        /// 一覧を返すツールの `limit` が、既定と上限を書いていないことを求める。どちらも一覧の
        /// 件数の規則が導く値なので、書けば導き直しを忘れたときにずれが残る。
        /// </summary>
        private static void RequireDerivedListingLimits(ToolSchema schema)
        {
            if (!IsListing(schema))
            {
                return;
            }

            foreach (SchemaBranch branch in schema.Branches)
            {
                SchemaItem limit = branch.Inputs.FirstOrDefault(
                    i => string.Equals(i.Name, LimitName, StringComparison.Ordinal));
                if (limit == null
                    || (!limit.HasDefault && (limit.Bounds == null || limit.Bounds.Maximum == null)))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    "一覧の件数は導く値なので既定と上限を持たない: " + schema.Tool);
            }
        }

        /// <summary>応答が総数と切り出した並びを返す形か。一覧を返すツールはこの形を取る。</summary>
        private static bool IsListing(ToolSchema schema)
        {
            IList<SchemaItem> members = schema.Output.Members;

            return members != null
                && members.Any(m => string.Equals(m.Name, TotalName, StringComparison.Ordinal))
                && members.Any(
                    m => string.Equals(m.Name, ListingLimitRule.ItemsName, StringComparison.Ordinal)
                        && m.Element != null);
        }

        /// <summary>綴りの閉じた集合は仕様書が持つので、そこに実在することまで求める。</summary>
        private static void RequireShapes(ToolSchema schema, ISet<string> spellings)
        {
            foreach (SchemaItem item in schema.AllItems.Where(i => i.Shape != null))
            {
                if (!spellings.Contains(item.Shape))
                {
                    throw new InvalidOperationException(
                        "表現の綴りが仕様書に無い: " + schema.Tool + "(" + item.Shape + ")");
                }
            }
        }
    }
}
