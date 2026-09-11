using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 行のシグネチャとスキーマ正本の対応を照合する配線。ファイルは書き出さず、合否だけを終了
    /// コードで返す。
    /// </summary>
    public static class SchemaCorrespondenceRunner
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

            if (args.Length != 6)
            {
                error.WriteLine(
                    "引数は6つ: <PMXエディタ導入ディレクトリ> <能力台帳のパス>"
                        + " <型役割表の正本のパス> <共通契約割当の正本のパス>"
                        + " <能力対応表の正本のパス> <スキーマ正本のパス>");
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

            IDictionary<string, string> toolNames;
            try
            {
                IDictionary<string, SignatureRecord> signatures = inventory.Signatures
                    .ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);
                TypeRoleTable owned = TypeGroupRule.Resolve(
                    roles, TypeGroupEvidence.OwnersByType(ledger, inventory));
                toolNames = ToolNameEvidence.Resolve(map, owned, assignments, signatures);
                SchemaCorrespondenceGate.Require(map, schemas, owned, signatures, toolNames);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("シグネチャとスキーマの対応が規則に合わない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: ツールを持つ行 {0} 件・入出力の形 {1} 件",
                toolNames.Count,
                schemas.Tools.Count));

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
