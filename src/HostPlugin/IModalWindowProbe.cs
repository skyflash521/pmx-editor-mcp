using System;

namespace PmxEditorMcp
{
    /// <summary>人の応答を待つ表示が出ていないかを見る。実装はUIスレッドを要さない手段で調べること。</summary>
    public interface IModalWindowProbe
    {
        /// <summary>出ているダイアログのタイトルと本文。出ていなければ null。</summary>
        string TryDescribe();
    }
}
