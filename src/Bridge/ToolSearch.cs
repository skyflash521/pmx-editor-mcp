using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using PmxEditorMcp.SignatureDump;

namespace PmxEditorMcp.Bridge
{
    public static class ToolSearch
    {
        public const int DefaultLimit = FixedToolTable.FindToolDefaultLimit;

        public const int MinimumLimit = FixedToolTable.FindToolMinimumLimit;

        public const int MaximumLimit = FixedToolTable.FindToolMaximumLimit;

        private const string TotalName = "total";

        private const string ToolsName = "tools";

        private const string NextOffsetName = "nextOffset";

        /// <summary>包みの外側と、名前を隔てる引用符と読点の分。</summary>
        private const int WrapperChars = 64;

        /// <summary>
        /// 語を当てた結果を、ホストが返すのと同じ形の包みにする。渡された値が範囲の外にあれば
        /// 断りの包みを返す。<paramref name="budgetChars"/> は応答の枠で、名前は件数と枠の
        /// どちらか先に尽きるところまで並べ、残りがあれば続きの位置を添える。
        /// </summary>
        public static JsonObject Answer(
            IList<string> texts,
            int? limit,
            int? offset,
            IEnumerable<ToolMatch.Entry> entries,
            int budgetChars)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            if (texts == null || texts.Count == 0 || texts.Any(string.IsNullOrEmpty))
            {
                return Refusal(
                    FixedToolTable.FindToolTextsParameter
                        + " は1文字以上の文字列を1つ以上並べた配列でなければならない。");
            }

            int taking = limit ?? DefaultLimit;
            if (taking < MinimumLimit)
            {
                return Refusal("limit は " + MinimumLimit + " 以上の整数でなければならない: " + Written(taking));
            }

            int from = offset ?? 0;
            if (from < 0)
            {
                return Refusal("offset は0以上の整数でなければならない: " + Written(from));
            }

            IList<string> found = ToolMatch.Found(texts, entries);
            if (from > found.Count)
            {
                return Refusal("offset が当たりの件数を超えている: " + Written(from)
                    + "(当たりは " + Written(found.Count) + " 件)");
            }

            return Envelope(found, from, taking, budgetChars);
        }

        private static JsonObject Envelope(
            IList<string> found, int offset, int limit, int budgetChars)
        {
            JsonArray tools = new JsonArray();
            int room = budgetChars - WrapperChars;
            int used = 0;
            int at = offset;
            for (; at < found.Count && tools.Count < limit; at++)
            {
                int size = found[at].Length + 4;
                if (used + size > room)
                {
                    break;
                }

                used += size;
                tools.Add(JsonValue.Create(found[at]));
            }

            JsonObject value = new JsonObject
            {
                [TotalName] = JsonValue.Create(found.Count),
                [ToolsName] = tools,
            };
            if (at < found.Count)
            {
                value[NextOffsetName] = JsonValue.Create(at);
            }

            return new JsonObject { [ToolEnvelope.OkName] = JsonValue.Create(true), [ToolEnvelope.ValueName] = value };
        }

        private static JsonObject Refusal(string message)
        {
            return new JsonObject
            {
                [ToolEnvelope.OkName] = JsonValue.Create(false),
                [ToolEnvelope.ErrorName] = new JsonObject
                {
                    [ToolEnvelope.CodeName] = JsonValue.Create(ToolEnvelope.InvalidArgument),
                    [ToolEnvelope.MessageName] = JsonValue.Create(message),
                },
            };
        }

        private static string Written(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
