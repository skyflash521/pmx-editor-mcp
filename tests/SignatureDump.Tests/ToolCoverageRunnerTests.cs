using System;
using System.IO;
using System.Runtime.CompilerServices;
using PmxEditorMcp.SignatureDump.Tests.Sample;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolCoverageRunnerTests : IDisposable
    {
        private readonly string _root;

        public ToolCoverageRunnerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-coverage-" + Guid.NewGuid().ToString("N"));
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
        public void AnUncoveredListWithAToolTwiceEndsWithInputUnavailable()
        {
            string uncovered = Path.Combine(_root, "uncovered-tools.json");
            File.WriteAllText(uncovered, @"{ ""tools"": [
  { ""tool"": ""model_clone_ik"", ""reason"": ""noCase"" },
  { ""tool"": ""model_clone_ik"", ""reason"": ""noEffectCheck"" }
] }");
            StringWriter error = new StringWriter();

            int code = ToolCoverageRunner.Run(
                new[]
                {
                    CreateEditorDirectory(),
                    Catalog("observed", "capability-ledger.json"),
                    Catalog("authored", "common-contract.json"),
                    Catalog("authored", "type-roles.json"),
                    Catalog("authored", "property-names.json"),
                    Catalog("authored", "common-assignments.json"),
                    Catalog("authored", "tool-map.json"),
                    Catalog("authored", "tool-schemas.json"),
                    Catalog("authored", "sample-values.json"),
                    Catalog("authored", "acceptance-scenarios.json"),
                    uncovered,
                },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("model_clone_ik", error.ToString(), StringComparison.Ordinal);
        }

        private string CreateEditorDirectory()
        {
            string editorDirectory = Path.Combine(_root, "editor");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            File.Copy(new Uri(typeof(ISampleApi).Assembly.CodeBase).LocalPath, assemblyPath);
            File.WriteAllText(
                SdkAssemblyLocator.GetDocumentPath(editorDirectory), "<doc><members /></doc>");

            return editorDirectory;
        }

        private static string Catalog(
            string place, string name, [CallerFilePath] string here = null)
        {
            return Path.Combine(Path.GetDirectoryName(here), "..", "..", "catalog", place, name);
        }
    }
}
