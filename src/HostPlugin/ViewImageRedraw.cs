// エディタはビューの画像を、そのとき持っているバッファから作る。描き直しを挟まないと、直前に
// 変えた視点も選択も画像へ出ない。

using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    public static class ViewImageRedraw
    {
        public const string ImageRow = "PEPlugin.View.IPEPMDViewConnector.GetClientImage()";

        public const string RedrawRow = "PEPlugin.View.IPEPMDViewConnector.UpdateView()";

        /// <summary>
        /// 表の中の画像を取る行を、先にビューを描き直すものへ置き換える。描き直す行が表に無ければ
        /// 何もしない。ほかの行は触らない。
        /// </summary>
        public static void Fit(IDictionary<string, SdkCall> calls)
        {
            if (calls == null)
            {
                throw new ArgumentNullException(nameof(calls));
            }

            SdkCall image;
            SdkCall redraw;
            if (!calls.TryGetValue(ImageRow, out image)
                || !calls.TryGetValue(RedrawRow, out redraw))
            {
                return;
            }

            calls[ImageRow] = (target, arguments) =>
            {
                redraw(target, new object[0]);

                return image(target, arguments);
            };
        }
    }
}
