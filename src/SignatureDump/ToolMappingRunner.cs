using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表とスキーマ正本を写像の規則と照合する配線。ファイルは書き出さず、合否だけを終了
    /// コードで返す。
    /// </summary>
    public static class ToolMappingRunner
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

            if (args.Length != 5)
            {
                error.WriteLine(
                    "引数は5つ: <PMXエディタ導入ディレクトリ> <能力台帳のパス>"
                        + " <型役割表の正本のパス> <能力対応表の正本のパス> <スキーマ正本のパス>");
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
            ToolMap map;
            ToolSchemaTable schemas;
            try
            {
                ledger = LedgerParser.Parse(Read(args[1], "能力台帳"));
                roles = TypeRoleTableJsonReader.ReadTypeRoles(Read(args[2], "型役割表の正本"));
                map = ToolMapJsonReader.Read(Read(args[3], "能力対応表の正本"));
                schemas = ToolSchemaJsonReader.Read(Read(args[4], "スキーマ正本"));
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

            try
            {
                roles = TypeGroupRule.Resolve(
                    roles, TypeGroupEvidence.OwnersByType(ledger, inventory));
                ToolMappingGate.Require(
                    map,
                    roles,
                    inventory.Signatures.ToDictionary(s => s.Key, s => s, StringComparer.Ordinal),
                    schemas);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("写像の規則に合わない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: ツールを持つ行 {0} 件・埋め込み先 {1} 件・呼び分け {2} 件",
                map.Rows.Count(r => r.Tool != null),
                map.Rows.Sum(r => r.EmbeddedIn == null ? 0 : r.EmbeddedIn.Count),
                schemas.Tools.Sum(t => t.Branches.Count)));

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
