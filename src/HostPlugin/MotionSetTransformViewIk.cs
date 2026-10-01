// ComposedScreen に載る合成ツール。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    public static class MotionSetTransformViewIk
    {
        public const string ToolName = "motion_set_transform_view_ik";

        public const string EnabledName = "enabled";

        public const string BoneIndicesName = "boneIndices";

        public const string IksName = "iks";

        public const string BoneIndexName = "boneIndex";

        private const string TransformForm = "PmxViewForm.TransformView";

        private static readonly string[] ListPath = { "panel1", "clIK" };

        /// <summary><paramref name="forms"/> は開いているウィンドウを返す。UIスレッドで呼ばれる。</summary>
        public static void AddTo(McpMethodTable methods, ComposedScreen screen, Func<IEnumerable<Form>> forms)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            if (forms == null)
            {
                throw new ArgumentNullException(nameof(forms));
            }

            methods.Add(
                ToolName,
                screen.Method(
                    new List<string> { EnabledName, BoneIndicesName },
                    ScreenNeeds.Pmx | ScreenNeeds.ReadsPmx,
                    ScreenRefreshKind.None,
                    (context, parts) => Run(context, parts, forms)));
        }

        private static ComposedEditResult Run(
            McpMethodContext context, ScreenParts parts, Func<IEnumerable<Form>> forms)
        {
            object given;
            if (!context.Params.TryGetValue(EnabledName, out given) || !(given is bool))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, EnabledName + " は真偽でなければならない。");
            }

            bool enabled = (bool)given;
            IList<int> wanted;
            string code;
            string message;
            if (!TryBoneIndices(context, out wanted, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            Form view = UiLive.Shown(forms(), TransformForm);
            if (view == null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "TransformView が開いていない。" + UiOpenWindow.ToolName + " で開いてから呼ぶ。");
            }

            CheckedListBox list = UiLive.Find(view, ListPath, new List<UiMenu>()) as CheckedListBox;
            if (list == null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable, "TransformView のIKの一覧が見つからない。");
            }

            // 一覧の並びは、IKを持つボーンをボーンの並びの順に並べたもの。
            IPXPmx model = (IPXPmx)parts.Pmx;
            List<int> listed = new List<int>();
            for (int at = 0; at < model.Bone.Count; at++)
            {
                if (model.Bone[at].IsIK)
                {
                    listed.Add(at);
                }
            }

            if (listed.Count != list.Items.Count)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "TransformView のIKの一覧の数 " + list.Items.Count + " が、モデルでIKを持つボーンの数 " + listed.Count
                        + " と合わない。TransformView を開き直してから呼ぶ。");
            }

            string separator = null;
            for (int position = 0; position < listed.Count; position++)
            {
                string between = BetweenNumberAndName(
                    Convert.ToString(list.Items[position]), position, model.Bone[listed[position]].Name ?? string.Empty);
                if (between == null || (separator != null && between != separator))
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.NotApplicable,
                        "TransformView のIKの一覧の " + position + " 番目が、モデルの " + listed[position]
                            + " 番のボーンと合わない。TransformView を開き直してから呼ぶ。");
                }

                separator = between;
            }

            foreach (int bone in wanted ?? new int[0])
            {
                if (!PositionInput.TryWithin(bone, model.Bone.Count, BoneIndicesName, out code, out message))
                {
                    return ComposedEditResult.Refuse(code, message);
                }
            }

            IList<int> targets = wanted ?? listed;
            foreach (int bone in targets)
            {
                if (!listed.Contains(bone))
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.InvalidArgument,
                        BoneIndicesName + " の " + bone + " は、IKを持つボーンではない。");
                }
            }

            List<object> states = new List<object>();
            foreach (int bone in targets)
            {
                int position = listed.IndexOf(bone);
                list.SetItemChecked(position, enabled);
                states.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { BoneIndexName, bone },
                    { EnabledName, list.GetItemChecked(position) },
                });
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal) { { IksName, states.ToArray() } });
        }

        /// <summary>
        /// 一覧の項目の表示が、その位置の番号で始まりそのボーンの名前で終わるとき、番号と名前の間の文字列を
        /// 返す。間に英数字があるときと、始まりか終わりが合わないときは null を返す。
        /// </summary>
        private static string BetweenNumberAndName(string label, int position, string name)
        {
            string number = position.ToString(CultureInfo.InvariantCulture);
            if (label.Length < number.Length + name.Length
                || !label.StartsWith(number, StringComparison.Ordinal)
                || !label.EndsWith(name, StringComparison.Ordinal))
            {
                return null;
            }

            string between = label.Substring(number.Length, label.Length - number.Length - name.Length);
            foreach (char one in between)
            {
                if (char.IsLetterOrDigit(one))
                {
                    return null;
                }
            }

            return between;
        }

        /// <summary>渡されていなければ null を返す。</summary>
        private static bool TryBoneIndices(
            McpMethodContext context, out IList<int> indices, out string code, out string message)
        {
            indices = null;
            code = null;
            message = null;
            object given;
            if (!context.Params.TryGetValue(BoneIndicesName, out given) || given == null)
            {
                return true;
            }

            List<int> read;
            if (!PositionInput.TryMany(
                given,
                BoneIndicesName,
                BoneIndicesName + " はボーンの位置を並べた並びでなければならない。",
                PositionInput.Unbounded,
                true,
                out read,
                out code,
                out message))
            {
                return false;
            }

            indices = read.Distinct().ToList();

            return true;
        }
    }
}
