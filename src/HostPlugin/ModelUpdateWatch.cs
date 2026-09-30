using System;
using System.Threading;
using PXCPlugin;
using PXCPlugin.Event;

namespace PmxEditorMcp
{
    /// <summary>数えるのはエディタのUIスレッドの上である。</summary>
    public sealed class ModelUpdateWatch : IModelUpdates, IDisposable
    {
        private readonly IPXEventConnector _events;

        private readonly IPXViewEventListener _listener;

        private int _count;

        private int _moves;

        private ModelUpdateWatch(IPXEventConnector events)
        {
            _events = events;
            _listener = events.CreateViewEventListener();
            _listener.ModelUpdated += (sender, e) => Interlocked.Increment(ref _count);
            _listener.Undo += (sender, e) => Interlocked.Increment(ref _moves);
            _listener.Redo += (sender, e) => Interlocked.Increment(ref _moves);
        }

        public int Count
        {
            get { return Volatile.Read(ref _count); }
        }

        public int HistoryMoves
        {
            get { return Volatile.Read(ref _moves); }
        }

        /// <summary>UIスレッドで呼ぶ。</summary>
        public static ModelUpdateWatch Start(IPXCPluginConnector connector)
        {
            if (connector == null)
            {
                throw new ArgumentNullException(nameof(connector));
            }

            return new ModelUpdateWatch(PXCBridge.CreateEventConnector(connector));
        }

        public void Dispose()
        {
            _events.ReleaseViewEventListener(_listener);
            PXCBridge.ReleaseEventConnector(_events);
        }
    }
}
