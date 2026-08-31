using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>明示的な対象外一覧の正本をJSONから読み取る。</summary>
    public static class LedgerOutOfScopeJsonReader
    {
        private const string TypesName = "types";

        private const string SignaturesName = "signatures";

        private const string NameName = "name";

        private const string KeyName = "key";

        private const string ReasonName = "reason";

        private static readonly Dictionary<string, OutOfScopeReason> Reasons =
            new Dictionary<string, OutOfScopeReason>(StringComparer.Ordinal)
            {
                { "enumType", OutOfScopeReason.EnumType },
                { "delegateType", OutOfScopeReason.DelegateType },
                { "route", OutOfScopeReason.Route },
                { "argumentOnly", OutOfScopeReason.ArgumentOnly },
            };

        /// <summary>形が違えば <see cref="FormatException"/>。</summary>
        public static LedgerOutOfScopeRecord Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            Dictionary<string, object> root = Members(Parse(json), TypesName, SignaturesName);
            object[] typeItems = Array(root[TypesName], TypesName);
            object[] signatureItems = Array(root[SignaturesName], SignaturesName);

            try
            {
                List<OutOfScopeTypeEntry> types = new List<OutOfScopeTypeEntry>();
                foreach (object item in typeItems)
                {
                    Dictionary<string, object> members = Members(item, NameName, ReasonName);
                    types.Add(new OutOfScopeTypeEntry(
                        Text(members[NameName], NameName), Reason(members[ReasonName])));
                }

                List<OutOfScopeSignatureEntry> signatures = new List<OutOfScopeSignatureEntry>();
                foreach (object item in signatureItems)
                {
                    Dictionary<string, object> members = Members(item, KeyName, ReasonName);
                    signatures.Add(new OutOfScopeSignatureEntry(
                        Text(members[KeyName], KeyName), Reason(members[ReasonName])));
                }

                return new LedgerOutOfScopeRecord(types, signatures);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        private static object Parse(string json)
        {
            try
            {
                return new JavaScriptSerializer().DeserializeObject(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("JSONとして読めない。", exception);
            }
        }

        private static object[] Array(object value, string name)
        {
            object[] items = value as object[];
            if (items == null)
            {
                throw new FormatException(name + " は項目の並びでなければならない。");
            }

            return items;
        }

        /// <summary>
        /// 求める項目だけを持つ対象として読む。余分な項目を黙って捨てると、正本の形が崩れても
        /// 気づけない。
        /// </summary>
        private static Dictionary<string, object> Members(object value, params string[] names)
        {
            Dictionary<string, object> members = value as Dictionary<string, object>;
            if (members == null)
            {
                throw new FormatException("項目の組でなければならない。");
            }

            foreach (string name in names)
            {
                if (!members.ContainsKey(name))
                {
                    throw new FormatException("項目が無い: " + name);
                }
            }

            foreach (string name in members.Keys)
            {
                if (!names.Contains(name, StringComparer.Ordinal))
                {
                    throw new FormatException("知らない項目がある: " + name);
                }
            }

            return members;
        }

        private static string Text(object value, string name)
        {
            string text = value as string;
            if (string.IsNullOrEmpty(text))
            {
                throw new FormatException(name + " は空でない文字列でなければならない。");
            }

            return text;
        }

        private static OutOfScopeReason Reason(object value)
        {
            string text = Text(value, ReasonName);
            OutOfScopeReason reason;
            if (!Reasons.TryGetValue(text, out reason))
            {
                throw new FormatException("知らない理由: " + text);
            }

            return reason;
        }
    }
}
