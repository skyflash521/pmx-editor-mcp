// ComposedScreen に載る合成ツール。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    public static class MotionApplyCurrentPose
    {
        public const string ToolName = "motion_apply_current_pose";

        public const string ThresholdName = "morphThreshold";

        private const string TransformForm = "PmxViewForm.TransformView";

        // エディタが閾値の入力と、正規化の進み具合の表示に付ける題。
        private const string ThresholdCaption = "頂点モーフ再計算用閾値";

        private const string ProgressCaption = "経過状態";

        private static readonly TimeSpan DialogLimit = TimeSpan.FromSeconds(10);

        private static readonly string[] ApplyPath = { "menuStrip1", "MenuItem_File", "MenuItem_SetupCurrentPose" };

        private static readonly string[] NormalizePath = { "menuStrip1", "MenuItem_File", "MenuItem_SaveNormalize" };

        private static readonly int[] ShiftKeys = { 0x10, 0xA0, 0xA1 };

        private const byte Down = 0x80;

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
                    new List<string> { ThresholdName },
                    ScreenNeeds.None,
                    ScreenRefreshKind.None,
                    (context, parts) => Run(context, forms)));
        }

        private static ComposedEditResult Run(McpMethodContext context, Func<IEnumerable<Form>> forms)
        {
            float? threshold = null;
            object given;
            if (context.Params.TryGetValue(ThresholdName, out given))
            {
                float value;
                if (!ValueInput.TrySingle(given, out value) || !(value > 0))
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.InvalidArgument, ThresholdName + " は正の有限の数で与える。");
                }

                threshold = value;
            }

            Form view = UiLive.Shown(forms(), TransformForm);
            if (view == null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "TransformView が開いていない。" + UiOpenWindow.ToolName + " で開いてから呼ぶ。");
            }

            if (!IsWindowEnabled(view.Handle))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "TransformView が人の応答を待つ表示を出している。表示が消えてから呼ぶ。表示が消えたかは "
                        + EditorPrompt.ToolName + " で確かめる。");
            }

            List<UiMenu> menus = new List<UiMenu>();
            ToolStripMenuItem apply = UiLive.Find(view, ApplyPath, menus) as ToolStripMenuItem;
            ToolStripMenuItem normalize = UiLive.Find(view, NormalizePath, menus) as ToolStripMenuItem;
            if (apply == null || normalize == null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "TransformView のファイルメニューに「現在の変形状態でモデル形状を更新」の項目が見つからない。");
            }

            if (!apply.Enabled)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable, "「現在の変形状態でモデル形状を更新」の項目がいまは押せない。");
            }

            // エディタは閾値の文字列を、動いているカルチャで数へ読む。
            AnsweredDialog expected = threshold.HasValue
                ? AnsweredDialog.Input(ThresholdCaption, threshold.Value.ToString("R", CultureInfo.CurrentCulture))
                : null;
            string failure;
            bool normalized = normalize.Checked;
            byte[] held = Keyboard();
            DialogAnswer answer = DialogAnswer.Start(view.Handle, expected, new[] { ProgressCaption }, DialogLimit);
            try
            {
                // 正規化が入っているとエディタは閾値を訊き、Shift を押しているとその入り切りを逆に扱う。
                normalize.Checked = threshold.HasValue;
                SetKeyboard(WithoutShift(held));
                apply.PerformClick();
            }
            finally
            {
                failure = answer.Stop();
                try
                {
                    SetKeyboard(held);
                }
                finally
                {
                    normalize.Checked = normalized;
                }
            }

            if (failure != null)
            {
                return ComposedEditResult.Refuse(ToolEnvelope.OperationFailed, failure);
            }

            return ComposedEditResult.Complete(new Dictionary<string, object>(StringComparer.Ordinal));
        }

        private static byte[] Keyboard()
        {
            byte[] held = new byte[256];
            if (!GetKeyboardState(held))
            {
                throw new InvalidOperationException("キーボードの状態を読めなかった。");
            }

            return held;
        }

        private static byte[] WithoutShift(byte[] held)
        {
            byte[] released = (byte[])held.Clone();
            foreach (int key in ShiftKeys)
            {
                released[key] = (byte)(released[key] & ~Down);
            }

            return released;
        }

        private static void SetKeyboard(byte[] state)
        {
            // SetKeyboardState はこのスレッドの状態だけを書き、物理的なキーの状態にもほかのスレッドにも及ばない。
            if (!SetKeyboardState(state))
            {
                throw new InvalidOperationException("キーボードの状態を書けなかった。");
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetKeyboardState(byte[] state);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetKeyboardState(byte[] state);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr window);
    }
}
