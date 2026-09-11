using System;
using System.Drawing;

namespace PmxEditorMcp
{
    /// <summary>列挙の扱いのうち、型ごとに決まっていて実行時には引けないもの。</summary>
    internal static class ValueEnums
    {
        /// <summary>
        /// 名前を並べて組み合わせられる列挙かどうか。SDKの列挙はビルド時に集めた一覧が持ち、
        /// 書体の装飾はこの配布物が直に扱う型なのでここで持つ。
        /// </summary>
        internal static bool IsCombinable(Type target)
        {
            return target == typeof(FontStyle)
                || GeneratedSdkEnums.Combinable.Contains(target.FullName);
        }
    }
}
