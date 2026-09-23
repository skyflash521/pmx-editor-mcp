using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>どのPMXを相手にするかを決めた結果。</summary>
    public sealed class PmxTarget
    {
        public PmxTarget(object pmx, bool current)
        {
            Pmx = pmx;
            Current = current;
        }

        /// <summary>相手にするPMXの実体。</summary>
        public object Pmx { get; }

        /// <summary>現在のPMXの複製か。偽ならハンドルが指すPMXのオブジェクト。</summary>
        public bool Current { get; }
    }

    /// <summary>
    /// PMXのデータを相手にする呼び出しが、どのPMXを見るかを決め、複製編集の流れを閉じる。省略した
    /// 呼び出しは現在のPMXの状態を複製して相手にし、変えたぶんをまとめて反映する。指定した
    /// 呼び出しはそのハンドルが指すオブジェクトを直に相手にし、反映は要らない。
    /// このクラスの呼び出しはSDKへ届くので、UIスレッドへ委譲した中で使う。
    /// </summary>
    public sealed class PmxSession
    {
        /// <summary>どのPMXを見るかを切り替える共通引数の名前。</summary>
        public const string HandleName = "pmxHandle";

        private readonly SdkRelayTable _relay;

        private readonly IDictionary<string, SdkReceiver> _receivers;

        private readonly ResidentConnection _connection;

        private readonly PmxFlow _flow;

        private readonly Type _pmxType;

        private readonly UndoSuppression _undo;

        /// <summary>
        /// 中継・受け手の道・常駐と、複製編集の流れ・PMXの実体の型・Undoの抑止の枠を与えて
        /// 生成する。抑止の枠は接続をまたぐ1つの状態なので、流れが2つでも同じものを渡す。
        /// </summary>
        public PmxSession(
            SdkRelayTable relay,
            IDictionary<string, SdkReceiver> receivers,
            ResidentConnection connection,
            PmxFlow flow,
            Type pmxType,
            UndoSuppression undo)
        {
            if (relay == null)
            {
                throw new ArgumentNullException(nameof(relay));
            }

            if (receivers == null)
            {
                throw new ArgumentNullException(nameof(receivers));
            }

            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (flow == null)
            {
                throw new ArgumentNullException(nameof(flow));
            }

            if (pmxType == null)
            {
                throw new ArgumentNullException(nameof(pmxType));
            }

            if (undo == null)
            {
                throw new ArgumentNullException(nameof(undo));
            }

            _relay = relay;
            _receivers = receivers;
            _connection = connection;
            _flow = flow;
            _pmxType = pmxType;
            _undo = undo;
        }

        /// <summary>
        /// この流れでUndoの記録を止め戻しする相手。反映する行が止めるかどうかを引数で取る流れは
        /// 相手を持たないので null。
        /// </summary>
        public IUndoLock UndoLock
        {
            get { return _flow.StopUndo == null ? null : new UndoRows(this); }
        }

        /// <summary>
        /// 相手にするPMXを決める。ハンドルが有効でなければ偽で、断る内容を渡す。現在のPMXを
        /// 複製できなければ、その中継の断り方を渡す。
        /// </summary>
        public bool TryTake(
            long? handle,
            HandleLedger handles,
            out PmxTarget target,
            out string code,
            out string message)
        {
            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            target = null;
            code = null;
            message = null;
            if (handle.HasValue)
            {
                object held;
                if (handle.Value < int.MinValue || handle.Value > int.MaxValue
                    || !handles.TryGet((int)handle.Value, _pmxType.FullName, out held))
                {
                    code = ToolEnvelope.InvalidHandle;
                    message = HandleName + " が指すPMXが台帳に無い。";

                    return false;
                }

                target = new PmxTarget(held, false);

                return true;
            }

            object clone;
            SdkRelayRefusal refusal;
            if (!_relay.TryInvoke(
                _flow.StateRead, Receiver(), Passed(_flow.Reading, null), out clone, out refusal))
            {
                code = ToolEnvelope.NotApplicable;
                message = "現在のPMXを複製できない: " + _flow.StateRead;

                return false;
            }

            target = new PmxTarget(clone, true);

            return true;
        }

        /// <summary>
        /// 変えた複製をまとめて反映し、反映した中身をエディタの画面へ映す。現在のPMXを相手に
        /// していない呼び出しでは何もしない。<paramref name="suppressUndo"/> を頼まれたら、この
        /// 反映をエディタのUndoへ積ませない。映せなかったときは
        /// <paramref name="context"/> へその印を置く——映せないことは反映の失敗ではないので、
        /// 断りへ変えない。反映できなければ偽で、断る内容を渡す。<paramref name="listRow"/> は
        /// 反映で変えたリストの行で、分からなければ null。<paramref name="rewritten"/> は要素の
        /// 数を変えずに中身だけを書き換えた種類で、渡せばそれを映し直しの手がかりにする。
        /// </summary>
        public bool TryCommit(
            PmxTarget target,
            bool suppressUndo,
            McpMethodContext context,
            ScreenRefresh refresh,
            out string code,
            out string message,
            string listRow = null,
            IList<string> rewritten = null)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            code = null;
            message = null;
            if (!target.Current)
            {
                return true;
            }

            bool reflected = false;
            if (_flow.StopUndo != null && suppressUndo)
            {
                _undo.Run(new UndoRows(this), () => reflected = Reflect(target, false));
            }
            else
            {
                reflected = Reflect(target, suppressUndo);
            }

            if (reflected)
            {
                bool shown = rewritten == null
                    ? refresh.ApplyReflected(listRow)
                    : refresh.ApplyRewritten(rewritten);
                if (!shown)
                {
                    context.NotShown = true;
                }

                return true;
            }

            code = ToolEnvelope.NotApplicable;
            message = "複製したPMXを反映できない: " + _flow.Commit;

            return false;
        }

        /// <summary>まとめて反映する行を1度呼ぶ。</summary>
        private bool Reflect(PmxTarget target, bool suppressUndo)
        {
            object ignored;
            SdkRelayRefusal refusal;

            return _relay.TryInvoke(
                _flow.Commit,
                Receiver(),
                Passed(_flow.Reflecting, target.Pmx, suppressUndo),
                out ignored,
                out refusal);
        }

        /// <summary>流れが取る引数。置き場の並びのまま値を入れる。</summary>
        private object[] Passed(IList<FlowSlot> slots, object pmx, bool suppressUndo = false)
        {
            object[] passed = new object[slots.Count];
            for (int at = 0; at < slots.Count; at++)
            {
                switch (slots[at])
                {
                    case FlowSlot.Connector:
                        passed[at] = _connection.Use();
                        break;

                    case FlowSlot.Pmx:
                        passed[at] = pmx;
                        break;

                    default:
                        passed[at] = suppressUndo;
                        break;
                }
            }

            return passed;
        }

        /// <summary>SDKの行を呼んでUndoの記録を止め、また戻す相手。</summary>
        private sealed class UndoRows : IUndoLock
        {
            private readonly PmxSession _session;

            public UndoRows(PmxSession session)
            {
                _session = session;
            }

            public void Lock()
            {
                Invoke(_session._flow.StopUndo);
            }

            public void Unlock()
            {
                Invoke(_session._flow.ResumeUndo);
            }

            private void Invoke(string rowKey)
            {
                object ignored;
                SdkRelayRefusal refusal;
                if (!_session._relay.TryInvoke(
                    rowKey, _session.Receiver(), new object[0], out ignored, out refusal))
                {
                    throw new InvalidOperationException("Undoの記録を動かせない: " + rowKey);
                }
            }
        }

        private object Receiver()
        {
            if (_flow.ReceiverType == null)
            {
                return null;
            }

            SdkReceiver receiver;
            if (!_receivers.TryGetValue(_flow.ReceiverType, out receiver))
            {
                throw new InvalidOperationException("受け手を得る道が無い: " + _flow.ReceiverType);
            }

            return receiver(_connection);
        }
    }
}
