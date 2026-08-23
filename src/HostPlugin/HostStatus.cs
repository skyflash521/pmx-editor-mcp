namespace PmxEditorMcp
{
    /// <summary>ホストの稼働状態の区分。メニュー再実行のたびに判定する。</summary>
    public enum HostStatus
    {
        /// <summary>稼働中。要求を受け付けている。</summary>
        Running,

        /// <summary>停止処理中。停止した稼働世代のIPCサーバースレッドがまだ終わっていない。</summary>
        Stopping,

        /// <summary>停止済み。開始できる。</summary>
        Stopped,

        /// <summary>開始せず。応答サイズ予算の設定が不正なため待受を開始していない。</summary>
        NotStartedInvalidBudget,
    }
}
