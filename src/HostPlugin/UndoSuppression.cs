using System;

namespace PmxEditorMcp
{
    /// <summary>Undoの記録を止めたり戻したりする相手。</summary>
    public interface IUndoLock
    {
        /// <summary>Undoの記録を止める。</summary>
        void Lock();

        /// <summary>Undoの記録を戻す。</summary>
        void Unlock();
    }

    /// <summary>
    /// Undoの記録を止めて編集を走らせる枠。止めたら必ず戻すが、戻せないことがありうるので、
    /// 戻せなかったことを覚えて後から回収する。覚えているのは接続をまたぐ1つの状態で、
    /// 複数のスレッドから同時に呼んでよい。
    /// </summary>
    public sealed class UndoSuppression
    {
        private enum State
        {
            None,

            Left,

            Recovering,
        }

        private readonly HostLog _log;

        private readonly object _gate = new object();

        private State _state;

        public UndoSuppression(HostLog log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            _log = log;
        }

        /// <summary>戻せていないものが残っているか。戻している最中も残っているものとして数える。</summary>
        public bool HasLeftover
        {
            get
            {
                lock (_gate)
                {
                    return _state != State.None;
                }
            }
        }

        /// <summary>
        /// Undoの記録を止めて編集を走らせ、成否によらず戻す。戻せなかったときは真を返す。編集が
        /// 投げた例外はそのまま通す。
        /// </summary>
        public bool Run(IUndoLock target, Action edit)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            bool left;
            target.Lock();
            try
            {
                edit();
            }
            finally
            {
                left = !Release(target);
            }

            return left;
        }

        /// <summary>
        /// 残っているものを戻せないか試す。残っていない状態にできたときだけ真を返す。戻せなかった
        /// とき、戻す間に新しく残ったとき、ほかの経路が戻している最中は、いずれも偽を返す。残って
        /// いなければ何もせず真を返す。
        /// </summary>
        public bool TryRecover(IUndoLock target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            lock (_gate)
            {
                if (_state == State.None)
                {
                    return true;
                }

                if (_state == State.Recovering)
                {
                    return false;
                }

                _state = State.Recovering;
            }

            if (!TryUnlock(target))
            {
                lock (_gate)
                {
                    _state = State.Left;
                }

                return false;
            }

            bool cleared;
            lock (_gate)
            {
                cleared = _state == State.Recovering;
                if (cleared)
                {
                    _state = State.None;
                }
            }

            if (cleared)
            {
                _log.Write("Undoの記録の回収: 成功");
            }

            return cleared;
        }

        private bool Release(IUndoLock target)
        {
            if (TryUnlock(target))
            {
                return true;
            }

            if (TryUnlock(target))
            {
                return true;
            }

            bool first;
            lock (_gate)
            {
                first = _state == State.None;
                _state = State.Left;
            }

            if (first)
            {
                _log.Write("Undoの記録が止まったまま残った。");
            }

            return false;
        }

        private bool TryUnlock(IUndoLock target)
        {
            try
            {
                target.Unlock();

                return true;
            }
            catch (Exception exception)
            {
                _log.WriteException("Undoの記録を戻せなかった。", exception);

                return false;
            }
        }
    }
}
