using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class SchemaCorrespondenceRunnerTests : IDisposable
    {
        private const string Vertex = "PEPlugin.Pmx.IPXVertex";

        private const string ListTool = "model_list_vertices";

        private const string Roles =
            "{\"types\":[{\"typeName\":\"" + Vertex + "\",\"role\":\"operationTarget\""
                + ",\"basis\":\"題材の根拠。\",\"elementNoun\":\"vertex\""
                + ",\"elementNounPlural\":\"vertices\",\"group\":\"model\""
                + ",\"tools\":{\"list\":\"" + ListTool + "\""
                + ",\"update\":\"model_update_vertices\"}}]"
                + ",\"issuances\":[],\"collections\":[]}\n";

        private const string EmptyMap = "{\"rows\":[]}\n";

        private const string EmptySchemas = "{\"tools\":[]}\n";

        private readonly string _root;

        public SchemaCorrespondenceRunnerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-correspondence-" + Guid.NewGuid().ToString("N"));
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
            foreach (int count in new[] { 0, 1, 2, 3, 5 })
            {
                StringWriter error = new StringWriter();

                int code = SchemaCorrespondenceRunner.Run(
                    Enumerable.Repeat("a", count).ToArray(), new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void MissingTargetAssemblyIsInputUnavailable()
        {
            string[] args = Arguments(EmptyMap, EmptySchemas);
            args[0] = Path.Combine(_root, "missing");
            StringWriter error = new StringWriter();

            int code = SchemaCorrespondenceRunner.Run(args, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("PEPlugin.dll", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMissingInputFileIsInputUnavailable()
        {
            foreach (int missing in new[] { 1, 2, 3 })
            {
                string[] args = Arguments(EmptyMap, EmptySchemas);
                args[missing] = Path.Combine(_root, "gone");
                StringWriter error = new StringWriter();

                int code = SchemaCorrespondenceRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InputUnavailable, code);
                Assert.Contains("gone", error.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void UnreadableInputIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = SchemaCorrespondenceRunner.Run(
                Arguments("{", EmptySchemas), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
        }

        [Fact]
        public void AnUnreadableAssemblyIsInputUnavailable()
        {
            string[] args = Arguments(EmptyMap, EmptySchemas);
            File.WriteAllBytes(
                SdkAssemblyLocator.GetAssemblyPath(args[0]), new byte[] { 0x4D, 0x5A });
            StringWriter error = new StringWriter();

            int code = SchemaCorrespondenceRunner.Run(args, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("読めない", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ARowWhoseSignatureIsNotEnumeratedIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = SchemaCorrespondenceRunner.Run(
                Arguments(Map(), EmptySchemas), new StringWriter(), error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("規則に合わない", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AnEmptyMapWritesOneSummaryLineAndSucceeds()
        {
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();

            int code = SchemaCorrespondenceRunner.Run(
                Arguments(EmptyMap, EmptySchemas), output, error);

            Assert.Equal(error.ToString(), string.Empty);
            Assert.Equal(ExitCodes.Success, code);
            string line = Assert.Single(
                output.ToString().Split(
                    new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal("照合した: ツールを持つ行 0 件・入出力の形 0 件", line);
        }

        [Fact]
        public void ARowWithoutAToolIsNotCountedInTheSummary()
        {
            StringWriter output = new StringWriter();

            int code = SchemaCorrespondenceRunner.Run(
                Arguments(Assigned(), EmptySchemas), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains(
                "ツールを持つ行 0 件", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ASuccessfulRunDoesNotWriteAnyFile()
        {
            string[] args = Arguments(EmptyMap, EmptySchemas);
            string[] before = Fingerprints();

            Assert.Equal(
                ExitCodes.Success,
                SchemaCorrespondenceRunner.Run(args, new StringWriter(), new StringWriter()));
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

        /// <summary>ツールを持たない行だけの能力対応表。</summary>
        private static string Assigned()
        {
            return "{\"rows\":[{\"signatureKey\":\"" + Vertex + ".Gone()\""
                + ",\"capabilityIds\":[\"C1\"],\"rowKind\":\"commonContract\""
                + ",\"editKind\":\"read\",\"direction\":\"read\",\"basis\":\"題材の根拠。\""
                + ",\"assignment\":\"internalFlow\",\"target\":\"connect\""
                + ",\"slotBinding\":{\"return\":\"runArgsClone\",\"parameters\":{}}}]}";
        }

        /// <summary>列挙に無い行キーへツールを割り当てた能力対応表。</summary>
        private static string Map()
        {
            return "{\"rows\":[{\"signatureKey\":\"" + Vertex + ".Gone()\""
                + ",\"capabilityIds\":[\"C1\"],\"rowKind\":\"directDispatch\",\"editKind\":\"read\""
                + ",\"direction\":\"read\",\"basis\":\"題材の根拠。\",\"tool\":\"" + ListTool + "\""
                + ",\"postcondition\":[{\"effectType\":\"none\",\"effectKey\":\"\""
                + ",\"kind\":\"callLogOnly\",\"comparison\":\"exists\"}]}]}\n";
        }

        private string[] Arguments(string map, string schemas)
        {
            return new[]
            {
                EditorDirectory(),
                Write("roles.json", Roles),
                Write("map.json", map),
                Write("schemas.json", schemas),
            };
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
                    new Uri(typeof(SchemaCorrespondenceRunnerTests).Assembly.CodeBase).LocalPath,
                    assemblyPath);
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
