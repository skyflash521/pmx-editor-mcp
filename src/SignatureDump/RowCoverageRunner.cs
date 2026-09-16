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
        /// <summary>
        /// 届かせられない理由のうち、組み立ての側の限界を述べるものが含む語。分けて数えるのは、
        /// 手立てを足せば届く行と、正本へ値を足せば届く行とで、足す相手が違うからである。
        /// </summary>
        private static readonly string[] GeneratorLimits = { "この検査", "この生成器" };

        /// <summary>届かせられない理由のうち、正本の側の欠けを述べるものが含む語。</summary>
        private const string MissingSource = "正本";

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
                    inputs.UnkeptMembers,
                    inputs.HandledTypes(),
                    inputs.PickingRows(inventory),
                    inputs.ReceiverPaths(inventory),
                    inputs.ElementAdders(inventory));
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

            ToolMapRow[] unreachable = inputs.Map.Rows
                .Where(r => r.Basis.IndexOf(
                    E2eCaseBuilder.UnreachableReason, StringComparison.Ordinal) >= 0)
                .ToArray();
            int limited = unreachable.Count(Limited);
            int missing = unreachable.Count(r => !Limited(r) && Missing(r));

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: 行 {0} 件・検査 {1} 件・ツール {2} 件。"
                    + "届かせられない {3} 件(生成器の限界 {4}・正本の欠け {5}・ほか {6})、"
                    + "確認の表示で止まる {7} 件",
                inputs.Map.Rows.Count,
                cases.Count,
                cases.Select(c => c.Tool).Distinct(StringComparer.Ordinal).Count(),
                unreachable.Length,
                limited,
                missing,
                unreachable.Length - limited - missing,
                inputs.Map.Rows.Count(E2eCaseBuilder.Prompts)));

            return ExitCodes.Success;
        }

        /// <summary>
        /// 届かせられない理由が、いまの組み立てでは届かせようがないと述べているか。受け手を作る
        /// 手立てを足せば届くようになる行がこちらで、足す仕事は検査を増やす側にある。
        /// </summary>
        public static bool Limited(ToolMapRow row)
        {
            return GeneratorLimits.Any(
                word => row.Basis.IndexOf(word, StringComparison.Ordinal) >= 0);
        }

        /// <summary>
        /// 届かせられない理由が、正本の側に値が無いと述べているか。正本へ値を足せば届くように
        /// なる行がこちらで、足す仕事は正本を書く側にある。
        /// </summary>
        public static bool Missing(ToolMapRow row)
        {
            return row.Basis.IndexOf(MissingSource, StringComparison.Ordinal) >= 0;
        }
    }
}
