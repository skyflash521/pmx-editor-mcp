using System;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>型役割表が日本語名を持つ、読み取り可能な公開プロパティ1件。</summary>
    public sealed class PropertyRecord
    {
        public PropertyRecord(string declaringType, string memberName, string propertyType)
        {
            RequireText(declaringType, nameof(declaringType));
            RequireText(memberName, nameof(memberName));
            RequireText(propertyType, nameof(propertyType));

            DeclaringType = declaringType;
            MemberName = memberName;
            PropertyType = propertyType;
        }

        public string DeclaringType { get; }

        public string MemberName { get; }

        public string PropertyType { get; }

        private static void RequireText(string value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }

            if (value.Trim().Length == 0)
            {
                throw new ArgumentException("空にも空白だけにもできない。", name);
            }
        }
    }
}
