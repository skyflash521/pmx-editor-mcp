using System.Globalization;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>C# の文字列を組み立てる。</summary>
    public static class CSharpText
    {
        /// <summary>両端を引用符で囲み、印字できる ASCII 以外を逃がした文字列リテラル。</summary>
        public static string Quote(string value)
        {
            StringBuilder text = new StringBuilder("\"");
            foreach (char one in value)
            {
                if (one == '"' || one == '\\')
                {
                    text.Append('\\').Append(one);
                }
                else if (one >= ' ' && one <= '~')
                {
                    text.Append(one);
                }
                else
                {
                    text.Append("\\u").Append(((int)one).ToString("x4", CultureInfo.InvariantCulture));
                }
            }

            return text.Append('"').ToString();
        }
    }
}
