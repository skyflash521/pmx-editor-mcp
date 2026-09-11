using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolMappingRunnerTests : IDisposable
    {
        private const string Vertex = "PEPlugin.Pmx.IPXVertex";

        private const string Sample = "PmxEditorMcp.SignatureDump.Tests.Sample.ISampleApi";

        private const string Roles =
            "{\"types\":[{\"typeName\":\"" + Vertex + "\",\"role\":\"operationTarget\""
                + ",\"basis\":\"題材の根拠。\",\"elementNoun\":\"vertex\""
                + ",\"elementNounPlural\":\"vertices\"},"
                + "{\"typeName\":\"" + Sample + "\",\"role\":\"operationTarget\""
                + ",\"basis\":\"題材の根拠。\",\"elementNoun\":\"sample\""
                + ",\"elementNounPlural\":\"samples\"}]"
                + ",\"issuances\":[],\"collections\":[]}\n";

        private const string EmptyMap = "{\"rows\":[]}\n";

        /// <summary>合成ツールを1件だけ持つ共通契約仕様書。</summary>
        private const string Contract =
            "### 合成ツール\n\n| ツール | 分岐 | 受け持つこと |\n|---|---|---|\n"
            + "| `session_release_handle` | 持たない | 解放する |\n";

        /// <summary>行を持たない共通契約割当の正本。</summary>
        private const string EmptyAssignments = "{\"assignments\":[]}\n";

        /// <summary>合成ツールの形だけを持つスキーマ正本。</summary>
        private const string ComposedSchemas =
            "{\"tools\":[{\"tool\":\"session_release_handle\""
            + ",\"branches\":[{\"branch\":\"only\",\"inputs\":[]}]"
            + ",\"output\":{\"origin\":\"hostOutput\",\"shape\":\"number\"}}]}\n";

        private readonly string _root;

        public ToolMappingRunnerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-tool-mapping-" + Guid.NewGuid().ToString("N"));
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
            foreach (int count in new[] { 0, 1, 2, 3, 4, 5, 6, 8 })
            {
                StringWriter error = new StringWriter();

                int code = ToolMappingRunner.Run(
                    Enumerable.Repeat("a", count).ToArray(), new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void MissingTargetAssemblyIsInputUnavailable()
        {
            string[] args = Arguments(EmptyMap);
            args[0] = Path.Combine(_root, "missing");
            StringWriter error = new StringWriter();

            int code = ToolMappingRunner.Run(args, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("PEPlugin.dll", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMissingInputFileIsInputUnavailable()
        {
            foreach (int missing in new[] { 1, 2, 3 })
            {
                string[] args = Arguments(EmptyMap);
                args[missing] = Path.Combine(_root, "gone");
                StringWriter error = new StringWriter();

                int code = ToolMappingRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InputUnavailable, code);
                Assert.Contains("gone", error.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void UnreadableInputIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = ToolMappingRunner.Run(Arguments("{"), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
        }

        [Fact]
        public void AnUnreadableAssemblyIsInputUnavailable()
        {
            string[] args = Arguments(EmptyMap);
            File.WriteAllBytes(
                SdkAssemblyLocator.GetAssemblyPath(args[0]), new byte[] { 0x4D, 0x5A });
            StringWriter error = new StringWriter();

            int code = ToolMappingRunner.Run(args, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("読めない", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ARowWhoseSignatureIsNotEnumeratedIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = ToolMappingRunner.Run(
                Arguments(EmbeddedGone()), new StringWriter(), error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("規則に合わない", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AnEmptyMapWritesOneSummaryLineAndSucceeds()
        {
            StringWriter output = new StringWriter();

            int code = ToolMappingRunner.Run(
                Arguments(EmptyMap), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            string line = Assert.Single(
                output.ToString().Split(
                    new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal("照合した: ツールを持つ行 0 件・埋め込み先 0 件・呼び分け 1 件", line);
        }

        [Fact]
        public void ARowWithoutAToolIsNotCountedInTheSummary()
        {
            StringWriter output = new StringWriter();

            int code = ToolMappingRunner.Run(
                Arguments(CommonContract()), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("ツールを持つ行 0 件", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void TheSummaryCountsTheEmbeddedTargets()
        {
            StringWriter output = new StringWriter();

            StringWriter error = new StringWriter();

            int code = ToolMappingRunner.Run(Arguments(Embedded()), output, error);

            Assert.Equal(error.ToString(), string.Empty);
            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("埋め込み先 1 件", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void TheSummaryCountsTheBranches()
        {
            string[] args = Arguments(EmptyMap);
            args[6] = Write(
                "branches.json",
                "{\"tools\":[{\"tool\":\"session_release_handle\""
                    + ",\"branches\":[{\"branch\":\"first\",\"inputs\":[]},"
                    + "{\"branch\":\"second\",\"inputs\":["
                    + "{\"name\":\"handles\",\"origin\":\"hostInput\""
                    + ",\"shape\":\"number\",\"required\":true}]}]"
                    + ",\"output\":{\"origin\":\"hostOutput\",\"shape\":\"number\"}}]}\n");
            StringWriter output = new StringWriter();

            int code = ToolMappingRunner.Run(args, output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("呼び分け 2 件", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ASuccessfulRunDoesNotWriteAnyFile()
        {
            string[] args = Arguments(EmptyMap);
            string[] before = Fingerprints();

            Assert.Equal(
                ExitCodes.Success,
                ToolMappingRunner.Run(args, new StringWriter(), new StringWriter()));
            Assert.Equal(before, Fingerprints());
        }

        /// <summary>
        /// 試験用ディレクトリの全ファイルを、名前と中身と更新時刻の指紋の並びにしたもの。入力だけを
        /// 見ると、隣へ書き出す実装を見逃す。
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
                        File.GetLastWriteTimeUtc(p).Ticks.ToString()))
                    .ToArray();
            }
        }

        /// <summary>列挙に無い行キーが埋め込み先を持つ能力対応表。</summary>
        private static string EmbeddedGone()
        {
            return "{\"rows\":[{\"signatureKey\":\"" + Vertex + ".Gone()\""
                + ",\"editKind\":\"read\",\"basis\":\"題材の根拠。\""
                + ",\"embeddedIn\":[\"model_list_samples\"]}]}\n";
        }

        /// <summary>
        /// 埋め込む行と、埋め込み先を持たない行を1件ずつ持つ能力対応表。行の数と埋め込み先の数を
        /// 分けて数えられる。
        /// </summary>
        private static string Embedded()
        {
            return "{\"rows\":[{\"signatureKey\":\"" + Vertex + ".Gone()\""
                + ",\"editKind\":\"read\",\"basis\":\"題材の根拠。\"},"
                + "{\"signatureKey\":\""
                + "PmxEditorMcp.SignatureDump.Tests.Sample.ISampleApi.Value()\""
                + ",\"editKind\":\"read\",\"basis\":\"題材の根拠。\""
                + ",\"embeddedIn\":[\"model_list_samples\"]}]}\n";
        }

        /// <summary>ツールを持たない行だけの能力対応表。</summary>
        private static string CommonContract()
        {
            return "{\"rows\":[{\"signatureKey\":\"" + Vertex + ".Gone()\""
                + ",\"editKind\":\"read\",\"basis\":\"題材の根拠。\"}]}\n";
        }

        private string[] Arguments(string map)
        {
            return new[]
            {
                EditorDirectory(),
                Write("ledger.md", Ledger(typeof(ToolMappingRunnerTests).Assembly)),
                Write("contract.md", Contract),
                Write("roles.json", Roles),
                Write("assignments.json", EmptyAssignments),
                Write("map.json", map),
                Write("schemas.json", ComposedSchemas),
            };
        }

        /// <summary>題材のアセンブリの公開型を提供として並べた台帳。担当はどれもモデルになる。</summary>
        private static string Ledger(Assembly assembly)
        {
            LedgerJsonBuilder builder = new LedgerJsonBuilder();

            int id = 1;
            foreach (TypeRecord type in AssemblyEnumerator.Enumerate(assembly).Types)
            {
                string name = type.Name;
                int open = name.IndexOf('<');
                builder.Add(
                    string.Format(CultureInfo.InvariantCulture, "CAP-{0:D3}", id++),
                    open < 0 ? name : name.Substring(0, open));
            }

            return builder.AddNamespaceRows().ToString();
        }

        /// <summary>題材のアセンブリを対象として置いた導入ディレクトリを作る。</summary>
        private string EditorDirectory()
        {
            string directory = Path.Combine(_root, "editor");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(directory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            if (!File.Exists(assemblyPath))
            {
                File.Copy(
                    new Uri(typeof(ToolMappingRunnerTests).Assembly.CodeBase).LocalPath, assemblyPath);
            }

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
