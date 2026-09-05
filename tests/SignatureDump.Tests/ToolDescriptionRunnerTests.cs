using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolDescriptionRunnerTests : IDisposable
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

        private const string EmptyNames = "{\"propertyNames\":[]}\n";

        private const string EmptyMap = "{\"rows\":[]}\n";

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
            foreach (int count in new[] { 0, 1, 2, 3, 5 })
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
                new[] { Path.Combine(_root, "missing"), Write("r.json", Roles),
                    Write("n.json", EmptyNames), Write("m.json", EmptyMap) },
                new StringWriter(),
                error);

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
            Assert.Contains("ツール 0 件", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ARowOnATypeWithoutARoleIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = ToolDescriptionRunner.Run(
                Arguments(Map("PEPlugin.Pmx.IPXBone.Index()", ListTool)),
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("規則に合わない", error.ToString(), StringComparison.Ordinal);
        }

        private static string Map(string signatureKey, string tool)
        {
            return "{\"rows\":[{\"signatureKey\":\"" + signatureKey + "\""
                + ",\"capabilityIds\":[\"C1\"],\"rowKind\":\"composed\",\"editKind\":\"read\""
                + ",\"direction\":\"read\",\"basis\":\"題材の根拠。\",\"tool\":\"" + tool + "\""
                + ",\"postcondition\":[{\"effectType\":\"none\",\"effectKey\":\"\""
                + ",\"kind\":\"callLogOnly\",\"comparison\":\"exists\"}]}]}\n";
        }

        private string[] Arguments(string map)
        {
            return new[]
            {
                EditorDirectory(),
                Write("roles.json", Roles),
                Write("names.json", EmptyNames),
                Write("map.json", map),
            };
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
