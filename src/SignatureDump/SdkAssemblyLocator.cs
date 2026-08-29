using System;
using System.Collections.Generic;

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
        public static string GetAssemblyPath(string editorDirectory)
        {
            throw new NotImplementedException();
        }

        /// <summary>探索する順に並べる。</summary>
        public static IList<string> GetProbeDirectories(string editorDirectory)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 依存アセンブリの単純名を、探索するディレクトリの順に探して最初に見つかったパスを返す。
        /// どこにも無ければ null を返す。
        /// </summary>
        public static string FindDependency(string simpleName, IList<string> probeDirectories)
        {
            throw new NotImplementedException();
        }
    }
}
