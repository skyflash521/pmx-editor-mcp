using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表のツールの名前を規則と照合する配線。ファイルは書き出さず、合否だけを終了コードで
    /// 返す。
    /// </summary>
    public static class ToolNameRunner
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
                    "引数は3つ: <PMXエディタ導入ディレクトリ> <型役割表の正本のパス>"
                        + " <能力対応表の正本のパス>");
                return ExitCodes.InvalidArguments;
            }

            string editorDirectory = args[0];
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            if (!File.Exists(assemblyPath))
            {
                error.WriteLine("対象のアセンブリが無い: " + assemblyPath);
                return ExitCodes.InputUnavailable;
            }

            TypeRoleTable roles;
            ToolMap map;
            try
            {
                roles = TypeRoleTableJsonReader.ReadTypeRoles(Read(args[1], "型役割表の正本"));
                map = ToolMapJsonReader.Read(Read(args[2], "能力対応表の正本"));
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
                ToolNameGate.Require(
                    map,
                    roles,
                    inventory.Signatures.ToDictionary(s => s.Key, s => s, StringComparer.Ordinal));
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("ツールの名前が規則に合わない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: ツールを持つ行 {0} 件・埋め込み先 {1} 件",
                map.Rows.Count(r => r.Tool != null),
                map.Rows.Sum(r => r.EmbeddedIn == null ? 0 : r.EmbeddedIn.Count)));

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
