using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PmxEditorMcp.SignatureDump.Tests.Sample;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class CommandRunnerTests : IDisposable
    {
        // 凍結の対象になる能力の行を、実台帳からそのまま採る。凍結が前提にするのは分類と対象の欄、
        // それに分類が提供の能力の備考で、そのいずれかが違えば実物では止まる。
        private static readonly string[][] LedgerRows =
        {
            new[] { "CAP-114", "PmxView", "IPXPmxViewConnector.BootupVmdView", "提供", "変形・モーション" },
            new[] { "CAP-269", "Cプラグイン連携", "IPXSystemControl.GetCPluginInfo", "提供", "セッション" },
            new[] { "CAP-304", "Cプラグイン連携", "IPXUIModel.SetAutoRelease", "非対応", "" },
            new[] { "CAP-339", "モデルデータ型", "IPXPmx", "提供", "モデル" },
            new[] { "CAP-390", "VMD/VMEビルダ", "IPEBuilder.CreateVmd", "提供", "変形・モーション" },
            new[] { "CAP-398", "VMD/VMEビルダ", "IPEBuilder.CreateVme", "提供", "変形・モーション" },
            new[] { "CAP-459", "プラグイン機構", "IPEPlugin / PEPluginClass / PEPluginOption / IPERunArgs / PECheckResult", "非対応", "" },
            new[] { "CAP-461", "ビルダ別経路", "PEStaticBuilder / IPEShortBuilder", "非対応", "" },
            new[] { "CAP-462", "プラグイン拡張点", "IPECheckerPlugin / IPEImportPlugin / IPEExportPlugin", "非対応", "" },
            new[] { "CAP-463", "PMDレガシー", "PEPlugin.Pmd.* のコネクタ・データ型と IPEBuilder のPMD/X系生成", "非対応", "" },
            new[] { "CAP-465", "Cプラグイン実装拡張点", "PXCPlugin.RegisterBase / IPXCPlugin / PXCPluginClass", "非対応", "" },
            new[] { "CAP-466", "SDX数値型", "PEPlugin.SDX.*(M・Q・V2・V3・V4)", "非対応", "" },
        };

        /// <summary>行ごとの備考。台帳の本文をそのまま使う。</summary>
        private static readonly Dictionary<string, string> LedgerRemarks =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
            { "CAP-114", "PMX+VMD版と引数なし版を対象。契約注記: PMDを引数に取る版はレガシーのため対象外" },
            { "CAP-269", "Int32版を提供。契約注記: IPXCPluginを引数に取る版は対象外。取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨" },
            { "CAP-304", "引数のIPXCPlugin(実装拡張点・非対応)を取得経路から得られないため" },
            { "CAP-339", "全公開メンバー(型単位)。契約注記: FromStream/ToStreamはファイルパス版で代替し対象外" },
            { "CAP-390", "他のオーバーロードを提供。契約注記: PMDを引数に取る版はレガシーのため対象外" },
            { "CAP-398", "契約注記: PMDを引数に取る版はレガシーのため対象外" },
            { "CAP-459", "プラグイン自身がホストに登録されるための実装専用API" },
            { "CAP-461", "IPXPmxBuilder等の提供経路と重複する短絡経路のため" },
            { "CAP-462", "プラグインDLL側の拡張点(MCPからの呼び出し対象ではない)" },
            { "CAP-463", "PMX系に同等機能。PMDファイル入出力はFormコネクタの能力として提供" },
            { "CAP-465", "Cプラグインを実装する側の基底クラス・エントリポイント(実装専用)" },
            { "CAP-466", "SlimDX数値型の橋渡し型。演算メンバーはモデル状態に作用せずクライアント側で完結する数値計算のため対象外。値の受け渡しはJSON数値配列(共通契約仕様書が定める)" },
            };

        /// <summary>題材のアセンブリが必ず持つシグネチャ。列挙が実際に走ったことを見分ける。</summary>
        private const string SampleSignature =
            "\"key\":\"PmxEditorMcp.SignatureDump.Tests.Sample.ISampleApi.GetCount()\"";

        /// <summary>先に置いた書き出し先が、失敗した実行で変わっていないことを見るための内容。</summary>
        private const string Existing = "{\"types\":[]}\n";

        private readonly string _root;

        public CommandRunnerTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-command-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
                // 取り除けなくてもテストの結果は変わらない。一時ディレクトリなので放置してよい。
            }
        }

        // 導入ディレクトリの形をまねた一時ディレクトリを作り、対象アセンブリの位置へこの
        // テストアセンブリ自身を置く。実物のSDKを持ち込まずに、入口から書き出しまでを通せる。
        private string CreateEditorDirectory()
        {
            string editorDirectory = Path.Combine(_root, "editor");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            if (!File.Exists(assemblyPath))
            {
                File.Copy(new Uri(typeof(ISampleApi).Assembly.CodeBase).LocalPath, assemblyPath);
            }

            return editorDirectory;
        }

        private string CreateLedger()
        {
            string path = Path.Combine(_root, "ledger.md");
            File.WriteAllText(
                path,
                "| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |\n|---|---|---|---|---|---|\n"
                + string.Concat(LedgerRows.Select(
                    r => "| " + string.Join(" | ", r) + " | " + LedgerRemarks[r[0]] + " |\n")));
            return path;
        }

        [Fact]
        public void 下位コマンドを渡さないと引数の誤りで終わる()
        {
            StringWriter error = new StringWriter();

            int code = CommandRunner.Run(new string[0], new StringWriter(), error);

            Assert.Equal(ExitCodes.InvalidArguments, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
        }

        [Fact]
        public void 下位コマンドの名前は呼び出し側との約束である()
        {
            // 名前は外から打つ文字列そのものなので、定数と実装を揃えて変えても気づけるようにする。
            Assert.Equal("signatures", CommandRunner.SignaturesCommand);
            Assert.Equal("excluded-baseline", CommandRunner.ExcludedBaselineCommand);
            Assert.Equal("excluded-signatures", CommandRunner.ExcludedSignaturesCommand);
            Assert.Equal("ledger-coverage", CommandRunner.LedgerCoverageCommand);
        }

        [Fact]
        public void 照合の下位コマンドは台帳と正本の照合を実行する()
        {
            string ledgerPath = Path.Combine(_root, "ledger.md");
            File.WriteAllText(
                ledgerPath,
                "| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |" + Environment.NewLine
                    + "|---|---|---|---|---|---|" + Environment.NewLine);
            string baselinePath = Path.Combine(_root, "excluded-baseline.json");
            File.WriteAllText(baselinePath, "{\"capabilities\":[]}");
            string excludedPath = Path.Combine(_root, "excluded-signatures.json");
            File.WriteAllText(excludedPath, "{\"signatures\":[]}");
            string outOfScopePath = Path.Combine(_root, "out-of-scope.json");
            File.WriteAllText(outOfScopePath, "{\"types\":[],\"signatures\":[]}");

            int code = CommandRunner.Run(
                new[]
                {
                    CommandRunner.LedgerCoverageCommand,
                    CreateEditorDirectory(),
                    ledgerPath,
                    baselinePath,
                    excludedPath,
                    outOfScopePath,
                },
                new StringWriter(),
                new StringWriter());

            Assert.Equal(ExitCodes.Unresolved, code);

            int missingCode = CommandRunner.Run(
                new[]
                {
                    CommandRunner.LedgerCoverageCommand,
                    Path.Combine(_root, "none"),
                    ledgerPath,
                    baselinePath,
                    excludedPath,
                    outOfScopePath,
                },
                new StringWriter(),
                new StringWriter());

            Assert.Equal(ExitCodes.InputUnavailable, missingCode);
        }

        [Fact]
        public void 除外一覧の下位コマンドは除外一覧の書き出しを実行する()
        {
            string baselinePath = Path.Combine(_root, "excluded-baseline.json");
            File.WriteAllText(
                baselinePath,
                "{\"capabilities\":[{\"capabilityId\":\"CAP-1\",\"signatures\":[\"T.Removed()\"]}]}");
            string outputPath = Path.Combine(_root, "excluded-signatures.json");

            int code = CommandRunner.Run(
                new[] { CommandRunner.ExcludedSignaturesCommand, CreateEditorDirectory(), baselinePath, outputPath },
                new StringWriter(),
                new StringWriter());

            Assert.Equal(ExitCodes.Unresolved, code);

            int missingCode = CommandRunner.Run(
                new[]
                {
                    CommandRunner.ExcludedSignaturesCommand,
                    Path.Combine(_root, "none"),
                    baselinePath,
                    outputPath,
                },
                new StringWriter(),
                new StringWriter());

            Assert.Equal(ExitCodes.InputUnavailable, missingCode);
        }

        [Fact]
        public void 使用法はすべての下位コマンドと引数の並びを示す()
        {
            StringWriter error = new StringWriter();

            CommandRunner.Run(new string[0], new StringWriter(), error);
            string usage = error.ToString();

            Assert.Contains(
                CommandRunner.SignaturesCommand + " <PMXエディタ導入ディレクトリ> <書き出し先パス>",
                usage,
                StringComparison.Ordinal);
            Assert.Contains(
                CommandRunner.ExcludedBaselineCommand
                    + " <PMXエディタ導入ディレクトリ> <能力台帳のパス> <書き出し先パス>",
                usage,
                StringComparison.Ordinal);
            Assert.Contains(
                CommandRunner.ExcludedSignaturesCommand
                    + " <PMXエディタ導入ディレクトリ> <ベースライン正本のパス> <書き出し先パス>",
                usage,
                StringComparison.Ordinal);
            Assert.Contains(
                CommandRunner.LedgerCoverageCommand
                    + " <PMXエディタ導入ディレクトリ> <能力台帳のパス> <ベースライン正本のパス>"
                    + " <除外一覧のパス> <対象外一覧のパス>",
                usage,
                StringComparison.Ordinal);
        }

        [Fact]
        public void 終了コードの値は呼び出し側との約束である()
        {
            // 値は呼び出し元が見分けに使うものそのもの。重ねてしまうと、直し方の違う失敗が同じに見える。
            Assert.Equal(0, ExitCodes.Success);
            Assert.Equal(2, ExitCodes.InvalidArguments);
            Assert.Equal(3, ExitCodes.InputUnavailable);
            Assert.Equal(4, ExitCodes.WriteFailed);
            Assert.Equal(5, ExitCodes.Unresolved);
        }

        [Fact]
        public void 知らない下位コマンドは引数の誤りで終わる()
        {
            // 後ろの引数は列挙として正しい形にする。名前を見ずに引数の数で振り分ける作りでは、
            // これが列挙として実行されてしまう。
            string outputPath = Path.Combine(_root, "signatures.json");
            File.WriteAllText(outputPath, Existing);
            StringWriter error = new StringWriter();

            int code = CommandRunner.Run(
                new[] { "signature", CreateEditorDirectory(), outputPath }, new StringWriter(), error);

            Assert.Equal(ExitCodes.InvalidArguments, code);
            Assert.Contains("signature", error.ToString(), StringComparison.Ordinal);
            Assert.Equal(Existing, File.ReadAllText(outputPath));

            // 凍結として正しい形の後ろの引数でも同じ。名前を見ない作りを、どちらの引数の数でも弾く。
            StringWriter longerError = new StringWriter();

            int longer = CommandRunner.Run(
                new[] { "baseline", CreateEditorDirectory(), CreateLedger(), outputPath },
                new StringWriter(),
                longerError);

            Assert.Equal(ExitCodes.InvalidArguments, longer);
            Assert.Contains("baseline", longerError.ToString(), StringComparison.Ordinal);
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact]
        public void 列挙の下位コマンドは列挙を実行する()
        {
            string outputPath = Path.Combine(_root, "signatures.json");
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();

            int code = CommandRunner.Run(
                new[] { CommandRunner.SignaturesCommand, CreateEditorDirectory(), outputPath },
                output,
                error);

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains(SampleSignature, File.ReadAllText(outputPath), StringComparison.Ordinal);

            // 報告先をそのまま渡さないと、実行の要約や理由が呼び出し元へ届かない。
            Assert.False(string.IsNullOrWhiteSpace(output.ToString()));
            Assert.Equal(string.Empty, error.ToString());

            StringWriter failedError = new StringWriter();

            int failed = CommandRunner.Run(
                new[] { CommandRunner.SignaturesCommand, Path.Combine(_root, "none"), outputPath },
                new StringWriter(),
                failedError);

            Assert.Equal(ExitCodes.InputUnavailable, failed);
            Assert.False(string.IsNullOrWhiteSpace(failedError.ToString()));
        }

        [Fact]
        public void 凍結の下位コマンドは凍結を実行する()
        {
            // 題材のアセンブリには台帳が指す先が無いので、突き合わせで止まるところまでを見る。
            string outputPath = Path.Combine(_root, "excluded-baseline.json");
            StringWriter error = new StringWriter();

            int code = CommandRunner.Run(
                new[] { CommandRunner.ExcludedBaselineCommand, CreateEditorDirectory(), CreateLedger(), outputPath },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("指す先が無い", error.ToString(), StringComparison.Ordinal);

            // 入力を変えると結果も変わる。振り分けずに決まった終了コードを返す作りでは、
            // 導入ディレクトリを変えても同じ結果になってしまう。
            string missing = Path.Combine(_root, "none");
            StringWriter missingError = new StringWriter();

            int missingCode = CommandRunner.Run(
                new[] { CommandRunner.ExcludedBaselineCommand, missing, CreateLedger(), outputPath },
                new StringWriter(),
                missingError);

            Assert.Equal(ExitCodes.InputUnavailable, missingCode);
            Assert.Contains(missing, missingError.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void 実行ファイルの入口が列挙を通す()
        {
            string outputPath = Path.Combine(_root, "from-main.json");
            StringWriter summary = new StringWriter();
            TextWriter previous = Console.Out;
            int code;
            try
            {
                Console.SetOut(summary);
                code = Program.Main(
                    new[] { CommandRunner.SignaturesCommand, CreateEditorDirectory(), outputPath });
            }
            finally
            {
                Console.SetOut(previous);
            }

            Assert.Equal(ExitCodes.Success, code);
            Assert.False(string.IsNullOrWhiteSpace(summary.ToString()));
            Assert.Contains(SampleSignature, File.ReadAllText(outputPath), StringComparison.Ordinal);
        }

        [Fact]
        public void 実行ファイルの入口が凍結を通す()
        {
            // 入口が下位コマンドを1つだけ特別に扱っていると、他の下位コマンドへ実行ファイルから
            // 到達できない。列挙の経路とは別に見るので、片方が落ちてももう片方の結果が分かる。
            StringWriter error = new StringWriter();
            TextWriter previous = Console.Error;
            int code;
            try
            {
                Console.SetError(error);
                code = Program.Main(new[]
                {
                    CommandRunner.ExcludedBaselineCommand,
                    CreateEditorDirectory(),
                    CreateLedger(),
                    Path.Combine(_root, "from-main-baseline.json"),
                });
            }
            finally
            {
                Console.SetError(previous);
            }

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("指す先が無い", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void 実行ファイルの入口でも入力の違いが結果に出る()
        {
            // 入口が決まった終了コードを返すだけでは、導入ディレクトリを変えても同じ結果になる。
            string missing = Path.Combine(_root, "none");
            StringWriter error = new StringWriter();
            TextWriter previous = Console.Error;
            int code;
            try
            {
                Console.SetError(error);
                code = Program.Main(new[]
                {
                    CommandRunner.ExcludedBaselineCommand,
                    missing,
                    CreateLedger(),
                    Path.Combine(_root, "from-main-missing.json"),
                });
            }
            finally
            {
                Console.SetError(previous);
            }

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains(missing, error.ToString(), StringComparison.Ordinal);
        }
    }
}
