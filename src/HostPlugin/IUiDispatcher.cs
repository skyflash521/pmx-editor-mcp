using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// UIスレッドへの委譲そのもの。PEPlugin API はスレッドセーフを仮定できないため、
    /// ワーカースレッドからの呼び出しはすべてこれを通してUIスレッドで実行する。実行してよいかの
    /// 判定は含まないので、稼働世代の可否を伴う委譲は <see cref="IUiInvoker"/> を用いる。
    /// </summary>
    public interface IUiDispatcher
    {
        /// <summary>UIスレッドで実行し、完了するまで待つ。</summary>
        void Invoke(Action action);
    }
}
