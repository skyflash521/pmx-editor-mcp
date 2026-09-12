using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp
{
    /// <summary>キューへ溜めたイベント1件。</summary>
    public sealed class QueuedEvent
    {
        public QueuedEvent(long seq, string type, int sourceHandle, object payload)
        {
            Seq = seq;
            Type = type;
            SourceHandle = sourceHandle;
            Payload = payload;
        }

        /// <summary>
        /// ホストが1から増やす連番。発行元はホストにひとつなので、このキューの中では飛びうる
        /// ——ほかのキューへ発行したぶんがそこに入る。取りこぼしは
        /// <see cref="EventDrainResult.Dropped"/> で知る。
        /// </summary>
        public long Seq { get; }

        /// <summary>イベント種別の識別子。</summary>
        public string Type { get; }

        /// <summary>そのイベントを購読しているリスナのハンドルID。</summary>
        public int SourceHandle { get; }

        /// <summary>イベント固有の値。</summary>
        public object Payload { get; }
    }

    /// <summary>取り出しの判定が、そのイベントをどう扱うかを返す。</summary>
    public enum EventFit
    {
        /// <summary>取り出す。</summary>
        Take,

        /// <summary>収まらないので捨てる。捨てた件数へ数える。</summary>
        Drop,

        /// <summary>ここで打ち切る。列には残す。</summary>
        Stop,
    }

    /// <summary>キューから取り出した結果。</summary>
    public sealed class EventDrainResult
    {
        public EventDrainResult(IList<QueuedEvent> events, int dropped, int remaining)
        {
            Events = new ReadOnlyCollection<QueuedEvent>(events);
            Dropped = dropped;
            Remaining = remaining;
        }

        /// <summary>取り出したイベント。古い順。</summary>
        public IList<QueuedEvent> Events { get; }

        /// <summary>前回の取り出しから今回までに、あふれて捨てた件数。</summary>
        public int Dropped { get; }

        /// <summary>取り出した後にキューへ残っている件数。</summary>
        public int Remaining { get; }
    }

    /// <summary>
    /// セッションが溜める購読中のイベント。押し出す通知は持たず、取りに来たぶんだけ渡す。あふれたら
    /// 古いものから捨て、捨てた件数を次の取り出しで知らせる。
    /// 複数のスレッドから同時に呼んでよい。
    /// </summary>
    public sealed class EventQueue
    {
        /// <summary>溜めておける件数。超えたぶんは古い順に捨てる。</summary>
        public const int Capacity = 1000;

        /// <summary>1回の取り出しの既定の件数。</summary>
        public const int DefaultLimit = 100;

        /// <summary>1回の取り出しに指定できる最大の件数。</summary>
        public const int MaxLimit = 1000;

        private readonly Queue<QueuedEvent> _events = new Queue<QueuedEvent>();

        private readonly object _gate = new object();

        private readonly EventSequenceIssuer _issuer;

        private long _lastSeq;

        private int _dropped;

        private bool _closed;

        public EventQueue(EventSequenceIssuer issuer)
        {
            if (issuer == null)
            {
                throw new ArgumentNullException(nameof(issuer));
            }

            _issuer = issuer;
        }

        /// <summary>閉じていれば真。</summary>
        public bool IsClosed
        {
            get
            {
                lock (_gate)
                {
                    return _closed;
                }
            }
        }

        /// <summary>
        /// 溜めるのをやめ、残っているイベントを捨てる。セッションが終わるときに呼ぶ。以後は
        /// 溜めることも取り出すこともできない——取り出せると、誰のものでもない列を読めてしまう。
        /// </summary>
        public void Close()
        {
            lock (_gate)
            {
                _closed = true;
                _events.Clear();
                _dropped = 0;
            }
        }

        /// <summary>いま溜まっている件数。</summary>
        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _events.Count;
                }
            }
        }

        /// <summary>
        /// イベントを溜める。溜められる件数を超えるときは、いちばん古いものを捨てて捨てた件数を
        /// 数える。<paramref name="sourceHandle"/> は購読しているリスナのハンドルIDで、
        /// 正の整数でなければならない。
        /// </summary>
        public QueuedEvent Enqueue(string type, int sourceHandle, object payload)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (type.Trim().Length == 0)
            {
                throw new ArgumentException("空にも空白だけにもできない。", nameof(type));
            }

            if (sourceHandle < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceHandle), sourceHandle, "ハンドルIDは正の整数である。");
            }

            lock (_gate)
            {
                RequireOpen();
                _lastSeq = _issuer.Next();
                QueuedEvent queued = new QueuedEvent(_lastSeq, type, sourceHandle, payload);
                _events.Enqueue(queued);
                while (_events.Count > Capacity)
                {
                    _events.Dequeue();
                    _dropped++;
                }

                return queued;
            }
        }

        /// <summary>
        /// 古い順に <paramref name="limit"/> 件まで取り出す。捨てた件数は返したところで0へ戻す
        /// ——次の取り出しが知らせるのは、その取り出しまでに捨てたぶんである。
        /// </summary>
        public EventDrainResult Drain(int limit)
        {
            return Drain(limit, queued => EventFit.Take);
        }

        /// <summary>
        /// 古い順に <paramref name="limit"/> 件まで、<paramref name="decide"/> が取り出すと判じた
        /// ものを取り出す。捨てると判じたものは列から外して捨てた件数へ数え、打ち切ると判じたものは
        /// 列に残す。捨てた件数は返したところで0へ戻す。
        /// </summary>
        public EventDrainResult Drain(int limit, Func<QueuedEvent, EventFit> decide)
        {
            if (limit < 1 || limit > MaxLimit)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(limit), limit, "1以上 " + MaxLimit + " 以下でなければならない。");
            }

            if (decide == null)
            {
                throw new ArgumentNullException(nameof(decide));
            }

            lock (_gate)
            {
                RequireOpen();
                List<QueuedEvent> taken = new List<QueuedEvent>();
                while (taken.Count < limit && _events.Count != 0)
                {
                    EventFit fit = decide(_events.Peek());
                    if (fit == EventFit.Stop)
                    {
                        break;
                    }

                    QueuedEvent queued = _events.Dequeue();
                    if (fit == EventFit.Take)
                    {
                        taken.Add(queued);
                    }
                    else
                    {
                        _dropped++;
                    }
                }

                int dropped = _dropped;
                _dropped = 0;

                return new EventDrainResult(taken, dropped, _events.Count);
            }
        }

        private void RequireOpen()
        {
            if (_closed)
            {
                throw new InvalidOperationException("キューは閉じている。");
            }
        }
    }
}
