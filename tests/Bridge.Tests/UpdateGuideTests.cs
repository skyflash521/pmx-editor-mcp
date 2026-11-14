using System;
using System.IO;
using System.Text.Json.Nodes;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public sealed class UpdateGuideTests
    {
        [Fact]
        public void TheGuideIsNamedWhenTheInstallDirectoryHoldsIt()
        {
            string directory = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-update-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            try
            {
                string guide = Path.Combine(directory, UpdateGuide.InstallGuideFileName);
                File.WriteAllText(guide, "#");

                JsonObject value = UpdateGuide.Describe(directory);

                Assert.Equal(Path.GetFullPath(guide), (string)value["installGuide"]);
                Assert.Contains("「更新する」", (string)value["steps"]);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void TheGuideIsNullAndTheStepsPointToTheReleaseWhenTheInstallDirectoryLacksIt()
        {
            string directory = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-update-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            try
            {
                JsonObject value = UpdateGuide.Describe(directory);

                Assert.Null(value["installGuide"]);
                Assert.Contains("latestRelease", (string)value["steps"]);
                Assert.Contains("digest", (string)value["steps"]);
                Assert.Contains("「導入する」", (string)value["steps"]);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void TheInstallGuideSpellsTheSameSources()
        {
            string text = File.ReadAllText(InstallGuideInRepository());

            Assert.Contains("## 更新する", text);
            Assert.Contains(UpdateGuide.RepositoryUrl, text);
            Assert.Contains(UpdateGuide.LatestReleaseUrl, text);
        }

        private static string InstallGuideInRepository()
        {
            for (DirectoryInfo at = new DirectoryInfo(AppContext.BaseDirectory); at != null; at = at.Parent)
            {
                string guide = Path.Combine(at.FullName, "docs", "package", UpdateGuide.InstallGuideFileName);
                if (File.Exists(guide))
                {
                    return guide;
                }
            }

            throw new FileNotFoundException("docs/package/INSTALL.md が見つからない。");
        }
    }
}
