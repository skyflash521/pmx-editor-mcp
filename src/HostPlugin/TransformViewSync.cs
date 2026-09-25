using PEPlugin.View;

namespace PmxEditorMcp
{
    public static class TransformViewSync
    {
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
            }
        }
    }
}
