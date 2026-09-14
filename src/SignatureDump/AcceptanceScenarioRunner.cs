using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 受入シナリオの定義を、登録されるツール定義と要求仕様書へ突き合わせる配線。実機のエディタも
    /// MCPクライアントも要らないので、常設の検査から走らせられる。
    /// </summary>
    public static class AcceptanceScenarioRunner
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

            if (args.Length != 10)
            {
                error.WriteLine(
                    "引数は10個: <PMXエディタ導入ディレクトリ> <能力台帳のパス>"
                        + " <共通契約の正本のパス> <型役割表の正本のパス> <日本語名の正本のパス>"
                        + " <共通契約割当の正本のパス> <能力対応表の正本のパス>"
                        + " <スキーマ正本のパス> <受入シナリオの正本のパス> <要求仕様書のパス>");
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
            JsonNode scenarios;
            RequirementNames requirements;
            try
            {
                inputs = ToolDefinitionInputs.Read(editorDirectory, args);
                scenarios = AcceptanceScenarioGate.Read(Read(args[8], "受入シナリオの正本"));
                requirements = RequirementDocumentReader.Read(Read(args[9], "要求仕様書"));
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
                IDictionary<SchemaItem, string> sdkShapes = inputs.SdkShapes(inventory);
                IList<ToolDefinition> definitions = ToolDefinitionBuilder.Build(
                    inputs.Schemas,
                    inputs.Descriptions(inventory),
                    new AssumedLength(inputs.Lengths, sdkShapes),
                    inputs.BudgetChars - inputs.WarningChars,
                    inputs.RequestBytes,
                    inputs.TokenLimit,
                    sdkShapes,
                    inputs.DangerousTools(inventory),
                    inputs.ConditionalDangerousTools(inventory),
                    inputs.SuppressingTools(inventory),
                    inputs.DrawingTools());
                AcceptanceScenarioGate.Require(
                    scenarios,
                    definitions,
                    new HashSet<string>(
                        FixedToolTable.Descriptions(debugHooks: true).Keys, StringComparer.Ordinal),
                    requirements);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("受入シナリオが登録される定義と要求に合わない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            JsonArray listed = scenarios["scenarios"].AsArray();
            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: シナリオ {0} 本・段 {1} 件・作業 {2} 件",
                listed.Count,
                listed.Sum(s => s["steps"].AsArray().Count),
                requirements.Tasks.Count));

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
