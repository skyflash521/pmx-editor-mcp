// ComposedScreen に載る合成ツール。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    public static class MotionSaveTransformedPmxFile
    {
        public const string ToolName = "motion_save_transformed_pmx_file";

        public const string PathName = "path";

        private const string TransformForm = "PmxViewForm.TransformView";

        private static readonly TimeSpan DialogLimit = TimeSpan.FromSeconds(10);

        private static readonly string[] SavePath = { "menuStrip1", "MenuItem_File", "MenuItem_SaveModel" };

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
                    new List<string> { PathName, ToolDispatch.ConfirmName },
                    ScreenNeeds.None,
                    ScreenRefreshKind.None,
                    (context, parts) => Run(context, forms)));
        }

        private static ComposedEditResult Run(McpMethodContext context, Func<IEnumerable<Form>> forms)
        {
            bool confirm;
            string code;
            string message;
            if (!ToolDispatch.TryConfirm(context, out confirm, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            object given;
            string path = context.Params.TryGetValue(PathName, out given) ? given as string : null;
            if (!IsFullyQualified(path))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, PathName + " は絶対パスで与える。");
            }

            string folder = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder) || Directory.Exists(path))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument, PathName + " は在るフォルダの中のファイルを指す: " + path);
            }

            if (!ConfirmGate.TryPass(DangerKind.Overwrite, confirm, out code, out message))
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

            if (!IsWindowEnabled(view.Handle))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "TransformView が人の応答を待つ表示を出している。表示が消えてから呼ぶ。表示が消えたかは "
                        + EditorPrompt.ToolName + " で確かめる。");
            }

            List<UiMenu> menus = new List<UiMenu>();
            ToolStripMenuItem save = UiLive.Find(view, SavePath, menus) as ToolStripMenuItem;
            ToolStripMenuItem normalize = UiLive.Find(view, NormalizePath, menus) as ToolStripMenuItem;
            if (save == null || normalize == null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable, "TransformView のファイルメニューに「現在の形状で保存」の項目が見つからない。");
            }

            if (!save.Enabled)
            {
                return ComposedEditResult.Refuse(ToolEnvelope.NotApplicable, "「現在の形状で保存」の項目がいまは押せない。");
            }

            // 同じ名前のファイルが在ると保存のダイアログは上書きを訊き、書き出しの失敗はエディタが握りつぶす。
            string staging = Path.Combine(folder, "~" + Guid.NewGuid().ToString("N") + ".pmx");
            string failure;
            bool normalized = normalize.Checked;
            byte[] held = Keyboard();
            SaveDialogAnswer answer = SaveDialogAnswer.Start(view.Handle, staging, DialogLimit);
            try
            {
                // 正規化が入っていると、エディタは閾値を訊く表示を出す。
                normalize.Checked = false;

                // Shift を押していると、エディタは正規化の入り切りを逆に扱う。
                SetKeyboard(WithoutShift(held));
                save.PerformClick();
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
                Discard(staging);

                return ComposedEditResult.Refuse(ToolEnvelope.OperationFailed, failure);
            }

            if (!File.Exists(staging))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.OperationFailed, "エディタが変形した形を書き出さなかった。");
            }

            try
            {
                File.Copy(staging, path, true);
            }
            finally
            {
                Discard(staging);
            }

            return ComposedEditResult.Complete(new Dictionary<string, object>(StringComparer.Ordinal));
        }

        private static bool IsFullyQualified(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return false;
            }

            if (path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && IsSeparator(path[2]))
            {
                return true;
            }

            return path.Length >= 3 && IsSeparator(path[0]) && IsSeparator(path[1]) && !IsSeparator(path[2]);
        }

        private static bool IsSeparator(char at)
        {
            return at == Path.DirectorySeparatorChar || at == Path.AltDirectorySeparatorChar;
        }

        private static void Discard(string staging)
        {
            if (File.Exists(staging))
            {
                File.Delete(staging);
            }
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
        private static extern bool IsWindowEnabled(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetKeyboardState(byte[] state);
    }
}
