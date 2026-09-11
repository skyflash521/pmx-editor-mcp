using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 中継の組み立てに要るSDKの事実。1度の読み込みでまとめて取る——読み込むたびに別のアセンブリが
    /// 立ち上がるので、事実ごとに読み直さない。
    /// </summary>
    public sealed class SdkFacts
    {
        public SdkFacts(InventoryRecord inventory, IList<string> combinableEnums)
        {
            Inventory = inventory;
            CombinableEnums = combinableEnums;
        }

        /// <summary>公開シグネチャの列挙。</summary>
        public InventoryRecord Inventory { get; }

        /// <summary>名前を並べて組み合わせられる列挙の綴り。</summary>
        public IList<string> CombinableEnums { get; }

        public static SdkFacts Of(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            return new SdkFacts(AssemblyEnumerator.Enumerate(assembly), Combinable(assembly));
        }

        private static IList<string> Combinable(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(t => t.IsVisible && t.IsEnum && t.IsDefined(typeof(FlagsAttribute), false))
                .Select(t => t.FullName)
                .ToList();
        }
    }
}
