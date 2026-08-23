using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// 名前付きパイプの待受と稼働世代を持つホスト本体。開始・停止は排他ロックで直列化し、
    /// 呼び出しスレッド(UIスレッドでありうる)をIPCサーバースレッドの終了待ちでブロックしない。
    /// </summary>
    public sealed class McpHost
    {
        /// <summary>
        /// 待受に使うパイプ名・ログ・応答サイズ予算・UIディスパッチ・接続処理を与えて生成する。
        /// </summary>
        public McpHost(
            string pipeName,
            HostLog log,
            ResponseBudget budget,
            IUiDispatcher uiDispatcher,
            ConnectionHandler connectionHandler)
        {
            throw new NotImplementedException();
        }

        /// <summary>待受に使うパイプ名。</summary>
        public string PipeName => throw new NotImplementedException();

        /// <summary>ログの書き込み先。</summary>
        public string LogFilePath => throw new NotImplementedException();

        /// <summary>応答サイズ予算の設定。</summary>
        public ResponseBudget Budget => throw new NotImplementedException();

        /// <summary>現在の稼働状態の区分。呼ばれるたびに判定する。</summary>
        public HostStatus Status => throw new NotImplementedException();

        /// <summary>クライアントと接続中かどうか。</summary>
        public bool IsClientConnected => throw new NotImplementedException();

        /// <summary>エディタのプロセスIDから待受に使うパイプ名を組み立てる。</summary>
        public static string BuildPipeName(int editorProcessId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 新しい稼働世代を作って待受を始める。開始できないときは偽を返し、
        /// <paramref name="reason"/> に理由を入れる。
        /// </summary>
        public bool TryStart(out string reason)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 現在の稼働世代の受付を止め、公開中のパイプインスタンスを閉じる。
        /// IPCサーバースレッドの終了は待たない。
        /// </summary>
        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
}
