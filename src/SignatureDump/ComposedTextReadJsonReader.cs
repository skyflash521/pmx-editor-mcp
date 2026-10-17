using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        private const string TableName = "合成ツールの文字の読み取りの表";

        private const string UiStructureName = "観測台帳";

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(
                InputsName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(ToolName, JsonForm.Text()),
                        JsonForm.Member(InputName, JsonForm.Text()),
                        JsonForm.Optional(ChoicesName, JsonForm.Array(JsonForm.Text())),
                        JsonForm.Optional(ChoicesFromName, JsonForm.Text()),
                        JsonForm.Optional(MinLengthName, JsonForm.Count()),
                        JsonForm.Member(BasisName, JsonForm.Text())),
                    allowEmpty: true)),
            JsonForm.Member(
                ListsName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(ToolName, JsonForm.Text()),
                        JsonForm.Member(InputName, JsonForm.Text()),
                        JsonForm.Optional(LeastName, JsonForm.Count()),
                        JsonForm.Member(BasisName, JsonForm.Text())),
                    allowEmpty: true)));

        private static readonly JsonForm UiStructure = JsonForm.Part(
            JsonForm.Member(
                WindowsName,
                JsonForm.Array(
                    JsonForm.Part(JsonForm.Member(FormName, JsonForm.Text())), allowEmpty: true)));

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

            IDictionary<string, object> root = Read(Form, json, TableName);
            Dictionary<string, HostTextRead> texts = new Dictionary<string, HostTextRead>(StringComparer.Ordinal);
            foreach (IDictionary<string, object> row in Rows(root, InputsName))
            {
                string key = Key(row, texts.ContainsKey);
                IList<string> choices = null;
                object value;
                if (row.TryGetValue(ChoicesFromName, out value))
                {
                    if (!string.Equals((string)value, WindowFormsSource, StringComparison.Ordinal)
                        || windowForms.Count == 0)
                    {
                        throw new FormatException(
                            "合成ツールの文字の読み取りの " + ChoicesFromName + " は " + WindowFormsSource
                                + " で、観測台帳にウィンドウが要る: " + key);
                    }

                    choices = windowForms.ToList();
                }

                if (row.TryGetValue(ChoicesName, out value))
                {
                    if (choices != null)
                    {
                        throw new FormatException(
                            "合成ツールの文字の読み取りの " + ChoicesName + " は "
                                + ChoicesFromName + " と一緒に書かない: " + key);
                    }

                    choices = ((object[])value).Cast<string>().ToList();
                }

                int? minLength = row.TryGetValue(MinLengthName, out value) ? (int)value : (int?)null;
                if (choices != null && minLength.HasValue)
                {
                    throw new FormatException(
                        "合成ツールの文字の読み取りは、値の並びと " + MinLengthName + " を一緒に持たない: " + key);
                }

                texts[key] = new HostTextRead(choices, minLength);
            }

            Dictionary<string, HostListLength> lengths = new Dictionary<string, HostListLength>(StringComparer.Ordinal);
            foreach (IDictionary<string, object> row in Rows(root, ListsName))
            {
                string key = Key(row, lengths.ContainsKey);
                object least;
                lengths[key] = new HostListLength(
                    row.TryGetValue(LeastName, out least) ? (int)least : (int?)null, null);
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

            return Rows(Read(UiStructure, json, UiStructureName), WindowsName)
                .Select(window => (string)window[FormName])
                .ToList();
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

        private static IDictionary<string, object> Read(JsonForm form, string json, string what)
        {
            try
            {
                return (IDictionary<string, object>)form.Read(json);
            }
            catch (FormatException exception)
            {
                throw new FormatException(what + ": " + exception.Message, exception);
            }
        }

        private static IEnumerable<IDictionary<string, object>> Rows(
            IDictionary<string, object> root, string name)
        {
            return ((object[])root[name]).Cast<IDictionary<string, object>>();
        }

        private static string Key(IDictionary<string, object> row, Func<string, bool> seen)
        {
            string key = (string)row[ToolName] + " " + (string)row[InputName];
            if (seen(key))
            {
                throw new FormatException("合成ツールの文字の読み取りの表に同じ入力が二度在る: " + key);
            }

            return key;
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
