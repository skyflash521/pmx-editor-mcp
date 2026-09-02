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

        /// <summary>
        /// 接続初期化がここから辿り始める型。プラグインが受け取る起動引数と、Cプラグインの静的な
        /// 橋渡しがこれに当たる。
        /// </summary>
        public static readonly IList<string> ConnectionRoots = new ReadOnlyCollection<string>(
            new[] { "PEPlugin.IPERunArgs", "PXCPlugin.IPXCPluginRunArgs", "PXCPlugin.PXCBridge" });

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
            return new ReadOnlyDictionary<string, string>(Walk(inventory, roots).Paths);
        }

        /// <summary>
        /// 接続の根から辿り着ける型と、その基底型。基底型は自分では辿り着く先にならないが、辿り着ける
        /// 型の実体はその基底型の実体でもあるので、ホストが常駐保持する側に立つ。根の扱いは
        /// <see cref="ReachableFromRoots"/> と同じ。
        /// </summary>
        public static ISet<string> ReachableWithBaseTypes(
            InventoryRecord inventory, IEnumerable<string> roots)
        {
            RequireInventory(inventory);

            IDictionary<string, IList<string>> bases = BaseTypes(inventory);
            HashSet<string> types = new HashSet<string>(
                Walk(inventory, roots).Paths.Keys, StringComparer.Ordinal);
            Queue<string> pending = new Queue<string>(types);
            while (pending.Count != 0)
            {
                IList<string> inherited;
                if (!bases.TryGetValue(pending.Dequeue(), out inherited))
                {
                    continue;
                }

                foreach (string baseType in inherited.Where(types.Add))
                {
                    pending.Enqueue(baseType);
                }
            }

            return types;
        }

        /// <summary>
        /// 接続の根から辿り着け、かつそこから <paramref name="targets"/> のいずれかへ辿り着ける型。
        /// 根そのものと途中の型を返し、<paramref name="targets"/> 自身は入れない。どの目的地へも
        /// 至らない枝は入らない。根の扱いは <see cref="ReachableFromRoots"/> と同じで、列挙に
        /// メンバーの無い根は <see cref="ArgumentException"/>。
        /// </summary>
        public static ISet<string> RouteTypesToward(
            InventoryRecord inventory, IEnumerable<string> roots, ISet<string> targets)
        {
            RequireInventory(inventory);
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            Reach reach = Walk(inventory, roots);
            IDictionary<string, ISet<string>> toward = Reversed(reach.Steps);
            HashSet<string> route = new HashSet<string>(StringComparer.Ordinal);
            Queue<string> pending = new Queue<string>(targets.Where(reach.Paths.ContainsKey));
            while (pending.Count != 0)
            {
                ISet<string> before;
                if (!toward.TryGetValue(pending.Dequeue(), out before))
                {
                    continue;
                }

                foreach (string type in before.Where(route.Add))
                {
                    pending.Enqueue(type);
                }
            }

            route.ExceptWith(targets);

            return route;
        }

        /// <summary>辿り着いた型と、そこへ一歩で進める型。</summary>
        private static IDictionary<string, ISet<string>> Reversed(
            IDictionary<string, ISet<string>> steps)
        {
            Dictionary<string, ISet<string>> reversed =
                new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, ISet<string>> step in steps)
            {
                foreach (string next in step.Value)
                {
                    ISet<string> before;
                    if (!reversed.TryGetValue(next, out before))
                    {
                        before = new HashSet<string>(StringComparer.Ordinal);
                        reversed.Add(next, before);
                    }

                    before.Add(step.Key);
                }
            }

            return reversed;
        }

        /// <summary>根から辿った結果。経路と、一歩で進める型を持つ。</summary>
        private sealed class Reach
        {
            public Reach()
            {
                Paths = new Dictionary<string, string>(StringComparer.Ordinal);
                Steps = new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
            }

            /// <summary>辿り着いた型と、根からの経路。根は空の経路を持つ。</summary>
            public IDictionary<string, string> Paths { get; }

            /// <summary>辿り着いた型と、そこから一歩で進める型。</summary>
            public IDictionary<string, ISet<string>> Steps { get; }
        }

        private static Reach Walk(InventoryRecord inventory, IEnumerable<string> roots)
        {
            RequireInventory(inventory);
            InventoryAmbiguity.Require(inventory);
            if (roots == null)
            {
                throw new ArgumentNullException(nameof(roots));
            }

            IDictionary<string, IList<SignatureRecord>> members = MembersByType(inventory);
            Reach reach = new Reach();
            Queue<string> queue = new Queue<string>();
            foreach (string root in roots)
            {
                PropertyRecord.RequireText(root, nameof(roots));
                string key = TypeDefinitionName.Of(root);
                if (!members.ContainsKey(key))
                {
                    throw new ArgumentException("列挙にメンバーの無い根が在る: " + root, nameof(roots));
                }

                if (reach.Paths.ContainsKey(key))
                {
                    continue;
                }

                reach.Paths.Add(key, string.Empty);
                queue.Enqueue(key);
            }

            while (queue.Count != 0)
            {
                string type = queue.Dequeue();
                foreach (KeyValuePair<string, string> step in Steps(members[type]))
                {
                    if (!members.ContainsKey(step.Value))
                    {
                        continue;
                    }

                    Next(reach, type).Add(step.Value);
                    if (reach.Paths.ContainsKey(step.Value))
                    {
                        continue;
                    }

                    string path = reach.Paths[type];
                    reach.Paths.Add(step.Value, path.Length == 0 ? step.Key : path + "." + step.Key);
                    queue.Enqueue(step.Value);
                }
            }

            return reach;
        }

        private static ISet<string> Next(Reach reach, string type)
        {
            ISet<string> next;
            if (!reach.Steps.TryGetValue(type, out next))
            {
                next = new HashSet<string>(StringComparer.Ordinal);
                reach.Steps.Add(type, next);
            }

            return next;
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
