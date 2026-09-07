using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// 検査が指定した文字数のテキストを取るための入口。応答の大きさをMCPクライアントがどう扱うかは、
    /// 同じ大きさの応答を条件だけ変えて比べないと分からないので、大きさを指定して同じ本文を
    /// 作れるようにする。
    /// </summary>
    public static class DebugLargeText
    {
        /// <summary>この入口のメソッド名。</summary>
        public const string MethodName = "debug_large_text";

        /// <summary>
        /// 本文を埋める文字。UTF-8で1バイトに収まり、JSONの文字列でエスケープが要らないものを
        /// 使う——文字数と本文のバイト数を近いままに保つ。
        /// </summary>
        public const char FillCharacter = 'x';

        private const string CharsParameterName = "chars";

        /// <summary>
        /// 入口が開いているときだけ表へ足す。閉じているときは足さないので、要求は未知のメソッド
        /// として返る。
        /// </summary>
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

        /// <summary>
        /// 指定された文字数のテキストを返す。応答サイズ予算を超える大きさは断る——ホストは予算を
        /// 超える応答を返さないので、検査のためにそこだけ外すと、確かめている相手が本番と別物になる。
        /// </summary>
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
