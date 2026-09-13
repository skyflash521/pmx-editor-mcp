using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 提供対象のシグネチャと能力対応表の行の過不足を照合する配線。ファイルは書き出さず、合否だけを
    /// 終了コードで返す。
    /// </summary>
    public static class MapCoverageRunner
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
                        + " <型役割表の正本のパス> <能力対応表の正本のパス>");
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
            ToolMap map;
            try
            {
                ledger = LedgerJsonReader.Read(Read(args[1], "能力台帳"));
                excluded = ExcludedSignatureJsonReader.Read(Read(args[2], "除外一覧"));
                roles = TypeRoleTableJsonReader.ReadTypeRoles(Read(args[3], "型役割表の正本"));
                map = ToolMapJsonReader.Read(Read(args[4], "能力対応表の正本"));
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
            try
            {
                provided = TypeRolePopulation.Resolve(ledger, inventory, excluded).Signatures;
            }
            catch (Exception exception)
                when (exception is InvalidOperationException || exception is ArgumentException)
            {
                error.WriteLine("提供対象を決められない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            try
            {
                MapCoverageGate.Require(
                    provided,
                    inventory.Signatures.ToDictionary(s => s.Key, s => s, StringComparer.Ordinal),
                    ToolMapEvidence.EmbeddedTypeNames(roles),
                    map);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("提供対象と能力対応表の行が食い違う。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: 提供対象 {0} 件・行 {1} 件",
                provided.Count,
                map.Rows.Count));

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
