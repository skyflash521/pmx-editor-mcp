namespace PmxEditorMcp
{
    public enum HostStatus
    {
        Running,

        /// <summary>停止処理中。停止した稼働世代のIPCサーバースレッドがまだ終わっていない。</summary>
        Stopping,

        Stopped,

        /// <summary>開始せず。応答サイズ予算の設定が不正なため待受を開始していない。</summary>
        NotStartedInvalidBudget,
    }
}
