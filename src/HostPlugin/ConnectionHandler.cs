using System.IO;

namespace PmxEditorMcp
{
    /// <summary>
    /// 接続1本ぶんの処理。戻るまでが1接続で、戻った時点で切断として扱われる。
    /// </summary>
    /// <param name="stream">接続済みのパイプ。</param>
    /// <param name="generation">この接続を受けた稼働世代。UIスレッドへの委譲に用いる。</param>
    public delegate void ConnectionHandler(Stream stream, HostGeneration generation);
}
