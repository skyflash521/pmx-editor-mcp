using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// その並びを空にできるかどうかを、項目の名前から決める。
    /// </summary>
    public static class NonEmptyArrayRule
    {
        /// <summary>共通契約仕様書とツール仕様書が空にできないと定める並びの名前。</summary>
        private static readonly ReadOnlyCollection<string> Names = Array.AsReadOnly(new[]
        {
            "assignments",
            "handles",
            "indices",
            "parentHandles",
            "parentIndices",
            "refIndices",
            "targets",
        });

        /// <summary>その項目が空にできない並びか。並びでない項目は空にできるかを問われない。</summary>
        public static bool NonEmpty(SchemaItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return item.Element != null
                && item.Name != null
                && Names.Contains(item.Name, StringComparer.Ordinal);
        }
    }
}
