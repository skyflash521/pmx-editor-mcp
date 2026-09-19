using System;
using System.IO;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>実行1回ぶんの配線。台帳を読み、組み込むC#の本文を書き出す。</summary>
    public static class UiStructureSourceRunner
    {
        /// <summary>
        /// 実行する。引数は台帳のパスと書き出し先パスの2つ。結果は書き出し先へBOMなしUTF-8で書く。
        /// </summary>
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
                error.WriteLine("引数は2つ: <画面の構造の台帳のパス> <書き出し先パス>");
                return ExitCodes.InvalidArguments;
            }

            string catalogPath = args[0];
            string outputPath = args[1];
            if (!File.Exists(catalogPath))
            {
                error.WriteLine("台帳が無い: " + catalogPath);
                return ExitCodes.InputUnavailable;
            }

            string built;
            try
            {
                built = UiStructureSourceBuilder.Build(File.ReadAllText(catalogPath, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                error.WriteLine("台帳を読めない: " + catalogPath);
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            if (Same(outputPath, built))
            {
                output.WriteLine("同じ本文が在る: " + outputPath);

                return ExitCodes.Success;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
                File.WriteAllText(outputPath, built, new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                error.WriteLine("結果を書き出せない: " + outputPath);
                error.WriteLine(exception.Message);
                return ExitCodes.WriteFailed;
            }

            output.WriteLine("書き出した: " + outputPath);

            return ExitCodes.Success;
        }

        /// <summary>書き出し先に同じ本文が既に在るか。読めないときは在らないものとして扱う。</summary>
        private static bool Same(string outputPath, string built)
        {
            try
            {
                return File.Exists(outputPath)
                    && string.Equals(
                        File.ReadAllText(outputPath, Encoding.UTF8), built, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
