using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Globalization;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolDescriptionRunnerTests : IDisposable
    {
        private const string Vertex = "PEPlugin.Pmx.IPXVertex";

        /// <summary>題材のアセンブリが持つ、型役割表に載せない型。</summary>
        private const string Sample = "PmxEditorMcp.SignatureDump.Tests.Sample.ISampleApi";

        private const string Roles =
            "{\"types\":[{\"typeName\":\"" + Vertex + "\",\"role\":\"operationTarget\""
                + ",\"basis\":\"題材の根拠。\",\"elementNoun\":\"vertex\""
                + ",\"elementNounPlural\":\"vertices\"}]"
                + ",\"issuances\":[],\"collections\":[]}\n";

        private const string EmptyNames = "{\"propertyNames\":[]}\n";

        private const string Contract =
            "### 合成ツール\n\n| ツール | 分岐 | 受け持つこと |\n|---|---|---|\n"
            + "| `session_release_handle` | 持たない | 解放する |\n";

        private const string EmptyMap = "{\"rows\":[]}\n";

        /// <summary>行を持たない共通契約割当の正本。</summary>
        private const string EmptyAssignments = "{\"assignments\":[]}\n";

        private readonly string _root;

        public ToolDescriptionRunnerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-descriptions-" + Guid.NewGuid().ToString("N"));
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

                int code = ToolDescriptionRunner.Run(
                    Enumerable.Repeat("a", count).ToArray(), new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void MissingTargetAssemblyIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = ToolDescriptionRunner.Run(
                new[]
                {
                    Path.Combine(_root, "missing"),
                    Write("l.md", Ledger(typeof(ToolDescriptionRunnerTests).Assembly)),
                    Write("c.md", Contract),
                    Write("r.json", Roles),
                    Write("n.json", EmptyNames),
                    Write("a.json", EmptyAssignments),
                    Write("m.json", EmptyMap),
                },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("PEPlugin.dll", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMissingInputFileIsInputUnavailable()
        {
            foreach (int missing in new[] { 1, 2, 3, 4 })
            {
                string[] args = Arguments(EmptyMap);
                args[missing] = Path.Combine(_root, "gone");
                StringWriter error = new StringWriter();

                int code = ToolDescriptionRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InputUnavailable, code);
                Assert.Contains("gone", error.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void AMissingDocumentIsInputUnavailable()
        {
            string[] args = Arguments(EmptyMap);
            File.Delete(SdkAssemblyLocator.GetDocumentPath(args[0]));
            StringWriter error = new StringWriter();

            int code = ToolDescriptionRunner.Run(args, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("ドキュメントXML", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void UnreadableInputIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = ToolDescriptionRunner.Run(Arguments("{"), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
        }

        [Fact]
        public void AnUnreadableAssemblyIsInputUnavailable()
        {
            string[] args = Arguments(EmptyMap);
            File.WriteAllBytes(SdkAssemblyLocator.GetAssemblyPath(args[0]), new byte[] { 0x4D, 0x5A });
            StringWriter error = new StringWriter();

            int code = ToolDescriptionRunner.Run(args, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("読めない", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AnEmptyMapPasses()
        {
            StringWriter output = new StringWriter();

            int code = ToolDescriptionRunner.Run(Arguments(EmptyMap), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("ツール 1 件", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ARowOnATypeWithoutARoleIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = ToolDescriptionRunner.Run(
                Arguments(Map(Sample + ".GetCount()")),
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("規則に合わない", error.ToString(), StringComparison.Ordinal);
        }

        private static string Map(string signatureKey)
        {
            return "{\"rows\":[{\"signatureKey\":\"" + signatureKey + "\""
                + ",\"editKind\":\"read\""
                + ",\"basis\":\"題材の根拠。\""
                + ",\"postcondition\":[{\"effectType\":\"none\",\"effectKey\":\"\""
                + ",\"kind\":\"callLogOnly\",\"comparison\":\"exists\"}]}]}\n";
        }

        private string[] Arguments(string map)
        {
            return new[]
            {
                EditorDirectory(),
                Write("ledger.md", Ledger(typeof(ToolDescriptionRunnerTests).Assembly)),
                Write("contract.md", Contract),
                Write("roles.json", Roles),
                Write("names.json", EmptyNames),
                Write("assignments.json", EmptyAssignments),
                Write("map.json", map),
            };
        }

        /// <summary>題材のアセンブリの公開型を提供として並べた台帳。担当はどれもモデルになる。</summary>
        private static string Ledger(Assembly assembly)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |\n");
            builder.Append("|---|---|---|---|---|---|\n");

            int id = 1;
            foreach (TypeRecord type in AssemblyEnumerator.Enumerate(assembly).Types)
            {
                string name = type.Name;
                int open = name.IndexOf('<');
                builder.Append(string.Format(
                    CultureInfo.InvariantCulture,
                    "| CAP-{0:D3} | 標本 | {1} | 提供 | モデル |  |\n",
                    id++,
                    open < 0 ? name : name.Substring(0, open)));
            }

            builder.Append("| CAP-463 | 標本 | PEPlugin.Pmd.* のまとめ | 非対応 |  |  |\n");
            builder.Append("| CAP-466 | 標本 | PEPlugin.SDX.* のまとめ | 非対応 |  |  |\n");

            return builder.ToString();
        }

        /// <summary>題材のアセンブリと記載を対象として置いた導入ディレクトリを作る。</summary>
        private string EditorDirectory()
        {
            string directory = Path.Combine(_root, "editor");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(directory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            if (!File.Exists(assemblyPath))
            {
                File.Copy(
                    new Uri(typeof(ToolDescriptionRunnerTests).Assembly.CodeBase).LocalPath,
                    assemblyPath);
            }

            File.WriteAllText(
                SdkAssemblyLocator.GetDocumentPath(directory),
                "<?xml version=\"1.0\"?><doc><assembly><name>PEPlugin</name></assembly>"
                    + "<members><member name=\"P:" + Vertex + ".Index\">"
                    + "<summary>頂点の番号</summary></member></members></doc>");

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
