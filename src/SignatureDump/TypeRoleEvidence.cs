using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 型の役割の判定へ、列挙結果から機械で導ける根拠を与える。役割そのものは意味を含む判定なので
    /// ここでは決めず、機械で確かめられる事実だけを返す。型の名前は総称型引数を数へ置き換えた鍵で
    /// 扱う。
    /// </summary>
    public static class TypeRoleEvidence
    {
        /// <summary>ホストが常駐保持するものへ辿り着くとき、引数として自動で注入されるコネクタ。</summary>
        public const string InjectedConnector = "PXCPlugin.IPXCPluginConnector";

        private const string Handler = "System.EventHandler";

        /// <summary>
        /// 公開イベントのハンドラの型引数に現れる型。ハンドラが <c>System.EventHandler</c> と
        /// その総称型以外なら <see cref="InvalidOperationException"/>。
        /// </summary>
        public static ISet<string> EventArgumentTypes(InventoryRecord inventory)
        {
            RequireInventory(inventory);
            InventoryAmbiguity.Require(inventory);

            HashSet<string> types = new HashSet<string>(StringComparer.Ordinal);
            foreach (SignatureRecord signature in inventory.Signatures
                .Where(s => s.MemberKind == MemberKind.Event))
            {
                if (!string.Equals(
                    TypeDefinitionName.Of(signature.ValueType).Split('<')[0],
                    Handler,
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "引数型を取れないハンドラのイベントが在る: " + signature.Key);
                }

                foreach (string argument in TypeDefinitionName.Arguments(signature.ValueType))
                {
                    types.Add(TypeDefinitionName.Of(argument));
                }
            }

            return types;
        }

        /// <summary>
        /// 接続の根から辿れる型と、その経路。辿るのは引数の無い取得プロパティと、自動注入コネクタを
        /// 1つ以上取りそれ以外の引数を取らない戻り値ありのメソッドで、経路はその名前を点でつないだ
        /// もの(メソッドは丸括弧を付ける)。根そのものは経路が空の項目として入る。同じ型へ道が
        /// 複数あるときは、根の並びと列挙の並びで先に見つかった短い方を返す。メンバーを宣言も継承も
        /// しない根は <see cref="ArgumentException"/>。
        /// </summary>
        public static IDictionary<string, string> ReachableFromRoots(
            InventoryRecord inventory, IEnumerable<string> roots)
        {
            RequireInventory(inventory);
            InventoryAmbiguity.Require(inventory);
            if (roots == null)
            {
                throw new ArgumentNullException(nameof(roots));
            }

            IDictionary<string, IList<SignatureRecord>> members = MembersByType(inventory);
            Dictionary<string, string> found = new Dictionary<string, string>(StringComparer.Ordinal);
            Queue<string> queue = new Queue<string>();
            foreach (string root in roots)
            {
                PropertyRecord.RequireText(root, nameof(roots));
                string key = TypeDefinitionName.Of(root);
                if (!members.ContainsKey(key))
                {
                    throw new ArgumentException("列挙にメンバーの無い根が在る: " + root, nameof(roots));
                }

                if (found.ContainsKey(key))
                {
                    continue;
                }

                found.Add(key, string.Empty);
                queue.Enqueue(key);
            }

            while (queue.Count != 0)
            {
                string type = queue.Dequeue();
                foreach (KeyValuePair<string, string> step in Steps(members[type]))
                {
                    if (found.ContainsKey(step.Value) || !members.ContainsKey(step.Value))
                    {
                        continue;
                    }

                    string path = found[type];
                    found.Add(step.Value, path.Length == 0 ? step.Key : path + "." + step.Key);
                    queue.Enqueue(step.Value);
                }
            }

            return new ReadOnlyDictionary<string, string>(found);
        }

        /// <summary>
        /// 型ごとの、その型と基底型が宣言するメンバー。1件も持たない型は入れない。
        /// </summary>
        private static IDictionary<string, IList<SignatureRecord>> MembersByType(
            InventoryRecord inventory)
        {
            ILookup<string, SignatureRecord> declared = inventory.Signatures
                .ToLookup(s => TypeDefinitionName.Of(s.DeclaringType), StringComparer.Ordinal);
            IDictionary<string, IList<string>> bases = BaseTypes(inventory);
            Dictionary<string, IList<SignatureRecord>> members =
                new Dictionary<string, IList<SignatureRecord>>(StringComparer.Ordinal);
            foreach (string type in declared.Select(g => g.Key).Concat(bases.Keys).Distinct(
                StringComparer.Ordinal))
            {
                List<SignatureRecord> owned = new List<SignatureRecord>();
                HashSet<string> walked = new HashSet<string>(new[] { type }, StringComparer.Ordinal);
                Queue<string> queue = new Queue<string>(new[] { type });
                while (queue.Count != 0)
                {
                    string current = queue.Dequeue();
                    owned.AddRange(declared[current]);
                    IList<string> inherited;
                    if (!bases.TryGetValue(current, out inherited))
                    {
                        continue;
                    }

                    foreach (string baseType in inherited.Where(b => walked.Add(b)))
                    {
                        queue.Enqueue(baseType);
                    }
                }

                if (owned.Count != 0)
                {
                    members.Add(type, owned);
                }
            }

            return members;
        }

        private static IDictionary<string, IList<string>> BaseTypes(InventoryRecord inventory)
        {
            Dictionary<string, IList<string>> bases =
                new Dictionary<string, IList<string>>(StringComparer.Ordinal);
            foreach (TypeRecord type in inventory.Types.Concat(inventory.ReferencedTypes))
            {
                string key = TypeDefinitionName.Of(type.Name);
                if (!bases.ContainsKey(key))
                {
                    bases.Add(
                        key, type.BaseTypes.Select(TypeDefinitionName.Of).ToList());
                }
            }

            return bases;
        }

        private static IEnumerable<KeyValuePair<string, string>> Steps(
            IEnumerable<SignatureRecord> members)
        {
            foreach (SignatureRecord member in members)
            {
                string value = TypeDefinitionName.Of(member.ValueType);
                if (member.MemberKind == MemberKind.Property
                    && member.CanRead
                    && member.Parameters.Count == 0)
                {
                    yield return new KeyValuePair<string, string>(member.MemberName, value);
                }
                else if (member.MemberKind == MemberKind.Method && TakesOnlyTheConnector(member))
                {
                    yield return new KeyValuePair<string, string>(member.MemberName + "()", value);
                }
            }
        }

        private static bool TakesOnlyTheConnector(SignatureRecord member)
        {
            return !string.Equals(member.ValueType, "System.Void", StringComparison.Ordinal)
                && member.Parameters.Count != 0
                && member.Parameters.All(p =>
                    p.Direction == ParameterDirection.In
                    && string.Equals(p.TypeName, InjectedConnector, StringComparison.Ordinal));
        }

        private static void RequireInventory(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }
        }
    }
}
