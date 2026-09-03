using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class CommonAssignmentRunnerTests : IDisposable
    {
        private const string EmptyExcluded = "{\"signatures\":[]}\n";

        private const string EmptyRoles = "{\"types\":[],\"issuances\":[],\"collections\":[]}\n";

        private const string ReleaseTool = "session_release_handle";

        private readonly string _root;

        public CommonAssignmentRunnerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-assign-" + Guid.NewGuid().ToString("N"));
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
            foreach (int count in new[] { 0, 1, 2, 3, 4, 6 })
            {
                StringWriter error = new StringWriter();

                int code = CommonAssignmentRunner.Run(
                    Enumerable.Repeat("a", count).ToArray(), new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void MissingTargetAssemblyIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = CommonAssignmentRunner.Run(
                Arguments(Path.Combine(_root, "missing"), Table()), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("PEPlugin.dll", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMissingInputFileIsInputUnavailable()
        {
            foreach (int missing in new[] { 1, 2, 3, 4 })
            {
                string[] args = Arguments(Sdk(), Table());
                args[missing] = Path.Combine(_root, "gone");
                StringWriter error = new StringWriter();

                int code = CommonAssignmentRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InputUnavailable, code);
                Assert.Contains("gone", error.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void UnreadableInputIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = CommonAssignmentRunner.Run(
                Arguments(Sdk(), "{"), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
        }

        [Fact]
        public void AnUnloadableAssemblyIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = CommonAssignmentRunner.Run(
                Arguments(Broken(), Table()), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("読めない", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ASignatureOutsideTheProvidedIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = CommonAssignmentRunner.Run(
                Arguments(
                    Sdk(),
                    "{\"assignments\":[{\"signatureKey\":\"N.A.Absent()\",\"assignment\":\"tool\""
                        + ",\"target\":\"t\",\"slotBinding\":{\"parameters\":{}}"
                        + ",\"basis\":\"題材の根拠。\"}]}"),
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("共通契約割当が規則に合わない。", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("N.A.Absent()", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMatchingTableWritesOneSummaryLineAndSucceeds()
        {
            StringWriter output = new StringWriter();

            int code = CommonAssignmentRunner.Run(
                Arguments(Sdk(), Table()), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            string line = Assert.Single(
                output.ToString().Split(
                    new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            int releases = Releases().Count;
            Assert.Equal(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "照合した: 割当 {0} 件(ツール {0}・共通引数 0・内部フロー 0)・束縛 {0} 件",
                    releases),
                line);
            Assert.True(releases > 0);
        }

        [Fact]
        public void ASuccessfulRunDoesNotWriteAnyFile()
        {
            string[] args = Arguments(Sdk(), Table());
            string[] before = Fingerprints();

            Assert.Equal(
                ExitCodes.Success,
                CommonAssignmentRunner.Run(args, new StringWriter(), new StringWriter()));
            Assert.Equal(before, Fingerprints());
        }

        /// <summary>
        /// 試験用ディレクトリの全ファイルを、名前と中身と更新時刻の指紋の並びにしたもの。入力だけを
        /// 見ると、導入ディレクトリの隣へ書き出す実装を見逃す。
        /// </summary>
        private string[] Fingerprints()
        {
            using (SHA256 sha = SHA256.Create())
            {
                return Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .Select(p => string.Join(
                        " ",
                        p,
                        BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))),
                        File.GetLastWriteTimeUtc(p).Ticks.ToString(CultureInfo.InvariantCulture),
                        File.GetCreationTimeUtc(p).Ticks.ToString(CultureInfo.InvariantCulture),
                        File.GetAttributes(p).ToString()))
                    .ToArray();
            }
        }

        /// <summary>
        /// 題材の提供対象の解放・破棄を、解放のツールへの束縛として並べた正本。この照合は解放・破棄が
        /// 漏れなく表に在ることを求めるので、空の表では通らない。
        /// </summary>
        private static string Table()
        {
            StringBuilder builder = new StringBuilder("{\"assignments\":[");
            int index = 0;
            foreach (string key in Releases())
            {
                builder.Append(index++ == 0 ? string.Empty : ",")
                    .Append("{\"signatureKey\":\"").Append(key)
                    .Append("\",\"assignment\":\"tool\",\"target\":\"").Append(ReleaseTool)
                    .Append("\",\"slotBinding\":{\"receiver\":\"targetHandle\"")
                    .Append(",\"parameters\":{}},\"basis\":\"題材の根拠。\"}");
            }

            return builder.Append("]}").ToString();
        }

        /// <summary>題材の提供対象の解放・破棄の行キー。序数の昇順に並べる。</summary>
        private static IList<string> Releases()
        {
            InventoryRecord inventory = AssemblyEnumerator.Enumerate(Sample);
            ISet<string> provided = TypeRolePopulation.Resolve(
                LedgerParser.Parse(Ledger()),
                inventory,
                new List<ExcludedSignatureRecord>()).Signatures;

            return CommonAssignmentEvidence.ReleaseSignatures(inventory, provided)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>題材のアセンブリの公開型を提供として並べ、まとめて指す行を足した台帳。</summary>
        private static string Ledger()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |\n");
            builder.Append("|---|---|---|---|---|---|\n");

            int id = 1;
            foreach (TypeRecord type in AssemblyEnumerator.Enumerate(Sample).Types)
            {
                builder.Append(string.Format(
                    CultureInfo.InvariantCulture,
                    "| CAP-{0:D3} | 標本 | {1} | 提供 | モデル |  |\n",
                    id++,
                    WithoutTypeArguments(type.Name)));
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
            get { return typeof(CommonAssignmentRunnerTests).Assembly; }
        }

        private string[] Arguments(string editorDirectory, string table)
        {
            return new[]
            {
                editorDirectory,
                Write("l.md", Ledger()),
                Write("e.json", EmptyExcluded),
                Write("r.json", EmptyRoles),
                Write("a.json", table),
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
