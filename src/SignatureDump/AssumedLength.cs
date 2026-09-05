using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 項目1件をJSONへ写したときの想定文字数。上限ではなく、一覧の件数を逆算するための取り決めで、
    /// 綴りごとの値は共通契約仕様書の表が正本である。
    /// </summary>
    public sealed class AssumedLength
    {
        /// <summary>組の中の項目1件が、名前と区切りに使う分。</summary>
        public const int MemberOverhead = 8;

        /// <summary>一次資料が要素数を定めていない並びの、想定する要素数。</summary>
        private const int AssumedElements = 8;

        private readonly IDictionary<string, int> _bySpelling;

        public AssumedLength(IDictionary<string, int> bySpelling)
        {
            if (bySpelling == null)
            {
                throw new ArgumentNullException(nameof(bySpelling));
            }

            _bySpelling = bySpelling;
        }

        /// <summary>綴りを知らないか、形を持たない項目であれば <see cref="InvalidOperationException"/>。</summary>
        public int Of(SchemaItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (item.Members != null)
            {
                return item.Members.Sum(m => Of(m) + MemberOverhead);
            }

            if (item.Element != null)
            {
                return Of(item.Element) * Elements(item);
            }

            if (item.Bounds != null && item.Bounds.Maximum.HasValue)
            {
                return item.Bounds.Maximum.Value.ToString("R", CultureInfo.InvariantCulture).Length;
            }

            int length;
            if (item.Shape == null || !_bySpelling.TryGetValue(item.Shape, out length))
            {
                throw new InvalidOperationException(
                    "想定文字数を持たない項目がある: " + (item.Shape ?? item.Name ?? "名前無し"));
            }

            return length;
        }

        /// <summary>
        /// 並びの想定要素数。一次資料が定めた要素数だけを採るので、逆算した上限は使わない。
        /// </summary>
        private static int Elements(SchemaItem item)
        {
            return item.Source != null && item.MaxItems.HasValue
                ? item.MaxItems.Value
                : AssumedElements;
        }
    }
}
