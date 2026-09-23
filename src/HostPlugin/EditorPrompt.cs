using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// いまエディタが人の応答を待つ表示を出しているかと、その題と文面を返すツール。UIスレッドを
    /// 使わずに答えるので、表示が出ていてほかのツールが進めない間も答えを返す。
    /// </summary>
    public static class EditorPrompt
    {
        public const string ToolName = "editor_get_prompt";

        public const string ShownName = "shown";

        /// <summary>表示が無ければ載せない。</summary>
        public const string TextName = "text";

        public static void AddTo(McpMethodTable methods, IModalWindowProbe probe)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (probe == null)
            {
                throw new ArgumentNullException(nameof(probe));
            }

            methods.Add(ToolName, context => Answer(probe.TryDescribe()));
        }

        private static IDictionary<string, object> Answer(string shown)
        {
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ShownName, shown != null },
            };
            if (shown != null)
            {
                value.Add(TextName, shown);
            }

            return ToolEnvelope.Success(value);
        }
    }
}
