using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// 実行してよいかの判定を伴うUIスレッドへの委譲。判定の内容は実装が定める(稼働世代は
    /// 受付を止めた後に拒否する)。
    /// </summary>
    public interface IUiInvoker
    {
        /// <summary>UIスレッドで実行する。実行できなかったときは、その理由を結果が持つ。</summary>
        UiInvocation TryInvokeOnUi(Action action);
    }
}
