using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力台帳が行を作らない理由。台帳の凡例が数えないと定める種類だけを並べる。ここに無い
    /// 理由では対象外にできない。
    /// </summary>
    public enum OutOfScopeReason
    {
        /// <summary>列挙型。</summary>
        EnumType,

        /// <summary>デリゲート型。</summary>
        DelegateType,

        /// <summary>参照先の型の行で能力を計上する経路。</summary>
        Route,

        /// <summary>提供対象シグネチャの引数の型としてだけ現れる型。</summary>
        ArgumentOnly,
    }

    /// <summary>台帳が行を作らない公開型1件。</summary>
    public sealed class OutOfScopeTypeEntry
    {
        public OutOfScopeTypeEntry(string name, OutOfScopeReason reason)
        {
            OutOfScopeText.Require(name, nameof(name));
            OutOfScopeText.RequireDefined(reason, nameof(reason));

            Name = name;
            Reason = reason;
        }

        /// <summary>公開API列挙が書き出した型名。</summary>
        public string Name { get; }

        public OutOfScopeReason Reason { get; }
    }

    /// <summary>
    /// 台帳が行を作る型に属しながら、台帳のどの行も指さない公開シグネチャ1件。理由を経路に
    /// 限るのは、列挙型・デリゲート型・引数専用型が型ごと対象外になり、シグネチャ単位で現れ
    /// ないためである。
    /// </summary>
    public sealed class OutOfScopeSignatureEntry
    {
        public OutOfScopeSignatureEntry(string key, OutOfScopeReason reason)
        {
            OutOfScopeText.Require(key, nameof(key));
            OutOfScopeText.RequireDefined(reason, nameof(reason));

            if (reason != OutOfScopeReason.Route)
            {
                throw new ArgumentException(
                    "シグネチャ単位で採れない理由: " + reason, nameof(reason));
            }

            Key = key;
            Reason = reason;
        }

        /// <summary>宣言型・メンバー名・総称型引数の数・引数の型と方向の列で決まる行キー。</summary>
        public string Key { get; }

        public OutOfScopeReason Reason { get; }
    }

    /// <summary>
    /// 明示的な対象外一覧の正本1件ぶん。型単位とシグネチャ単位を分けて持つ。シグネチャの差集合
    /// だけで照合すると、公開メンバーを1件も宣言しない型が台帳から漏れても差集合に現れず
    /// 素通りするので、型単位の並びを別に持つ。
    /// </summary>
    public sealed class LedgerOutOfScopeRecord
    {
        public LedgerOutOfScopeRecord(
            IList<OutOfScopeTypeEntry> types, IList<OutOfScopeSignatureEntry> signatures)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            RequireAscending(Identifiers(types), nameof(types));
            RequireAscending(Identifiers(signatures), nameof(signatures));

            Types = new ReadOnlyCollection<OutOfScopeTypeEntry>(new List<OutOfScopeTypeEntry>(types));
            Signatures =
                new ReadOnlyCollection<OutOfScopeSignatureEntry>(new List<OutOfScopeSignatureEntry>(signatures));
        }

        public IList<OutOfScopeTypeEntry> Types { get; }

        public IList<OutOfScopeSignatureEntry> Signatures { get; }

        private static IList<string> Identifiers(IList<OutOfScopeTypeEntry> entries)
        {
            List<string> names = new List<string>();
            foreach (OutOfScopeTypeEntry entry in entries)
            {
                if (entry == null)
                {
                    throw new ArgumentException("空の項目は置けない。", nameof(entries));
                }

                names.Add(entry.Name);
            }

            return names;
        }

        private static IList<string> Identifiers(IList<OutOfScopeSignatureEntry> entries)
        {
            List<string> keys = new List<string>();
            foreach (OutOfScopeSignatureEntry entry in entries)
            {
                if (entry == null)
                {
                    throw new ArgumentException("空の項目は置けない。", nameof(entries));
                }

                keys.Add(entry.Key);
            }

            return keys;
        }

        /// <summary>
        /// 序数の昇順で重複が無いことを求める。並びを決めておくと、正本の差分が並べ替えで
        /// 揺れない。
        /// </summary>
        private static void RequireAscending(IList<string> identifiers, string name)
        {
            for (int i = 1; i < identifiers.Count; i++)
            {
                int order = string.CompareOrdinal(identifiers[i - 1], identifiers[i]);
                if (order == 0)
                {
                    throw new ArgumentException("同じ識別子が二度現れる: " + identifiers[i], name);
                }

                if (order > 0)
                {
                    throw new ArgumentException("序数の昇順で並んでいない: " + identifiers[i], name);
                }
            }
        }
    }

    internal static class OutOfScopeText
    {
        internal static void Require(string value, string name)
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

        internal static void RequireDefined(OutOfScopeReason reason, string name)
        {
            if (!Enum.IsDefined(typeof(OutOfScopeReason), reason))
            {
                throw new ArgumentException("知らない理由: " + reason, name);
            }
        }
    }
}
