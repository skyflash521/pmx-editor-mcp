using System.Globalization;

namespace PmxEditorMcp
{
    /// <summary>
    /// 要求が大きすぎないかを見る。判定をここに集めるのは、大きさの数え方がツールごとに分かれると、
    /// 同じ要求が受理される側と断られる側に分かれるからである。
    /// </summary>
    public static class RequestBudget
    {
        /// <summary>要求の大きさの上限。設定では変えない。</summary>
        public const int Bytes = 8000000;

        /// <summary>要求の大きさ。数え方はIPCが本文を数えるのと同じUTF-8のバイト数とする。</summary>
        public static int Measure(string request)
        {
            return MessageChannel.MeasureBytes(request);
        }

        /// <summary>
        /// 予算に収まっていれば真。超えていれば偽を返し、断る内容を渡す。真を返したときは
        /// <paramref name="code"/> も <paramref name="message"/> も持たない。
        /// </summary>
        public static bool TryPass(string request, out string code, out string message)
        {
            int size = Measure(request);
            code = null;
            message = null;
            if (size <= Bytes)
            {
                return true;
            }

            code = ToolEnvelope.RequestTooLarge;
            message = string.Format(
                CultureInfo.InvariantCulture,
                "要求が要求サイズ予算に収まらない: {0} バイト(予算は {1} バイト)",
                size,
                Bytes);

            return false;
        }
    }
}
