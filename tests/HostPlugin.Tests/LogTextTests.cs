using System;
using System.IO;
using System.Text;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class LogTextTests : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-logtext-" + Guid.NewGuid().ToString("N") + ".log");

        public void Dispose()
        {
            File.Delete(_path);
        }

        [Fact]
        public void TheLogIsReadWhileTheHostHoldsItOpenForAWrite()
        {
            File.WriteAllText(_path, "一行目\r\n", Encoding.UTF8);
            using (new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                Assert.Equal("一行目", LogText.Lines(_path)[0]);
            }
        }
    }
}
