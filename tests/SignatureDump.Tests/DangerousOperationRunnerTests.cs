using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class DangerousOperationRunnerTests : IDisposable
    {
        private const string EmptyExcluded = "{\"signatures\":[]}\n";

        private readonly string _root;

        public DangerousOperationRunnerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-danger-" + Guid.NewGuid().ToString("N"));
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
            }
        }

        [Fact]
        public void WrongArgumentCountEndsWithInvalidArguments()
        {
            foreach (int count in new[] { 0, 1, 2, 4 })
            {
                StringWriter error = new StringWriter();

                int code = DangerousOperationRunner.Run(
                    Enumerable.Repeat("a", count).ToArray(), new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void MissingTargetAssemblyIsInputUnavailable()
        {
            Assert.Equal(
                ExitCodes.InputUnavailable,
                DangerousOperationRunner.Run(
                    Arguments(Path.Combine(_root, "none"), Ledger(string.Empty)),
                    new StringWriter(),
                    new StringWriter()));
        }

        [Fact]
        public void AMissingInputFileIsInputUnavailable()
        {
            string[] args = Arguments(Sdk(), Ledger(string.Empty));
            args[2] = Path.Combine(_root, "none.json");

            Assert.Equal(
                ExitCodes.InputUnavailable,
                DangerousOperationRunner.Run(args, new StringWriter(), new StringWriter()));
        }

        [Fact]
        public void AnUnloadableAssemblyIsInputUnavailable()
        {
            Assert.Equal(
                ExitCodes.InputUnavailable,
                DangerousOperationRunner.Run(
                    Arguments(Broken(), Ledger(string.Empty)), new StringWriter(), new StringWriter()));
        }

        [Fact]
        public void AKindTheRuleDoesNotKnowIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = DangerousOperationRunner.Run(
                Arguments(Sdk(), Ledger("危険操作(知らない種別)。該当は Do()。")),
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("知らない種別", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ANotedSignatureThatIsNotThereIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = DangerousOperationRunner.Run(
                Arguments(Sdk(), Ledger("危険操作(エディタ終了)。該当は Absent()。")),
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("Absent()", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMatchingLedgerWritesOneSummaryLineAndSucceeds()
        {
            StringWriter output = new StringWriter();

            int code = DangerousOperationRunner.Run(
                Arguments(Sdk(), Ledger(string.Empty)), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            string line = Assert.Single(
                output.ToString().Split(
                    new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal(
                "照合した: 危険操作に当たるシグネチャ 0 件(エディタ終了 0・上書き保存 0・モデル初期化 0)",
                line);
        }

        /// <summary>題材のアセンブリの公開型を提供として並べ、備考を与えた台帳。</summary>
        private static string Ledger(string remarks)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |\n");
            builder.Append("|---|---|---|---|---|---|\n");

            int id = 1;
            foreach (TypeRecord type in AssemblyEnumerator.Enumerate(Sample).Types)
            {
                builder.Append(string.Format(
                    CultureInfo.InvariantCulture,
                    "| CAP-{0:D3} | 標本 | {1} | 提供 | モデル | {2} |\n",
                    id++,
                    WithoutTypeArguments(type.Name),
                    id == 2 ? remarks : string.Empty));
            }

            builder.Append("| CAP-463 | 標本 | PEPlugin.Pmd.* のまとめ | 非対応 |  |  |\n");
            builder.Append("| CAP-466 | 標本 | PEPlugin.SDX.* のまとめ | 非対応 |  |  |\n");

            return builder.ToString();
        }

        private static string WithoutTypeArguments(string typeName)
        {
            int open = typeName.IndexOf('<');

            return open < 0 ? typeName : typeName.Substring(0, open);
        }

        private static Assembly Sample
        {
            get { return typeof(DangerousOperationRunnerTests).Assembly; }
        }

        private string[] Arguments(string editorDirectory, string ledger)
        {
            return new[]
            {
                editorDirectory,
                Write("l.md", ledger),
                Write("e.json", EmptyExcluded),
            };
        }

        /// <summary>題材のアセンブリを対象アセンブリとして置いた導入ディレクトリを作る。</summary>
        private string Sdk()
        {
            string directory = Path.Combine(_root, "sdk");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(directory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            if (!File.Exists(assemblyPath))
            {
                File.Copy(new Uri(Sample.CodeBase).LocalPath, assemblyPath);
            }

            return directory;
        }

        /// <summary>読み込めないアセンブリを置いた導入ディレクトリを作る。</summary>
        private string Broken()
        {
            string directory = Path.Combine(_root, "broken");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(directory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            File.WriteAllBytes(assemblyPath, new byte[] { 0x4D, 0x5A });

            return directory;
        }

        private string Write(string name, string text)
        {
            string path = Path.Combine(_root, name);
            File.WriteAllText(path, text);

            return path;
        }
    }
}
