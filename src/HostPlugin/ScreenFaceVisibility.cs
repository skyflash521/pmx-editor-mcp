// 面の選択は、ビューの表示の設定が切れている間は描かれない。

using System;
using System.Collections.Generic;
using PEPlugin.View;

namespace PmxEditorMcp
{
    /// <summary>選んだ面が画面に出るかを見て、出ないときの知らせを組む。</summary>
    public static class ScreenFaceVisibility
    {
        private const string NotDrawn =
            "選んだ面は画面に出ない。ビューの表示の設定の visibleSelectedFace が偽なので、"
                + "撮った画像には選択が写らない。view_update_view_setting_connector で真にすると出る。";

        /// <summary>
        /// その種類の選択について添える知らせ。面でないか、面が画面に出る設定なら空を渡す。
        /// <paramref name="setting"/> はビューの表示の設定の口で、引けていなければ null。
        /// </summary>
        public static IList<string> Warnings(object setting, string kind)
        {
            IPEViewSettingConnector held = setting as IPEViewSettingConnector;
            if (held == null
                || !string.Equals(kind, ElementKinds.Face, StringComparison.Ordinal)
                || held.Visible_SelectedFace)
            {
                return new string[0];
            }

            return new[] { NotDrawn };
        }
    }
}
