using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PmxEditorMcp.SignatureDump
{
    public static class SchemaCompactor
    {
        private const string DefsName = "$defs";

        private const string RefName = "$ref";

        private const string RefPrefix = "#/$defs/";

        private static readonly string[] MapKeys = { "properties", DefsName };

        private static readonly string[] OneKeys = { "items", "not", "if", "then", "else" };

        private static readonly string[] ListKeys = { "anyOf", "allOf", "oneOf" };

        private static readonly JsonSerializerOptions Writing = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>
        /// 縮めたスキーマの綴り。<see cref="Inline"/> で戻すと <see cref="Shortened"/> と同じJSONになる。
        /// 根と根の properties は置き換えない。縮めても短くならなければ元の綴りをそのまま返す。$defs か $ref を既に持つ
        /// スキーマは扱わず、<see cref="InvalidOperationException"/>。
        /// </summary>
        public static string Compact(string schema)
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            JsonObject root = JsonNode.Parse(schema) as JsonObject;
            if (root == null)
            {
                return schema;
            }

            if (root.ContainsKey(DefsName) || schema.Contains("\"" + RefName + "\""))
            {
                throw new InvalidOperationException("$defs か $ref を既に持つスキーマは縮めない。");
            }

            Shorten(root);
            string shortened = Text(root);
            JsonObject defs = new JsonObject();
            int named = 0;
            while (true)
            {
                List<JsonObject> found = new List<JsonObject>();
                Subschemas(root, true, found);
                foreach (KeyValuePair<string, JsonNode> def in defs)
                {
                    Subschemas((JsonObject)def.Value, true, found);
                }

                string name = Name(named);
                int referenceChars = Text(Reference(name)).Length;
                int defsChars = defs.Count == 0 ? Text(DefsName).Length + 1 + 2 + 1 : 1;
                string best = null;
                int bestGain = 0;
                foreach (IGrouping<string, string> same in found
                    .Where(f => !IsReference(f))
                    .Select(Text)
                    .GroupBy(t => t, StringComparer.Ordinal))
                {
                    int count = same.Count();
                    int chars = same.Key.Length;
                    int gain = (count * chars) - ((count * referenceChars) + Text(name).Length + 1 + chars + defsChars);
                    if (count >= 2
                        && (gain > bestGain
                            || (gain == bestGain && best != null && string.CompareOrdinal(same.Key, best) < 0)))
                    {
                        best = same.Key;
                        bestGain = gain;
                    }
                }

                if (best == null)
                {
                    break;
                }

                Replace(root, best, name);
                foreach (string key in defs.Select(d => d.Key).ToList())
                {
                    Replace((JsonObject)defs[key], best, name);
                }

                defs[name] = JsonNode.Parse(best);
                named++;
            }

            if (defs.Count == 0)
            {
                return shortened.Length < schema.Length ? shortened : schema;
            }

            root[DefsName] = defs;
            string compacted = Text(root);
            if (!string.Equals(Inline(compacted), shortened, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("縮めたスキーマを戻すと元の形にならない。");
            }

            return compacted.Length < schema.Length ? compacted : schema;
        }

        /// <summary>
        /// 同じ値を通し同じ値を断る、短い同義の綴りへ置き換えた綴り。置き換えるのは次の3つだけである。
        /// 根が type object のとき、根の anyOf・allOf・oneOf の要素が重ねて持つ type object を外す。
        /// const が真偽値か文字のとき、それと同じ型を言うだけの type を外す。uniqueItems の並びの要素が
        /// enum だけで値を選ぶとき、enum の値の数以上の maxItems を外す。
        /// </summary>
        public static string Shortened(string schema)
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            JsonObject root = JsonNode.Parse(schema) as JsonObject;
            if (root == null)
            {
                return schema;
            }

            Shorten(root);

            return Text(root);
        }

        private static void Shorten(JsonObject root)
        {
            if (root["type"] is JsonValue rootType
                && rootType.TryGetValue(out string rootKind)
                && rootKind == "object")
            {
                foreach (string key in ListKeys)
                {
                    foreach (JsonObject branch in (root[key] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                    {
                        if (branch["type"] is JsonValue branchType
                            && branchType.TryGetValue(out string branchKind)
                            && branchKind == "object")
                        {
                            branch.Remove("type");
                        }
                    }
                }
            }

            List<JsonObject> found = new List<JsonObject>();
            Subschemas(root, false, found);
            foreach (JsonObject schema in found)
            {
                DropConstType(schema);
            }

            foreach (JsonObject schema in found)
            {
                DropImpliedMaxItems(schema);
            }
        }

        private static void DropConstType(JsonObject schema)
        {
            if (!(schema["type"] is JsonValue type) || !type.TryGetValue(out string kind) || !schema.ContainsKey("const"))
            {
                return;
            }

            JsonValueKind held = schema["const"] == null ? JsonValueKind.Null : schema["const"].GetValueKind();
            bool same = (kind == "boolean" && (held == JsonValueKind.True || held == JsonValueKind.False))
                || (kind == "string" && held == JsonValueKind.String);
            if (same)
            {
                schema.Remove("type");
            }
        }

        private static void DropImpliedMaxItems(JsonObject schema)
        {
            if (!(schema["uniqueItems"] is JsonValue unique)
                || !unique.TryGetValue(out bool distinct)
                || !distinct
                || !(schema["maxItems"] is JsonValue most)
                || !most.TryGetValue(out int longest)
                || !(schema["items"] is JsonObject items)
                || !(items["enum"] is JsonArray choices)
                || items.Any(m => m.Key != "enum" && m.Key != "type")
                || schema.ContainsKey("prefixItems"))
            {
                return;
            }

            int values = choices.Select(c => c == null ? "null" : Text(c)).Distinct(StringComparer.Ordinal).Count();
            if (values <= longest)
            {
                schema.Remove("maxItems");
            }
        }

        /// <summary>$ref を根の $defs の中身へ戻し、$defs を外した綴り。</summary>
        public static string Inline(string schema)
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            JsonObject root = JsonNode.Parse(schema) as JsonObject;
            if (root == null || !(root[DefsName] is JsonObject defs))
            {
                return schema;
            }

            root.Remove(DefsName);

            return Text(Expanded(root, defs));
        }

        private static JsonNode Expanded(JsonNode node, JsonObject defs)
        {
            if (node is JsonObject held)
            {
                if (held.Count == 1 && held[RefName] is JsonValue pointer)
                {
                    string target = pointer.GetValue<string>();
                    if (!target.StartsWith(RefPrefix, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("根の $defs を指さない $ref: " + target);
                    }

                    return Expanded(defs[target.Substring(RefPrefix.Length)], defs);
                }

                JsonObject copied = new JsonObject();
                foreach (KeyValuePair<string, JsonNode> member in held)
                {
                    copied[member.Key] = Expanded(member.Value, defs);
                }

                return copied;
            }

            if (node is JsonArray listed)
            {
                return new JsonArray(listed.Select(item => Expanded(item, defs)).ToArray());
            }

            return node == null ? null : node.DeepClone();
        }

        private static void Subschemas(JsonObject schema, bool root, IList<JsonObject> found)
        {
            if (!root)
            {
                found.Add(schema);
            }

            foreach (KeyValuePair<string, JsonNode> member in schema)
            {
                if (MapKeys.Contains(member.Key) && member.Value is JsonObject map && !(root && member.Key == DefsName))
                {
                    foreach (KeyValuePair<string, JsonNode> entry in map)
                    {
                        if (entry.Value is JsonObject one)
                        {
                            Subschemas(one, false, found);
                        }
                    }
                }
                else if (OneKeys.Contains(member.Key) && member.Value is JsonObject single)
                {
                    Subschemas(single, false, found);
                }
                else if (ListKeys.Contains(member.Key) && member.Value is JsonArray list)
                {
                    foreach (JsonObject item in list.OfType<JsonObject>())
                    {
                        Subschemas(item, false, found);
                    }
                }
            }
        }

        private static void Replace(JsonObject schema, string target, string name)
        {
            foreach (string key in schema.Select(m => m.Key).ToList())
            {
                JsonNode value = schema[key];
                if (MapKeys.Contains(key) && value is JsonObject map)
                {
                    foreach (string entry in map.Select(m => m.Key).ToList())
                    {
                        if (map[entry] is JsonObject one)
                        {
                            if (Matches(one, target))
                            {
                                map[entry] = Reference(name);
                            }
                            else
                            {
                                Replace(one, target, name);
                            }
                        }
                    }
                }
                else if (OneKeys.Contains(key) && value is JsonObject single)
                {
                    if (Matches(single, target))
                    {
                        schema[key] = Reference(name);
                    }
                    else
                    {
                        Replace(single, target, name);
                    }
                }
                else if (ListKeys.Contains(key) && value is JsonArray list)
                {
                    for (int at = 0; at < list.Count; at++)
                    {
                        if (list[at] is JsonObject item)
                        {
                            if (Matches(item, target))
                            {
                                list[at] = Reference(name);
                            }
                            else
                            {
                                Replace(item, target, name);
                            }
                        }
                    }
                }
            }
        }

        private static bool Matches(JsonObject schema, string target)
        {
            return string.Equals(Text(schema), target, StringComparison.Ordinal);
        }

        private static JsonObject Reference(string name)
        {
            return new JsonObject { [RefName] = RefPrefix + name };
        }

        private static bool IsReference(JsonObject schema)
        {
            return schema.Count == 1 && schema.ContainsKey(RefName);
        }

        private static string Name(int index)
        {
            const string letters = "abcdefghijklmnopqrstuvwxyz";
            string name = string.Empty;
            int left = index;
            do
            {
                name = letters[left % letters.Length] + name;
                left = (left / letters.Length) - 1;
            }
            while (left >= 0);

            return name;
        }

        private static string Text(JsonNode node)
        {
            return node.ToJsonString(Writing);
        }

        private static string Text(string text)
        {
            return JsonSerializer.Serialize(text, Writing);
        }
    }
}
