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

        /// <summary>UIスレッドで実行し、完了するまで待つ。</summary>
        public void Invoke(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (_uiAnchor.InvokeRequired)
            {
                _uiAnchor.Invoke(action);
                return;
            }

            action();
        }
    }
}
