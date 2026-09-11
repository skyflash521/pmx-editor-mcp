using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// プロパティを集めるツールの名前を、型の役割から決める。これらのツールは行を持たず、
    /// 埋め込み先として名指しされることで現れるので、名前の決め方をここ1つに置く。
    /// </summary>
    public static class AggregationToolRule
    {
        /// <summary>その型の項目を集める先。取得と更新の2つで、追加と削除は集める先にならない。</summary>
        public static IEnumerable<string> Of(TypeRoleRecord owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            ToolVerb[] verbs = owner.Role == TypeRole.Connector
                ? new[] { ToolVerb.Get, ToolVerb.Update }
                : new[] { ToolVerb.List, ToolVerb.Update };

            return verbs.Select(v => ToolNameRule.OfRole(owner, v));
        }

        /// <summary>担当群と要素名詞を持つ型のぶんを集めた名前。</summary>
        public static ISet<string> Names(IEnumerable<TypeRoleRecord> owners)
        {
            if (owners == null)
            {
                throw new ArgumentNullException(nameof(owners));
            }

            return new HashSet<string>(
                owners.Where(o => o.Group != CapabilityOwner.None
                        && !string.IsNullOrEmpty(o.ElementNoun)
                        && (o.Role == TypeRole.Connector
                            || !string.IsNullOrEmpty(o.ElementNounPlural)))
                    .SelectMany(Of),
                StringComparer.Ordinal);
        }
    }
}
