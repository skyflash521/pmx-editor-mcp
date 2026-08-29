using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class SdkAssemblyLocatorTests
    {
        private const string EditorDirectory = @"C:\editor";

        [Fact]
        public void 対象アセンブリのパスを組み立てる()
        {
            Assert.Equal(
                Path.Combine(EditorDirectory, "Lib", "PEPlugin", "PEPlugin.dll"),
                SdkAssemblyLocator.GetAssemblyPath(EditorDirectory));
        }

        [Fact]
        public void 探索するディレクトリを優先順に並べる()
        {
            IList<string> directories = SdkAssemblyLocator.GetProbeDirectories(EditorDirectory);

            Assert.Equal(
                new[]
                {
                    Path.Combine(EditorDirectory, "Lib", "PEPlugin"),
                    Path.Combine(EditorDirectory, "Lib", "SlimDX", "x64"),
                    EditorDirectory,
                },
                directories);
        }

        [Fact]
        public void 依存アセンブリは探索する順に探される()
        {
            string root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-probe-" + Guid.NewGuid().ToString("N"));
            string first = Path.Combine(root, "first");
            string second = Path.Combine(root, "second");
            Directory.CreateDirectory(first);
            Directory.CreateDirectory(second);
            File.WriteAllText(Path.Combine(second, "Dep.dll"), string.Empty);
            List<string> probes = new List<string> { first, second };

            try
            {
                Assert.Equal(Path.Combine(second, "Dep.dll"), SdkAssemblyLocator.FindDependency("Dep", probes));

                File.WriteAllText(Path.Combine(first, "Dep.dll"), string.Empty);
                Assert.Equal(Path.Combine(first, "Dep.dll"), SdkAssemblyLocator.FindDependency("Dep", probes));

                Assert.Null(SdkAssemblyLocator.FindDependency("Missing", probes));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void 導入ディレクトリを渡さないと例外になる()
        {
            Assert.Throws<ArgumentNullException>(() => SdkAssemblyLocator.GetAssemblyPath(null));
            Assert.Throws<ArgumentNullException>(() => SdkAssemblyLocator.GetProbeDirectories(null));
            Assert.Throws<ArgumentNullException>(
                () => SdkAssemblyLocator.FindDependency(null, new List<string>()));
            Assert.Throws<ArgumentNullException>(() => SdkAssemblyLocator.FindDependency("Dep", null));
        }
    }
}
