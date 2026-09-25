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

        private ModelUpdateWatch(IPXEventConnector events)
        {
            _events = events;
            _listener = events.CreateViewEventListener();
            _listener.ModelUpdated += (sender, e) => Interlocked.Increment(ref _count);
        }

        public int Count
        {
            get { return Volatile.Read(ref _count); }
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
