using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// ホストの稼働世代。受付停止の有無・公開中のパイプインスタンス・IPCサーバースレッドを
    /// ひとまとめに持つ。停止手順も状態判定もUIディスパッチの可否もこの単位で決まり、
    /// 停止した稼働世代での禁止は新しい稼働世代へ持ち越さない。
    /// </summary>
    public sealed class HostGeneration
    {
        /// <summary>稼働世代はホストだけが作る。</summary>
        internal HostGeneration()
        {
            throw new NotImplementedException();
        }

        /// <summary>この稼働世代で受付を止めたかどうか。</summary>
        public bool IsStopRequested => throw new NotImplementedException();

        /// <summary>
        /// UIスレッドで実行する。停止した稼働世代では実行せず偽を返す。
        /// </summary>
        public bool TryInvokeOnUi(Action action)
        {
            throw new NotImplementedException();
        }
    }
}
