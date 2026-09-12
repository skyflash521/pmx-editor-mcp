using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class E2eCaseRunnerTests
    {
        [Fact]
        public void WrongArgumentCountEndsWithInvalidArguments()
        {
            foreach (int count in new[] { 0, 1, 11, 13 })
            {
                StringWriter error = new StringWriter();

                int code = E2eCaseRunner.Run(
                    Enumerable.Repeat("a", count).ToArray(), new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.Contains("サンプル値の正本", error.ToString());
            }
        }

        [Fact]
        public void AMissingSampleTableEndsWithInputUnavailable()
        {
            string root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-e2e-cases-" + Guid.NewGuid().ToString("N"));
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(root);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            File.Copy(new Uri(typeof(Sample.ISampleApi).Assembly.CodeBase).LocalPath, assemblyPath);
            string[] args = Enumerable.Repeat(Path.Combine(root, "other.json"), 12).ToArray();
            args[0] = root;
            args[10] = Path.Combine(root, "samples.json");
            StringWriter error = new StringWriter();

            try
            {
                int code = E2eCaseRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InputUnavailable, code);
                Assert.Contains("samples.json", error.ToString(), StringComparison.Ordinal);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
