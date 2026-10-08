using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// ツールの説明文を組み立てて規則と照合する配線。ファイルは書き出さず、合否だけを終了コードで
    /// 返す。上限に入りきらなかった項目は出力へ並べる。
    /// </summary>
    public static class ToolDescriptionRunner
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
                        + " <スキーマ正本のパス>");
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
            try
            {
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

            IList<ToolDescriptionMaterial> materials;
            IDictionary<string, ToolDescription> descriptions;
            try
            {
                materials = inputs.DescriptionMaterials(inventory);
                descriptions = inputs.DescriptionsFrom(materials);
                ToolDescriptionGate.Require(materials, descriptions, inputs.ComposedTools.Keys);
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("ツールの説明文が規則に合わない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            foreach (KeyValuePair<string, ToolDescription> description in descriptions
                .Where(d => d.Value.Dropped.Count != 0)
                .OrderBy(d => d.Key, StringComparer.Ordinal))
            {
                output.WriteLine(
                    "載せきれない索引語: " + description.Key + " "
                        + string.Join("・", description.Value.Dropped.ToArray()));
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: ツール {0} 件・索引語 {1} 件・最大 {2} バイト",
                descriptions.Count,
                materials.Sum(m => m.IndexTerms.Count),
                descriptions.Count == 0
                    ? 0
                    : descriptions.Values.Max(d => Encoding.UTF8.GetByteCount(d.Text))));

            return ExitCodes.Success;
        }
    }
}
