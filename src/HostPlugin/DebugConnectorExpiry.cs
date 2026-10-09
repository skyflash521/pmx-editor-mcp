using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    public static class DebugConnectorExpiry
    {
        /// <summary>この入口のメソッド名。MCPのツールとしては公開しない。</summary>
        public const string MethodName = "debug_expire_connector";

        public static void AddTo(McpMethodTable methods, bool enabled, ResidentConnection resident)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (resident == null)
            {
                throw new ArgumentNullException(nameof(resident));
            }

            if (enabled)
            {
                methods.Add(MethodName, context => Expire(resident));
            }
        }

        public static object Expire(ResidentConnection resident)
        {
            if (resident == null)
            {
                throw new ArgumentNullException(nameof(resident));
            }

            resident.Expire();
            resident.Use();

            return new Dictionary<string, object>(StringComparer.Ordinal) { { "renewed", true } };
        }
    }
}
