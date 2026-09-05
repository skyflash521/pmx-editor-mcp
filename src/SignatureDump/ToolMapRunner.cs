using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表の正本を規則と照合する配線。ファイルは書き出さず、合否だけを終了コードで返す。
    /// </summary>
    public static class ToolMapRunner
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
                    "引数は6つ: <PMXエディタ導入ディレクトリ> <能力台帳のパス> <除外一覧のパス>"
                        + " <型役割表の正本のパス> <共通契約割当の正本のパス> <能力対応表の正本のパス>");
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
            TypeRoleTable roles;
            CommonAssignmentTable assignments;
            ToolMap map;
            try
            {
                ledger = LedgerParser.Parse(Read(args[1], "能力台帳"));
                excluded = ExcludedSignatureJsonReader.Read(Read(args[2], "除外一覧"));
                roles = TypeRoleTableJsonReader.ReadTypeRoles(Read(args[3], "型役割表の正本"));
                assignments = CommonAssignmentJsonReader.Read(Read(args[4], "共通契約割当の正本"));
                map = ToolMapJsonReader.Read(Read(args[5], "能力対応表の正本"));
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

            ToolMapEvidence evidence;
            try
            {
                evidence = ToolMapEvidence.Collect(ledger, excluded, inventory, roles);
            }
            catch (Exception exception)
                when (exception is InvalidOperationException || exception is ArgumentException)
            {
                error.WriteLine("照合の材料を決められない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            try
            {
                ToolMapGate.Require(map, evidence, assignments);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("能力対応表が規則に合わない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: 行 {0} 件(共通契約割当 {1}・イベント {2}・スキーマ埋め込み {3}"
                    + "・直接ディスパッチ {4})・提供対象 {5} 件",
                map.Rows.Count,
                Count(map, ToolMapRowKind.CommonContract),
                Count(map, ToolMapRowKind.EventBranch),
                Count(map, ToolMapRowKind.SchemaEmbedded),
                Count(map, ToolMapRowKind.DirectDispatch),
                evidence.Provided.Count));

            return ExitCodes.Success;
        }

        private static int Count(ToolMap map, ToolMapRowKind kind)
        {
            return map.Rows.Count(r => r.RowKind == kind);
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
