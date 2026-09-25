using System;
using PEPlugin.View;

namespace PmxEditorMcp
{
    public static class TransformViewSync
    {
        [ThreadStatic]
        private static int _refreshes;

        /// <summary>このスレッドで <see cref="Refresh"/> が TransformView を読み直させた回数。</summary>
        public static int Refreshes
        {
            get { return _refreshes; }
        }

        /// <summary>
        /// UIスレッドで呼ぶ。<paramref name="connector"/> が開いている TransformView の口なら、いまのモデルを
        /// 読み直させる。変形の状態はエディタが保ったまま読み直す。
        /// </summary>
        public static void Refresh(object connector)
        {
            // TransformView は、モデルが変わっても自分がアクティブになるまで変形の元のモデルを読み直さない。
            IPETransformViewConnector view = connector as IPETransformViewConnector;
            if (view != null && view.Visible)
            {
                view.UpdateView();
                _refreshes++;
            }
        }
    }
}
