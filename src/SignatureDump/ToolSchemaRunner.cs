using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// スキーマ正本を規則と照合する配線。ファイルは書き出さず、合否だけを終了コードで返す。
    /// </summary>
    public static class ToolSchemaRunner
    {
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

            if (args.Length != 3)
            {
                error.WriteLine(
                    "引数は3つ: <共通契約仕様書のパス> <能力対応表の正本のパス>"
                        + " <スキーマ正本のパス>");
                return ExitCodes.InvalidArguments;
            }

            ISet<string> spellings;
            ToolMap map;
            ToolSchemaTable schemas;
            try
            {
                spellings = ValueShapeDocument.ReadSpellings(Read(args[0], "共通契約仕様書"));
                map = ToolMapJsonReader.Read(Read(args[1], "能力対応表の正本"));
                schemas = ToolSchemaJsonReader.Read(Read(args[2], "スキーマ正本"));
            }
            catch (Exception exception)
            {
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            try
            {
                ToolSchemaGate.Require(schemas, map, spellings);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("スキーマ正本が規則に合わない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: ツール {0} 件(呼び分け {1}・項目 {2}・イベントの分岐 {3})・綴り {4} 種",
                schemas.Tools.Count,
                schemas.Tools.Sum(t => t.Branches.Count),
                schemas.Tools.Sum(t => t.AllItems.Count()),
                schemas.Tools.Sum(t => t.Payloads == null ? 0 : t.Payloads.Count),
                spellings.Count));

            return ExitCodes.Success;
        }

        private static string Read(string path, string name)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(name + "が無い: " + path, path);
            }

            return File.ReadAllText(path);
        }
    }
}
