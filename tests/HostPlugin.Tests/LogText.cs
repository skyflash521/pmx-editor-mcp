using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PmxEditorMcp.Tests
{
    public static class LogText
    {
        public static string Read(string path)
        {
            using (StreamReader reader = Open(path))
            {
                return reader.ReadToEnd();
            }
        }

        public static string[] Lines(string path)
        {
            List<string> lines = new List<string>();
            using (StreamReader reader = Open(path))
            {
                for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    lines.Add(line);
                }
            }

            return lines.ToArray();
        }

        private static StreamReader Open(string path)
        {
            return new StreamReader(
                new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete),
                Encoding.UTF8);
        }
    }
}
