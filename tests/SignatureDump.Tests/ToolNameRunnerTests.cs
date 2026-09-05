using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolNameRunnerTests : IDisposable
    {
        private const string Vertex = "PEPlugin.Pmx.IPXVertex";

        private const string Sample = "PmxEditorMcp.SignatureDump.Tests.Sample.ISampleApi";

        private const string Roles =
            "{\"types\":[{\"typeName\":\"" + Vertex + "\",\"role\":\"operationTarget\""
                + ",\"basis\":\"題材の根拠。\",\"elementNoun\":\"vertex\""
                + ",\"elementNounPlural\":\"vertices\",\"group\":\"model\""
                + ",\"tools\":{\"list\":\"model_list_vertices\""
                + ",\"update\":\"model_update_vertices\"}},"
                + "{\"typeName\":\"" + Sample + "\",\"role\":\"operationTarget\""
                + ",\"basis\":\"題材の根拠。\",\"elementNoun\":\"sample\""
                + ",\"elementNounPlural\":\"samples\",\"group\":\"model\""
                + ",\"tools\":{\"list\":\"model_list_samples\""
                + ",\"update\":\"model_update_samples\"}}]"
                + ",\"issuances\":[],\"collections\":[]}\n";

        private const string EmptyMap = "{\"rows\":[]}\n";

        private readonly string _root;

        public ToolNameRunnerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-tool-names-" + Guid.NewGuid().ToString("N"));
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

                int code = ToolNameRunner.Run(
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

            int code = ToolNameRunner.Run(args, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("PEPlugin.dll", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMissingInputFileIsInputUnavailable()
        {
            foreach (int missing in new[] { 1, 2 })
            {
                string[] args = Arguments(EmptyMap);
                args[missing] = Path.Combine(_root, "gone");
                StringWriter error = new StringWriter();

                int code = ToolNameRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InputUnavailable, code);
                Assert.Contains("gone", error.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void UnreadableInputIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = ToolNameRunner.Run(Arguments("{"), new StringWriter(), error);

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

            int code = ToolNameRunner.Run(args, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("読めない", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ARowWhoseSignatureIsNotEnumeratedIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = ToolNameRunner.Run(
                Arguments(Assigned()), new StringWriter(), error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("規則に合わない", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AnEmptyMapWritesOneSummaryLineAndSucceeds()
        {
            StringWriter output = new StringWriter();

            int code = ToolNameRunner.Run(
                Arguments(EmptyMap), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            string line = Assert.Single(
                output.ToString().Split(
                    new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal("照合した: ツールを持つ行 0 件・埋め込み先 0 件", line);
        }

        [Fact]
        public void ARowWithoutAToolIsNotCountedInTheSummary()
        {
            StringWriter output = new StringWriter();

            int code = ToolNameRunner.Run(
                Arguments(CommonContract()), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("ツールを持つ行 0 件", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void TheSummaryCountsTheEmbeddedTargets()
        {
            StringWriter output = new StringWriter();

            StringWriter error = new StringWriter();

            int code = ToolNameRunner.Run(Arguments(Embedded()), output, error);

            Assert.Equal(error.ToString(), string.Empty);
            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("埋め込み先 1 件", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ASuccessfulRunDoesNotWriteAnyFile()
        {
            string[] args = Arguments(EmptyMap);
            string[] before = Fingerprints();

            Assert.Equal(
                ExitCodes.Success,
                ToolNameRunner.Run(args, new StringWriter(), new StringWriter()));
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

        /// <summary>列挙に無い行キーへツールを割り当てた能力対応表。</summary>
        private static string Assigned()
        {
            return "{\"rows\":[{\"signatureKey\":\"" + Vertex + ".Gone()\""
                + ",\"capabilityIds\":[\"C1\"],\"rowKind\":\"directDispatch\",\"editKind\":\"read\""
                + ",\"direction\":\"read\",\"basis\":\"題材の根拠。\""
                + ",\"tool\":\"model_gone_vertex\""
                + ",\"postcondition\":[{\"effectType\":\"none\",\"effectKey\":\"\""
                + ",\"kind\":\"callLogOnly\",\"comparison\":\"exists\"}]}]}\n";
        }

        /// <summary>
        /// 埋め込む行と、埋め込み先を持たない行を1件ずつ持つ能力対応表。行の数と埋め込み先の数を
        /// 分けて数えられる。
        /// </summary>
        private static string Embedded()
        {
            return "{\"rows\":[{\"signatureKey\":\"" + Vertex + ".Gone()\""
                + ",\"capabilityIds\":[\"C2\"],\"rowKind\":\"commonContract\""
                + ",\"editKind\":\"read\",\"direction\":\"read\",\"basis\":\"題材の根拠。\""
                + ",\"assignment\":\"internalFlow\",\"target\":\"connect\""
                + ",\"slotBinding\":{\"return\":\"runArgsClone\",\"parameters\":{}}},"
                + "{\"signatureKey\":\""
                + "PmxEditorMcp.SignatureDump.Tests.Sample.ISampleApi.GetCount()\""
                + ",\"capabilityIds\":[\"C1\"],\"rowKind\":\"schemaEmbedded\""
                + ",\"editKind\":\"read\",\"direction\":\"read\",\"basis\":\"題材の根拠。\""
                + ",\"embeddedIn\":[\"model_list_samples\"]}]}\n";
        }

        /// <summary>ツールを持たない行だけの能力対応表。</summary>
        private static string CommonContract()
        {
            return "{\"rows\":[{\"signatureKey\":\"" + Vertex + ".Gone()\""
                + ",\"capabilityIds\":[\"C1\"],\"rowKind\":\"commonContract\""
                + ",\"editKind\":\"read\",\"direction\":\"read\",\"basis\":\"題材の根拠。\""
                + ",\"assignment\":\"internalFlow\",\"target\":\"connect\""
                + ",\"slotBinding\":{\"return\":\"runArgsClone\",\"parameters\":{}}}]}\n";
        }

        private string[] Arguments(string map)
        {
            return new[]
            {
                EditorDirectory(),
                Write("roles.json", Roles),
                Write("map.json", map),
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
                    new Uri(typeof(ToolNameRunnerTests).Assembly.CodeBase).LocalPath, assemblyPath);
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
