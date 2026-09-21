using System.Globalization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 探す語を文へ当てる規則。エンドユーザーが打つ語とエディタやツールの文言は、大文字小文字も
    /// 全角半角も仮名の種類も揃わないので、そのどれも区別せずに当てる。
    /// </summary>
    public static class TextMatch
    {
        private const CompareOptions Options =
            CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth | CompareOptions.IgnoreKanaType;

        /// <summary>
        /// <paramref name="haystack"/> が <paramref name="needle"/> を含むか。どちらかが null なら偽、
        /// <paramref name="needle"/> が空なら真。
        /// </summary>
        public static bool Contains(string haystack, string needle)
        {
            if (haystack == null || needle == null)
            {
                return false;
            }

            if (needle.Length == 0)
            {
                return true;
            }

            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(haystack, needle, Options) >= 0;
        }
    }
}
