using System.IO;

namespace PmxEditorMcp.Tests
{
    internal static class DialogStartFolder
    {
        internal static string Path
        {
            get
            {
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pmx-editor-mcp-tests", "dialog-start");
                Directory.CreateDirectory(path);

                return path;
            }
        }
    }
}
