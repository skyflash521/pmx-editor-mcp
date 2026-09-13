using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// UIスレッドでの実行を頼んだ結果。実行できなかったときは、なぜ受け付けられなかったのかを
    /// 呼び出し側がそのまま返せるよう、説明を持つ。
    /// </summary>
    public sealed class UiInvocation
    {
        private UiInvocation(bool ran, bool started, string unavailable)
        {
            DidRun = ran;
            DidStart = started;
            Unavailable = unavailable;
        }

        /// <summary>UIスレッドで実行した。</summary>
        public static UiInvocation Done { get; } = new UiInvocation(true, true, null);

        /// <summary>受付を止めているので実行しなかった。</summary>
        public static UiInvocation Declined { get; } = new UiInvocation(false, false, null);

        /// <summary>実行したかどうか。</summary>
        public bool DidRun { get; }

        /// <summary>
        /// UIスレッドへ委譲を渡したかどうか。渡していなければ何も起きていないので、
        /// 呼び出し側は同じ要求を投げ直せる。
        /// </summary>
        public bool DidStart { get; }

        /// <summary>
        /// UIスレッドで進められなかった事情。進められたときと、受付を止めていて実行しなかった
        /// ときは null。呼び出し側はこれをそのまま呼び出し元へ返す。
        /// </summary>
        public string Unavailable { get; }

        /// <summary>UIスレッドが進まず実行できなかった。事情はそのまま呼び出し元へ渡る。</summary>
        public static UiInvocation Blocked(string unavailable)
        {
            return new UiInvocation(false, true, Required(unavailable));
        }

        /// <summary>
        /// UIスレッドが空かず、委譲を渡さないまま戻った。事情はそのまま呼び出し元へ渡る。
        /// </summary>
        public static UiInvocation NotStarted(string unavailable)
        {
            return new UiInvocation(false, false, Required(unavailable));
        }

        private static string Required(string unavailable)
        {
            if (unavailable == null)
            {
                throw new ArgumentNullException(nameof(unavailable));
            }

            if (unavailable.Trim().Length == 0)
            {
                throw new ArgumentException("進められなかった事情が空。", nameof(unavailable));
            }

            return unavailable;
        }
    }
}
