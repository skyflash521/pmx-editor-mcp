using System;
using System.Collections.Generic;
using System.IO;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// PMXエディタ導入ディレクトリから、列挙の対象アセンブリと、その依存を解決するために
    /// 探索するディレクトリを決める。SDKアセンブリは配布物の中で描画ライブラリを参照して
    /// おり、そのライブラリは別のディレクトリにビット数ごとの実体が置かれているため、
    /// 読み込みには導入ディレクトリを起点とした探索が要る。
    /// </summary>
    public static class SdkAssemblyLocator
    {
        private const string SdkDirectoryName = "PEPlugin";
        private const string LibraryDirectoryName = "Lib";
        private const string SdkAssemblyFileName = "PEPlugin.dll";
        private const string DrawingLibraryDirectoryName = "SlimDX";
        private const string DrawingLibraryPlatformName = "x64";

        public static string GetAssemblyPath(string editorDirectory)
        {
            if (editorDirectory == null)
            {
                throw new ArgumentNullException(nameof(editorDirectory));
            }

            return Path.Combine(editorDirectory, LibraryDirectoryName, SdkDirectoryName, SdkAssemblyFileName);
        }

        /// <summary>探索する順に並べる。</summary>
        public static IList<string> GetProbeDirectories(string editorDirectory)
        {
            if (editorDirectory == null)
            {
                throw new ArgumentNullException(nameof(editorDirectory));
            }

            return new List<string>
            {
                Path.Combine(editorDirectory, LibraryDirectoryName, SdkDirectoryName),
                Path.Combine(
                    editorDirectory, LibraryDirectoryName, DrawingLibraryDirectoryName, DrawingLibraryPlatformName),
                editorDirectory,
            };
        }

        /// <summary>
        /// 依存アセンブリの単純名を、探索するディレクトリの順に探して最初に見つかったパスを返す。
        /// どこにも無ければ null を返す。
        /// </summary>
        public static string FindDependency(string simpleName, IList<string> probeDirectories)
        {
            if (simpleName == null)
            {
                throw new ArgumentNullException(nameof(simpleName));
            }

            if (probeDirectories == null)
            {
                throw new ArgumentNullException(nameof(probeDirectories));
            }

            foreach (string directory in probeDirectories)
            {
                string candidate = Path.Combine(directory, simpleName + ".dll");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
