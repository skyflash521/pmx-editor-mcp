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

        /// <summary>台帳と正本が公開APIを過不足なく覆っていることを照合する。</summary>
        public const string LedgerCoverageCommand = "ledger-coverage";

        /// <summary>日本語名の正本が規則どおりに付いていることを照合する。</summary>
        public const string PropertyNamesCommand = "property-names";

        /// <summary>型役割表の正本が規則どおりに割り当てられていることを照合する。</summary>
        public const string TypeRolesCommand = "type-roles";

        /// <summary>共通契約割当の正本が規則どおりに割り当てられていることを照合する。</summary>
        public const string CommonAssignmentsCommand = "common-assignments";

        /// <summary>値の表現の表が、値として写せる型を過不足なく覆っていることを照合する。</summary>
        public const string ValueShapesCommand = "value-shapes";

        /// <summary>危険操作に当たるシグネチャが、決め方と台帳で一致していることを照合する。</summary>
        public const string DangerousOperationsCommand = "dangerous-operations";

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

            if (string.Equals(args[0], LedgerCoverageCommand, StringComparison.Ordinal))
            {
                return LedgerCoverageRunner.Run(rest, output, error);
            }

            if (string.Equals(args[0], PropertyNamesCommand, StringComparison.Ordinal))
            {
                return PropertyNameRunner.Run(rest, output, error);
            }

            if (string.Equals(args[0], TypeRolesCommand, StringComparison.Ordinal))
            {
                return TypeRoleRunner.Run(rest, output, error);
            }

            if (string.Equals(args[0], CommonAssignmentsCommand, StringComparison.Ordinal))
            {
                return CommonAssignmentRunner.Run(rest, output, error);
            }

            if (string.Equals(args[0], ValueShapesCommand, StringComparison.Ordinal))
            {
                return ValueShapeRunner.Run(rest, output, error);
            }

            if (string.Equals(args[0], DangerousOperationsCommand, StringComparison.Ordinal))
            {
                return DangerousOperationRunner.Run(rest, output, error);
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
            error.WriteLine(
                LedgerCoverageCommand
                    + " <PMXエディタ導入ディレクトリ> <能力台帳のパス> <除外一覧のパス>"
                    + " <対象外一覧のパス>");
            error.WriteLine(
                PropertyNamesCommand
                    + " <PMXエディタ導入ディレクトリ> <能力台帳のパス> <除外一覧のパス>"
                    + " <日本語名の正本のパス>");
            error.WriteLine(
                TypeRolesCommand
                    + " <PMXエディタ導入ディレクトリ> <能力台帳のパス> <除外一覧のパス>"
                    + " <型役割表の正本のパス>");
            error.WriteLine(
                CommonAssignmentsCommand
                    + " <PMXエディタ導入ディレクトリ> <能力台帳のパス> <除外一覧のパス>"
                    + " <型役割表の正本のパス> <共通契約割当の正本のパス>");
            error.WriteLine(
                ValueShapesCommand
                    + " <PMXエディタ導入ディレクトリ> <能力台帳のパス> <除外一覧のパス>"
                    + " <共通契約仕様書のパス>");
            error.WriteLine(
                DangerousOperationsCommand
                    + " <PMXエディタ導入ディレクトリ> <能力台帳のパス> <除外一覧のパス>");
        }
    }
}
