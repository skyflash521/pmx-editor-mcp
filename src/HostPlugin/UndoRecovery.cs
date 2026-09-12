using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// 止めたままのUndoの記録を戻しにいく窓口。止まっているのはエディタの側なので、接続をまたいで
    /// 1つだけ持ち、戻せたことを次の応答で1度だけ知らせる。
    /// </summary>
    public sealed class UndoRecovery
    {
        private readonly UndoSuppression _undo;

        private readonly IUndoLock _target;

        private readonly object _gate = new object();

        private bool _notice;

        /// <summary>
        /// 記録の枠と、記録を止め戻しする相手を与えて生成する。相手を持たない流れしか無ければ
        /// 相手は null でよく、そのときは戻すものが何も無い。
        /// </summary>
        public UndoRecovery(UndoSuppression undo, IUndoLock target)
        {
            if (undo == null)
            {
                throw new ArgumentNullException(nameof(undo));
            }

            _undo = undo;
            _target = target;
        }

        /// <summary>戻せていないものが残っているか。</summary>
        public bool HasLeftover
        {
            get { return _undo.HasLeftover; }
        }

        /// <summary>
        /// 残っているものを戻しにいく。残っていない状態にできたときだけ真。SDKを呼ぶので、
        /// UIスレッドへの委譲を通す。委譲そのものが走らなければ偽を返す。
        /// </summary>
        public bool TryRecover(IUiInvoker ui)
        {
            if (ui == null)
            {
                throw new ArgumentNullException(nameof(ui));
            }

            if (!_undo.HasLeftover)
            {
                return true;
            }

            if (_target == null)
            {
                return false;
            }

            bool cleared = false;
            if (!ui.TryInvokeOnUi(() => cleared = _undo.TryRecover(_target)).DidRun || !cleared)
            {
                return false;
            }

            lock (_gate)
            {
                _notice = true;
            }

            return true;
        }

        /// <summary>戻せたことをまだ知らせていなければ真を返し、以後は知らせ済みとする。</summary>
        public bool TryTakeNotice()
        {
            lock (_gate)
            {
                bool taken = _notice;
                _notice = false;

                return taken;
            }
        }
    }
}
