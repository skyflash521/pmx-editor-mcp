using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表の2つの版を突き合わせ、中身の変わった行のキーと、名前で置いた段取りが変わった
    /// ツールの名前を書き出す配線。実機の検査を、変えたものだけへ絞るのに使う。
    /// </summary>
    public static class ChangedRowRunner
    {
        private const string RowsName = "rows";

        private const string SignatureKeyName = "signatureKey";

        private const string ToolSetupsName = "toolSetups";

        private const string ToolName = "tool";

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

            JsonNode before;
            JsonNode after;
            try
            {
                before = Map(args[0]);
                after = Map(args[1]);
            }
            catch (Exception exception)
            {
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            IDictionary<string, string> held = Spelled(before, RowsName, SignatureKeyName)
                .ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);

            // 書き出す順はいまの版の並びに従う。正本が行キーの昇順を保つので、読む側は並べ直さずに
            // そのまま使える。
            foreach (KeyValuePair<string, string> row in Spelled(after, RowsName, SignatureKeyName))
            {
                string kept;
                if (!held.TryGetValue(row.Key, out kept)
                    || !string.Equals(kept, row.Value, StringComparison.Ordinal))
                {
                    output.WriteLine(row.Key);
                }
            }

            IDictionary<string, string> setupsBefore = Spelled(before, ToolSetupsName, ToolName)
                .ToDictionary(setup => setup.Key, setup => setup.Value, StringComparer.Ordinal);
            IDictionary<string, string> setupsAfter = Spelled(after, ToolSetupsName, ToolName)
                .ToDictionary(setup => setup.Key, setup => setup.Value, StringComparer.Ordinal);
            foreach (string tool in setupsBefore.Keys.Union(setupsAfter.Keys, StringComparer.Ordinal)
                .OrderBy(tool => tool, StringComparer.Ordinal))
            {
                string was;
                string now;
                setupsBefore.TryGetValue(tool, out was);
                setupsAfter.TryGetValue(tool, out now);
                if (!string.Equals(was, now, StringComparison.Ordinal))
                {
                    output.WriteLine(tool);
                }
            }

            return ExitCodes.Success;
        }

        private static JsonNode Map(string path)
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

            return JsonNode.Parse(json);
        }

        private static IList<KeyValuePair<string, string>> Spelled(
            JsonNode map, string listName, string keyName)
        {
            JsonNode list = map[listName];
            if (list == null)
            {
                return new KeyValuePair<string, string>[0];
            }

            return list.AsArray()
                .Select(item => new KeyValuePair<string, string>(
                    item[keyName].GetValue<string>(), item.ToJsonString()))
                .ToList();
        }
    }
}
