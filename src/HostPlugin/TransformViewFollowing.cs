using System;

namespace PmxEditorMcp
{
    public interface IModelUpdates
    {
        int Count { get; }

        int HistoryMoves { get; }
    }

    public sealed class TransformViewFollowing : IUiDispatcher
    {
        private readonly IUiDispatcher _inner;

        private readonly HostLog _log;

        private readonly IModelUpdates _updates;

        private readonly Func<object> _transformView;

        /// <summary><paramref name="transformView"/> は TransformView のコネクタを返す。UIスレッドで呼ばれる。</summary>
        public TransformViewFollowing(
            IUiDispatcher inner, HostLog log, IModelUpdates updates, Func<object> transformView)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _updates = updates ?? throw new ArgumentNullException(nameof(updates));
            _transformView = transformView ?? throw new ArgumentNullException(nameof(transformView));
        }

        public IAsyncResult Begin(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            return _inner.Begin(() =>
            {
                int updated = _updates.Count;
                int refreshed = TransformViewSync.Refreshes;
                action();
                if (_updates.Count != updated && TransformViewSync.Refreshes == refreshed)
                {
                    try
                    {
                        TransformViewSync.Refresh(_transformView());
                    }
                    catch (Exception exception)
                    {
                        _log.WriteException("TransformView の読み直しで例外が起きた。", exception);
                    }
                }
            });
        }

        public bool Wait(IAsyncResult pending, TimeSpan limit)
        {
            return _inner.Wait(pending, limit);
        }

        public void End(IAsyncResult pending)
        {
            _inner.End(pending);
        }
    }
}
