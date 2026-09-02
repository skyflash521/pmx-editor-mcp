using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 対象アセンブリはバイト列から読み込むので、実行が終わってもそのファイルは掴まない。
    /// 依存アセンブリはパスから読み込むため掴んだままに
    /// なる——混在モードのアセンブリはバイト列から読み込めず、SDKが参照する描画ライブラリが
    /// これに当たるためである。依存は導入ディレクトリの実体を指すので、呼び出し元が消す対象には
    /// ならない。
    /// </summary>
    public static class SdkInventory
    {
        public static InventoryRecord Load(string editorDirectory, string assemblyPath)
        {
            return Read(editorDirectory, assemblyPath, AssemblyEnumerator.Enumerate);
        }

        /// <summary>
        /// 依存を解決できる状態で対象アセンブリを読み込み、<paramref name="read"/> へ渡す。型の
        /// メンバーを辿ると依存の解決が起きるので、辿り終えるまでこの中に居ること。
        /// </summary>
        public static T Read<T>(string editorDirectory, string assemblyPath, Func<Assembly, T> read)
        {
            if (editorDirectory == null)
            {
                throw new ArgumentNullException(nameof(editorDirectory));
            }

            if (assemblyPath == null)
            {
                throw new ArgumentNullException(nameof(assemblyPath));
            }

            if (read == null)
            {
                throw new ArgumentNullException(nameof(read));
            }

            IList<string> probeDirectories = SdkAssemblyLocator.GetProbeDirectories(editorDirectory);
            ResolveEventHandler resolver = (sender, e) =>
            {
                string found = SdkAssemblyLocator.FindDependency(new AssemblyName(e.Name).Name, probeDirectories);
                return found == null ? null : Assembly.LoadFrom(found);
            };

            AppDomain.CurrentDomain.AssemblyResolve += resolver;
            try
            {
                return read(Assembly.Load(File.ReadAllBytes(assemblyPath)));
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolver;
            }
        }
    }
}
