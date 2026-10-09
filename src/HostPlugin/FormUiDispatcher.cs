using System;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    internal sealed class FormUiDispatcher : IUiDispatcher
    {
        private readonly Control _uiAnchor;

        /// <summary>UIスレッドでハンドルを確保済みのコントロールを与えて生成する。</summary>
        public FormUiDispatcher(Control uiAnchor)
        {
            if (uiAnchor == null)
            {
                throw new ArgumentNullException(nameof(uiAnchor));
            }

            _uiAnchor = uiAnchor;
        }

        public IAsyncResult Begin(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            return _uiAnchor.BeginInvoke(action);
        }

        public bool Wait(IAsyncResult pending, TimeSpan limit)
        {
            if (pending == null)
            {
                throw new ArgumentNullException(nameof(pending));
            }

            return pending.AsyncWaitHandle.WaitOne(limit);
        }

        public void End(IAsyncResult pending)
        {
            if (pending == null)
            {
                throw new ArgumentNullException(nameof(pending));
            }

            _uiAnchor.EndInvoke(pending);
        }
    }
}
