using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>その型の実体を得る道が在るかを、公開API列挙と型役割表から導く。</summary>
    public static class HandleRouteEvidence
    {
        private const string PmxTypeName = "PEPlugin.Pmx.IPXPmx";

        /// <summary>
        /// その型の実体を得る道が在る型の名前。総称と配列の印を外した鍵で持つ。
        /// </summary>
        public static ISet<string> Reached(
            IDictionary<string, SignatureRecord> signatures, TypeRoleTable roles)
        {
            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            SignatureRecord[] readable = signatures.Values
                .Where(s => (s.MemberKind == MemberKind.Property || s.MemberKind == MemberKind.Field)
                    && s.CanRead)
                .ToArray();
            HashSet<string> reached = new HashSet<string>(
                new[] { PmxTypeName }, StringComparer.Ordinal);
            for (bool grew = true; grew;)
            {
                grew = false;
                foreach (SignatureRecord signature in readable)
                {
                    if (reached.Contains(TypeDefinitionName.OfElement(signature.DeclaringType))
                        && reached.Add(Value(signature)))
                    {
                        grew = true;
                    }
                }
            }

            reached.UnionWith(roles.Types
                .Where(t => t.Role == TypeRole.Connector)
                .Select(t => TypeDefinitionName.OfElement(t.TypeName)));
            foreach (string key in roles.Collections.Where(c => c.Owns).Select(c => c.SignatureKey)
                .Concat(roles.Issuances.Where(i => i.Issues).Select(i => i.SignatureKey)))
            {
                SignatureRecord issuing;
                if (signatures.TryGetValue(key, out issuing))
                {
                    reached.Add(Value(issuing));
                }
            }

            return reached;
        }

        private static string Value(SignatureRecord signature)
        {
            return TypeDefinitionName.OfElement(
                ValueTypeName.Contained(signature.ValueType ?? string.Empty));
        }
    }
}
