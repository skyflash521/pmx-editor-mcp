using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力台帳の各行が指す公開型と公開シグネチャを、公開API列挙と突き合わせて決める。台帳は
    /// 名前だけを書き、それが型を指すのかメンバーを指すのかは書かないので、列挙の側と突き合わせて
    /// 初めて決まる。
    /// </summary>
    public sealed class LedgerPopulation
    {
        private const string BuilderType = "PEPlugin.IPEBuilder";

        private static readonly Dictionary<string, string> PatternNamespaces =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "CAP-463", "PEPlugin.Pmd." },
                { "CAP-466", "PEPlugin.SDX." },
            };

        private static readonly Dictionary<string, string> PatternCreated =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "CAP-463", "PEPlugin.Pmd." },
            };

        private LedgerPopulation(
            ISet<string> types, ISet<string> signatures, IDictionary<string, ISet<string>> owners)
        {
            Types = types;
            Signatures = signatures;
            Owners = owners;
        }

        /// <summary>台帳のどれかの行が指す公開型の名前。</summary>
        public ISet<string> Types { get; }

        /// <summary>母集合。台帳のどれかの行が指す公開シグネチャの行キー。</summary>
        public ISet<string> Signatures { get; }

        /// <summary>行キーから、それを指す行の能力ID。1つのシグネチャが複数の行に属してよい。</summary>
        public IDictionary<string, ISet<string>> Owners { get; }

        /// <summary>
        /// 指す先を決められない行があれば <see cref="InvalidOperationException"/>。解決の結果が
        /// 0件になること自体は許す——公開メンバーを自分では宣言しない入れ物の型が実在する。
        /// </summary>
        public static LedgerPopulation Resolve(IList<CapabilityRecord> ledger, InventoryRecord inventory)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            Index index = new Index(inventory);
            HashSet<string> types = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, ISet<string>> owners =
                new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
            HashSet<string> patternRows = new HashSet<string>(StringComparer.Ordinal);

            foreach (CapabilityRecord row in ledger)
            {
                if (row.TargetKind == CapabilityTargetKind.Pattern)
                {
                    patternRows.Add(row.Id);
                    ResolvePattern(row, index, types, owners);
                    continue;
                }

                foreach (string name in row.TargetNames)
                {
                    ResolveName(row, name, index, types, owners);
                }
            }

            RequirePatternRules(patternRows);

            return new LedgerPopulation(
                types,
                new HashSet<string>(owners.Keys, StringComparer.Ordinal),
                new ReadOnlyDictionary<string, ISet<string>>(owners));
        }

        private static void ResolvePattern(
            CapabilityRecord row,
            Index index,
            ISet<string> types,
            IDictionary<string, ISet<string>> owners)
        {
            string prefix;
            if (!PatternNamespaces.TryGetValue(row.Id, out prefix))
            {
                throw Unresolved(row, "まとめて指す書き方の解決規則が無い");
            }

            foreach (TypeRecord type in index.Types)
            {
                if (!type.Name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (type.Kind != TypeKind.Enum)
                {
                    types.Add(type.Name);
                }

                foreach (SignatureRecord signature in index.Declared(type.Name))
                {
                    Own(owners, signature.Key, row.Id);
                }
            }

            string created;
            if (!PatternCreated.TryGetValue(row.Id, out created))
            {
                return;
            }

            foreach (SignatureRecord signature in index.Declared(BuilderType))
            {
                if (signature.ValueType != null
                    && signature.ValueType.StartsWith(created, StringComparison.Ordinal))
                {
                    Own(owners, signature.Key, row.Id);
                }
            }
        }

        /// <summary>
        /// 名前をまず型として引き、引けなかったものだけをメンバーとして読み直す。型として引けた
        /// 名前をメンバーとして読み直すと、入れ子の型をメンバーと取り違える。
        /// </summary>
        private static void ResolveName(
            CapabilityRecord row,
            string name,
            Index index,
            ISet<string> types,
            IDictionary<string, ISet<string>> owners)
        {
            string type = index.FindType(name, row);
            if (type != null)
            {
                foreach (string declaring in index.Closure(type))
                {
                    if (index.IsPublicType(declaring))
                    {
                        types.Add(declaring);
                    }

                    foreach (SignatureRecord signature in index.Declared(declaring))
                    {
                        Own(owners, signature.Key, row.Id);
                    }
                }

                return;
            }

            int separator = name.LastIndexOf('.');
            if (separator <= 0 || separator == name.Length - 1)
            {
                throw Unresolved(row, "指す先が無い: " + name);
            }

            string member = name.Substring(separator + 1);
            string owner = index.FindType(name.Substring(0, separator), row);
            if (owner == null)
            {
                throw Unresolved(row, "指す先が無い: " + name);
            }

            bool found = false;
            foreach (string declaring in index.Closure(owner))
            {
                if (index.IsPublicType(declaring))
                {
                    types.Add(declaring);
                }

                foreach (SignatureRecord signature in index.Declared(declaring))
                {
                    if (string.Equals(signature.MemberName, member, StringComparison.Ordinal))
                    {
                        Own(owners, signature.Key, row.Id);
                        found = true;
                    }
                }
            }

            if (!found)
            {
                throw Unresolved(row, "その名前の公開メンバーが無い: " + name);
            }
        }

        private static void Own(IDictionary<string, ISet<string>> owners, string key, string id)
        {
            ISet<string> ids;
            if (!owners.TryGetValue(key, out ids))
            {
                ids = new HashSet<string>(StringComparer.Ordinal);
                owners[key] = ids;
            }

            ids.Add(id);
        }

        private static void RequirePatternRules(ICollection<string> patternRows)
        {
            foreach (string id in PatternNamespaces.Keys)
            {
                if (!patternRows.Contains(id))
                {
                    throw new InvalidOperationException(
                        "まとめて指す書き方の解決規則に対応する行が台帳に無い: " + id);
                }
            }
        }

        private static InvalidOperationException Unresolved(CapabilityRecord row, string reason)
        {
            return new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture, "{0} の対象を解決できない: {1}", row.Id, reason));
        }

        /// <summary>公開API列挙を、名前から引ける形にしたもの。</summary>
        private sealed class Index
        {
            private readonly Dictionary<string, TypeRecord> _types;

            private readonly Dictionary<string, List<string>> _byWrittenName;

            private readonly Dictionary<string, List<SignatureRecord>> _declared;

            private readonly Dictionary<string, ISet<string>> _closures;

            internal Index(InventoryRecord inventory)
            {
                _types = inventory.Types.ToDictionary(t => t.Name, StringComparer.Ordinal);
                _declared = inventory.Signatures
                    .GroupBy(s => s.DeclaringType, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
                _closures = new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
                _byWrittenName = new Dictionary<string, List<string>>(StringComparer.Ordinal);

                foreach (TypeRecord type in inventory.Types)
                {
                    foreach (string written in WrittenNames(type.Name))
                    {
                        List<string> found;
                        if (!_byWrittenName.TryGetValue(written, out found))
                        {
                            found = new List<string>();
                            _byWrittenName[written] = found;
                        }

                        found.Add(type.Name);
                    }
                }
            }

            internal IEnumerable<TypeRecord> Types
            {
                get { return _types.Values; }
            }

            /// <summary>対象アセンブリの公開型かどうか。基底の並びは外部の型も含む。</summary>
            internal bool IsPublicType(string name)
            {
                return _types.ContainsKey(name);
            }

            internal IEnumerable<SignatureRecord> Declared(string type)
            {
                List<SignatureRecord> signatures;
                return _declared.TryGetValue(type, out signatures)
                    ? (IEnumerable<SignatureRecord>)signatures
                    : new SignatureRecord[0];
            }

            /// <summary>
            /// 台帳の書き方に当たる型を1つだけ返す。当たらなければ null、2つ以上に当たれば例外。
            /// 指す先が定まらない名前を通すと、後段の照合が誤った集合の上で成立してしまう。
            /// </summary>
            internal string FindType(string name, CapabilityRecord row)
            {
                List<string> found;
                if (!_byWrittenName.TryGetValue(name, out found))
                {
                    return null;
                }

                if (found.Count > 1)
                {
                    throw Unresolved(row, "指す先が2つ以上ある: " + name);
                }

                return found[0];
            }

            /// <summary>その型と、基底クラス・実装インターフェースを推移的に辿った全体。</summary>
            internal ISet<string> Closure(string type)
            {
                ISet<string> closure;
                if (_closures.TryGetValue(type, out closure))
                {
                    return closure;
                }

                closure = new HashSet<string>(StringComparer.Ordinal);
                Stack<string> pending = new Stack<string>();
                pending.Push(type);
                while (pending.Count > 0)
                {
                    string current = pending.Pop();
                    if (!closure.Add(current))
                    {
                        continue;
                    }

                    TypeRecord record;
                    if (!_types.TryGetValue(current, out record))
                    {
                        continue;
                    }

                    foreach (string baseType in record.BaseTypes)
                    {
                        pending.Push(baseType);
                    }
                }

                _closures[type] = closure;
                return closure;
            }

            /// <summary>
            /// その型を台帳がどう書きうるか。完全修飾の名前と、名前空間を落とした名前の2通りとする。
            /// 名前空間を持たない型では両方が同じになるので、重ならないようにして返す。
            /// </summary>
            private static IEnumerable<string> WrittenNames(string name)
            {
                string head = WithoutTypeArguments(name);
                int nested = head.IndexOf('+');
                string outer = nested < 0 ? head : head.Substring(0, nested);
                string rest = nested < 0 ? string.Empty : head.Substring(nested);
                int namespaceEnd = outer.LastIndexOf('.');
                string simple = namespaceEnd < 0 ? outer : outer.Substring(namespaceEnd + 1);
                string stripped = (simple + rest).Replace('+', '.');

                yield return head;
                if (!string.Equals(head, stripped, StringComparison.Ordinal))
                {
                    yield return stripped;
                }
            }

            /// <summary>
            /// 山括弧で囲まれた型引数だけを落とす。入れ子の型は段ごとに型引数を持ちうるので、最初の
            /// 山括弧から末尾までを捨てると内側の段が失われる。
            /// </summary>
            private static string WithoutTypeArguments(string name)
            {
                StringBuilder builder = new StringBuilder(name.Length);
                int depth = 0;
                foreach (char c in name)
                {
                    if (c == '<')
                    {
                        depth++;
                    }
                    else if (c == '>')
                    {
                        depth--;
                    }
                    else if (depth == 0)
                    {
                        builder.Append(c);
                    }
                }

                return builder.ToString();
            }
        }
    }
}
