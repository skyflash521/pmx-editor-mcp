using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表の行が実機の検査に覆われることを照合する配線。ファイルは書き出さず、合否だけを
    /// 終了コードで返す。
    /// </summary>
    public static class RowCoverageRunner
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
                    "引数は9個: <PMXエディタ導入ディレクトリ> <能力台帳のパス>"
                        + " <共通契約の正本のパス>"
                        + " <型役割表の正本のパス> <日本語名の正本のパス>"
                        + " <共通契約割当の正本のパス> <能力対応表の正本のパス>"
                        + " <スキーマ正本のパス> <サンプル値の正本のパス>");
                return ExitCodes.InvalidArguments;
            }

            string editorDirectory = args[0];
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            if (!File.Exists(assemblyPath))
            {
                error.WriteLine("対象のアセンブリが無い: " + assemblyPath);
                return ExitCodes.InputUnavailable;
            }

            ToolDefinitionInputs inputs;
            SampleValueTable samples;
            try
            {
                samples = SampleValueJsonReader.Read(
                    File.ReadAllText(args[8], new UTF8Encoding(false)));
                inputs = ToolDefinitionInputs.Read(editorDirectory, args);
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

            IList<E2eCase> cases;
            try
            {
                cases = E2eCaseBuilder.Build(
                    inputs.Map,
                    inputs.Schemas,
                    inputs.ToolsByRow(inventory),
                    inputs.ConnectionPaths(inventory),
                    inputs.Dangerous(inventory),
                    inputs.SdkShapes(inventory),
                    inputs.SdkTypes(inventory),
                    samples,
                    inputs.ViewImages,
                    inputs.PositionedTypes(),
                    inputs.ElementFactories(inventory),
                    inputs.Readers(inventory),
                    inputs.UnkeptMembers);
                RowCoverageGate.Require(
                    inputs.Map,
                    inventory.Signatures.ToDictionary(
                        s => s.Key, s => s, StringComparer.Ordinal),
                    inputs.ToolsByRow(inventory),
                    inputs.ComposedTools,
                    inputs.Assignments,
                    inputs.OwnedRoles(inventory),
                    ElementPathEvidence.Traversed(inventory, inputs.Roles),
                    cases);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("行が実機の検査に覆われない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: 行 {0} 件・検査 {1} 件・ツール {2} 件",
                inputs.Map.Rows.Count,
                cases.Count,
                cases.Select(c => c.Tool).Distinct(StringComparer.Ordinal).Count()));

            return ExitCodes.Success;
        }
    }
}
