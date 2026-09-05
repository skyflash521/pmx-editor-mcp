using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 一覧が返す件数の既定と最大を、要素1件の想定文字数から逆算する。予算を変えれば値も変わるので、
    /// 正本へ書いた値はこの規則で導いたものと突き合わせる。
    /// </summary>
    public static class ListingLimitRule
    {
        /// <summary>切り出した並びを載せる項目の名前。</summary>
        public const string ItemsName = "items";

        /// <summary>`value` の内側の総数と続きの位置と配列の構文に充てる分。</summary>
        private const int ListingOverhead = 1000;

        /// <summary>逆算できなければ <see cref="InvalidOperationException"/>。</summary>
        public static ListingLimits Derive(
            ToolSchema schema, AssumedLength lengths, int valueChars)
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            if (lengths == null)
            {
                throw new ArgumentNullException(nameof(lengths));
            }

            SchemaItem element = Element(schema);
            int room = valueChars - ListingOverhead;
            if (room < 1)
            {
                throw new InvalidOperationException(
                    "値の枠が一覧応答の枠に足りない: " + schema.Tool);
            }

            int always = element.Members.Where(m => m.Origin == ItemOrigin.HostOutput)
                .Sum(m => lengths.Of(m) + AssumedLength.MemberOverhead);
            List<int> chosen = element.Members.Where(m => m.Origin != ItemOrigin.HostOutput)
                .Select(m => lengths.Of(m) + AssumedLength.MemberOverhead).ToList();
            if (chosen.Count == 0)
            {
                throw new InvalidOperationException(
                    "返す項目で選べる項目が無い: " + schema.Tool);
            }

            return new ListingLimits(
                AtLeastOne(Count(room, always + chosen.Sum()) / 2),
                Count(room, always + chosen.Min()));
        }

        private static int Count(int room, int each)
        {
            return AtLeastOne(room / each);
        }

        private static int AtLeastOne(int count)
        {
            return count < 1 ? 1 : count;
        }

        /// <summary>切り出した並びの要素の項目。一覧の形をしていなければ例外。</summary>
        private static SchemaItem Element(ToolSchema schema)
        {
            SchemaItem items = (schema.Output.Members ?? new SchemaItem[0]).FirstOrDefault(
                m => string.Equals(m.Name, ItemsName, StringComparison.Ordinal));
            if (items == null || items.Element == null || items.Element.Members == null)
            {
                throw new InvalidOperationException(
                    "一覧の応答が切り出した並びを持たない: " + schema.Tool);
            }

            return items.Element;
        }
    }
}
