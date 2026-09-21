using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using PmxEditorMcp.SignatureDump;

namespace PmxEditorMcp.Bridge
{
    /// <summary>語からツールを引く。名前と説明文の両方へ当てる。</summary>
    public static class ToolSearch
    {
        /// <summary>返す件数の既定。</summary>
        public const int DefaultLimit = 50;

        /// <summary>返す件数の下限。</summary>
        public const int MinimumLimit = 1;

        /// <summary>返す件数の上限。</summary>
        public const int MaximumLimit = 500;

        private const string TotalName = "total";

        private const string ToolsName = "tools";

        private const string NextOffsetName = "nextOffset";

        private const string OkName = "ok";

        private const string ValueName = "value";

        private const string ErrorName = "error";

        private const string CodeName = "code";

        private const string MessageName = "message";

        private const string InvalidArgument = "TOOL_INVALID_ARGUMENT";

        /// <summary>包みの外側と、名前を隔てる引用符と読点の分。</summary>
        private const int WrapperChars = 64;

        /// <summary>探す語を当てるツール1件。</summary>
        public sealed class Entry
        {
            public Entry(string name, string description)
            {
                if (name == null)
                {
                    throw new ArgumentNullException(nameof(name));
                }

                Name = name;
                Description = description;
            }

            public string Name { get; }

            public string Description { get; }
        }

        /// <summary>
        /// <paramref name="text"/> を名前と説明文へ当てたツールの名前を、名前の昇順で返す。
        /// </summary>
        public static IList<string> Found(string text, IEnumerable<Entry> entries)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            List<string> found = new List<string>();
            foreach (Entry entry in entries)
            {
                if (TextMatch.Contains(entry.Name, text)
                    || TextMatch.Contains(entry.Description, text))
                {
                    found.Add(entry.Name);
                }
            }

            found.Sort(StringComparer.Ordinal);

            return found;
        }

        /// <summary>
        /// 語を当てた結果を、ホストが返すのと同じ形の包みにする。渡された値が範囲の外にあれば
        /// 断りの包みを返す。<paramref name="budgetChars"/> は応答の枠で、名前は件数と枠の
        /// どちらか先に尽きるところまで並べ、残りがあれば続きの位置を添える。
        /// </summary>
        public static JsonObject Answer(
            string text,
            int? limit,
            int? offset,
            IEnumerable<Entry> entries,
            int budgetChars)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            if (string.IsNullOrEmpty(text))
            {
                return Refusal(
                    FixedToolTable.FindToolTextParameter + " は1文字以上の文字列でなければならない。");
            }

            int taking = limit ?? DefaultLimit;
            if (taking < MinimumLimit || taking > MaximumLimit)
            {
                return Refusal("limit は " + MinimumLimit + " 以上 " + MaximumLimit
                    + " 以下の整数でなければならない: " + Written(taking));
            }

            int from = offset ?? 0;
            if (from < 0)
            {
                return Refusal("offset は0以上の整数でなければならない: " + Written(from));
            }

            IList<string> found = Found(text, entries);
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

            return new JsonObject { [OkName] = JsonValue.Create(true), [ValueName] = value };
        }

        private static JsonObject Refusal(string message)
        {
            return new JsonObject
            {
                [OkName] = JsonValue.Create(false),
                [ErrorName] = new JsonObject
                {
                    [CodeName] = JsonValue.Create(InvalidArgument),
                    [MessageName] = JsonValue.Create(message),
                },
            };
        }

        private static string Written(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
