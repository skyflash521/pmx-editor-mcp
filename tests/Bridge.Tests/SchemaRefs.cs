using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace PmxEditorMcp.Bridge.Tests
{
    /// <summary>
    /// 公開した入力スキーマの形を辿るテストのために、根の $defs を指す $ref をその中身へ置き換え、
    /// $defs を外した形を作る。値を通すか断るかを見るテストは、$ref のまま照合器へ渡す。
    /// </summary>
    internal static class SchemaRefs
    {
        private const string DefsName = "$defs";

        private const string RefName = "$ref";

        private const string RefPrefix = "#/$defs/";

        public static string Inlined(string schema)
        {
            JsonNode root = JsonNode.Parse(schema);

            return Inlined(root).ToJsonString();
        }

        public static JsonNode Inlined(JsonNode root)
        {
            if (!(root is JsonObject held) || !(held[DefsName] is JsonObject defs))
            {
                return root == null ? null : root.DeepClone();
            }

            JsonObject copied = (JsonObject)held.DeepClone();
            copied.Remove(DefsName);

            return Expanded(copied, defs);
        }

        private static JsonNode Expanded(JsonNode node, JsonObject defs)
        {
            if (node is JsonObject held)
            {
                if (held.Count == 1 && held[RefName] is JsonValue pointer)
                {
                    string target = pointer.GetValue<string>();
                    if (!target.StartsWith(RefPrefix, StringComparison.Ordinal)
                        || defs[target.Substring(RefPrefix.Length)] == null)
                    {
                        throw new InvalidOperationException("根の $defs に無いものを指す $ref: " + target);
                    }

                    return Expanded(defs[target.Substring(RefPrefix.Length)], defs);
                }

                JsonObject copied = new JsonObject();
                foreach (var member in held)
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
    }
}
