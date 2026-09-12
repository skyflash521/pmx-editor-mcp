using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// UIスレッドでの実行を頼んだ結果。実行できなかったときは、なぜ受け付けられなかったのかを
    /// 呼び出し側がそのまま返せるよう、説明を持つ。
    /// </summary>
    public sealed class UiInvocation
    {
        private UiInvocation(bool ran, string unavailable)
        {
            DidRun = ran;
            Unavailable = unavailable;
        }

        /// <summary>UIスレッドで実行した。</summary>
        public static UiInvocation Done { get; } = new UiInvocation(true, null);

        /// <summary>受付を止めているので実行しなかった。</summary>
        public static UiInvocation Declined { get; } = new UiInvocation(false, null);

        /// <summary>実行したかどうか。</summary>
        public bool DidRun { get; }

        /// <summary>
        /// UIスレッドで進められなかった事情。進められたときと、受付を止めていて実行しなかった
        /// ときは null。呼び出し側はこれをそのまま呼び出し元へ返す。
        /// </summary>
        public string Unavailable { get; }

        /// <summary>UIスレッドが進まず実行できなかった。事情はそのまま呼び出し元へ渡る。</summary>
        public static UiInvocation Blocked(string unavailable)
        {
            if (unavailable == null)
            {
                throw new ArgumentNullException(nameof(unavailable));
            }

            if (unavailable.Trim().Length == 0)
            {
                throw new ArgumentException("進められなかった事情が空。", nameof(unavailable));
            }

            return new UiInvocation(false, unavailable);
        }
    }
}
