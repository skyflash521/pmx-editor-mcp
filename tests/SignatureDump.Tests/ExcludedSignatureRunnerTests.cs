using System;
using System.Collections.Generic;
using System.IO;
using PmxEditorMcp.SignatureDump.Tests.Sample;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ExcludedSignatureRunnerTests : IDisposable
    {
        private const string Known = "PmxEditorMcp.SignatureDump.Tests.Sample.SampleData..ctor()";

        private const string Categorized =
            "PmxEditorMcp.SignatureDump.Tests.Sample.ISampleApi.Walk("
                + "PmxEditorMcp.SignatureDump.Tests.Sample.SampleProc)";

        private const string Existing = "{\"signatures\":[]}\n";

        private readonly string _root;

        public ExcludedSignatureRunnerTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-excluded-" + Guid.NewGuid().ToString("N"));
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

        private string CreateEditorDirectory()
        {
            string editorDirectory = Path.Combine(_root, "editor");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            if (!File.Exists(assemblyPath))
            {
                File.Copy(new Uri(typeof(ISampleApi).Assembly.CodeBase).LocalPath, assemblyPath);
            }

            return editorDirectory;
        }

        private string CreateBaseline(string json)
        {
            string path = Path.Combine(_root, "excluded-baseline.json");
            File.WriteAllText(path, json);
            return path;
        }

        private string CreateBaseline()
        {
            return CreateBaseline("{\"capabilities\":[\n{\"capabilityId\":\"CAP-1\",\"signatures\":[\n\""
                + Known + "\"\n]}\n]}\n");
        }

        private string CreateExistingOutput()
        {
            string path = Path.Combine(_root, "excluded-signatures.json");
            File.WriteAllText(path, Existing);
            return path;
        }

        /// <summary>確定した一覧そのもの。先に置いた内容へ足す形も、余分な行を混ぜる形も落とす。</summary>
        private static string Expected()
        {
            InventoryRecord inventory = AssemblyEnumerator.Enumerate(typeof(ISampleApi).Assembly);
            IList<ExcludedBaselineEntry> baseline = new List<ExcludedBaselineEntry>
            {
                new ExcludedBaselineEntry("CAP-1", new List<string> { Known }),
            };

            return ExcludedSignatureJson.Write(ExcludedSignatureBuilder.Build(baseline, inventory));
        }

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

                int code = ExcludedSignatureRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void MissingTargetAssemblyIsInputUnavailable()
        {
            string outputPath = CreateExistingOutput();

            int code = ExcludedSignatureRunner.Run(
                new[] { Path.Combine(_root, "none"), CreateBaseline(), outputPath },
                new StringWriter(),
                new StringWriter());

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact]
        public void MissingBaselineCanonicalFileIsInputUnavailable()
        {
            string outputPath = CreateExistingOutput();

            int code = ExcludedSignatureRunner.Run(
                new[] { CreateEditorDirectory(), Path.Combine(_root, "none.json"), outputPath },
                new StringWriter(),
                new StringWriter());

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact]
        public void UnparsableBaselineCanonicalFileIsInputUnavailable()
        {
            string outputPath = CreateExistingOutput();

            int code = ExcludedSignatureRunner.Run(
                new[] { CreateEditorDirectory(), CreateBaseline("{"), outputPath },
                new StringWriter(),
                new StringWriter());

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Theory]
        [InlineData("{\"capabilities\":{}}")]
        [InlineData("{\"capabilities\":[],\"extra\":1}")]
        [InlineData("{\"capabilities\":[{\"capabilityId\":\"CAP-1\"}]}")]
        public void BaselineCanonicalFileWithWrongShapeIsInputUnavailable(string json)
        {
            string outputPath = CreateExistingOutput();
            StringWriter error = new StringWriter();

            int code = ExcludedSignatureRunner.Run(
                new[] { CreateEditorDirectory(), CreateBaseline(json), outputPath },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact]
        public void FrozenPairsConflictingWithEnumerationCannotBeResolved()
        {
            string outputPath = CreateExistingOutput();
            string baseline = CreateBaseline(
                "{\"capabilities\":[\n{\"capabilityId\":\"CAP-1\",\"signatures\":[\n\"T.Removed()\"\n]}\n]}\n");

            int code = ExcludedSignatureRunner.Run(
                new[] { CreateEditorDirectory(), baseline, outputPath },
                new StringWriter(),
                new StringWriter());

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact]
        public void UnwritableTargetEndsWithWriteFailure()
        {
            string outputPath = Path.Combine(_root, "busy");
            Directory.CreateDirectory(outputPath);

            int code = ExcludedSignatureRunner.Run(
                new[] { CreateEditorDirectory(), CreateBaseline(), outputPath },
                new StringWriter(),
                new StringWriter());

            Assert.Equal(ExitCodes.WriteFailed, code);
        }

        [Fact]
        public void FailureDuringWriteLeavesTheExistingFileUnchanged()
        {
            string outputPath = CreateExistingOutput();
            Directory.CreateDirectory(outputPath + ".writing");

            int code = ExcludedSignatureRunner.Run(
                new[] { CreateEditorDirectory(), CreateBaseline(), outputPath },
                new StringWriter(),
                new StringWriter());

            Assert.Equal(ExitCodes.WriteFailed, code);
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

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
                code = ExcludedSignatureRunner.Run(
                    new[] { editorDirectory, CreateBaseline(), outputPath }, new StringWriter(), error);
            }

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact]
        public void TargetThatIsNotAnAssemblyIsInputUnavailable()
        {
            string editorDirectory = Path.Combine(_root, "broken");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            File.WriteAllText(assemblyPath, "アセンブリではない");
            string outputPath = CreateExistingOutput();
            StringWriter error = new StringWriter();

            int code = ExcludedSignatureRunner.Run(
                new[] { editorDirectory, CreateBaseline(), outputPath }, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact]
        public void UnloadableBaselineCanonicalFileIsInputUnavailable()
        {
            string baselinePath = CreateBaseline();
            string outputPath = CreateExistingOutput();
            StringWriter error = new StringWriter();

            int code;
            using (new FileStream(baselinePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                code = ExcludedSignatureRunner.Run(
                    new[] { CreateEditorDirectory(), baselinePath, outputPath }, new StringWriter(), error);
            }

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            Assert.Equal(Existing, File.ReadAllText(outputPath));
        }

        [Fact]
        public void MatchingFrozenPairsAndEnumerationAreWritten()
        {
            string outputPath = CreateExistingOutput();
            StringWriter output = new StringWriter();

            int code = ExcludedSignatureRunner.Run(
                new[] { CreateEditorDirectory(), CreateBaseline(), outputPath },
                output,
                new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains(outputPath, output.ToString());

            string written = File.ReadAllText(outputPath);
            Assert.Contains(
                "{\"key\":\"" + Known + "\",\"qualification\":\"baseline\",\"capabilityId\":\"CAP-1\"}",
                written,
                StringComparison.Ordinal);
            Assert.Contains(
                "{\"key\":\"" + Categorized + "\",\"qualification\":\"category\",\"category\":\"delegate\"}",
                written,
                StringComparison.Ordinal);
            Assert.Equal(Expected(), written);
        }

        [Fact]
        public void OutputHasNoBomAndUsesLfOnly()
        {
            string outputPath = Path.Combine(_root, "excluded-signatures.json");

            ExcludedSignatureRunner.Run(
                new[] { CreateEditorDirectory(), CreateBaseline(), outputPath },
                new StringWriter(),
                new StringWriter());

            byte[] bytes = File.ReadAllBytes(outputPath);

            Assert.NotEqual(0xEF, bytes[0]);
            Assert.DoesNotContain((byte)'\r', bytes);
        }

        [Fact]
        public void MissingArgumentsOrWritersThrow()
        {
            Assert.Throws<ArgumentNullException>(
                () => ExcludedSignatureRunner.Run(null, new StringWriter(), new StringWriter()));
            Assert.Throws<ArgumentNullException>(
                () => ExcludedSignatureRunner.Run(new string[0], null, new StringWriter()));
            Assert.Throws<ArgumentNullException>(
                () => ExcludedSignatureRunner.Run(new string[0], new StringWriter(), null));
        }
    }
}
