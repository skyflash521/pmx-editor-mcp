using System;
using System.Threading;

namespace PmxEditorMcp
{
    /// <summary>
    /// ハンドルIDを発行する。ホストが1つだけ持ち、どのセッションの台帳もここから採る。セッション
    /// ごとに持つと、別のセッションが同じIDを発行して、繋ぎ直しや複数のセッションをまたいだときに
    /// 指す先が定まらなくなる。複数のスレッドから同時に呼んでよい。
    /// </summary>
    public sealed class HandleIdIssuer
    {
        /// <summary>
        /// 決して発行しないID。台帳に無いことが確かなハンドルとして渡せる値が要る検査のために
        /// 空けてある。発行は1から増えるので、この値へ届く前に発行できる数を使い切る。
        /// </summary>
        public const int Reserved = int.MaxValue;

        // 数える器はintより広く採る。intのまま数えると、使い切ったあとの呼び出しで折り返して
        // 負のIDを配り始める。
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

        /// <summary>直前に配ったID。まだ配っていなければ0。</summary>
        internal int Last
        {
            get { return (int)Interlocked.Read(ref _last); }
        }

        /// <summary>そこまで配った状態にする。配り切る境目を確かめるために要る。</summary>
        internal void SkipTo(int issued)
        {
            Interlocked.Exchange(ref _last, issued);
        }
    }

    /// <summary>
    /// イベントの連番を発行する。持ち方の理由は <see cref="HandleIdIssuer"/> と同じ。
    /// 複数のスレッドから同時に呼んでよい。
    /// </summary>
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
