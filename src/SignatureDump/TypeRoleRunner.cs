using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 型役割表の正本を規則と照合する配線。ファイルは書き出さず、合否だけを終了コードで返す。
    /// </summary>
    public static class TypeRoleRunner
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

            if (args.Length != 4)
            {
                error.WriteLine(
                    "引数は4つ: <PMXエディタ導入ディレクトリ> <能力台帳のパス> <除外一覧のパス>"
                        + " <型役割表の正本のパス>");
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
            TypeRoleTable table;
            try
            {
                ledger = LedgerParser.Parse(Read(args[1], "能力台帳"));
                excluded = ExcludedSignatureJsonReader.Read(Read(args[2], "除外一覧"));
                table = TypeRoleTableJsonReader.ReadTypeRoles(Read(args[3], "型役割表の正本"));
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

            TypeRolePopulation population;
            ISet<string> eventArgumentTypes;
            ISet<string> connectorCandidates;
            IDictionary<string, string> connectionPaths;
            IDictionary<string, HandleIssuanceKind> issuanceCandidates;
            IDictionary<string, string> collectionCandidates;
            try
            {
                population = TypeRolePopulation.Resolve(ledger, inventory, excluded);
                eventArgumentTypes = TypeRoleEvidence.EventArgumentTypes(inventory);
                connectorCandidates = TypeRoleEvidence.ConnectorCandidates(
                    inventory, TypeRoleEvidence.ConnectionRoots);
                connectionPaths = TypeRoleEvidence.ReachableFromRoots(
                    inventory, TypeRoleEvidence.ConnectionRoots);
                IDictionary<string, TypeRole> roles = table.Types.ToDictionary(
                    r => r.TypeName, r => r.Role, StringComparer.Ordinal);
                issuanceCandidates = HandleIssuanceEvidence.Candidates(
                    inventory, roles, population.Signatures);
                collectionCandidates = ElementCollectionEvidence.Candidates(
                    inventory, roles, population.Signatures);
            }
            catch (Exception exception)
                when (exception is InvalidOperationException || exception is ArgumentException)
            {
                error.WriteLine("役割の根拠を決められない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            try
            {
                TypeRoleGate.Require(
                    table,
                    population.RoleTypes,
                    TypeRoleEvidence.ConnectionRoots,
                    eventArgumentTypes,
                    connectorCandidates,
                    connectionPaths,
                    issuanceCandidates,
                    collectionCandidates);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("型役割が規則に合わない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: 型 {0} 件(コネクタ {1}・イベント引数 {2}・ハンドル操作 {3}・操作対象 {4}"
                    + "・DTO {5})・ハンドルを返しうる行 {6} 件(発行 {7})"
                    + "・要素のリスト {8} 件(所有 {9})",
                table.Types.Count,
                table.Types.Count(r => r.Role == TypeRole.Connector),
                table.Types.Count(r => r.Role == TypeRole.EventArgs),
                table.Types.Count(r => r.Role == TypeRole.HandleTarget),
                table.Types.Count(r => r.Role == TypeRole.OperationTarget),
                table.Types.Count(r => r.Role == TypeRole.Dto),
                table.Issuances.Count,
                table.Issuances.Count(r => r.Issues),
                table.Collections.Count,
                table.Collections.Count(r => r.Owns)));

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
