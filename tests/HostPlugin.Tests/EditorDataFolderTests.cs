using System;
using System.IO;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class EditorDataFolderTests : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), "pmx-editor-mcp-tests", Guid.NewGuid().ToString("N"));

        public EditorDataFolderTests()
        {
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            Directory.Delete(_root, true);
        }

        [Fact]
        public void WithoutASettingTheDataFolderIsUnderTheEditorFolder()
        {
            Assert.Equal(Path.Combine(_root, "_data"), EditorDataFolder.Of(_root));
        }

        [Fact]
        public void AnExistingFolderOnTheFirstLineOfTheSettingIsTheDataFolder()
        {
            string elsewhere = Path.Combine(_root, "別の場所");
            Directory.CreateDirectory(elsewhere);
            File.WriteAllLines(Path.Combine(_root, "_data.path"), new[] { "  " + elsewhere + "  ", "無視される行" });

            Assert.Equal(elsewhere, EditorDataFolder.Of(_root));
        }

        [Fact]
        public void AFolderThatDoesNotExistInTheSettingIsIgnored()
        {
            File.WriteAllLines(Path.Combine(_root, "_data.path"), new[] { Path.Combine(_root, "無い") });

            Assert.Equal(Path.Combine(_root, "_data"), EditorDataFolder.Of(_root));
        }
    }
}
