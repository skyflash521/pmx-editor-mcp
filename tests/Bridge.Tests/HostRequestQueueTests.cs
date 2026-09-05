using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public class HostRequestQueueTests
    {
        [Fact]
        public void PassesImmediatelyWhenNobodyIsWaiting()
        {
            HostRequestQueue queue = new HostRequestQueue();

            Task first = queue.EnterAsync(CancellationToken.None);

            Assert.True(first.IsCompletedSuccessfully);
        }

        [Fact]
        public void DoesNotPassWhileAnEarlierHolderRemains()
        {
            HostRequestQueue queue = new HostRequestQueue();
            queue.EnterAsync(CancellationToken.None);

            Task second = queue.EnterAsync(CancellationToken.None);

            Assert.False(second.IsCompleted);
        }

        [Fact]
        public async Task PassesInArrivalOrder()
        {
            HostRequestQueue queue = new HostRequestQueue();
            await queue.EnterAsync(CancellationToken.None);

            // 待ちに並んだことは、返された待ちがまだ完了していないことで分かる。
            Task second = queue.EnterAsync(CancellationToken.None);
            Task third = queue.EnterAsync(CancellationToken.None);
            Assert.False(second.IsCompleted);
            Assert.False(third.IsCompleted);

            queue.Leave();

            await second;
            Assert.False(third.IsCompleted);

            queue.Leave();

            await third;
        }

        [Fact]
        public async Task CancellingWhileWaitingCancelsOnlyThatWait()
        {
            HostRequestQueue queue = new HostRequestQueue();
            await queue.EnterAsync(CancellationToken.None);

            using CancellationTokenSource giving = new CancellationTokenSource();
            Task cancelled = queue.EnterAsync(giving.Token);
            Task following = queue.EnterAsync(CancellationToken.None);

            giving.Cancel();

            await Assert.ThrowsAnyAsync<System.OperationCanceledException>(() => cancelled);

            // 取り消した待ちは順番を消費しないので、次の順番は後続へ渡る。
            Assert.False(following.IsCompleted);

            queue.Leave();

            await following;
        }

        [Fact]
        public async Task NextWaiterPassesImmediatelyWhenNobodyElseWaits()
        {
            HostRequestQueue queue = new HostRequestQueue();
            await queue.EnterAsync(CancellationToken.None);

            queue.Leave();

            Task next = queue.EnterAsync(CancellationToken.None);

            Assert.True(next.IsCompletedSuccessfully);
        }

        /// <summary>
        /// 待つ相手がいないときに取り消しを見ずに通す作りだと、取り消した呼び出しが
        /// そのまま順番を握り、以後の呼び出しを塞いでしまう。
        /// </summary>
        [Fact]
        public void AlreadyCancelledTokenDoesNotPassWithoutHolder()
        {
            HostRequestQueue queue = new HostRequestQueue();

            using CancellationTokenSource given = new CancellationTokenSource();
            given.Cancel();

            Task cancelled = queue.EnterAsync(given.Token);

            Assert.True(cancelled.IsCanceled);

            // 順番を握ったまま取り消していれば、続く待ちが通らない。
            Assert.True(queue.EnterAsync(CancellationToken.None).IsCompletedSuccessfully);
        }

        [Fact]
        public void AlreadyCancelledTokenDoesNotPassWithHolder()
        {
            HostRequestQueue queue = new HostRequestQueue();
            queue.EnterAsync(CancellationToken.None);

            using CancellationTokenSource given = new CancellationTokenSource();
            given.Cancel();

            Task cancelled = queue.EnterAsync(given.Token);

            Assert.True(cancelled.IsCanceled);
        }
    }
}
