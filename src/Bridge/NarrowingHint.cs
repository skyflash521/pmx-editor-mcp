using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PmxEditorMcp.Bridge
{
    public static class NarrowingHint
    {
        private const string PropertiesName = "properties";

        private const string Separator = "・";

        private const string FieldsName = "fields";

        private const string OffsetName = "offset";

        private const string LimitName = "limit";

        private const string IndicesName = "indices";

        private const string RangeName = "range";

        /// <summary>
        /// そのツールの入力の形から、応答を小さくする手立てを組み立てる。手立てが無ければ null。
        /// </summary>
        public static string Of(JsonElement schema)
        {
            HashSet<string> taken = Taken(schema);
            List<string> ways = new List<string>();
            if (taken.Contains(FieldsName))
            {
                ways.Add(FieldsName + " で項目を絞る");
            }

            if (taken.Contains(OffsetName) && taken.Contains(LimitName))
            {
                ways.Add(OffsetName + " と " + LimitName + " で分けて読む");
            }

            if (taken.Contains(IndicesName) || taken.Contains(RangeName))
            {
                ways.Add(
                    (taken.Contains(IndicesName) ? IndicesName : RangeName) + " で対象を絞る");
            }

            return ways.Count == 0 ? null : string.Join(Separator, ways);
        }

        private static HashSet<string> Taken(JsonElement schema)
        {
            HashSet<string> taken = new HashSet<string>(StringComparer.Ordinal);
            JsonElement properties;
            if (schema.ValueKind != JsonValueKind.Object
                || !schema.TryGetProperty(PropertiesName, out properties)
                || properties.ValueKind != JsonValueKind.Object)
            {
                return taken;
            }

            foreach (JsonProperty property in properties.EnumerateObject())
            {
                taken.Add(property.Name);
            }

            return taken;
        }
    }
}
