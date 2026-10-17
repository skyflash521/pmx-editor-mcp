using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>配布物が実行時リフレクションを持たないことを照合する配線。</summary>
    public static class ReflectionFreeRunner
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

            if (args.Length != 2)
            {
                error.WriteLine("引数は2つ: <PMXエディタ導入ディレクトリ> <検査するアセンブリのパス>");
                return ExitCodes.InvalidArguments;
            }

            if (!Directory.Exists(args[0]))
            {
                error.WriteLine("PMXエディタ導入ディレクトリが無い: " + args[0]);
                return ExitCodes.InputUnavailable;
            }

            if (!File.Exists(args[1]))
            {
                error.WriteLine("検査するアセンブリが無い: " + args[1]);
                return ExitCodes.InputUnavailable;
            }

            ReflectionScan scan;
            try
            {
                scan = SdkInventory.Read(
                    args[0], args[1], assembly => ReflectionFreeGate.Scan(assembly.ManifestModule));
            }
            catch (Exception exception)
            {
                error.WriteLine("検査するアセンブリを読めない: " + args[1]);
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            if (scan.Found.Count > 0)
            {
                error.WriteLine("名前で型やメンバーを引く経路がある。");
                foreach (string reference in scan.Found)
                {
                    error.WriteLine("  " + reference);
                }

                return ExitCodes.Unresolved;
            }

            if (scan.Unreadable.Count > 0)
            {
                error.WriteLine("綴りを取れないメンバー参照があり、判じられていない。");
                foreach (int row in scan.Unreadable)
                {
                    error.WriteLine("  メンバー参照の表の行 " + row.ToString(CultureInfo.InvariantCulture));
                }

                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture, "実行時リフレクションは無い: {0}", args[1]));

            return ExitCodes.Success;
        }
    }
}
