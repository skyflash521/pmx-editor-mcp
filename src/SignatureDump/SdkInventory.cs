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
            if (editorDirectory == null)
            {
                throw new ArgumentNullException(nameof(editorDirectory));
            }

            if (assemblyPath == null)
            {
                throw new ArgumentNullException(nameof(assemblyPath));
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
                return AssemblyEnumerator.Enumerate(Assembly.Load(File.ReadAllBytes(assemblyPath)));
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolver;
            }
        }
    }
}
