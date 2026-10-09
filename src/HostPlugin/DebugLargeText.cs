using System;

namespace PmxEditorMcp
{
    public static class DebugLargeText
    {
        public const string MethodName = "debug_large_text";

        public const char FillCharacter = 'x';

        private const string CharsParameterName = "chars";

        public static void AddTo(McpMethodTable methods, bool enabled)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (enabled)
            {
                methods.Add(MethodName, Build);
            }
        }

        /// <summary>指定された文字数のテキストを返す。応答サイズ予算を超える大きさは断る。</summary>
        public static object Build(McpMethodContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            int chars = RequestParameter.PositiveInteger(context.Params, CharsParameterName);
            if (chars > context.BudgetChars)
            {
                throw new InvalidParamsException(
                    CharsParameterName + " が応答サイズ予算を超えている。");
            }

            return new string(FillCharacter, chars);
        }
    }
}
