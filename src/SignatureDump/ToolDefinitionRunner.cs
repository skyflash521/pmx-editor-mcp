using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// ツール定義を組み立ててブリッジへ組み込むC#として書き出す配線。ホストの中継と同じ能力対応表
    /// から作るので、同じ指紋を名乗る。
    /// </summary>
    public static class ToolDefinitionRunner
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

            if (args.Length != 11)
            {
                error.WriteLine(
                    "引数は11個: <PMXエディタ導入ディレクトリ> <能力台帳のパス>"
                        + " <共通契約仕様書のパス> <IPC仕様書のパス> <アーキテクチャ仕様書のパス>"
                        + " <型役割表の正本のパス> <日本語名の正本のパス>"
                        + " <共通契約割当の正本のパス> <能力対応表の正本のパス>"
                        + " <スキーマ正本のパス> <書き出し先パス>");
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
            try
            {
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

            IList<ToolDefinition> definitions;
            try
            {
                IDictionary<SchemaItem, string> sdkShapes = inputs.SdkShapes(inventory);
                definitions = ToolDefinitionBuilder.Build(
                    inputs.Schemas,
                    inputs.Descriptions(inventory),
                    new AssumedLength(inputs.Lengths, sdkShapes),
                    inputs.BudgetChars - inputs.WarningChars,
                    inputs.RequestBytes,
                    inputs.TokenLimit,
                    sdkShapes,
                    inputs.DangerousTools(inventory),
                    inputs.ConditionalDangerousTools(inventory));
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("ツール定義を組み立てられない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            string digest = inputs.MapDigest;
            try
            {
                WriteIfChanged(args[10], ToolDefinitionSource.Compose(definitions, digest));
            }
            catch (Exception exception)
            {
                error.WriteLine("書き出せない: " + args[10]);
                error.WriteLine(exception.Message);
                return ExitCodes.WriteFailed;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "ツール定義を組み立てた: {0} 件・指紋 {1}",
                definitions.Count,
                digest));

            return ExitCodes.Success;
        }

        /// <summary>
        /// 中身が変わっていなければ書かない。書き直すと更新時刻が動いて、ブリッジのビルドが毎回
        /// やり直しになる。
        /// </summary>
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
