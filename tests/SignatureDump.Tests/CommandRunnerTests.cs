using System;
using System.IO;
using System.Linq;
using PmxEditorMcp.SignatureDump.Tests.Sample;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class CommandRunnerTests : IDisposable
    {
        // 凍結の対象になる能力と、実物と同じ分類・担当。一部のシグネチャだけを対象外とする能力は
        // 分類が提供なので、非対応の行だけを渡す作りでは実物で取りこぼす。
        private static readonly string[][] LedgerRows =
        {
            new[] { "CAP-114", "提供", "変形・モーション" },
            new[] { "CAP-269", "提供", "セッション" },
            new[] { "CAP-304", "非対応", string.Empty },
            new[] { "CAP-339", "提供", "モデル" },
            new[] { "CAP-390", "提供", "変形・モーション" },
            new[] { "CAP-398", "提供", "変形・モーション" },
            new[] { "CAP-459", "非対応", string.Empty },
            new[] { "CAP-461", "非対応", string.Empty },
            new[] { "CAP-462", "非対応", string.Empty },
            new[] { "CAP-463", "非対応", string.Empty },
            new[] { "CAP-465", "非対応", string.Empty },
            new[] { "CAP-466", "非対応", string.Empty },
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
                    r => "| " + r[0] + " | SDK | IPXThing | " + r[1] + " | " + r[2] + " | 実装専用 |\n")));
            return path;
        }

        [Fact(Skip = "impl pending: 下位コマンドの名前で実行を振り分ける")]
        public void 下位コマンドを渡さないと引数の誤りで終わる()
        {
            StringWriter error = new StringWriter();

            int code = CommandRunner.Run(new string[0], new StringWriter(), error);

            Assert.Equal(ExitCodes.InvalidArguments, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
        }

        [Fact(Skip = "impl pending: 下位コマンドの名前を決める")]
        public void 下位コマンドの名前は呼び出し側との約束である()
        {
            // 名前は外から打つ文字列そのものなので、定数と実装を揃えて変えても気づけるようにする。
            Assert.Equal("signatures", CommandRunner.SignaturesCommand);
            Assert.Equal("excluded-baseline", CommandRunner.ExcludedBaselineCommand);
        }

        [Fact(Skip = "impl pending: 終了コードの値を決める")]
        public void 終了コードの値は呼び出し側との約束である()
        {
            // 値は呼び出し元が見分けに使うものそのもの。重ねてしまうと、直し方の違う失敗が同じに見える。
            Assert.Equal(0, ExitCodes.Success);
            Assert.Equal(2, ExitCodes.InvalidArguments);
            Assert.Equal(3, ExitCodes.InputUnavailable);
            Assert.Equal(4, ExitCodes.WriteFailed);
            Assert.Equal(5, ExitCodes.Unresolved);
        }

        [Fact(Skip = "impl pending: 知らない下位コマンドを引数の誤りとする")]
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

        [Fact(Skip = "impl pending: 列挙の下位コマンドを列挙の実行へ渡す")]
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

        [Fact(Skip = "impl pending: 凍結の下位コマンドを凍結の実行へ渡す")]
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

        [Fact(Skip = "impl pending: 実行ファイルの入口から列挙へ通す")]
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

        [Fact(Skip = "impl pending: 実行ファイルの入口から凍結へ通す")]
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

        [Fact(Skip = "impl pending: 実行ファイルの入口でも入力の違いが結果に出る")]
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
