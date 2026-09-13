using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class SampleValueRunnerTests : IDisposable
    {
        private static readonly string Contract = new CommonContractJsonBuilder()
            .AddType("System.Int32", "number")
            .AddType("PEPlugin.Pmx.PmxUpdateObject", "enum_name")
            .AddType("PEPlugin.SDX.V3", "number_array")
            .AddComponent("PEPlugin.SDX.V3", 3)
            .ToString();

        private const string Table =
            "{\"types\":[{\"typeName\":\"PEPlugin.Pmx.PmxUpdateObject\",\"default\":\"Vertex\""
            + ",\"second\":\"Bone\"},{\"typeName\":\"PEPlugin.SDX.V3\",\"default\":[1,2,3]"
            + ",\"second\":[4,5,6]},{\"typeName\":\"System.Int32\",\"default\":1,\"second\":2}]}\n";

        private readonly string _root;

        public SampleValueRunnerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-samples-" + Guid.NewGuid().ToString("N"));
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

                int code = SampleValueRunner.Run(
                    Enumerable.Repeat("a", count).ToArray(), new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void MissingTargetAssemblyIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = SampleValueRunner.Run(
                new[] { Path.Combine(_root, "missing"), Write("c.json", Contract), Write("s.json", Table) },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("PEPlugin.dll", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMissingInputFileIsInputUnavailable()
        {
            foreach (int missing in new[] { 1, 2 })
            {
                string[] args = Arguments(Table);
                args[missing] = Path.Combine(_root, "gone");
                StringWriter error = new StringWriter();

                int code = SampleValueRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InputUnavailable, code);
                Assert.Contains("gone", error.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void ASourceWithoutTheComponentsIsInputUnavailable()
        {
            string[] args = Arguments(Table);
            args[1] = Write(
                "c2.json",
                "{\"spellings\":[{\"spelling\":\"number\",\"assumedChars\":11}]"
                    + ",\"types\":[{\"typeName\":\"System.Int32\",\"shape\":\"number\"}]}");
            StringWriter error = new StringWriter();

            int code = SampleValueRunner.Run(args, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("components", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void UnreadableInputIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = SampleValueRunner.Run(Arguments("{"), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
        }

        [Fact]
        public void AnUnreadableAssemblyIsInputUnavailable()
        {
            string[] args = Arguments(Table);
            File.WriteAllBytes(SdkAssemblyLocator.GetAssemblyPath(args[0]), new byte[] { 0x4D, 0x5A });
            StringWriter error = new StringWriter();

            int code = SampleValueRunner.Run(args, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("読めない", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ATableThatMatchesTheDocumentPasses()
        {
            StringWriter output = new StringWriter();

            int code = SampleValueRunner.Run(Arguments(Table), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("型 3 件", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ASampleThatNamesAMissingEnumMemberIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = SampleValueRunner.Run(
                Arguments(Table.Replace("\"Vertex\"", "\"Gone\"")), new StringWriter(), error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("規則に合わない", error.ToString(), StringComparison.Ordinal);
        }

        private static Assembly Sample
        {
            get { return typeof(SampleValueRunnerTests).Assembly; }
        }

        private string[] Arguments(string table)
        {
            return new[] { EditorDirectory(), Write("contract.json", Contract), Write("table.json", table) };
        }

        /// <summary>題材のアセンブリを対象として置いた導入ディレクトリを作る。</summary>
        private string EditorDirectory()
        {
            string directory = Path.Combine(_root, "editor");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(directory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            if (!File.Exists(assemblyPath))
            {
                File.Copy(new Uri(Sample.CodeBase).LocalPath, assemblyPath);
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
