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

        internal static Func<string> EditorFolder { get; set; } = () => Application.StartupPath;

        private static readonly TimeSpan PromptReadLimit = TimeSpan.FromSeconds(1);

        private static readonly Dictionary<string, SavingItem> ItemsByWindowAndPath =
            new Dictionary<string, SavingItem>(StringComparer.Ordinal)
            {
                { "PmxEditor.CsvElementView|menuStrip1/MenuItem_File/MenuItem_SaveCsv", SavingItem.Asking() },
                { "PmxEditor.PmxForm|menuStrip1/MenuItem_File/MenuItem_TextInOut/MenuItem_TextSave", SavingItem.Asking() },
                {
                    "PmxEditor.PmxForm|menuStrip1/MenuItem_File/MenuItem_Export",
                    SavingItem.Asking()
                        .Then(AnsweredDialog.Pressed("ExportForm", "btnOK"))
                        .Instead(".pmx", "session_save_pmx_file")
                        .Instead(".pmd", "session_save_pmd_file")
                },
                { "PmxViewForm.EffectView|menuStrip1/MenuItem_File/MenuItem_SaveAs", SavingItem.Asking() },
                { "PmxViewForm.EffectView|menuStrip1/MenuItem_File/MenuItem_SaveDefault", SavingItem.Fixed(1).At("表示設定\\fx.xml") },
                { "PmxViewForm.PmxSkeletonView|menuStrip1/MenuItem_File/MenuItem_SaveSkeleton", SavingItem.Asking() },
                { "PmxViewForm.PmxSkeletonView|menuStrip1/MenuItem_File/MenuItem_SaveSkeletonAll", SavingItem.Asking(1) },
                { "PmxViewForm.TransMorphSlider|menuStrip1/MenuItem_Edit/MenuItem_SaveGroup", SavingItem.Asking() },
                { "PmxViewForm.TransSendView|btnSend", SavingItem.Fixed() },
                { "PmxViewForm.TransSlider|menuStrip1/MenuItem_File/MenuItem_Save", SavingItem.Asking() },
                { "PmxViewForm.TransformView|menuStrip1/MenuItem_Edit/MenuItem_Vpd/MenuItem_Vpd_Save", SavingItem.Asking(1) },
                { "PmxViewForm.UVSkinTexForm|btnCreate", SavingItem.Asking() },
                { "PmxViewForm.VmdListView|menuStrip1/MenuItem_File/MenuItem_SaveList", SavingItem.Fixed(2).At("VMDリスト.txt") },
                { "PmxViewForm.VmdListView|menuStrip1/MenuItem_File/MenuItem_SaveAs", SavingItem.Asking(1) },
                {
                    "VmdViewLib.VMDViewForm|menuStrip1/MenuItem_File/MenuItem_SaveFixVmd",
                    SavingItem.Asking(1, 1).Checked(LackingMotion)
                },
                {
                    "VmdViewLib.VMDViewForm|menuStrip1/MenuItem_View/MenuItem_ViewFile/MenuItem_ViewFile_SaveAs",
                    SavingItem.Asking()
                },
                {
                    "VmdViewLib.VMDViewForm|menuStrip1/MenuItem_View/MenuItem_ViewFile/MenuItem_ViewFile_Save",
                    SavingItem.Fixed(1).At("表示設定\\_VMDView.vdw")
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
                if (!IsFullyQualified(file) || Path.GetExtension(file).Length <= 1)
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
            string target = item.Target == null
                ? null
                : Path.Combine(EditorDataFolder.Of(EditorFolder()), item.Target);
            DateTime? before = target != null && File.Exists(target) ? File.GetLastWriteTimeUtc(target) : (DateTime?)null;
            string refused = null;
            string failure = null;
            IList<string> agreed = new string[0];
            bool written = false;
            string lacking = null;
            try
            {
                DialogAnswer answer = DialogAnswer.StartAcknowledging(
                    expected, item.Questions, item.Cautions, DialogAnswer.Limit);
                try
                {
                    refused = UiLive.Press(form, window, path);
                }
                finally
                {
                    failure = answer.Stop();
                    agreed = answer.Agreed;
                }

                bool produced = refused == null
                    && failure == null
                    && (!item.AsksFile || File.Exists(file))
                    && (target == null || (File.Exists(target) && File.GetLastWriteTimeUtc(target) != before));
                lacking = produced ? item.Lacking(file) : null;
                written = produced && lacking == null;
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

            if (lacking != null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.OperationFailed, UiAnswering.Told("エディタが書いたファイルは、" + lacking, agreed));
            }

            if (!written)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.OperationFailed,
                    UiAnswering.Told(
                        "エディタがファイルを書かなかった" + (target == null ? "" : ": " + target) + "。", agreed));
            }

            return ComposedEditResult.Complete(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { MessagesName, agreed.ToArray() },
            });
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

        private static string LackingMotion(string file)
        {
            return VmdFile.IsWhole(File.ReadAllBytes(file))
                ? null
                : "VMDとしては途中で切れているか、エディタの書くヘッダーで始まっていない。";
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
                int questions,
                int cautions,
                AnsweredDialog[] following,
                IDictionary<string, string> sdkToolsByExtension,
                Func<string, string> lacking,
                string target)
            {
                Target = target;
                Lacking = lacking;
                AsksFile = asksFile;
                Questions = questions;
                Cautions = cautions;
                Following = following;
                SdkToolsByExtension = new Dictionary<string, string>(
                    sdkToolsByExtension, StringComparer.OrdinalIgnoreCase);
            }

            internal bool AsksFile { get; }

            /// <summary>決まった場所へ書く部品の書き先の、エディタのデータフォルダからの相対パス。確かめない部品では null。</summary>
            internal string Target { get; }

            /// <summary>押したあとエディタが出す、はいといいえを持つ問いのうち、はいで答えてよい数。</summary>
            internal int Questions { get; }

            /// <summary>押したあとエディタが出す、ボタンが1つだけでアイコンの付いたメッセージボックスのうち、閉じて続けてよい数。</summary>
            internal int Cautions { get; }

            /// <summary>保存のダイアログの後に答えるダイアログ。</summary>
            internal AnsweredDialog[] Following { get; }

            /// <summary>書き先の拡張子から、同じことを画面を経ずに行うツールへ。</summary>
            internal IDictionary<string, string> SdkToolsByExtension { get; }

            /// <summary>書かれたファイルの中身が形式として揃っていなければ、どう揃っていないかを述べた文。揃っていれば null。</summary>
            internal Func<string, string> Lacking { get; }

            internal static SavingItem Asking(int questions = 0, int cautions = 0)
            {
                return new SavingItem(
                    true, questions, cautions, new AnsweredDialog[0], new Dictionary<string, string>(), Whole, null);
            }

            internal static SavingItem Fixed(int questions = 0)
            {
                return new SavingItem(
                    false, questions, 0, new AnsweredDialog[0], new Dictionary<string, string>(), Whole, null);
            }

            internal SavingItem Then(AnsweredDialog following)
            {
                return new SavingItem(
                    AsksFile,
                    Questions,
                    Cautions,
                    Following.Concat(new[] { following }).ToArray(),
                    SdkToolsByExtension,
                    Lacking,
                    Target);
            }

            internal SavingItem Instead(string extension, string tool)
            {
                Dictionary<string, string> tools = new Dictionary<string, string>(SdkToolsByExtension)
                {
                    { extension, tool },
                };

                return new SavingItem(AsksFile, Questions, Cautions, Following, tools, Lacking, Target);
            }

            internal SavingItem Checked(Func<string, string> lacking)
            {
                return new SavingItem(AsksFile, Questions, Cautions, Following, SdkToolsByExtension, lacking, Target);
            }

            internal SavingItem At(string target)
            {
                return new SavingItem(AsksFile, Questions, Cautions, Following, SdkToolsByExtension, Lacking, target);
            }

            private static string Whole(string file)
            {
                return null;
            }
        }
    }
}
