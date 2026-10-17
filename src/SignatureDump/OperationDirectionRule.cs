using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    public static class OperationDirectionRule
    {
        private const string VoidTypeName = "System.Void";

        public static readonly ReadOnlyCollection<string> ReadMethodPrefixes =
            Array.AsReadOnly(new[] { "Get", "Is", "Has", "Can", "Find", "Search" });

        public static OperationDirection ForMethod(string memberName, string returnType, bool hasOutOrRefParameter)
        {
            if (memberName == null)
            {
                throw new ArgumentNullException(nameof(memberName));
            }

            if (returnType == null)
            {
                throw new ArgumentNullException(nameof(returnType));
            }

            bool isRead = returnType != VoidTypeName
                && !hasOutOrRefParameter
                && ReadMethodPrefixes.Any(prefix => memberName.StartsWith(prefix, StringComparison.Ordinal));

            return isRead ? OperationDirection.Read : OperationDirection.Write;
        }

        public static OperationDirection ForProperty(bool hasPublicGetter)
        {
            return hasPublicGetter ? OperationDirection.Read : OperationDirection.Write;
        }

        public static OperationDirection ForOtherMember()
        {
            return OperationDirection.Write;
        }
    }
}
