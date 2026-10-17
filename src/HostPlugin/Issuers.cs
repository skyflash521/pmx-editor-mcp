using System;
using System.Threading;

namespace PmxEditorMcp
{
    /// <summary>
    /// ハンドルIDを発行する。ホストが1つだけ持ち、どのセッションの台帳もここから採る。
    /// 複数のスレッドから同時に呼んでよい。
    /// </summary>
    public sealed class HandleIdIssuer
    {
        /// <summary>決して発行しないID。台帳に無いことが確かなハンドルとして渡せる値。</summary>
        public const int Reserved = int.MaxValue;

        private long _last;

        /// <summary>次のID。1から増える。<see cref="Reserved"/> 以上は配らない。</summary>
        public int Next()
        {
            long next = Interlocked.Increment(ref _last);
            if (next >= Reserved)
            {
                throw new InvalidOperationException("発行できるハンドルIDを使い切った。");
            }

            return (int)next;
        }

    }

    /// <summary>イベントの連番を発行する。複数のスレッドから同時に呼んでよい。</summary>
    public sealed class EventSequenceIssuer
    {
        private long _last;

        /// <summary>次の連番。1から増える。</summary>
        public long Next()
        {
            return Interlocked.Increment(ref _last);
        }
    }
}
