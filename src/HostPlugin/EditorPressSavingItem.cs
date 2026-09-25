// ComposedScreen に載る合成ツール。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    public static class EditorPressSavingItem
    {
        public const string ToolName = "editor_press_saving_item";

        public const string FileName = "file";

        public const string MessagesName = "messages";

        private static readonly TimeSpan PromptReadLimit = TimeSpan.FromSeconds(1);

        private const string DefaultSettingAsked = "標準設定へ上書きします.よろしいですか？";

        private const string UnsavedListed = "未保存の対象はリストへ登録されません.よろしいですか？";

        private static readonly Dictionary<string, SavingItem> ItemsByWindowAndPath =
            new Dictionary<string, SavingItem>(StringComparer.Ordinal)
            {
                { "PmxEditor.CsvElementView|menuStrip1/MenuItem_File/MenuItem_SaveCsv", SavingItem.Asking() },
                { "PmxEditor.PmxForm|menuStrip1/MenuItem_File/MenuItem_TextInOut/MenuItem_TextSave", SavingItem.Asking() },
                {
                    "PmxEditor.PmxForm|menuStrip1/MenuItem_File/MenuItem_Export",
                    SavingItem.Asking()
                        .Then(AnsweredDialog.Pressed("サイズ調整他", "OK"))
                        .Instead(".pmx", "session_save_pmx_file")
                        .Instead(".pmd", "session_save_pmd_file")
                },
                { "PmxViewForm.EffectView|menuStrip1/MenuItem_File/MenuItem_SaveAs", SavingItem.Asking() },
                {
                    "PmxViewForm.EffectView|menuStrip1/MenuItem_File/MenuItem_SaveDefault",
                    SavingItem.Fixed("標準設定に保存してもよろしいですか？")
                },
                { "PmxViewForm.PmxSkeletonView|menuStrip1/MenuItem_File/MenuItem_SaveSkeleton", SavingItem.Asking() },
                {
                    "PmxViewForm.PmxSkeletonView|menuStrip1/MenuItem_File/MenuItem_SaveSkeletonAll",
                    SavingItem.Asking(
                        "標準のアンカースケルトンと同じ保存先になっていますが よろしいですか？ "
                            + "※自動保存により上書きされる可能性があります.")
                },
                { "PmxViewForm.TransMorphSlider|menuStrip1/MenuItem_Edit/MenuItem_SaveGroup", SavingItem.Asking() },
                { "PmxViewForm.TransSendView|btnSend", SavingItem.Fixed() },
                { "PmxViewForm.TransSlider|menuStrip1/MenuItem_File/MenuItem_Save", SavingItem.Asking() },
                {
                    "PmxViewForm.TransformView|menuStrip1/MenuItem_Edit/MenuItem_Vpd/MenuItem_Vpd_Save",
                    SavingItem.Asking("変形項目がありませんがありませんが保存しますか？")
                },
                { "PmxViewForm.UVSkinTexForm|btnCreate", SavingItem.Asking() },
                {
                    "PmxViewForm.VmdListView|menuStrip1/MenuItem_File/MenuItem_SaveList",
                    SavingItem.Fixed("標準リストへ保存してもよろしいですか？", UnsavedListed)
                },
                { "PmxViewForm.VmdListView|menuStrip1/MenuItem_File/MenuItem_SaveAs", SavingItem.Asking(UnsavedListed) },
                {
                    "VmdViewLib.VMDViewForm|menuStrip1/MenuItem_File/MenuItem_SaveFixVmd",
                    SavingItem.Asking(
                        "Fixモーションが作成されていません.構築しますか？",
                        "非物理化モーションへの変換は状態によっては不正な結果になる場合があります.ご注意ください. "
                            + "※物理関連でのIKをOFFにすることで改善する場合があります.",
                        "保存完了")
                },
                {
                    "VmdViewLib.VMDViewForm|menuStrip1/MenuItem_View/MenuItem_ViewFile/MenuItem_ViewFile_SaveAs",
                    SavingItem.Asking()
                },
                {
                    "VmdViewLib.VMDViewForm|menuStrip1/MenuItem_View/MenuItem_ViewFile/MenuItem_ViewFile_Save",
                    SavingItem.Fixed(DefaultSettingAsked)
                },
            };

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
                    new List<string> { UiPressItem.WindowName, UiPressItem.PathName, FileName, ToolDispatch.ConfirmName },
                    ScreenNeeds.None,
                    ScreenRefreshKind.None,
                    (context, parts) => Run(context, forms)));
        }

        /// <summary>このツールで押す項目の、ウィンドウの型の完全名と項目までの名前の並びを縦棒で繋いだもの。</summary>
        internal static IEnumerable<string> Items
        {
            get { return ItemsByWindowAndPath.Keys; }
        }

        /// <summary>このツールで押す項目なら真。</summary>
        internal static bool Presses(string window, IList<string> path)
        {
            return ItemsByWindowAndPath.ContainsKey(window + "|" + string.Join("/", path));
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
            string window = context.Params.TryGetValue(UiPressItem.WindowName, out given) ? given as string : null;
            IList<string> path = UiTree.Steps(context);
            if (string.IsNullOrEmpty(window) || path == null || path.Count == 0)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    UiPressItem.WindowName + " はウィンドウの型の完全名で、" + UiPressItem.PathName
                        + " はウィンドウの根から項目までの名前の並びで与える。");
            }

            SavingItem item;
            if (!ItemsByWindowAndPath.TryGetValue(window + "|" + string.Join("/", path), out item))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "このツールで押す、ファイルへ書き込む項目ではない: " + window + " の " + string.Join("/", path));
            }

            string file = context.Params.TryGetValue(FileName, out given) ? given as string : null;
            if (item.AsksFile)
            {
                if (!MotionSaveTransformedPmxFile.IsFullyQualified(file) || Path.GetExtension(file).Length <= 1)
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.InvalidArgument,
                        "この項目は書き先を訊くので、" + FileName + " に拡張子の付いたファイルの絶対パスを与える。");
                }

                string folder = Path.GetDirectoryName(file);
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder) || Directory.Exists(file))
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.InvalidArgument, FileName + " は在るフォルダの中のファイルを指す: " + file);
                }

                string instead;
                if (item.SdkToolsByExtension.TryGetValue(Path.GetExtension(file), out instead))
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.NotApplicable,
                        Path.GetExtension(file) + " のファイルへ書くのは " + instead + " で行う。");
                }
            }
            else if (given != null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument, "この項目は決まった場所へ書き、書き先を訊かないので、" + FileName + " を渡さない。");
            }

            if (!ConfirmGate.TryPass(DangerKind.Overwrite, confirm, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            Form form = UiLive.Shown(forms(), window);
            if (form == null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "そのウィンドウは開いていない。" + UiOpenWindow.ToolName + " で開いてから呼ぶ: " + window);
            }

            string shown = new DesktopModalWindowProbe(PromptReadLimit).TryDescribe();
            if (form.Modal || !IsWindowEnabled(form.Handle) || shown != null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "エディタが人の応答を待つ表示を出しているので押さない" + (shown == null ? "" : ": " + shown)
                        + "。表示が消えたかは " + EditorPrompt.ToolName + " で確かめる。");
            }

            // 保存のダイアログは、書き先に同じ名前のファイルが在ると上書きを訊く。
            string kept = item.AsksFile && File.Exists(file)
                ? Path.Combine(Path.GetDirectoryName(file), "~" + Guid.NewGuid().ToString("N") + Path.GetExtension(file))
                : null;
            if (kept != null)
            {
                File.Move(file, kept);
            }

            List<AnsweredDialog> expected = new List<AnsweredDialog>();
            if (item.AsksFile)
            {
                expected.Add(AnsweredDialog.Save(file));
            }

            expected.AddRange(item.Following);
            string refused = null;
            string failure = null;
            IList<string> agreed = new string[0];
            bool written = false;
            try
            {
                DialogAnswer answer = DialogAnswer.Start(
                    IntPtr.Zero, expected, item.Agreeable, new string[0], DialogAnswer.Limit);
                try
                {
                    refused = UiLive.Press(form, window, path);
                }
                finally
                {
                    failure = answer.Stop();
                    agreed = answer.Agreed;
                }

                written = refused == null && failure == null && (!item.AsksFile || File.Exists(file));
            }
            finally
            {
                RestoreUnlessWritten(file, kept, written);
            }

            if (refused != null)
            {
                return ComposedEditResult.Refuse(ToolEnvelope.NotApplicable, refused);
            }

            if (failure != null)
            {
                return ComposedEditResult.Refuse(ToolEnvelope.OperationFailed, UiAnswering.Told(failure, agreed));
            }

            if (!written)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.OperationFailed, UiAnswering.Told("エディタがファイルを書かなかった。", agreed));
            }

            return ComposedEditResult.Complete(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { MessagesName, agreed.ToArray() },
            });
        }

        private static void RestoreUnlessWritten(string file, string kept, bool written)
        {
            if (kept == null)
            {
                return;
            }

            if (written)
            {
                File.Delete(kept);

                return;
            }

            if (File.Exists(file))
            {
                File.Delete(file);
            }

            File.Move(kept, file);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr window);

        private sealed class SavingItem
        {
            private SavingItem(
                bool asksFile,
                string[] agreeable,
                AnsweredDialog[] following,
                IDictionary<string, string> sdkToolsByExtension)
            {
                AsksFile = asksFile;
                Agreeable = agreeable;
                Following = following;
                SdkToolsByExtension = new Dictionary<string, string>(
                    sdkToolsByExtension, StringComparer.OrdinalIgnoreCase);
            }

            internal bool AsksFile { get; }

            internal string[] Agreeable { get; }

            /// <summary>保存のダイアログの後に答える表示。</summary>
            internal AnsweredDialog[] Following { get; }

            /// <summary>書き先の拡張子から、同じことを画面を経ずに行うツールへ。</summary>
            internal IDictionary<string, string> SdkToolsByExtension { get; }

            internal static SavingItem Asking(params string[] agreeable)
            {
                return new SavingItem(true, agreeable, new AnsweredDialog[0], new Dictionary<string, string>());
            }

            internal static SavingItem Fixed(params string[] agreeable)
            {
                return new SavingItem(false, agreeable, new AnsweredDialog[0], new Dictionary<string, string>());
            }

            internal SavingItem Then(AnsweredDialog following)
            {
                return new SavingItem(
                    AsksFile, Agreeable, Following.Concat(new[] { following }).ToArray(), SdkToolsByExtension);
            }

            internal SavingItem Instead(string extension, string tool)
            {
                Dictionary<string, string> tools = new Dictionary<string, string>(SdkToolsByExtension)
                {
                    { extension, tool },
                };

                return new SavingItem(AsksFile, Agreeable, Following, tools);
            }
        }
    }
}
