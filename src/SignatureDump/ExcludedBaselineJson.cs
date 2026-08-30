using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 凍結した除外の組をJSONへ書き出す。同じ入力からは常に同じバイト列になり、配列の要素は
    /// 1行ずつに分かれるので、行単位の差分で変化を追える。
    /// </summary>
    public static class ExcludedBaselineJson
    {
        /// <summary>末尾に改行を1つ置く。</summary>
        public static string Write(IList<ExcludedBaselineEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("{\n");
            builder.Append("\"capabilities\":");
            if (entries.Count == 0)
            {
                builder.Append("[]");
            }
            else
            {
                builder.Append("[\n");
                builder.Append(string.Join(",\n", entries.Select(WriteEntry)));
                builder.Append("\n]");
            }

            builder.Append("\n}\n");

            return builder.ToString();
        }

        private static string WriteEntry(ExcludedBaselineEntry entry)
        {
            return "{\"capabilityId\":" + Text(entry.CapabilityId) + ",\"signatures\":["
                + (entry.Signatures.Count == 0
                    ? string.Empty
                    : "\n" + string.Join(",\n", entry.Signatures.Select(Text)) + "\n")
                + "]}";
        }

        private static string Text(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
