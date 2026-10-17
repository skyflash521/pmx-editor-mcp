using System;
using System.Collections.Generic;
using PEPlugin.Vmd;

namespace PmxEditorMcp
{
    /// <summary>
    /// SDKのVMDの保存は、ボーン・モーフ・IK表示・カメラ・照明・セルフシャドウの順にキーの並びを書く。
    /// VMDの形式とエディタの読み込みは、ボーン・モーフ・カメラ・照明・セルフシャドウ・IK表示の順で読む。
    /// カメラ・照明・セルフシャドウ・IK表示のどれかにキーがあると、保存したファイルは読み直せない。
    /// </summary>
    public static class VmdSaving
    {
        public const string ToFileKey = "PEPlugin.Vmd.IPEVmd.ToFile(System.String,System.Boolean)";

        /// <summary>VMDを保存する呼び出しでなければ確かめずに通す。</summary>
        public static bool TryCall(string rowKey, object item, out string code, out string message)
        {
            code = ToolEnvelope.NotApplicable;
            message = null;
            IPEVmd vmd = item as IPEVmd;
            if (vmd == null || !string.Equals(rowKey, ToFileKey, StringComparison.Ordinal))
            {
                return true;
            }

            List<string> held = new List<string>();
            Add(held, "カメラ", vmd.Camera == null ? 0 : vmd.Camera.Count);
            Add(held, "照明", vmd.Light == null ? 0 : vmd.Light.Count);
            Add(held, "セルフシャドウ", vmd.SelfShadow == null ? 0 : vmd.SelfShadow.Count);
            Add(held, "IK表示", vmd.VisibleIK == null ? 0 : vmd.VisibleIK.Count);
            if (held.Count == 0)
            {
                return true;
            }

            message = "カメラ・照明・セルフシャドウ・IK表示のキーを持つVMDは保存できない。"
                + "持っているキー: " + string.Join("、", held)
                + "。保存するとキーの並びの順がVMDの形式と食い違い、保存したファイルを読み直せない。"
                + "これらのキーを消したVMDを保存する。";

            return false;
        }

        private static void Add(List<string> held, string kind, int count)
        {
            if (count > 0)
            {
                held.Add(kind + " " + count + " 件");
            }
        }
    }
}
