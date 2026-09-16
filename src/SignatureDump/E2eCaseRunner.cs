using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 実機のエディタへ投げる検査を組み立てて書き出す配線。実行器はここが書いたものだけを読む。
    /// </summary>
    public static class E2eCaseRunner
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

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

            if (args.Length != 10)
            {
                error.WriteLine(
                    "引数は10個: <PMXエディタ導入ディレクトリ> <能力台帳のパス>"
                        + " <共通契約の正本のパス>"
                        + " <型役割表の正本のパス> <日本語名の正本のパス>"
                        + " <共通契約割当の正本のパス> <能力対応表の正本のパス>"
                        + " <スキーマ正本のパス> <サンプル値の正本のパス> <書き出し先パス>");
                return ExitCodes.InvalidArguments;
            }

            string editorDirectory = args[0];
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            if (!File.Exists(assemblyPath))
            {
                error.WriteLine("対象のアセンブリが無い: " + assemblyPath);
                return ExitCodes.InputUnavailable;
            }

            ToolDefinitionInputs inputs;
            SampleValueTable samples;
            try
            {
                samples = SampleValueJsonReader.Read(
                    File.ReadAllText(args[8], Encoding.UTF8));
                inputs = ToolDefinitionInputs.Read(editorDirectory, args);
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

            IList<E2eCase> cases;
            try
            {
                cases = E2eCaseBuilder.Build(
                    inputs.Map,
                    inputs.Schemas,
                    inputs.ToolsByRow(inventory),
                    inputs.ConnectionPaths(inventory),
                    inputs.Dangerous(inventory),
                    inputs.SdkShapes(inventory),
                    inputs.SdkTypes(inventory),
                    samples,
                    inputs.ViewImages,
                    inputs.PositionedTypes(),
                    inputs.ElementFactories(inventory),
                    inputs.Readers(inventory),
                    inputs.UnkeptMembers,
                    inputs.HandledTypes(),
                    inputs.PickingRows(inventory),
                    inputs.ReceiverPaths(inventory),
                    inputs.ElementAdders(inventory),
                    inputs.ElementRemovers(inventory),
                    inputs.ConditionalDangerousTools(inventory),
                    inputs.TypePaths(inventory));
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("検査を組み立てられない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            try
            {
                WriteIfChanged(args[9], E2eCaseJson.Compose(cases));
            }
            catch (Exception exception)
            {
                error.WriteLine("書き出せない: " + args[9]);
                error.WriteLine(exception.Message);
                return ExitCodes.WriteFailed;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "検査を組み立てた: {0} 件・行 {1} 件・流れ {2} 種・接続の経路 {3} 種",
                cases.Count,
                cases.Select(c => c.RowKey).Distinct(StringComparer.Ordinal).Count(),
                cases.Select(c => c.EditKind).Distinct(StringComparer.Ordinal).Count(),
                cases.Select(c => c.ConnectionPath).Distinct(StringComparer.Ordinal).Count()));

            return ExitCodes.Success;
        }

        /// <summary>中身が変わっていなければ書かない。更新時刻が動くと、読む側が作り直しと見る。</summary>
        private static void WriteIfChanged(string path, string text)
        {
            if (File.Exists(path)
                && string.Equals(File.ReadAllText(path, Utf8WithoutBom), text, StringComparison.Ordinal))
            {
                return;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, text, Utf8WithoutBom);
        }
    }
}
