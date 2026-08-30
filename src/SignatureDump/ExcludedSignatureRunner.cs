using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 除外一覧を書き出す実行1回ぶんの配線。凍結した組と、その時点のSDKの公開シグネチャの両方を
    /// 読んで確定するので、どちらかが欠けても食い違っても書き出さない。
    /// </summary>
    public static class ExcludedSignatureRunner
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
                    "引数は3つ: <PMXエディタ導入ディレクトリ> <ベースライン正本のパス> <書き出し先パス>");
                return ExitCodes.InvalidArguments;
            }

            string editorDirectory = args[0];
            string baselinePath = args[1];
            string outputPath = args[2];
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);

            if (!File.Exists(assemblyPath))
            {
                error.WriteLine("対象のアセンブリが無い: " + assemblyPath);
                return ExitCodes.InputUnavailable;
            }

            if (!File.Exists(baselinePath))
            {
                error.WriteLine("ベースライン正本が無い: " + baselinePath);
                return ExitCodes.InputUnavailable;
            }

            IList<ExcludedBaselineEntry> baseline;
            try
            {
                baseline = ExcludedBaselineJsonReader.Read(File.ReadAllText(baselinePath));
            }
            catch (Exception exception)
            {
                error.WriteLine("ベースライン正本を読めない: " + baselinePath);
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

            IList<ExcludedSignatureRecord> records;
            try
            {
                records = ExcludedSignatureBuilder.Build(baseline, inventory);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("除外を確定できない: " + baselinePath);
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            string writing = outputPath + ".writing";
            try
            {
                File.WriteAllText(writing, ExcludedSignatureJson.Write(records), new UTF8Encoding(false));
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

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "書き出した: {0}(除外 {1} 件・うちベースライン {2} 件)",
                outputPath,
                records.Count,
                records.Count(r => r.Qualification == ExclusionQualification.Baseline)));

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
