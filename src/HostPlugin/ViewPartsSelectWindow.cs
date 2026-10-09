using System;
using System.Collections.Generic;
using PEPlugin.View;

namespace PmxEditorMcp
{
    public static class ViewPartsSelectWindow
    {
        public const string ToolName = "view_update_parts_select_window";

        public const string VisibleName = "visible";

        public const string MaterialItemsName = "materialItemsCount";

        public const string BoneItemsName = "boneItemsCount";

        public const string ExpressionItemsName = "expressionItemsCount";

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
