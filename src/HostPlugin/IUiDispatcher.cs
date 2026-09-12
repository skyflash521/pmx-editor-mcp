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
        /// <summary>UIスレッドでの実行を始める。待ち合わせは <see cref="Wait"/> で行う。</summary>
        IAsyncResult Begin(Action action);

        /// <summary>
        /// 始めた実行が終わるのを、与えた長さまで待つ。終わっていれば真。偽で戻ったときも実行は
        /// 続いているので、同じ委譲をもう一度始めてはならない。
        /// </summary>
        bool Wait(IAsyncResult pending, TimeSpan limit);

        /// <summary>始めた実行の後始末をする。実行が例外で終わっていればそれを投げる。</summary>
        void End(IAsyncResult pending);
    }
}
