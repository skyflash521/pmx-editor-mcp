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
        /// <summary>警告の枠。値の枠は予算からこれを引いたものになる。</summary>
        private const int WarningRoom = 2000;

        /// <summary>食い違いがあれば <see cref="InvalidOperationException"/>。</summary>
        public static void Require(
            ToolSchemaTable schemas,
            ToolMap map,
            ISet<string> spellings,
            IDictionary<string, int> lengths,
            int budgetChars)
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

            RequireSameSpellings(spellings, lengths);
            RequireSameTools(schemas, map);
            RequireOnePollingTool(schemas);
            RequireSamePayloads(schemas, map);
            AssumedLength assumed = new AssumedLength(lengths);
            foreach (ToolSchema schema in schemas.Tools)
            {
                RequireShapes(schema, spellings);
                RequireListing(schema, assumed, budgetChars - WarningRoom);
            }
        }

        /// <summary>
        /// 表が持つツールが、能力対応表がツールを持つ行のツールと一致することを求める。片方にしか
        /// 無いツールは、入出力の形か割り当て先のどちらかを失う。
        /// </summary>
        private static void RequireSameTools(ToolSchemaTable schemas, ToolMap map)
        {
            HashSet<string> assigned = new HashSet<string>(
                map.Rows.Where(r => r.Tool != null).Select(r => r.Tool), StringComparer.Ordinal);
            HashSet<string> described = new HashSet<string>(
                schemas.Tools.Select(t => t.Tool), StringComparer.Ordinal);

            string missing = assigned.Except(described, StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException(
                    "能力対応表が割り当てたツールの入出力の形が無い: " + missing);
            }

            string extra = described.Except(assigned, StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (extra != null)
            {
                throw new InvalidOperationException(
                    "能力対応表がどの行にも割り当てていないツールの形がある: " + extra);
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
        /// 件数は予算から決まるので、書かれた値が想定文字数から逆算した値と合うことまで求める。
        /// </summary>
        private static void RequireListing(
            ToolSchema schema, AssumedLength lengths, int valueChars)
        {
            if (schema.Listing == null)
            {
                return;
            }

            ListingLimits derived = ListingLimitRule.Derive(schema, lengths, valueChars);
            if (derived.LimitDefault != schema.Listing.LimitDefault
                || derived.LimitMaximum != schema.Listing.LimitMaximum)
            {
                throw new InvalidOperationException(
                    "件数が想定文字数から逆算した値と合わない: " + schema.Tool
                        + "(表: " + Written(schema.Listing) + " / 逆算: " + Written(derived) + ")");
            }
        }

        private static string Written(ListingLimits limits)
        {
            return "既定 " + limits.LimitDefault + "・最大 " + limits.LimitMaximum;
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
