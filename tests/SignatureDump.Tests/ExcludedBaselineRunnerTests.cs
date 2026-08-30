using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PmxEditorMcp.SignatureDump.Tests.Sample;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ExcludedBaselineRunnerTests : IDisposable
    {
        // 凍結の対象になる能力と、実物と同じ分類・担当。一部のシグネチャだけを対象外とする能力は
        // 分類が提供なので、非対応の行だけを渡す作りでは実物で取りこぼす。能力が欠けていると、
        // 列挙結果と突き合わせる前に台帳の側の欠落で止まり、突き合わせの結果を見られない。
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

        /// <summary>先に置いた書き出し先が、失敗した実行で変わっていないことを見るための内容。</summary>
        private const string Existing = "{\"capabilities\":[]}\n";

        private readonly string _root;

        public ExcludedBaselineRunnerTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-baseline-" + Guid.NewGuid().ToString("N"));
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
        // テストアセンブリ自身を置く。実物のSDKを持ち込まずに、列挙と台帳との突き合わせまでを通せる。
        private string CreateEditorDirectory()
        {
            return CreateEditorDirectory("editor");
        }

        private string CreateEditorDirectory(string name)
        {
            string editorDirectory = Path.Combine(_root, name);
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            if (!File.Exists(assemblyPath))
            {
                File.Copy(new Uri(typeof(ISampleApi).Assembly.CodeBase).LocalPath, assemblyPath);
            }

            return editorDirectory;
        }

        private static string LedgerText(string[][] rows)
        {
            return "| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |\n|---|---|---|---|---|---|\n"
                + string.Concat(rows.Select(
                    r => "| " + r[0] + " | SDK | IPXThing | " + r[1] + " | " + r[2] + " | 実装専用 |\n"));
        }

        private string CreateLedger()
        {
            string path = Path.Combine(_root, "ledger.md");
            File.WriteAllText(path, LedgerText(LedgerRows));
            return path;
        }

        private string CreateExistingOutput()
        {
            string path = Path.Combine(_root, "excluded-baseline.json");
            File.WriteAllText(path, Existing);
            return path;
        }

        [Fact(Skip = "impl pending: 引数の数を確かめる")]
        public void 引数が3つでなければ引数の誤りで終わる()
        {
            // 足りない場合だけを見ると、余った場合に後ろを黙って捨てる作りを見逃す。
            string[][] wrong =
            {
                new string[0],
                new[] { "a" },
                new[] { "a", "b" },
                new[] { "a", "b", "c", "d" },
            };

            foreach (string[] args in wrong)
            {
                StringWriter error = new StringWriter();

                int code = ExcludedBaselineRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact(Skip = "impl pending: 対象アセンブリが無いときは入力を読めないとして終わる")]
        public void 対象アセンブリが無ければ入力を読めない()
        {
            string outputPath = CreateExistingOutput();
            StringWriter error = new StringWriter();

            int code = ExcludedBaselineRunner.Run(
                new[] { Path.Combine(_root, "empty"), CreateLedger(), outputPath },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact(Skip = "impl pending: 台帳が無いときは入力を読めないとして終わる")]
        public void 台帳が無ければ入力を読めない()
        {
            string outputPath = CreateExistingOutput();
            StringWriter error = new StringWriter();

            int code = ExcludedBaselineRunner.Run(
                new[] { CreateEditorDirectory(), Path.Combine(_root, "none.md"), outputPath },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact(Skip = "impl pending: 台帳と列挙が食い違うときは確定できないとして終わる")]
        public void 台帳と列挙が食い違えば確定できない()
        {
            // 台帳が非対応と記した能力の指す先が列挙結果に無い状態。空の結果を書き出すと、
            // 凍結したはずの除外が黙って消える。すでに正本があるときは、それも壊さない。
            string outputPath = CreateExistingOutput();
            StringWriter error = new StringWriter();

            int code = ExcludedBaselineRunner.Run(
                new[] { CreateEditorDirectory(), CreateLedger(), outputPath },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("指す先が無い", error.ToString(), StringComparison.Ordinal);
            Assert.Equal(Existing, File.ReadAllText(outputPath));

            // 突き合わせへ渡した件数を報告させる。空の並びや一部だけを渡す作りでも台帳の側の理由は
            // 同じに見えるので、列挙結果の全件と一致することで見分ける。
            Match counted = Regex.Match(error.ToString(), "突き合わせたシグネチャ: ([0-9]+) 件");
            Assert.True(counted.Success, error.ToString());
            Assert.Equal(
                AssemblyEnumerator.Enumerate(typeof(ISampleApi).Assembly).Signatures.Count,
                int.Parse(counted.Groups[1].Value, CultureInfo.InvariantCulture));
        }

        [Fact(Skip = "impl pending: 読み込めない対象アセンブリを入力を読めないとして扱う")]
        public void 読み込めない対象アセンブリは入力を読めない()
        {
            // 在ることだけを見て中身を読まない作りだと、SDKを列挙しないまま結果を出せてしまう。
            string editorDirectory = CreateEditorDirectory();
            File.WriteAllText(SdkAssemblyLocator.GetAssemblyPath(editorDirectory), "アセンブリではない");
            string outputPath = CreateExistingOutput();
            StringWriter error = new StringWriter();

            int code = ExcludedBaselineRunner.Run(
                new[] { editorDirectory, CreateLedger(), outputPath }, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains(
                SdkAssemblyLocator.GetAssemblyPath(editorDirectory), error.ToString(), StringComparison.Ordinal);
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact(Skip = "impl pending: 読めない対象アセンブリを入力を読めないとして扱う")]
        public void 読めない対象アセンブリは入力を読めない()
        {
            // 読み解けない中身と、そもそもファイルを読めないことは別の失敗。後者を通すと、
            // 読み取りの失敗がそのまま外へ漏れる。
            string editorDirectory = CreateEditorDirectory();
            string outputPath = CreateExistingOutput();
            StringWriter error = new StringWriter();

            int code;
            using (new FileStream(
                SdkAssemblyLocator.GetAssemblyPath(editorDirectory),
                FileMode.Open,
                FileAccess.Read,
                FileShare.None))
            {
                code = ExcludedBaselineRunner.Run(
                    new[] { editorDirectory, CreateLedger(), outputPath }, new StringWriter(), error);
            }

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact(Skip = "impl pending: 読めない台帳を入力を読めないとして扱う")]
        public void 読み込めない台帳は入力を読めない()
        {
            // 在ることだけを見て読めない場合を通すと、読み取りの失敗がそのまま外へ漏れる。
            string ledgerPath = CreateLedger();
            string outputPath = CreateExistingOutput();
            StringWriter error = new StringWriter();

            int code;
            using (new FileStream(ledgerPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                code = ExcludedBaselineRunner.Run(
                    new[] { CreateEditorDirectory(), ledgerPath, outputPath }, new StringWriter(), error);
            }

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact(Skip = "impl pending: 失敗したときは書き出し先を作らない")]
        public void 失敗しても書き出し先を作らない()
        {
            // すでにある正本を守るだけでなく、無いところへ空の結果を置かないことも要る。
            // 中身の無いファイルが残ると、読み手は結果が空だったのか失敗したのか区別できない。
            // 途中で止まる場所ごとに別の経路なので、どの止まり方でも置かないことを見る。
            Assert.False(RunAndFindOutput("mismatch", CreateEditorDirectory(), CreateLedger()));
            Assert.False(RunAndFindOutput("no-editor", Path.Combine(_root, "empty"), CreateLedger()));
            Assert.False(RunAndFindOutput("no-ledger", CreateEditorDirectory(), Path.Combine(_root, "none.md")));

            string broken = CreateEditorDirectory("broken-editor");
            File.WriteAllText(SdkAssemblyLocator.GetAssemblyPath(broken), "アセンブリではない");
            Assert.False(RunAndFindOutput("broken-assembly", broken, CreateLedger()));

            string unreadable = Path.Combine(_root, "unreadable-ledger.md");
            File.WriteAllText(unreadable, LedgerText(new[] { new[] { "CAP-114", "保留", "モデル" } }));
            Assert.False(RunAndFindOutput("broken-ledger", CreateEditorDirectory(), unreadable));

            string lockedLedger = Path.Combine(_root, "locked-ledger.md");
            File.WriteAllText(lockedLedger, LedgerText(LedgerRows));
            using (new FileStream(lockedLedger, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.False(RunAndFindOutput("locked-ledger", CreateEditorDirectory(), lockedLedger));
            }

            string lockedEditor = CreateEditorDirectory("locked-editor");
            using (new FileStream(
                SdkAssemblyLocator.GetAssemblyPath(lockedEditor),
                FileMode.Open,
                FileAccess.Read,
                FileShare.None))
            {
                Assert.False(RunAndFindOutput("locked-assembly", lockedEditor, CreateLedger()));
            }
        }

        private bool RunAndFindOutput(string name, string editorDirectory, string ledgerPath)
        {
            string outputPath = Path.Combine(_root, name + ".json");

            int code = ExcludedBaselineRunner.Run(
                new[] { editorDirectory, ledgerPath, outputPath }, new StringWriter(), new StringWriter());

            Assert.NotEqual(ExitCodes.Success, code);
            return File.Exists(outputPath);
        }

        [Fact(Skip = "impl pending: 読み解けない台帳を入力を読めないとして扱う")]
        public void 読み解けない台帳は入力を読めない()
        {
            // 読めたうえでの食い違いと、そもそも読み解けないことは、呼び出し元の直し方が違う。
            string path = Path.Combine(_root, "broken-ledger.md");
            File.WriteAllText(path, LedgerText(new[] { new[] { "CAP-114", "保留", "モデル" } }));
            string outputPath = CreateExistingOutput();
            StringWriter error = new StringWriter();

            int code = ExcludedBaselineRunner.Run(
                new[] { CreateEditorDirectory(), path, outputPath }, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact(Skip = "impl pending: 台帳の中身を突き合わせへ反映する")]
        public void 台帳の中身が突き合わせに効く()
        {
            // 在ることだけを見て中身を読まない作りだと、台帳が何を記していても同じ結果になる。
            // 最初に見る能力を落とした台帳では、止まる理由が指す先ではなく台帳の側になる。
            string path = Path.Combine(_root, "short-ledger.md");
            File.WriteAllText(path, LedgerText(LedgerRows.Where(r => r[0] != "CAP-114").ToArray()));
            string outputPath = CreateExistingOutput();
            StringWriter error = new StringWriter();

            int code = ExcludedBaselineRunner.Run(
                new[] { CreateEditorDirectory(), path, outputPath }, new StringWriter(), error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("CAP-114", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("台帳に無い", error.ToString(), StringComparison.Ordinal);
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact(Skip = "impl pending: 引数や報告先を渡さないときは例外にする")]
        public void 引数や報告先を渡さないと例外になる()
        {
            Assert.Throws<ArgumentNullException>(
                () => ExcludedBaselineRunner.Run(null, new StringWriter(), new StringWriter()));
            Assert.Throws<ArgumentNullException>(
                () => ExcludedBaselineRunner.Run(new string[0], null, new StringWriter()));
            Assert.Throws<ArgumentNullException>(
                () => ExcludedBaselineRunner.Run(new string[0], new StringWriter(), null));
        }
    }
}
