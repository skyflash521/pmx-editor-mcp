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

            if (args.Length != 8)
            {
                error.WriteLine(
                    "引数は8つ: <PMXエディタ導入ディレクトリ> <能力台帳のパス>"
                        + " <共通契約の正本のパス> <型役割表の正本のパス> <日本語名の正本のパス>"
                        + " <共通契約割当の正本のパス> <能力対応表の正本のパス>"
                        + " <用途の作業の正本のパス>");
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
            IList<CapabilityRecord> ledger;
            TypeRoleTable roles;
            IList<PropertyNameRecord> names;
            CommonAssignmentTable assignments;
            ToolMap map;
            IDictionary<string, ComposedTool> composedTools;
            IDictionary<string, string> methodNotes;
            IDictionary<string, string> propertyNotes;
            try
            {
                tasks = DiscoveryTaskJsonReader.Read(Read(args[7], "用途の作業の正本"));
                ledger = LedgerJsonReader.Read(Read(args[1], "能力台帳"));
                composedTools = CommonContractJsonReader
                    .Read(Read(args[2], "共通契約の正本")).ComposedTools;
                roles = TypeRoleTableJsonReader.ReadTypeRoles(Read(args[3], "型役割表の正本"));
                names = PropertyNameJsonReader.ReadPropertyNames(Read(args[4], "日本語名の正本"));
                assignments = CommonAssignmentJsonReader.Read(Read(args[5], "共通契約割当の正本"));
                map = ToolMapJsonReader.Read(Read(args[6], "能力対応表の正本"));
                string document = Read(
                    SdkAssemblyLocator.GetDocumentPath(editorDirectory), "ドキュメントXML");
                methodNotes = DocumentNoteReader.ReadMethods(document);
                propertyNotes = DocumentNoteReader.Read(document);
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

            Dictionary<string, string> descriptions =
                new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                TypeRoleTable owned = TypeGroupRule.Resolve(
                    roles, TypeGroupEvidence.OwnersByType(ledger, inventory));
                IDictionary<string, SignatureRecord> signatures = inventory.Signatures
                    .ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);
                foreach (ToolDescriptionMaterial material in ToolDescriptionEvidence.Collect(
                    map,
                    owned,
                    names,
                    inventory,
                    ToolNameEvidence.Resolve(map, owned, assignments, inventory),
                    ToolMapEvidence.ContractNotesBySignature(
                        ToolMapEvidence.ProvidedOwners(
                            LedgerPopulation.Resolve(ledger, inventory).Owners, ledger),
                        ToolMapEvidence.ContractNotes(ledger)),
                    methodNotes,
                    propertyNotes))
                {
                    descriptions.Add(material.Tool, ToolDescriptionRule.Compose(material).Text);
                }

                foreach (KeyValuePair<string, ComposedTool> composed in composedTools)
                {
                    descriptions[composed.Key] = composed.Value.Duty;
                }

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
