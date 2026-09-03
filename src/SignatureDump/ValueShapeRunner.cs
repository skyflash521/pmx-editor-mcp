using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 値の表現の表を規則と照合する配線。ファイルは書き出さず、合否だけを終了コードで返す。
    /// </summary>
    public static class ValueShapeRunner
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

            if (args.Length != 4)
            {
                error.WriteLine(
                    "引数は4つ: <PMXエディタ導入ディレクトリ> <能力台帳のパス> <除外一覧のパス>"
                        + " <共通契約仕様書のパス>");
                return ExitCodes.InvalidArguments;
            }

            string editorDirectory = args[0];
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            if (!File.Exists(assemblyPath))
            {
                error.WriteLine("対象のアセンブリが無い: " + assemblyPath);
                return ExitCodes.InputUnavailable;
            }

            IList<CapabilityRecord> ledger;
            IList<ExcludedSignatureRecord> excluded;
            IList<ValueShapeRow> rows;
            try
            {
                ledger = LedgerParser.Parse(Read(args[1], "能力台帳"));
                excluded = ExcludedSignatureJsonReader.Read(Read(args[2], "除外一覧"));
                rows = ValueShapeDocument.Read(Read(args[3], "共通契約仕様書"));
            }
            catch (Exception exception)
            {
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            InventoryRecord inventory;
            try
            {
                inventory = SdkInventory.Load(editorDirectory, assemblyPath);
            }
            catch (Exception exception)
            {
                error.WriteLine("対象のアセンブリを読めない: " + assemblyPath);
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            ISet<string> valueMapped;
            ValueRepresentationRule rule;
            try
            {
                valueMapped = TypeRolePopulation.Resolve(ledger, inventory, excluded).ValueMapped;
                rule = ValueRepresentationRule.Create(inventory);
            }
            catch (Exception exception)
                when (exception is InvalidOperationException || exception is ArgumentException)
            {
                error.WriteLine("値を写す型の集合を決められない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            try
            {
                ValueShapeGate.Require(rows, valueMapped, rule);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("値の表現が規則に合わない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: 型 {0} 件・綴り {1} 種・包む型 {2} 件",
                rows.Count,
                rows.Where(r => r.Shape != null).Select(r => r.Shape)
                    .Distinct(StringComparer.Ordinal).Count(),
                rows.Count(r => r.Shape == null)));

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
