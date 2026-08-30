using System.Globalization;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>JSONの文字列を組み立てる。</summary>
    public static class JsonText
    {
        /// <summary>
        /// 両端を引用符で囲み、JSONが生のまま置けない文字を逃がす。制御文字は仕様が必ず逃がすことを
        /// 求めるので、行キーに紛れ込んでも壊れたJSONにならない。
        /// </summary>
        public static string Quote(string value)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;

                    case '\\':
                        builder.Append("\\\\");
                        break;

                    case '\b':
                        builder.Append("\\b");
                        break;

                    case '\f':
                        builder.Append("\\f");
                        break;

                    case '\n':
                        builder.Append("\\n");
                        break;

                    case '\r':
                        builder.Append("\\r");
                        break;

                    case '\t':
                        builder.Append("\\t");
                        break;

                    default:
                        if (c < ' ')
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }
    }
}
