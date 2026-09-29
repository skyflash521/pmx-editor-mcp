using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 合成ツールの文字の入力と文字の並びを、ホストがどう読むかを記した表をJSONから読み取る。文字の入力は
    /// 受け取る値の並びか最小の長さを、文字の並びは受け取る要素の数の下限を持つ。値の並びを観測台帳の
    /// ウィンドウの form から取る入力は、choicesFrom に windowForms を書く。
    /// </summary>
    public static class ComposedTextReadJsonReader
    {
        private const string InputsName = "inputs";

        private const string ListsName = "lists";

        private const string ToolName = "tool";

        private const string InputName = "input";

        private const string ChoicesName = "choices";

        private const string ChoicesFromName = "choicesFrom";

        private const string WindowFormsSource = "windowForms";

        private const string MinLengthName = "minLength";

        private const string LeastName = "least";

        private const string BasisName = "basis";

        private const string FileName = "composed-text-reads.json";

        private const string UiStructureFileName = "ui-structure.json";

        private const string WindowsName = "windows";

        private const string FormName = "form";

        /// <summary>
        /// 表を読む。<paramref name="windowForms"/> は観測台帳のウィンドウの form。入力はツールの名前と
        /// 入力の中の位置(組の項目は点で、並びの要素は [] で区切る)を空白1つで区切って綴る。形が違えば
        /// <see cref="FormatException"/>。
        /// </summary>
        public static ComposedTextReads Read(string json, IList<string> windowForms)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (windowForms == null)
            {
                throw new ArgumentNullException(nameof(windowForms));
            }

            JsonObject members = Parse(json, "合成ツールの文字の読み取りの表") as JsonObject;
            JsonArray inputs = members == null ? null : members[InputsName] as JsonArray;
            JsonArray lists = members == null ? null : members[ListsName] as JsonArray;
            if (inputs == null || lists == null)
            {
                throw new FormatException(
                    "合成ツールの文字の読み取りの表は " + InputsName + " と " + ListsName + " の並びを持つ。");
            }

            Dictionary<string, HostTextRead> texts = new Dictionary<string, HostTextRead>(StringComparer.Ordinal);
            foreach (JsonNode row in inputs)
            {
                string key = Key(row, texts.ContainsKey);
                JsonObject held = (JsonObject)row;
                IList<string> choices = null;
                if (held[ChoicesFromName] != null)
                {
                    if (!string.Equals(Text(row, ChoicesFromName), WindowFormsSource, StringComparison.Ordinal)
                        || windowForms.Count == 0)
                    {
                        throw new FormatException(
                            "合成ツールの文字の読み取りの " + ChoicesFromName + " は " + WindowFormsSource
                                + " で、観測台帳にウィンドウが要る: " + key);
                    }

                    choices = windowForms.ToList();
                }

                if (held[ChoicesName] != null)
                {
                    JsonArray listed = held[ChoicesName] as JsonArray;
                    if (choices != null || listed == null || listed.Count == 0)
                    {
                        throw new FormatException(
                            "合成ツールの文字の読み取りの " + ChoicesName + " は1つ以上の文字の並びで、"
                                + ChoicesFromName + " と一緒に書かない: " + key);
                    }

                    choices = listed.Select(v => Choice(v, key)).ToList();
                }

                int? minLength = Count(held, MinLengthName, key);
                if (choices != null && minLength.HasValue)
                {
                    throw new FormatException(
                        "合成ツールの文字の読み取りは、値の並びと " + MinLengthName + " を一緒に持たない: " + key);
                }

                texts[key] = new HostTextRead(choices, minLength);
            }

            Dictionary<string, HostListLength> lengths = new Dictionary<string, HostListLength>(StringComparer.Ordinal);
            foreach (JsonNode row in lists)
            {
                string key = Key(row, lengths.ContainsKey);
                lengths[key] = new HostListLength(Count((JsonObject)row, LeastName, key), null);
            }

            return new ComposedTextReads(texts, lengths);
        }

        /// <summary>観測台帳のウィンドウの form を、台帳の順に読む。</summary>
        public static IList<string> ReadWindowForms(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            JsonObject root = Parse(json, "観測台帳") as JsonObject;
            JsonArray windows = root == null ? null : root[WindowsName] as JsonArray;
            if (windows == null)
            {
                throw new FormatException("観測台帳は " + WindowsName + " の並びを持つ。");
            }

            return windows.Select(w => Text(w, FormName)).ToList();
        }

        /// <summary>
        /// 表のパス。表は、<paramref name="ledgerPath"/> の能力台帳と同じ観測データの置き場に在る。
        /// </summary>
        public static string Beside(string ledgerPath)
        {
            return Path.Combine(Folder(ledgerPath), FileName);
        }

        /// <summary>
        /// 観測台帳のパス。台帳は、<paramref name="ledgerPath"/> の能力台帳と同じ観測データの置き場に在る。
        /// </summary>
        public static string UiStructureBeside(string ledgerPath)
        {
            return Path.Combine(Folder(ledgerPath), UiStructureFileName);
        }

        private static string Folder(string ledgerPath)
        {
            if (ledgerPath == null)
            {
                throw new ArgumentNullException(nameof(ledgerPath));
            }

            return Path.GetDirectoryName(Path.GetFullPath(ledgerPath));
        }

        private static JsonNode Parse(string json, string what)
        {
            try
            {
                return JsonNode.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new FormatException(what + "がJSONとして読めない: " + exception.Message, exception);
            }
        }

        private static string Key(JsonNode row, Func<string, bool> seen)
        {
            string key = Text(row, ToolName) + " " + Text(row, InputName);
            if (seen(key))
            {
                throw new FormatException("合成ツールの文字の読み取りの表に同じ入力が二度在る: " + key);
            }

            Text(row, BasisName);

            return key;
        }

        private static string Text(JsonNode row, string name)
        {
            JsonValue value = row is JsonObject members ? members[name] as JsonValue : null;
            string text;
            if (value == null || !value.TryGetValue(out text) || text.Length == 0)
            {
                throw new FormatException("合成ツールの文字の読み取りの表の行は " + name + " の文字列を持つ。");
            }

            return text;
        }

        private static string Choice(JsonNode node, string key)
        {
            string text;
            if (!(node is JsonValue value) || !value.TryGetValue(out text) || text.Length == 0)
            {
                throw new FormatException("合成ツールの文字の読み取りの値は空でない文字である: " + key);
            }

            return text;
        }

        private static int? Count(JsonObject row, string name, string key)
        {
            JsonNode node = row[name];
            if (node == null)
            {
                return null;
            }

            int count;
            if (!(node is JsonValue value) || !value.TryGetValue(out count) || count < 1)
            {
                throw new FormatException("合成ツールの文字の読み取りの " + name + " は1以上の整数である: " + key);
            }

            return count;
        }
    }

    /// <summary>合成ツールの文字の入力と文字の並びを、ホストがどう読むか。</summary>
    public sealed class ComposedTextReads
    {
        public ComposedTextReads(
            IDictionary<string, HostTextRead> inputs, IDictionary<string, HostListLength> lists)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (lists == null)
            {
                throw new ArgumentNullException(nameof(lists));
            }

            Inputs = new Dictionary<string, HostTextRead>(inputs, StringComparer.Ordinal);
            Lists = new Dictionary<string, HostListLength>(lists, StringComparer.Ordinal);
        }

        /// <summary>文字の入力から、ホストの読み方へ。</summary>
        public IDictionary<string, HostTextRead> Inputs { get; }

        /// <summary>文字の並びの入力から、ホストが受け取る要素の数へ。</summary>
        public IDictionary<string, HostListLength> Lists { get; }
    }

    /// <summary>ホストが文字の入力として受け取る値。決まりを持たない側は null。</summary>
    public sealed class HostTextRead
    {
        public HostTextRead(IList<string> choices, int? minLength)
        {
            Choices = choices;
            MinLength = minLength;
        }

        /// <summary>受け取る値の並び。どの文字も受け取るなら null。</summary>
        public IList<string> Choices { get; }

        /// <summary>受け取る最小の長さ。長さを問わないなら null。</summary>
        public int? MinLength { get; }
    }
}
