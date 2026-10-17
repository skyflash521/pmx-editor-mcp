using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 合成ツールの数の入力と数の並びを、ホストがどう読むかを記した表をJSONから読み取る。数の入力は
    /// 整数と実数のどちらで読むかと受け取る範囲を、数の並びは受け取る要素の数を持つ。
    /// </summary>
    public static class ComposedNumberReadJsonReader
    {
        private const string InputsName = "inputs";

        private const string ListsName = "lists";

        private const string ToolName = "tool";

        private const string InputName = "input";

        private const string ReadsName = "reads";

        private const string LeastName = "least";

        private const string MostName = "most";

        private const string LeastExcludedName = "leastExcluded";

        private const string BasisName = "basis";

        private const string FileName = "composed-number-reads.json";

        private const string IntegerReads = "integer";

        private const string RealReads = "real";

        private const string TableName = "合成ツールの数の読み取りの表";

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(
                InputsName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(ToolName, JsonForm.Text()),
                        JsonForm.Member(InputName, JsonForm.Text()),
                        JsonForm.Member(ReadsName, JsonForm.Text()),
                        JsonForm.Member(LeastName, JsonForm.Number()),
                        JsonForm.Optional(LeastExcludedName, JsonForm.Flag()),
                        JsonForm.Member(MostName, JsonForm.Number()),
                        JsonForm.Member(BasisName, JsonForm.Text())),
                    allowEmpty: true)),
            JsonForm.Member(
                ListsName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(ToolName, JsonForm.Text()),
                        JsonForm.Member(InputName, JsonForm.Text()),
                        JsonForm.Optional(LeastName, JsonForm.Number()),
                        JsonForm.Optional(MostName, JsonForm.Number()),
                        JsonForm.Member(BasisName, JsonForm.Text())),
                    allowEmpty: true)));

        /// <summary>
        /// 表を読む。入力はツールの名前と入力の中の位置(組の項目は点で、並びの要素は [] で区切る)を
        /// 空白1つで区切って綴る。形が違えば <see cref="FormatException"/>。
        /// </summary>
        public static ComposedReads Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            IDictionary<string, object> root;
            try
            {
                root = (IDictionary<string, object>)Form.Read(json);
            }
            catch (FormatException exception)
            {
                throw new FormatException(TableName + ": " + exception.Message, exception);
            }

            Dictionary<string, HostNumberRead> numbers = new Dictionary<string, HostNumberRead>(StringComparer.Ordinal);
            foreach (IDictionary<string, object> row in Rows(root, InputsName))
            {
                string key = Key(row, numbers.ContainsKey);
                string reads = (string)row[ReadsName];
                if (!string.Equals(reads, IntegerReads, StringComparison.Ordinal)
                    && !string.Equals(reads, RealReads, StringComparison.Ordinal))
                {
                    throw new FormatException(
                        "合成ツールの数の読み取りは " + IntegerReads + " か " + RealReads + " である: " + key);
                }

                double least = Number(row, LeastName).Value;
                double most = Number(row, MostName).Value;
                if (least > most)
                {
                    throw new FormatException(
                        "合成ツールの数の読み取りの表の行は、" + LeastName + " 以上 " + MostName
                            + " 以下となる2つの数を持つ: " + key);
                }

                object excluded;
                numbers[key] = new HostNumberRead(
                    string.Equals(reads, IntegerReads, StringComparison.Ordinal),
                    least,
                    row.TryGetValue(LeastExcludedName, out excluded) && (bool)excluded,
                    most);
            }

            Dictionary<string, HostListLength> lengths = new Dictionary<string, HostListLength>(StringComparer.Ordinal);
            foreach (IDictionary<string, object> row in Rows(root, ListsName))
            {
                string key = Key(row, lengths.ContainsKey);
                int? least = Count(row, LeastName, key);
                int? most = Count(row, MostName, key);
                if (least.HasValue && most.HasValue && least.Value > most.Value)
                {
                    throw new FormatException(
                        "合成ツールの数の並びの読み取りの " + LeastName + " が " + MostName + " を超えている: " + key);
                }

                lengths[key] = new HostListLength(least, most);
            }

            return new ComposedReads(numbers, lengths);
        }

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
                throw new FormatException("合成ツールの数の読み取りの表に同じ入力が二度在る: " + key);
            }

            return key;
        }

        private static double? Number(IDictionary<string, object> row, string name)
        {
            object value;
            return row.TryGetValue(name, out value)
                ? Convert.ToDouble(value, CultureInfo.InvariantCulture)
                : (double?)null;
        }

        private static int? Count(IDictionary<string, object> row, string name, string key)
        {
            double? number = Number(row, name);
            if (number == null)
            {
                return null;
            }

            if (number.Value != Math.Floor(number.Value) || number.Value < 0 || number.Value > int.MaxValue)
            {
                throw new FormatException("合成ツールの数の並びの読み取りの " + name + " は0以上の整数である: " + key);
            }

            return (int)number.Value;
        }
    }

    /// <summary>合成ツールの数の入力と数の並びを、ホストがどう読むか。</summary>
    public sealed class ComposedReads
    {
        public ComposedReads(
            IDictionary<string, HostNumberRead> numbers, IDictionary<string, HostListLength> lists)
        {
            if (numbers == null)
            {
                throw new ArgumentNullException(nameof(numbers));
            }

            if (lists == null)
            {
                throw new ArgumentNullException(nameof(lists));
            }

            Numbers = new Dictionary<string, HostNumberRead>(numbers, StringComparer.Ordinal);
            Lists = new Dictionary<string, HostListLength>(lists, StringComparer.Ordinal);
        }

        /// <summary>数の入力から、ホストの読み方へ。</summary>
        public IDictionary<string, HostNumberRead> Numbers { get; }

        /// <summary>数の並びの入力から、ホストが受け取る要素の数へ。</summary>
        public IDictionary<string, HostListLength> Lists { get; }
    }

    /// <summary>ホストが数の入力を読む型と、受け取る範囲。</summary>
    public sealed class HostNumberRead
    {
        public HostNumberRead(bool integer, double least, bool leastExcluded, double most)
        {
            Integer = integer;
            Least = least;
            LeastExcluded = leastExcluded;
            Most = most;
        }

        /// <summary>整数として読むなら真、実数として読むなら偽。</summary>
        public bool Integer { get; }

        public double Least { get; }

        /// <summary>下限そのものを断るなら真。</summary>
        public bool LeastExcluded { get; }

        public double Most { get; }
    }

    /// <summary>ホストが数の並びとして受け取る要素の数。数を問わない側は null。</summary>
    public sealed class HostListLength
    {
        public HostListLength(int? least, int? most)
        {
            Least = least;
            Most = most;
        }

        public int? Least { get; }

        public int? Most { get; }
    }
}
