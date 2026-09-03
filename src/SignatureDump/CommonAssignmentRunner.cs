using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 共通契約割当の正本を規則と照合する配線。ファイルは書き出さず、合否だけを終了コードで返す。
    /// </summary>
    public static class CommonAssignmentRunner
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
                    "引数は5つ: <PMXエディタ導入ディレクトリ> <能力台帳のパス> <除外一覧のパス>"
                        + " <型役割表の正本のパス> <共通契約割当の正本のパス>");
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
            CommonAssignmentTable table;
            try
            {
                ledger = LedgerParser.Parse(Read(args[1], "能力台帳"));
                excluded = ExcludedSignatureJsonReader.Read(Read(args[2], "除外一覧"));
                roles = TypeRoleTableJsonReader.ReadTypeRoles(Read(args[3], "型役割表の正本"));
                table = CommonAssignmentJsonReader.Read(Read(args[4], "共通契約割当の正本"));
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

            ISet<string> provided;
            ISet<string> residentObjects;
            ISet<string> releases;
            IDictionary<string, SlotBinding> bindings;
            try
            {
                provided = TypeRolePopulation.Resolve(ledger, inventory, excluded).Signatures;
                IDictionary<string, TypeRole> byType = roles.Types.ToDictionary(
                    r => r.TypeName, r => r.Role, StringComparer.Ordinal);
                residentObjects = CommonAssignmentEvidence.ResidentObjectSignatures(
                    inventory, byType, provided);
                releases = CommonAssignmentEvidence.ReleaseSignatures(inventory, provided);
                bindings = CommonAssignmentEvidence.Bindings(
                    inventory,
                    byType,
                    new HashSet<string>(
                        table.Assignments.Select(a => a.SignatureKey), StringComparer.Ordinal));
            }
            catch (Exception exception)
                when (exception is InvalidOperationException || exception is ArgumentException)
            {
                error.WriteLine("割当の根拠を決められない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            try
            {
                CommonAssignmentGate.Require(
                    table, provided, residentObjects, releases, bindings);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("共通契約割当が規則に合わない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: 割当 {0} 件(ツール {1}・共通引数 {2}・内部フロー {3})・束縛 {4} 件",
                table.Assignments.Count,
                table.Assignments.Count(a => a.Assignment == CommonAssignmentKind.Tool),
                table.Assignments.Count(a => a.Assignment == CommonAssignmentKind.CommonArg),
                table.Assignments.Count(a => a.Assignment == CommonAssignmentKind.InternalFlow),
                table.Assignments.Sum(a => Bound(a.SlotBinding))));

            return ExitCodes.Success;
        }

        private static int Bound(SlotBinding binding)
        {
            return binding.Parameters.Count
                + (binding.Returned.HasValue ? 1 : 0)
                + (binding.Receiver.HasValue ? 1 : 0);
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
