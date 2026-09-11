using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表の行キーからSDKへの中継を組み立てて書き出す配線。ホストのビルドがこれを呼び、
    /// 出来た本文だけを配布物へ入れる。
    /// </summary>
    public static class RelaySourceRunner
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

            if (args.Length != 3)
            {
                error.WriteLine(
                    "引数は3つ: <PMXエディタ導入ディレクトリ> <能力対応表の正本のパス> <書き出し先パス>");
                return ExitCodes.InvalidArguments;
            }

            string editorDirectory = args[0];
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            if (!File.Exists(assemblyPath))
            {
                error.WriteLine("対象のアセンブリが無い: " + assemblyPath);
                return ExitCodes.InputUnavailable;
            }

            ToolMap toolMap;
            try
            {
                if (!File.Exists(args[1]))
                {
                    throw new FileNotFoundException("能力対応表が無い: " + args[1], args[1]);
                }

                toolMap = ToolMapJsonReader.Read(File.ReadAllText(args[1]));
            }
            catch (Exception exception)
            {
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            SdkFacts facts;
            try
            {
                facts = SdkInventory.Read(editorDirectory, assemblyPath, SdkFacts.Of);
            }
            catch (Exception exception)
            {
                error.WriteLine("対象のアセンブリを読めない: " + assemblyPath);
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            InventoryRecord inventory = facts.Inventory;
            RelaySource source = RelaySourceBuilder.Build(
                toolMap.Rows.Select(r => r.SignatureKey),
                inventory,
                inventory.AssemblyVersion,
                facts.CombinableEnums);

            try
            {
                WriteIfChanged(args[2], source.Text);
            }
            catch (Exception exception)
            {
                error.WriteLine("書き出せない: " + args[2]);
                error.WriteLine(exception.Message);
                return ExitCodes.WriteFailed;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "中継を組み立てた: 行 {0} 件・解決 {1} 件・未解決 {2} 件・SDK {3}",
                source.Resolved.Count + source.Unresolved.Count,
                source.Resolved.Count,
                source.Unresolved.Count,
                inventory.AssemblyVersion));

            return ExitCodes.Success;
        }

        /// <summary>
        /// 中身が変わっていなければ書かない。書き直すと更新時刻が動いて、ホストのビルドが毎回
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
