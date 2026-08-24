using System;
using System.Threading;
using System.Threading.Tasks;

namespace PmxEditorMcp.Bridge
{
    /// <summary>
    /// ホストへの要求を到着順に1件ずつ通す順番待ち。ホストは要求を直列に処理するので、同時に
    /// 未完了の要求を1件に抑える必要がある。単なる排他では待っている側のどれが次に通るかが
    /// 決まらないため、待つ側を並びとして持ち、到着順に譲る。
    /// </summary>
    internal sealed class HostRequestQueue
    {
        /// <summary>順番を待つ。自分の番が来るまで完了しない。</summary>
        public Task EnterAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>順番を次へ譲る。取り消された待ちは飛ばす。</summary>
        public void Leave()
        {
            throw new NotImplementedException();
        }
    }
}
