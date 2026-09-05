using System;
using System.Collections.Generic;
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
            { "CAP-114", "非対応件数: 1。PMX+VMD版と引数なし版を対象。契約注記: PMDを引数に取る版はレガシーのため対象外" },
            { "CAP-269", "非対応件数: 1。Int32版を提供。契約注記: IPXCPluginを引数に取る版は対象外。取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨" },
            { "CAP-304", "引数のIPXCPlugin(実装拡張点・非対応)を取得経路から得られないため" },
            { "CAP-339", "非対応件数: 2。全公開メンバー(型単位)。契約注記: FromStream/ToStreamはファイルパス版で代替し対象外。危険操作(上書き保存)。該当は ToFile(System.String)。危険操作(モデル初期化)。該当は Clear()。現在のモデルを対象に呼ぶ場合に限って当たり、対象を指定して呼ぶ場合は当たらない。呼び出しには確認が要る" },
            { "CAP-390", "非対応件数: 2。他のオーバーロードを提供。契約注記: PMDを引数に取る版はレガシーのため対象外" },
            { "CAP-398", "非対応件数: 1。契約注記: PMDを引数に取る版はレガシーのため対象外" },
            { "CAP-459", "プラグイン自身がホストに登録されるための実装専用API" },
            { "CAP-461", "IPXPmxBuilder等の提供経路と重複する短絡経路のため" },
            { "CAP-462", "プラグインDLL側の拡張点(MCPからの呼び出し対象ではない)" },
            { "CAP-463", "PMX系に同等機能。PMDファイル入出力はFormコネクタの能力として提供" },
            { "CAP-465", "Cプラグインを実装する側の基底クラス・エントリポイント(実装専用)" },
            { "CAP-466", "SlimDX数値型の橋渡し型。演算メンバーはモデル状態に作用せずクライアント側で完結する数値計算のため対象外。値の受け渡し方はこの台帳では定めない" },
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
                    r => "| " + string.Join(" | ", r) + " | " + LedgerRemarks[r[0]] + " |\n"));
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

        /// <summary>
        /// 足りない場合だけを見ると、余った場合に後ろを黙って捨てる作りを見逃す。
        /// </summary>
        [Fact]
        public void WrongArgumentCountEndsWithInvalidArguments()
        {
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

        [Fact]
        public void MissingTargetAssemblyIsInputUnavailable()
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

        [Fact]
        public void MissingLedgerIsInputUnavailable()
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

        /// <summary>
        /// 台帳が非対応と記した能力の指す先が列挙結果に無い状態。空の結果を書き出すと、
        /// 凍結したはずの除外が黙って消える。すでに正本があるときは、それも壊さない。
        /// </summary>
        [Fact]
        public void LedgerConflictingWithEnumerationCannotBeResolved()
        {
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

        /// <summary>
        /// 在ることだけを見て中身を読まない作りだと、SDKを列挙しないまま結果を出せてしまう。
        /// </summary>
        [Fact]
        public void UnloadableTargetAssemblyIsInputUnavailable()
        {
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

        /// <summary>
        /// 読み解けない中身と、そもそもファイルを読めないことは別の失敗。後者を通すと、
        /// 読み取りの失敗がそのまま外へ漏れる。
        /// </summary>
        [Fact]
        public void UnreadableTargetAssemblyIsInputUnavailable()
        {
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

        /// <summary>
        /// 在ることだけを見て読めない場合を通すと、読み取りの失敗がそのまま外へ漏れる。
        /// </summary>
        [Fact]
        public void UnreadableLedgerIsInputUnavailable()
        {
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

        /// <summary>
        /// すでにある正本を守るだけでなく、無いところへ空の結果を置かないことも要る。
        /// 中身の無いファイルが残ると、読み手は結果が空だったのか失敗したのか区別できない。
        /// 途中で止まる場所ごとに別の経路なので、どの止まり方でも置かないことを見る。
        /// </summary>
        [Fact]
        public void FailureLeavesNoOutputFile()
        {
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

        /// <summary>
        /// 読めたうえでの食い違いと、そもそも読み解けないことは、呼び出し元の直し方が違う。
        /// </summary>
        [Fact]
        public void UnparsableLedgerIsInputUnavailable()
        {
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

        /// <summary>
        /// 在ることだけを見て中身を読まない作りだと、台帳が何を記していても同じ結果になる。
        /// 最初に見る能力を落とした台帳では、止まる理由が指す先ではなく台帳の側になる。
        /// </summary>
        [Fact]
        public void LedgerContentAffectsTheComparison()
        {
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

        [Fact]
        public void MissingArgumentsOrWritersThrow()
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
