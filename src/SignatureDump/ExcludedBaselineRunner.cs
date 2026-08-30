using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 凍結した除外の組を書き出す実行1回ぶんの配線。台帳と、その時点のSDKの公開シグネチャの
    /// 両方を読んで確定するので、どちらかが欠けても食い違っても書き出さない。
    /// </summary>
    public static class ExcludedBaselineRunner
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
                error.WriteLine("引数は3つ: <PMXエディタ導入ディレクトリ> <能力台帳のパス> <書き出し先パス>");
                return ExitCodes.InvalidArguments;
            }

            string editorDirectory = args[0];
            string ledgerPath = args[1];
            string outputPath = args[2];
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);

            if (!File.Exists(assemblyPath))
            {
                error.WriteLine("対象のアセンブリが無い: " + assemblyPath);
                return ExitCodes.InputUnavailable;
            }

            if (!File.Exists(ledgerPath))
            {
                error.WriteLine("能力台帳が無い: " + ledgerPath);
                return ExitCodes.InputUnavailable;
            }

            IList<CapabilityRecord> ledger;
            try
            {
                ledger = LedgerParser.Parse(File.ReadAllText(ledgerPath));
            }
            catch (Exception exception)
            {
                error.WriteLine("能力台帳を読めない: " + ledgerPath);
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            if (ledger.Count == 0)
            {
                // 能力の表が1行も無いものは台帳ではない。読み解けた台帳との食い違いと混ぜない。
                error.WriteLine("能力台帳に能力の行が無い: " + ledgerPath);
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

            IList<ExcludedBaselineEntry> entries;
            try
            {
                entries = ExcludedBaselineBuilder.Build(ledger, inventory.Signatures);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("凍結する組を確定できない: " + ledgerPath);
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            string writing = outputPath + ".writing";
            try
            {
                File.WriteAllText(writing, ExcludedBaselineJson.Write(entries), new UTF8Encoding(false));
                if (File.Exists(outputPath))
                {
                    File.Replace(writing, outputPath, null);
                }
                else
                {
                    File.Move(writing, outputPath);
                }
            }
            catch (Exception exception)
            {
                error.WriteLine("結果を書き出せない: " + outputPath);
                error.WriteLine(exception.Message);
                Discard(writing);
                return ExitCodes.WriteFailed;
            }

            int signatures = 0;
            foreach (ExcludedBaselineEntry entry in entries)
            {
                signatures += entry.Signatures.Count;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "書き出した: {0}(能力 {1} 件・シグネチャ {2} 件)",
                outputPath,
                entries.Count,
                signatures));

            return ExitCodes.Success;
        }

        private static void Discard(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
