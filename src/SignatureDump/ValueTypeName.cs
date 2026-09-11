using System;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 値の型から、一列に並ぶ並びの印を外した先の型を取る。並びかどうかと、並びの中身が何かを
    /// 見る箇所が複数あるので、判定をここ1つに置く。
    /// </summary>
    public static class ValueTypeName
    {
        private const string ListTypeName = "System.Collections.Generic.IList";

        /// <summary>
        /// 一列に並ぶ配列とリストなら、その要素の型を渡す。並びでなければ偽で、
        /// <paramref name="element"/> は null のまま。
        /// </summary>
        public static bool TryElement(string typeName, out string element)
        {
            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            element = null;
            if (typeName.EndsWith("[]", StringComparison.Ordinal))
            {
                element = typeName.Substring(0, typeName.Length - 2);

                return true;
            }

            if (!typeName.StartsWith(ListTypeName + "<", StringComparison.Ordinal)
                || !typeName.EndsWith(">", StringComparison.Ordinal))
            {
                return false;
            }

            string[] arguments = TypeDefinitionName.Arguments(typeName).ToArray();
            if (arguments.Length != 1)
            {
                return false;
            }

            element = arguments[0];

            return true;
        }

        /// <summary>並びの印をすべて外した先の型。並びでない型はそのまま返す。</summary>
        public static string Contained(string typeName)
        {
            string name = typeName;
            string element;
            while (TryElement(name, out element))
            {
                name = element;
            }

            return name;
        }
    }
}
