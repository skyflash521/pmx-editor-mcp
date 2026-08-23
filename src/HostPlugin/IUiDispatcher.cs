using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// UIスレッドへの委譲。PEPlugin API はスレッドセーフを仮定できないため、
    /// ワーカースレッドからの呼び出しはすべてこれを通してUIスレッドで実行する。
    /// </summary>
    public interface IUiDispatcher
    {
        /// <summary>UIスレッドで実行し、完了するまで待つ。</summary>
        void Invoke(Action action);
    }
}
