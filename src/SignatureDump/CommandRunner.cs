using System;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 台帳と公開APIの突き合わせは工程ごとに別の入力と出力を持つので、下位コマンドの名前で
    /// 実行を振り分ける。
    /// </summary>
    public static class CommandRunner
    {
        /// <summary>SDKの公開APIを列挙して書き出す。</summary>
        public const string SignaturesCommand = "signatures";

        /// <summary>台帳の非対応記載を公開シグネチャの集合として凍結して書き出す。</summary>
        public const string ExcludedBaselineCommand = "excluded-baseline";

        /// <summary>凍結した組と列挙から除外一覧を確定して書き出す。</summary>
        public const string ExcludedSignaturesCommand = "excluded-signatures";

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

            if (args.Length == 0)
            {
                error.WriteLine("下位コマンドを指定する。");
                WriteUsage(error);
                return ExitCodes.InvalidArguments;
            }

            string[] rest = args.Skip(1).ToArray();
            if (string.Equals(args[0], SignaturesCommand, StringComparison.Ordinal))
            {
                return SignatureDumpRunner.Run(rest, output, error);
            }

            if (string.Equals(args[0], ExcludedBaselineCommand, StringComparison.Ordinal))
            {
                return ExcludedBaselineRunner.Run(rest, output, error);
            }

            if (string.Equals(args[0], ExcludedSignaturesCommand, StringComparison.Ordinal))
            {
                return ExcludedSignatureRunner.Run(rest, output, error);
            }

            error.WriteLine("知らない下位コマンド: " + args[0]);
            WriteUsage(error);
            return ExitCodes.InvalidArguments;
        }

        private static void WriteUsage(TextWriter error)
        {
            error.WriteLine(SignaturesCommand + " <PMXエディタ導入ディレクトリ> <書き出し先パス>");
            error.WriteLine(
                ExcludedBaselineCommand + " <PMXエディタ導入ディレクトリ> <能力台帳のパス> <書き出し先パス>");
            error.WriteLine(
                ExcludedSignaturesCommand
                    + " <PMXエディタ導入ディレクトリ> <ベースライン正本のパス> <書き出し先パス>");
        }
    }
}
