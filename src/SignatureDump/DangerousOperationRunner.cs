using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 危険操作に当たるシグネチャの照合の配線。ファイルは書き出さず、合否だけを終了コードで返す。
    /// </summary>
    public static class DangerousOperationRunner
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
                    "引数は3つ: <PMXエディタ導入ディレクトリ> <能力台帳のパス> <除外一覧のパス>");
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
            try
            {
                ledger = LedgerParser.Parse(Read(args[1], "能力台帳"));
                excluded = ExcludedSignatureJsonReader.Read(Read(args[2], "除外一覧"));
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

            IDictionary<string, DangerKind> derived;
            IDictionary<string, DangerKind> noted;
            try
            {
                ISet<string> provided = TypeRolePopulation.Resolve(ledger, inventory, excluded).Signatures;
                derived = DangerousOperationRule.Classify(
                    inventory.Signatures.Where(s => provided.Contains(s.Key)));
                noted = DangerousOperationLedger.Read(
                    ledger, LedgerPopulation.Resolve(ledger, inventory), inventory);
            }
            catch (Exception exception)
                when (exception is InvalidOperationException || exception is ArgumentException)
            {
                error.WriteLine("危険操作に当たるシグネチャを決められない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            try
            {
                DangerousOperationGate.Require(derived, noted);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("危険操作に当たるものが台帳と食い違う。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: 危険操作に当たるシグネチャ {0} 件(エディタ終了 {1}・上書き保存 {2}・モデル初期化 {3})",
                derived.Count,
                derived.Values.Count(k => k == DangerKind.Shutdown),
                derived.Values.Count(k => k == DangerKind.Overwrite),
                derived.Values.Count(k => k == DangerKind.Reset)));

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
