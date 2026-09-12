using System;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    /// <summary>
    /// 不可視フォームの Invoke でUIスレッドへ委譲する。PEPlugin API はスレッドセーフを
    /// 仮定できないため、ワーカースレッドからの呼び出しはすべてここを通す。
    /// </summary>
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

        /// <summary>UIスレッドでの実行を始める。</summary>
        public IAsyncResult Begin(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            return _uiAnchor.BeginInvoke(action);
        }

        /// <summary>始めた実行が終わるのを、与えた長さまで待つ。</summary>
        public bool Wait(IAsyncResult pending, TimeSpan limit)
        {
            if (pending == null)
            {
                throw new ArgumentNullException(nameof(pending));
            }

            return pending.AsyncWaitHandle.WaitOne(limit);
        }

        /// <summary>始めた実行の後始末をする。</summary>
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
