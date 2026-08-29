using System;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 型を、行キーとJSONの双方で使う一意な表記へ写す。入れ子は <c>+</c>、参照渡しは末尾の
    /// <c>&amp;</c>、総称型は山括弧で表す。
    /// </summary>
    public static class TypeNameFormatter
    {
        public static string Format(Type type)
        {
            throw new NotImplementedException();
        }
    }
}
