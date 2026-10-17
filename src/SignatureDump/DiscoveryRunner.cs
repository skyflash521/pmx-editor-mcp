using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 用途の作業ごとの検索が、その作業に要るツールをちょうど引き当てるかを照合する配線。外部の
    /// サービスへは問い合わせず、ツールの名前と説明文だけで判じる。
    /// </summary>
    public static class DiscoveryRunner
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

            if (args.Length != 9)
            {
                error.WriteLine(
                    "引数は9つ: <PMXエディタ導入ディレクトリ> <能力台帳のパス>"
                        + " <共通契約の正本のパス> <型役割表の正本のパス> <日本語名の正本のパス>"
                        + " <共通契約割当の正本のパス> <能力対応表の正本のパス>"
                        + " <用途の作業の正本のパス> <スキーマ正本のパス>");
                return ExitCodes.InvalidArguments;
            }

            string editorDirectory = args[0];
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            if (!File.Exists(assemblyPath))
            {
                error.WriteLine("対象のアセンブリが無い: " + assemblyPath);
                return ExitCodes.InputUnavailable;
            }

            DiscoveryTaskTable tasks;
            ToolDefinitionInputs inputs;
            try
            {
                tasks = DiscoveryTaskJsonReader.Read(Read(args[7], "用途の作業の正本"));
                inputs = ToolDefinitionInputs.Read(
                    editorDirectory, args.Take(7).Concat(new[] { args[8] }).ToArray());
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

            Dictionary<string, string> descriptions;
            try
            {
                descriptions = new Dictionary<string, string>(
                    inputs.Descriptions(inventory), StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> own in
                    FixedToolTable.Descriptions(debugHooks: true))
                {
                    descriptions.Add(own.Key, own.Value);
                }

                DiscoveryGate.Require(tasks, descriptions);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("用途の作業が検索で引き当たらない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: 作業 {0} 件・検索 {1} 件・ツール {2} 件",
                tasks.Tasks.Count,
                tasks.Tasks.Sum(t => t.Searches.Count),
                descriptions.Count));

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
