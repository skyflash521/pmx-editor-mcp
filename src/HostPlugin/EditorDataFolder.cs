using System;
using System.IO;
using System.Linq;

namespace PmxEditorMcp
{
    public static class EditorDataFolder
    {
        /// <param name="editorFolder">エディタの実行ファイルがあるフォルダ。</param>
        public static string Of(string editorFolder)
        {
            if (editorFolder == null)
            {
                throw new ArgumentNullException(nameof(editorFolder));
            }

            string setting = Path.Combine(editorFolder, "_data.path");
            if (File.Exists(setting))
            {
                string first = File.ReadAllLines(setting).FirstOrDefault();
                string pointed = first == null ? string.Empty : first.Trim();
                if (pointed.Length != 0 && Directory.Exists(pointed))
                {
                    return pointed;
                }
            }

            return Path.Combine(editorFolder, "_data");
        }
    }
}
