using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    public static class UiPressItem
    {
        public const string ToolName = "editor_press_item";

        public const string WindowName = "window";

        public const string PathName = "path";

        public const string MessagesName = "messages";

        private static readonly Dictionary<string, string> OwnToolGuidesByWindowAndPath =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                {
                    "PmxViewForm.TransformView|menuStrip1/MenuItem_File/MenuItem_SetupCurrentPose",
                    MotionApplyCurrentPose.ToolName + " で行う。"
                },
                {
                    "PmxViewForm.TransformView|menuStrip1/MenuItem_File/MenuItem_SaveModel",
                    MotionSaveTransformedPmxFile.ToolName + " で行う。"
                },
                { "PmxEditor.PmxForm|menuStrip1/MenuItem_File/MenuItem_Save", SavePmxGuide },
                { "PmxEditor.PmxForm|menuStrip1/MenuItem_File/MenuItem_SaveAs", SavePmxGuide },
                { "PmxViewForm.PmxViewSetting|menuStrip1/MenuItem_File/MenuItem_SaveAs", ViewSettingGuide },
                { "PmxViewForm.PmxViewSetting|menuStrip1/MenuItem_File/MenuItem_Save", ViewSettingGuide },
            };

        private const string SavePmxGuide = "session_save_pmx_file で行う。";

        private const string ViewSettingGuide =
            "PMXView の表示設定をファイルへ書くのは view_save_view_setting で行う。TransformView と SubView の表示設定を書くツールは無い。";

        /// <summary><paramref name="forms"/> は開いているウィンドウを返す。UIスレッドで呼ばれる。</summary>
        public static void AddTo(McpMethodTable methods, Func<IEnumerable<Form>> forms)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (forms == null)
            {
                throw new ArgumentNullException(nameof(forms));
            }

            methods.Add(ToolName, context => Press(context, forms));
        }

        private static object Press(McpMethodContext context, Func<IEnumerable<Form>> forms)
        {
            object given;
            string named = context.Params.TryGetValue(WindowName, out given) ? given as string : null;
            IDictionary<string, object> window = string.IsNullOrEmpty(named) ? null : UiStructureCatalog.Window(named);
            if (window == null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidArgument,
                    WindowName + " はウィンドウの型の完全名で与える。名前は " + UiTree.ToolName + " の form で分かる。");
            }

            IList<string> path = UiTree.Steps(context);
            if (path == null || path.Count == 0)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidArgument,
                    PathName + " はウィンドウの根から部品までの名前の並びで与える。並びは " + UiTree.ToolName
                        + " の node の name で分かる。");
            }

            IDictionary<string, object> node = UiStructureCatalog.Node(window, UiStructureCatalog.RootName);
            foreach (string step in path)
            {
                node = UiStructureCatalog.Child(node, step);
                if (node == null)
                {
                    return ToolEnvelope.Failure(ToolEnvelope.InvalidArgument, PathName + " に当たる部品が無い: " + step);
                }

                if (string.Equals(
                    UiStructureCatalog.Text(node, UiStructureCatalog.TypeName), "ContextMenuStrip", StringComparison.Ordinal))
                {
                    return ToolEnvelope.Failure(
                        ToolEnvelope.NotApplicable, "右クリックメニューの中の項目は押さない: " + string.Join("/", path));
                }
            }

            if (!UiStructureCatalog.Pressable(node) || UiStructureCatalog.Children(node).Count > 0)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidArgument,
                    "押すと何かが起きるメニュー項目かボタンではない: " + string.Join("/", path));
            }

            string guide;
            if (OwnToolGuidesByWindowAndPath.TryGetValue(named + "|" + string.Join("/", path), out guide))
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable, "この項目は押さない: " + string.Join("/", path) + "。" + guide);
            }

            string danger = Danger(UiStructureCatalog.Text(node, UiStructureCatalog.DangerName), named, path);
            if (danger != null)
            {
                return ToolEnvelope.Failure(ToolEnvelope.NotApplicable, danger);
            }

            bool shown = false;
            bool modal = false;
            string waiting = null;
            string refused = null;
            UiAnswering answered = null;
            Exception failure = null;
            UiInvocation invocation = context.Ui.TryInvokeOnUi(() =>
            {
                Form form = UiLive.Shown(forms(), named);
                if (form == null)
                {
                    return;
                }

                shown = true;
                modal = form.Modal;
                if (modal)
                {
                    return;
                }

                waiting = UiAnswering.Waiting();
                if (waiting != null)
                {
                    return;
                }

                answered = UiAnswering.Around(() =>
                {
                    try
                    {
                        refused = UiLive.Press(form, named, path);
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                });
            });
            if (!invocation.DidRun)
            {
                return ToolFailure.Unavailable(invocation);
            }

            if (!shown)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable,
                    "そのウィンドウは開いていない。" + UiOpenWindow.ToolName + " で開いてから呼ぶ: " + named);
            }

            if (modal)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable,
                    "そのウィンドウは人の応答を待つ表示で、答えるのは人なので押さない: " + named);
            }

            if (waiting != null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable,
                    "エディタが人の応答を待つ表示を出しているので押さない: " + waiting + "。表示が消えたかは "
                        + EditorPrompt.ToolName + " で確かめる。");
            }

            if (failure != null)
            {
                return ToolEnvelope.Failure(ToolEnvelope.OperationFailed, answered.Thrown(failure));
            }

            if (refused != null)
            {
                return ToolEnvelope.Failure(ToolEnvelope.NotApplicable, refused);
            }

            if (answered.Failure != null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.OperationFailed, UiAnswering.Told(answered.Failure, answered.Notices));
            }

            return ToolEnvelope.Success(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { MessagesName, answered.Notices.ToArray() },
            });
        }

        /// <summary>台帳の危険の区分を、断る事情の文へ直す。危険でなければ null。</summary>
        private static string Danger(string kind, string window, IList<string> path)
        {
            string part = string.Join("/", path);
            switch (kind)
            {
                case null:
                    return null;

                case "shutdown":
                    return "エディタを終了させるので押さない: " + part + "。終了は " + UiCloseWindow.ShutdownToolName
                        + " で行う。";

                case "overwrite":
                    return EditorPressSavingItem.Presses(window, path)
                        ? "ファイルへ書き込むので押さない: " + part + "。書き込みは " + EditorPressSavingItem.ToolName
                            + " で押して行う。"
                        : "ファイルへ書き込むので押さない: " + part + "。";

                case "reset":
                    return "編集中のモデルを空にするので押さない: " + part
                        + "。空にするのは session_initialize_pmx で行う。";

                default:
                    return "危険の区分 " + kind + " に当たるので押さない: " + part;
            }
        }
    }
}
