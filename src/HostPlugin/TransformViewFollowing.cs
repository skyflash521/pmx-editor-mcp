using System;

namespace PmxEditorMcp
{
    public interface IModelUpdates
    {
        int Count { get; }
    }

    public sealed class TransformViewFollowing : IUiDispatcher
    {
        private readonly IUiDispatcher _inner;

        private readonly IModelUpdates _updates;

        private readonly Func<object> _transformView;

        /// <summary><paramref name="transformView"/> は TransformView の口を返す。UIスレッドで呼ばれる。</summary>
        public TransformViewFollowing(IUiDispatcher inner, IModelUpdates updates, Func<object> transformView)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
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
                    TransformViewSync.Refresh(_transformView());
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
