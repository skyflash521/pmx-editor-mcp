using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// 実行してよいかの判定を伴うUIスレッドへの委譲。判定の内容は実装が定める(稼働世代は
    /// 受付を止めた後に断る)。<see cref="IUiDispatcher"/> が委譲そのものを担うのに対し、
    /// こちらは可否を含む。ツールの処理はこれだけを使うので、実機のエディタが無くても差し替えられる。
    /// </summary>
    public interface IUiInvoker
    {
        /// <summary>UIスレッドで実行する。実行しなかったときは偽を返す。</summary>
        bool TryInvokeOnUi(Action action);
    }
}
