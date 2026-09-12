using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// 人の応答を待つ表示が出ていないかを見る。UIスレッドが進まないときに、その原因を呼び出し側へ
    /// 返すために使う。見る側はUIスレッドの外に居るので、この実装はUIスレッドを要さない手段で
    /// 調べること。
    /// </summary>
    public interface IModalWindowProbe
    {
        /// <summary>出ている表示の文面。出ていなければ null。</summary>
        string TryDescribe();
    }
}
