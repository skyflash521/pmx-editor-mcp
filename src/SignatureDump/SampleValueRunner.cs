using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 型ごとのサンプル値の正本を規則と照合する配線。ファイルは書き出さず、合否だけを終了
    /// コードで返す。
    /// </summary>
    public static class SampleValueRunner
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

            if (args.Length != 3)
            {
                error.WriteLine(
                    "引数は3つ: <PMXエディタ導入ディレクトリ> <共通契約仕様書のパス>"
                        + " <サンプル値の正本のパス>");
                return ExitCodes.InvalidArguments;
            }

            string editorDirectory = args[0];
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            if (!File.Exists(assemblyPath))
            {
                error.WriteLine("対象のアセンブリが無い: " + assemblyPath);
                return ExitCodes.InputUnavailable;
            }

            IList<ValueShapeRow> shapes;
            IDictionary<string, int> components;
            SampleValueTable table;
            try
            {
                string contract = Read(args[1], "共通契約仕様書");
                shapes = ValueShapeDocument.Read(contract);
                components = ValueShapeDocument.ReadComponents(contract);
                table = SampleValueJsonReader.Read(Read(args[2], "サンプル値の正本"));
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
                SampleValueGate.Require(table, shapes, components, EnumMembers(inventory));
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("サンプル値が規則に合わない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: 型 {0} 件・成分の数 {1} 件",
                table.Types.Count,
                components.Count));

            return ExitCodes.Success;
        }

        private static IDictionary<string, EnumMemberSet> EnumMembers(InventoryRecord inventory)
        {
            Dictionary<string, EnumMemberSet> members =
                new Dictionary<string, EnumMemberSet>(StringComparer.Ordinal);
            foreach (TypeRecord type in inventory.Types.Concat(inventory.ReferencedTypes)
                .Where(t => t.Kind == TypeKind.Enum))
            {
                members[type.Name] = new EnumMemberSet(
                    new HashSet<string>(type.EnumMembers, StringComparer.Ordinal),
                    type.IsCombinable);
            }

            // 書体の飾りは配布物でなく実行環境の枠組みが持つ列挙なので、そちらから引く。
            members[typeof(FontStyle).FullName] = new EnumMemberSet(
                new HashSet<string>(Enum.GetNames(typeof(FontStyle)), StringComparer.Ordinal),
                typeof(FontStyle).IsDefined(typeof(FlagsAttribute), false));

            return members;
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
