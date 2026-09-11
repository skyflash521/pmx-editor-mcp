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
        private int _last;

        /// <summary>次のID。1から増える。</summary>
        public int Next()
        {
            return Interlocked.Increment(ref _last);
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
