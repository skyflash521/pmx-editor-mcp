using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 同梱する第三者ライセンス表示を、出荷台帳と転記元から組み立てて書き出す配線。
    /// 書き出す先は配布物の中で、追跡下には残さない——出荷する物を数えれば毎回同じ物が作れるので、
    /// 写しをバージョン管理に置いても読む相手がいない。
    /// </summary>
    public static class ThirdPartyNoticeRunner
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

            if (args.Length < 3)
            {
                error.WriteLine(
                    "引数は3個以上: <書き出し先パス> <標準の本文の置き場> <出荷台帳のパス>...");
                return ExitCodes.InvalidArguments;
            }

            string noticePath = args[0];
            string licenseDirectory = args[1];

            string built;
            int counted;
            try
            {
                ShippingLedger ledger = ShippingLedger.Read(Ledgers(args.Skip(2)));
                List<PackageLicenseSource> sources = ledger.Packages
                    .Select(package => PackageLicenseSource.Read(ledger.PackageRoot, package))
                    .ToList();
                built = ThirdPartyNoticeBuilder.Build(ledger, licenseDirectory, sources);
                counted = sources.Count;
            }
            catch (FormatException exception)
            {
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            // 出所のある物が1つも無い表示は、数え落としと見分けが付かない。自己完結で発行した
            // 実行ファイルは少なくともランタイムを取り込むので、空になるのは台帳が壊れたときである。
            if (counted == 0)
            {
                error.WriteLine("出荷台帳が第三者の出所を1つも挙げていない。");
                return ExitCodes.Unresolved;
            }

            try
            {
                string directory = Path.GetDirectoryName(noticePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(noticePath, built, new UTF8Encoding(false));
            }
            catch (IOException exception)
            {
                error.WriteLine("第三者ライセンス表示を書けない: " + exception.Message);
                return ExitCodes.WriteFailed;
            }

            output.WriteLine(
                "第三者ライセンス表示を書いた: " + noticePath + " (" + counted + "件)");
            return ExitCodes.Success;
        }

        private static List<string> Ledgers(IEnumerable<string> paths)
        {
            var read = new List<string>();
            foreach (string path in paths)
            {
                try
                {
                    read.Add(File.ReadAllText(path, Encoding.UTF8));
                }
                catch (IOException exception)
                {
                    throw new FormatException("出荷台帳を読めない: " + path, exception);
                }
            }

            return read;
        }
    }
}
