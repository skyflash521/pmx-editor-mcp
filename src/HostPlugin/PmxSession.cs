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

        /// <summary>
        /// 中継・受け手の道・常駐と、複製編集の流れ・PMXの実体の型を与えて生成する。
        /// </summary>
        public PmxSession(
            SdkRelayTable relay,
            IDictionary<string, SdkReceiver> receivers,
            ResidentConnection connection,
            PmxFlow flow,
            Type pmxType)
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

            _relay = relay;
            _receivers = receivers;
            _connection = connection;
            _flow = flow;
            _pmxType = pmxType;
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
        /// 変えた複製をまとめて反映する。現在のPMXを相手にしていない呼び出しでは何もしない。
        /// 反映できなければ偽で、断る内容を渡す。
        /// </summary>
        public bool TryCommit(PmxTarget target, out string code, out string message)
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

            object ignored;
            SdkRelayRefusal refusal;
            if (_relay.TryInvoke(
                _flow.Commit,
                Receiver(),
                Passed(_flow.Reflecting, target.Pmx),
                out ignored,
                out refusal))
            {
                return true;
            }

            code = ToolEnvelope.NotApplicable;
            message = "複製したPMXを反映できない: " + _flow.Commit;

            return false;
        }

        /// <summary>流れが取る引数。置き場の並びのまま値を入れる。</summary>
        private object[] Passed(IList<FlowSlot> slots, object pmx)
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
                        // 反映は取り消しへ積む。抑止は共通引数が別に受け持つ。
                        passed[at] = true;
                        break;
                }
            }

            return passed;
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
