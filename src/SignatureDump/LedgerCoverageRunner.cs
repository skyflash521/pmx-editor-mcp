using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 逆向きの照合と除外一覧の照合を1回行う配線。ファイルは書き出さず、合否だけを終了コードで
    /// 返す。
    /// </summary>
    public static class LedgerCoverageRunner
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
                        + " <対象外一覧のパス>");
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
            LedgerOutOfScopeRecord outOfScope;
            try
            {
                ledger = LedgerParser.Parse(Read(args[1], "能力台帳"));
                excluded = ExcludedSignatureJsonReader.Read(Read(args[2], "除外一覧"));
                outOfScope = LedgerOutOfScopeJsonReader.Read(Read(args[3], "対象外一覧"));
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

            LedgerCoverageResult result;
            try
            {
                result = LedgerCoverage.Verify(ledger, inventory, excluded, outOfScope);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("照合が合わない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: 型 {0} 件(台帳 {1}・対象外 {2})・シグネチャ {3} 件"
                    + "(母集合 {4}・対象外 {5})・除外 {6} 件・提供対象 {7} 件",
                result.PublicTypes,
                result.LedgerTypes,
                result.OutOfScopeTypes,
                result.PublicSignatures,
                result.Population,
                result.OutOfScopeSignatures,
                result.Excluded,
                result.Provided));

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
