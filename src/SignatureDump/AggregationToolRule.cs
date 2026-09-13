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
            return new[] { Reading(owner), Writing(owner) };
        }

        /// <summary>
        /// その型の項目を読むツールの名前。自分1つを指すコネクタ型は取得、要素を並べる型は一覧で
        /// 読む。
        /// </summary>
        public static string Reading(TypeRoleRecord owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            return ToolNameRule.OfRole(
                owner, owner.Role == TypeRole.Connector ? ToolVerb.Get : ToolVerb.List);
        }

        /// <summary>その型の項目を書き換えるツールの名前。</summary>
        public static string Writing(TypeRoleRecord owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            return ToolNameRule.OfRole(owner, ToolVerb.Update);
        }

        /// <summary>
        /// 書き換えるツールの名前から、同じ型を読むツールの名前へ。書いた値を読み返す相手は
        /// この組で決まる。
        /// </summary>
        public static IDictionary<string, string> Readers(IEnumerable<TypeRoleRecord> owners)
        {
            if (owners == null)
            {
                throw new ArgumentNullException(nameof(owners));
            }

            Dictionary<string, string> readers =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (TypeRoleRecord owner in owners.Where(Named))
            {
                readers[Writing(owner)] = Reading(owner);
            }

            return readers;
        }

        /// <summary>担当群と要素名詞を持つ型のぶんを集めた名前。</summary>
        public static ISet<string> Names(IEnumerable<TypeRoleRecord> owners)
        {
            if (owners == null)
            {
                throw new ArgumentNullException(nameof(owners));
            }

            return new HashSet<string>(
                owners.Where(Named).SelectMany(Of),
                StringComparer.Ordinal);
        }

        /// <summary>担当群と要素名詞を持ち、名前を決められる型か。</summary>
        private static bool Named(TypeRoleRecord owner)
        {
            return owner.Group != CapabilityOwner.None
                && !string.IsNullOrEmpty(owner.ElementNoun)
                && (owner.Role == TypeRole.Connector
                    || !string.IsNullOrEmpty(owner.ElementNounPlural));
        }
    }
}
