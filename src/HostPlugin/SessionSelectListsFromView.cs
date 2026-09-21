using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Form;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    /// <summary>
    /// 画面の選択を、リストの選択へ写すツール。
    /// </summary>
    public static class SessionSelectListsFromView
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "session_select_lists_from_view";

        /// <summary>写す種類を受け取る入力の名前。</summary>
        public const string KindsName = "kinds";

        /// <summary>材質のリスト。</summary>
        public const string Material = "material";

        /// <summary>ボーンのリスト。</summary>
        public const string Bone = "bone";

        /// <summary>受け取れる種類。スキーマが並べる順。</summary>
        public static IList<string> Kinds
        {
            get
            {
                return new[] { Material, Bone };
            }
        }

        /// <summary>写した要素の数を返す項目の名前。</summary>
        public const string SelectedName = "selected";

        public const string CountsName = "counts";

        public const string KindName = "kind";

        /// <summary>ボーンのリストで、どの行も選んでいないことを表す位置。</summary>
        public const int NoBone = -1;

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedScreen screen)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            List<string> known = new List<string> { KindsName };
            methods.Add(
                ToolName,
                screen.Method(
                    known, ScreenNeeds.View | ScreenNeeds.Form | ScreenNeeds.Pmx, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, ScreenParts parts)
        {
            IPXPmx model = (IPXPmx)parts.Pmx;
            IList<string> kinds;
            string code;
            string message;
            if (!ComposedInput.TryChoices(
                    context, KindsName, Kinds, out kinds, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IPEFormConnector form = (IPEFormConnector)parts.Form;
            int selected = 0;
            IList<object> counts = new List<object>();
            if (kinds.Contains(Material, StringComparer.Ordinal))
            {
                int[] materials = Behind(model, parts).ToArray();
                form.SetSelectedMaterialIndices(materials);
                selected += materials.Length;
                counts.Add(Counted(Material, materials.Length));
            }

            if (kinds.Contains(Bone, StringComparer.Ordinal))
            {
                IList<int> bones = Held(parts, model, ElementKinds.Bone);
                form.SelectedBoneIndex = bones.Count == 0 ? NoBone : bones[0];
                int took = bones.Count == 0 ? 0 : 1;
                selected += took;
                counts.Add(Counted(Bone, took));
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { SelectedName, selected },
                    { CountsName, counts },
                });
        }

        private static object Counted(string kind, int selected)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { KindName, kind },
                { SelectedName, selected },
            };
        }

        /// <summary>画面で選んでいる面を持つ材質の位置。昇順で重なりを持たない。</summary>
        private static IEnumerable<int> Behind(IPXPmx model, ScreenParts parts)
        {
            IList<int> owners = ViewSelection.Owners(model);

            return Held(parts, model, ElementKinds.Face)
                .Select(at => owners[at])
                .Distinct()
                .OrderBy(at => at);
        }

        private static IList<int> Held(ScreenParts parts, IPXPmx model, string kind)
        {
            return ViewSelection.Taken(parts.View, kind, ViewSelection.Count(model, kind));
        }
    }
}
