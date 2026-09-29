using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 戻り値の綴りが object の行について、エディタがその呼び出しで実際に返すものの型を記した表を
    /// JSONから読み取る。
    /// </summary>
    public static class CloneReturnJsonReader
    {
        private const string RowsName = "rows";

        private const string KeyName = "signatureKey";

        private const string ReturnsName = "returns";

        private const string BasisName = "basis";

        private const string FileName = "clone-returns.json";

        /// <summary>
        /// 表のパス。表は、<paramref name="ledgerPath"/> の能力台帳と同じ観測データの置き場に在る。
        /// </summary>
        public static string Beside(string ledgerPath)
        {
            if (ledgerPath == null)
            {
                throw new ArgumentNullException(nameof(ledgerPath));
            }

            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ledgerPath)), FileName);
        }

        /// <summary>行キーから、返すものの型へ。形が違えば <see cref="FormatException"/>。</summary>
        public static IDictionary<string, string> Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            JsonNode root;
            try
            {
                root = JsonNode.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new FormatException("エディタが返す型の表がJSONとして読めない: " + exception.Message, exception);
            }

            JsonArray rows = root is JsonObject members ? members[RowsName] as JsonArray : null;
            if (rows == null)
            {
                throw new FormatException("エディタが返す型の表は " + RowsName + " の並びを持つ。");
            }

            Dictionary<string, string> returns = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonNode row in rows)
            {
                string key = Text(row, KeyName);
                if (returns.ContainsKey(key))
                {
                    throw new FormatException("エディタが返す型の表に同じ行が二度在る: " + key);
                }

                Text(row, BasisName);
                returns.Add(key, Text(row, ReturnsName));
            }

            return returns;
        }

        private static string Text(JsonNode row, string name)
        {
            JsonValue value = row is JsonObject members ? members[name] as JsonValue : null;
            string text;
            if (value == null || !value.TryGetValue(out text) || text.Length == 0)
            {
                throw new FormatException("エディタが返す型の表の行は " + name + " の文字列を持つ。");
            }

            return text;
        }
    }
}
