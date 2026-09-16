using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// スキーマ正本のツールが実機の検査に覆われることを照合する配線。ファイルは書き出さず、合否
    /// だけを終了コードで返す。
    /// </summary>
    public static class ToolCoverageRunner
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

            if (args.Length != 11)
            {
                error.WriteLine(
                    "引数は11個: <PMXエディタ導入ディレクトリ> <能力台帳のパス>"
                        + " <共通契約の正本のパス>"
                        + " <型役割表の正本のパス> <日本語名の正本のパス>"
                        + " <共通契約割当の正本のパス> <能力対応表の正本のパス>"
                        + " <スキーマ正本のパス> <サンプル値の正本のパス>"
                        + " <受入シナリオの正本のパス> <覆えないツールの正本のパス>");
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
            JsonNode scenarios;
            UncoveredToolTable uncoveredTools;
            try
            {
                samples = SampleValueJsonReader.Read(
                    File.ReadAllText(args[8], new UTF8Encoding(false)));
                inputs = ToolDefinitionInputs.Read(editorDirectory, args);
                scenarios = AcceptanceScenarioGate.Read(Read(args[9], "受入シナリオの正本"));
                uncoveredTools = UncoveredToolJsonReader.Read(
                    Read(args[10], "覆えないツールの正本"));
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

            ISet<string> succeeding = AcceptanceScenarioGate.SucceedingTools(scenarios);
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
                    inputs.UnkeptMembers,
                    inputs.HandledTypes(),
                    inputs.PickingRows(inventory),
                    inputs.ReceiverPaths(inventory),
                    inputs.ElementAdders(inventory),
                    inputs.ElementRemovers(inventory),
                    inputs.ConditionalDangerousTools(inventory),
                    inputs.TypePaths(inventory),
                    inputs.ElementParents(inventory));
                ToolCoverageGate.Require(
                    inputs.Schemas,
                    inputs.Map,
                    inputs.ToolsByRow(inventory),
                    cases,
                    succeeding,
                    uncoveredTools);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("ツールが実機の検査に覆われない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: ツール {0} 件・検査 {1} 件・受入シナリオが成功を期待するツール {2} 件",
                inputs.Schemas.Tools.Count,
                cases.Count,
                succeeding.Count));

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
