using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// ツールの名前から呼ぶ行へ結び付ける表を組み立てて書き出す配線。ホストのビルドがこれを呼び、
    /// 出来た本文だけを配布物へ入れる。
    /// </summary>
    public static class ToolBindingRunner
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

            if (args.Length != 7)
            {
                error.WriteLine(
                    "引数は7つ: <PMXエディタ導入ディレクトリ> <能力台帳のパス>"
                        + " <型役割表の正本のパス> <共通契約割当の正本のパス>"
                        + " <能力対応表の正本のパス> <スキーマ正本のパス> <書き出し先パス>");
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
            TypeRoleTable roles;
            CommonAssignmentTable assignments;
            ToolMap map;
            ToolSchemaTable schemas;
            try
            {
                ledger = LedgerJsonReader.Read(Read(args[1], "能力台帳"));
                roles = TypeRoleTableJsonReader.ReadTypeRoles(Read(args[2], "型役割表の正本"));
                assignments = CommonAssignmentJsonReader.Read(Read(args[3], "共通契約割当の正本"));
                map = ToolMapJsonReader.Read(Read(args[4], "能力対応表の正本"));
                schemas = ToolSchemaJsonReader.Read(Read(args[5], "スキーマ正本"));
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

            ToolBindingSource source;
            try
            {
                IDictionary<string, SignatureRecord> signatures = inventory.Signatures
                    .ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);
                TypeRoleTable owned = TypeGroupRule.Resolve(
                    roles, TypeGroupEvidence.OwnersByType(ledger, inventory));
                source = ToolBindingSourceBuilder.Build(
                    map,
                    owned,
                    inventory,
                    ToolNameEvidence.Resolve(map, owned, assignments, signatures),
                    assignments,
                    schemas);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("ツールの結び付きを組み立てられない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            try
            {
                WriteIfChanged(args[6], source.Text);
            }
            catch (Exception exception)
            {
                error.WriteLine("書き出せない: " + args[6]);
                error.WriteLine(exception.Message);
                return ExitCodes.WriteFailed;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "ツールの結び付きを組み立てた: 中継 {0} 件・項目を集める {1} 件・要素 {2} 件",
                source.Calls.Count,
                source.Aggregations.Count,
                source.Elements.Count));

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
