using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp
{
    /// <summary>複製編集の流れが渡す値の置き場。どの行がどれを取るかはビルド時に決まっている。</summary>
    public enum FlowSlot
    {
        /// <summary>Cプラグイン連携の常駐コネクタ。</summary>
        Connector,

        /// <summary>現在のPMXの複製。</summary>
        Pmx,

        /// <summary>反映のときにUndoの記録を止めるかどうか。</summary>
        UndoLock,
    }

    /// <summary>
    /// 現在のPMXを複製して、変えた中身をまとめて反映するまでの流れ1つ。SDKはPEPlugin側と
    /// Cプラグイン連携側で別の流れを持ち、片方で作った中身をもう片方で反映することはできない。
    /// </summary>
    public sealed class PmxFlow
    {
        public PmxFlow(
            string stateRead,
            string commit,
            string receiverType,
            IList<FlowSlot> reading,
            IList<FlowSlot> reflecting,
            string stopUndo = null,
            string resumeUndo = null)
        {
            if (stateRead == null)
            {
                throw new ArgumentNullException(nameof(stateRead));
            }

            if (commit == null)
            {
                throw new ArgumentNullException(nameof(commit));
            }

            if (reading == null)
            {
                throw new ArgumentNullException(nameof(reading));
            }

            if (reflecting == null)
            {
                throw new ArgumentNullException(nameof(reflecting));
            }

            StateRead = stateRead;
            Commit = commit;
            ReceiverType = receiverType;
            Reading = new ReadOnlyCollection<FlowSlot>(reading);
            Reflecting = new ReadOnlyCollection<FlowSlot>(reflecting);
            StopUndo = stopUndo;
            ResumeUndo = resumeUndo;
        }

        /// <summary>現在のPMXの複製を得る行。</summary>
        public string StateRead { get; }

        /// <summary>複製した中身をまとめて反映する行。</summary>
        public string Commit { get; }

        /// <summary>この2つの行の受け手を引く鍵。静的なメンバーの流れでは null。</summary>
        public string ReceiverType { get; }

        /// <summary>複製を得る行が取る引数の置き場。並びは引数の並びと同じ。</summary>
        public IList<FlowSlot> Reading { get; }

        /// <summary>反映する行が取る引数の置き場。並びは引数の並びと同じ。</summary>
        public IList<FlowSlot> Reflecting { get; }

        /// <summary>
        /// Undoの記録を止める行。反映する行が止めるかどうかを引数で取る流れでは null。
        /// </summary>
        public string StopUndo { get; }

        /// <summary>止めたUndoの記録を戻す行。<see cref="StopUndo"/> と対で持つ。</summary>
        public string ResumeUndo { get; }
    }
}
