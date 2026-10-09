using System.Globalization;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 探す語を文へ当てる規則。大文字小文字も全角半角も仮名の種類も区別せずに当てる。PMXエディタの
    /// 表記が英語の語とその読みは、どちらで探しても両方に当てる。
    /// </summary>
    public static class TextMatch
    {
        private const CompareOptions Options =
            CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth | CompareOptions.IgnoreKanaType;

        private static readonly EditorTerm[] EditorTerms =
        {
            new EditorTerm("Joint", "ジョイント"),
            new EditorTerm("SoftBody", "ソフトボディ"),
            new EditorTerm("PmxView", "PMXビュー"),
            new EditorTerm("TransformView", "トランスフォームビュー"),
            new EditorTerm("SubView", "サブビュー"),
            new EditorTerm("Index", "インデックス"),
            new EditorTerm("Diffuse", "拡散色", "ディフューズ"),
            new EditorTerm("Ambient", "環境色", "アンビエント"),
            new EditorTerm("Specular", "反射色", "スペキュラ"),
            new EditorTerm("Undo", "元に戻す", "アンドゥ"),
            new EditorTerm("Redo", "やり直し", "リドゥ"),
            new EditorTerm("Pin", "ピン"),
        };

        /// <summary>
        /// <paramref name="haystack"/> が <paramref name="needle"/> を含むか。<paramref name="needle"/> が
        /// 表記か読みのどれかと一致するときは、同じ語の表記か読みのどれかを含むときも真。どちらかが
        /// null なら偽、<paramref name="needle"/> が空なら真。
        /// </summary>
        public static bool Contains(string haystack, string needle)
        {
            if (haystack == null || needle == null)
            {
                return false;
            }

            if (needle.Length == 0 || Holds(haystack, needle))
            {
                return true;
            }

            EditorTerm term = EditorTerms.FirstOrDefault(t => t.Spellings.Any(s => Same(needle, s)));

            return term != null && term.Spellings.Any(s => Holds(haystack, s));
        }

        private static bool Holds(string haystack, string needle)
        {
            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(haystack, needle, Options) >= 0;
        }

        private static bool Same(string left, string right)
        {
            return CultureInfo.InvariantCulture.CompareInfo.Compare(left, right, Options) == 0;
        }

        private sealed class EditorTerm
        {
            public EditorTerm(string notation, params string[] readings)
            {
                Spellings = new[] { notation }.Concat(readings).ToArray();
            }

            public string[] Spellings { get; }
        }
    }
}
