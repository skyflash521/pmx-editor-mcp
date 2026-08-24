using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace PmxEditorMcp.Bridge
{
    /// <summary>ホストへの接続を開く役。</summary>
    public interface IHostConnector
    {
        /// <summary>
        /// ホストへの接続を開く。接続先を決められないときと確立に失敗したときは
        /// <see cref="BridgeException"/> を投げる。
        /// </summary>
        Task<Stream> ConnectAsync(CancellationToken cancellationToken);
    }

    /// <summary>環境変数と起動中のPMXエディタから決めた名前付きパイプへ接続する。</summary>
    public sealed class NamedPipeHostConnector : IHostConnector
    {
        /// <summary>
        /// 環境変数と起動中のPMXエディタから接続先を決め、名前付きパイプを開く既定の処理で生成する。
        /// </summary>
        public NamedPipeHostConnector()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 接続先を決める処理とパイプを開く処理を差し替えて生成する。入出力の失敗やアクセス拒否を
        /// エラーコードへ変換する経路は、実際にそれらを起こさないと通らないため、外から与える。
        /// 接続先の決定も差し替えるのは、実行環境のエディタの起動状況で手前の分岐へ逸れないようにするため。
        /// </summary>
        internal NamedPipeHostConnector(
            Func<string> resolvePipeName,
            Func<string, CancellationToken, Task<Stream>> openPipe)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 接続のたびに接続先を決め直してから開く。エディタの起動・終了でパイプ名は変わるので、
        /// 一度決めた名前を握り続けると、繋ぎ直しが消えたエディタを指したままになる。
        /// </summary>
        public Task<Stream> ConnectAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// ホストへ要求を中継する。ツール呼び出しのたびに接続を開くのではなく、最初の呼び出しで
    /// 接続して handshake し、以後はその接続を再利用する。応答の不正・切断・予算の不一致は
    /// いずれも接続を捨て、次の呼び出しで新しい接続からやり直す。
    /// </summary>
    public sealed class HostIpcClient : IDisposable
    {
        /// <summary>handshake で一致していなければならないプロトコル番号。</summary>
        public const int Protocol = 1;

        /// <summary>
        /// 接続の開き方と、ホストと一致していなければならない応答サイズ予算の文字数を与えて生成する。
        /// </summary>
        public HostIpcClient(IHostConnector connector, int budgetChars)
        {
            throw new NotImplementedException();
        }

        /// <summary>ホストと一致していなければならない応答サイズ予算の文字数。</summary>
        public int BudgetChars => throw new NotImplementedException();

        /// <summary>ホストへの接続を保っているかどうか。</summary>
        public bool IsConnected => throw new NotImplementedException();

        /// <summary>
        /// ホストのメソッドを1件呼び、成功応答の結果を返す。未接続なら接続して handshake を
        /// 済ませてから送る。失敗は <see cref="BridgeException"/> で返す。
        /// </summary>
        public Task<JsonNode> CallAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>保っている接続を閉じる。</summary>
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
