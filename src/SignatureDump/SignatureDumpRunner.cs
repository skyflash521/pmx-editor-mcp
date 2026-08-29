using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>実行1回ぶんの配線。引数の検査・アセンブリの読み込み・列挙・書き出しを順に行う。</summary>
    public static class SignatureDumpRunner
    {
        public const int ExitSuccess = 0;

        /// <summary>引数が足りない・多いときの終了コード。</summary>
        public const int ExitInvalidArguments = 2;

        public const int ExitAssemblyUnavailable = 3;

        public const int ExitWriteFailed = 4;

        /// <summary>
        /// 実行する。引数はPMXエディタ導入ディレクトリと書き出し先パスの2つ。結果は書き出し先へ
        /// BOMなしUTF-8で書く。成功したときは要約を <paramref name="output"/> へ書き、
        /// <paramref name="error"/> には何も書かない。失敗したときはその説明を
        /// <paramref name="error"/> へ書き、<paramref name="output"/> には何も書かない。
        ///
        /// 対象アセンブリはバイト列から読み込むので、実行が終わってもそのファイルは掴まない。
        /// 依存アセンブリはパスから読み込むため掴んだままになる——混在モードのアセンブリは
        /// バイト列から読み込めず、SDKが参照する描画ライブラリがこれに当たるためである。依存は
        /// 導入ディレクトリの実体を指すので、呼び出し元が消す対象にはならない。
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
                error.WriteLine("引数は2つ: <PMXエディタ導入ディレクトリ> <書き出し先パス>");
                return ExitInvalidArguments;
            }

            string editorDirectory = args[0];
            string outputPath = args[1];
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);

            if (!File.Exists(assemblyPath))
            {
                error.WriteLine("対象のアセンブリが無い: " + assemblyPath);
                return ExitAssemblyUnavailable;
            }

            IList<string> probeDirectories = SdkAssemblyLocator.GetProbeDirectories(editorDirectory);
            ResolveEventHandler resolver = (sender, e) =>
            {
                string found = SdkAssemblyLocator.FindDependency(new AssemblyName(e.Name).Name, probeDirectories);
                return found == null ? null : Assembly.LoadFrom(found);
            };

            InventoryRecord inventory;
            AppDomain.CurrentDomain.AssemblyResolve += resolver;
            try
            {
                inventory = AssemblyEnumerator.Enumerate(Assembly.Load(File.ReadAllBytes(assemblyPath)));
            }
            catch (Exception exception)
            {
                error.WriteLine("対象のアセンブリを列挙できない: " + assemblyPath);
                error.WriteLine(exception.Message);
                return ExitAssemblyUnavailable;
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolver;
            }

            try
            {
                File.WriteAllText(outputPath, InventoryJson.Write(inventory), new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                error.WriteLine("結果を書き出せない: " + outputPath);
                error.WriteLine(exception.Message);
                Discard(outputPath);
                return ExitWriteFailed;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "書き出した: {0}(型 {1} 件・シグネチャ {2} 件)",
                outputPath,
                inventory.Types.Count,
                inventory.Signatures.Count));

            return ExitSuccess;
        }

        // 途中まで書けたファイルを残すと、読み手が完全な結果と区別できない。取り除けないときは
        // 追加の報告をせず書き出し失敗の結果をそのまま返すので、部分的なファイルは残る。
        private static void Discard(string outputPath)
        {
            try
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
