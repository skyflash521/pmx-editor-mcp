using System.Collections.Generic;
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
        private readonly object _gate = new object();
        private readonly Queue<TaskCompletionSource> _waiting = new Queue<TaskCompletionSource>();

        private bool _held;

        /// <summary>順番を待つ。自分の番が来るまで完了しない。</summary>
        public Task EnterAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // 取り消し済みのまま順番を握ると、以後の呼び出しを永久に塞ぐ。
                return Task.FromCanceled(cancellationToken);
            }

            TaskCompletionSource waiter;
            lock (_gate)
            {
                if (!_held)
                {
                    _held = true;
                    return Task.CompletedTask;
                }

                // 継続を同期的に走らせると、順番を譲る側がロックを持ったまま次の処理へ入る。
                waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiting.Enqueue(waiter);
            }

            CancellationTokenRegistration registration = cancellationToken.Register(
                () => waiter.TrySetCanceled(cancellationToken));

            return AwaitTurnAsync(waiter, registration);
        }

        /// <summary>順番を次へ譲る。取り消された待ちは飛ばす。</summary>
        public void Leave()
        {
            TaskCompletionSource next = null;

            lock (_gate)
            {
                while (_waiting.Count > 0)
                {
                    TaskCompletionSource candidate = _waiting.Dequeue();
                    if (candidate.TrySetResult())
                    {
                        next = candidate;
                        break;
                    }

                    // 取り消された待ちは順番を消費しない。並びから外して次を見る。
                }

                if (next == null)
                {
                    _held = false;
                }
            }
        }

        private static async Task AwaitTurnAsync(
            TaskCompletionSource waiter, CancellationTokenRegistration registration)
        {
            try
            {
                await waiter.Task.ConfigureAwait(false);
            }
            finally
            {
                registration.Dispose();
            }
        }
    }
}
