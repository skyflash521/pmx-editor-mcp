using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    public static class ReplacedModelSelection
    {
        private static readonly HashSet<string> Rows =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "PEPlugin.Form.IPEFormConnector.OpenPMXFile(System.String)",
                "PEPlugin.Form.IPEFormConnector.OpenPMDFile(System.String)",
                "PEPlugin.Form.IPEFormConnector.ImportXFile(System.String)",
                "PEPlugin.Form.IPEFormConnector.InitializePMX()",
                "PEPlugin.Form.IPEFormConnector.InitializePMD()",
            };

        private static readonly HashSet<string> CurrentRows =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "PEPlugin.Pmx.IPXPmx.FromFile(System.String)",
            };

        public static bool Replaces(string rowKey)
        {
            return rowKey != null && Rows.Contains(rowKey);
        }

        public static bool Replaced(string rowKey, object result)
        {
            return Replaces(rowKey) && !(result is bool && !(bool)result);
        }

        public static bool ReplacesCurrent(string rowKey)
        {
            return rowKey != null && CurrentRows.Contains(rowKey);
        }
    }
}
