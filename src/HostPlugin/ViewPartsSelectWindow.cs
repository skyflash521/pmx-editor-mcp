using System;
using System.Collections.Generic;
using PEPlugin.View;

namespace PmxEditorMcp
{
    /// <summary>
    /// PMXビューの絞込の窓の表示を切り替えるツール。
    /// </summary>
    public static class ViewPartsSelectWindow
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "view_update_parts_select_window";

        /// <summary>窓を表示するかを受け取り、切り替えた結果を返す項目の名前。</summary>
        public const string VisibleName = "visible";

        /// <summary>材質の一覧に並んでいる項目の数を返す項目の名前。</summary>
        public const string MaterialItemsName = "materialItemsCount";

        /// <summary>ボーンの一覧に並んでいる項目の数を返す項目の名前。</summary>
        public const string BoneItemsName = "boneItemsCount";

        /// <summary>表情の一覧に並んでいる項目の数を返す項目の名前。</summary>
        public const string ExpressionItemsName = "expressionItemsCount";

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

            List<string> known = new List<string> { VisibleName };
            methods.Add(
                ToolName,
                screen.Method(known, ScreenNeeds.Parts, ScreenRefreshKind.None, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, ScreenParts parts)
        {
            object given;
            if (!context.Params.TryGetValue(VisibleName, out given) || !(given is bool))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument, VisibleName + " は真偽でなければならない。");
            }

            IPEPartsSelectConnector held = (IPEPartsSelectConnector)parts.Parts;
            held.Visible = (bool)given;

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { VisibleName, held.Visible },
                    { MaterialItemsName, held.MaterialItemsCount },
                    { BoneItemsName, held.BoneItemsCount },
                    { ExpressionItemsName, held.ExpressionItemsCount },
                });
        }
    }
}
