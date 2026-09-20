// 画面とリストへ触る組み立てのツールが通る枠。

using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>画面へ触るツールの中身が受け取る相手。引けなかった口は空になる。</summary>
    public sealed class ScreenParts
    {
        /// <summary>画面の口とリストの口と、いま相手にするPMXを与えて生成する。</summary>
        public ScreenParts(object view, object form, object pmx)
        {
            View = view;
            Form = form;
            Pmx = pmx;
        }

        /// <summary>3Dビューの口。</summary>
        public object View { get; }

        /// <summary>リストを持つ画面の口。</summary>
        public object Form { get; }

        /// <summary>いま相手にするPMX。</summary>
        public object Pmx { get; }
    }

    /// <summary>
    /// 画面とリストへ触る組み立てのツールを、UIスレッドの上で呼ぶ枠。画面の選択と表示は取り消しの
    /// 対象にならないので、まとめての反映も取り消しの抑止もここは通さない。
    /// </summary>
    public sealed class ComposedScreen
    {
        private readonly PmxSession _session;

        private readonly Func<object> _view;

        private readonly Func<object> _form;

        /// <summary>PMXの流れと、画面の口・リストの口を引く相手を与えて生成する。</summary>
        public ComposedScreen(PmxSession session, Func<object> view, Func<object> form)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (form == null)
            {
                throw new ArgumentNullException(nameof(form));
            }

            _session = session;
            _view = view;
            _form = form;
        }

        /// <summary>
        /// <paramref name="body"/> を、画面の口とPMXを受け取る呼び出しにする。
        /// <paramref name="known"/> はそのツールが受け取る項目の名前で、どのPMXを相手にするかの
        /// 指定はここが足す。<paramref name="body"/> はUIスレッドの上で呼ばれる。
        /// </summary>
        public McpMethod Method(
            IList<string> known, Func<McpMethodContext, ScreenParts, ComposedEditResult> body)
        {
            throw new NotImplementedException();
        }
    }
}
