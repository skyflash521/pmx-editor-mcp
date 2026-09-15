using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表の2つの版を突き合わせ、中身の変わった行のキーを書き出す配線。実機の検査を、
    /// 変えた行だけへ絞るのに使う。
    /// </summary>
    public static class ChangedRowRunner
    {
        private const string RowsName = "rows";

        private const string SignatureKeyName = "signatureKey";

        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            if (args.Length != 2)
            {
                error.WriteLine(
                    "引数は2個: " + CommandRunner.ChangedRowsCommand
                        + " <前の能力対応表の正本のパス> <いまの能力対応表の正本のパス>");
                return ExitCodes.InvalidArguments;
            }

            IList<KeyValuePair<string, string>> before;
            IList<KeyValuePair<string, string>> after;
            try
            {
                before = Rows(args[0]);
                after = Rows(args[1]);
            }
            catch (Exception exception)
            {
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            IDictionary<string, string> held = before.ToDictionary(
                row => row.Key, row => row.Value, StringComparer.Ordinal);

            // 書き出す順はいまの版の並びに従う。正本が行キーの昇順を保つので、読む側は並べ直さずに
            // そのまま使える。
            foreach (KeyValuePair<string, string> row in after)
            {
                string kept;
                if (!held.TryGetValue(row.Key, out kept)
                    || !string.Equals(kept, row.Value, StringComparison.Ordinal))
                {
                    output.WriteLine(row.Key);
                }
            }

            return ExitCodes.Success;
        }

        /// <summary>
        /// 行キーと、その行の綴りを並びのまま返す。形が正しいかは正本の読み取りに任せ、突き合わせは
        /// 行を丸ごと見て行う——どの項目が実機の検査の中身を決めるかは組み立て側の知識なので、
        /// ここが項目の部分集合を持つと、その外を直した行を変わっていないものとして取りこぼす。
        /// </summary>
        private static IList<KeyValuePair<string, string>> Rows(string path)
        {
            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (IOException exception)
            {
                throw new FormatException("読めない: " + path, exception);
            }

            try
            {
                ToolMapJsonReader.Read(json);
            }
            catch (FormatException exception)
            {
                throw new FormatException(path + ": " + exception.Message, exception);
            }

            return JsonNode.Parse(json)[RowsName].AsArray()
                .Select(row => new KeyValuePair<string, string>(
                    row[SignatureKeyName].GetValue<string>(), row.ToJsonString()))
                .ToList();
        }
    }
}
