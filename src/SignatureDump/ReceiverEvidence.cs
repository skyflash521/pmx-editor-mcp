using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>接続の根から受け手の型へ至る道。根と、根から進む一歩の並びからなる。</summary>
    public sealed class ReceiverPath
    {
        public ReceiverPath(string root, string steps, string initialize = null)
        {
            Root = root;
            Steps = steps;
            Initialize = initialize;
        }

        /// <summary>辿り始める接続の根の型名。</summary>
        public string Root { get; }

        /// <summary>根から進む一歩の名前を点でつないだもの。根そのものでは空。</summary>
        public string Steps { get; }

        /// <summary>
        /// 辿る前に呼ぶ初期化のメンバー名。初期化を持たない道では null。初期化は呼ぶ側の
        /// スレッドの状態を作るので、辿るのと同じ呼び出しの中で呼ぶ。
        /// </summary>
        public string Initialize { get; }
    }

    /// <summary>
    /// 受け手の型へ至る道を、根ごとに辿って決める。道は列挙から導くので正本へ書かない。同じ型へ
    /// 道が複数あるときは、一歩の数が最も少ないものを採り、並ぶときは根の並びで先のものを採る。
    /// 自分自身へ至る道を持たない型は、その型を実装する型を通る——実装する型の実体はその型としても
    /// 受け取れる。通れる実装する型が2つ以上あるときは、どちらを通るかで受け手が変わるので採らない。
    /// </summary>
    public static class ReceiverEvidence
    {
        /// <summary>
        /// 型ごとの道。辿り着けない型は持たない。<paramref name="types"/> は道を知りたい型の名前。
        /// <paramref name="routes"/> は、自分自身へ至る道を持たない型が通れる、その型を実装する型の
        /// 候補(<see cref="ReceiverRouteEvidence.Candidates"/>)。
        /// </summary>
        public static IDictionary<string, ReceiverPath> Resolve(
            InventoryRecord inventory,
            IEnumerable<string> types,
            IDictionary<string, ISet<string>> routes = null)
        {
            routes = routes ?? new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            string[] asked = types.Distinct(StringComparer.Ordinal).ToArray();
            string[] wanted = asked
                .Concat(asked.Where(routes.ContainsKey).SelectMany(t => routes[t]))
                .Distinct(StringComparer.Ordinal).ToArray();
            Dictionary<string, ReceiverPath> paths =
                new Dictionary<string, ReceiverPath>(StringComparer.Ordinal);
            foreach (string root in TypeRoleEvidence.ConnectionRoots)
            {
                IDictionary<string, string> reached =
                    TypeRoleEvidence.ReachableFromRoots(inventory, new[] { root });
                foreach (string type in wanted)
                {
                    string steps;
                    if (!reached.TryGetValue(type, out steps))
                    {
                        continue;
                    }

                    ReceiverPath found;
                    if (!paths.TryGetValue(type, out found) || Shorter(steps, found.Steps))
                    {
                        paths[type] = new ReceiverPath(
                            root, steps, Initializer(inventory, root, steps));
                    }
                }
            }

            foreach (string type in wanted.Where(t => !paths.ContainsKey(t))
                .OrderBy(t => t, StringComparer.Ordinal))
            {
                ISet<string> named;
                if (!routes.TryGetValue(type, out named))
                {
                    continue;
                }

                string[] through = named.Where(paths.ContainsKey)
                    .OrderBy(n => n, StringComparer.Ordinal).ToArray();
                if (through.Length > 1)
                {
                    throw new InvalidOperationException(
                        "どれを通るか決まらない: " + type
                            + "(通れる型 " + string.Join("・", through) + ")");
                }

                if (through.Length == 1)
                {
                    paths[type] = paths[through[0]];
                }
            }

            HashSet<string> answered = new HashSet<string>(asked, StringComparer.Ordinal);
            foreach (string type in paths.Keys.Where(t => !answered.Contains(t)).ToArray())
            {
                paths.Remove(type);
            }

            return paths;
        }

        /// <summary>
        /// その道を辿る前に呼ぶ初期化のメンバー名。根が、最初の一歩と同じ名前に初期化を続けた
        /// メソッドを宣言していて、それが常駐コネクタだけを取るときに限る。持たなければ null。
        /// </summary>
        private static string Initializer(InventoryRecord inventory, string root, string steps)
        {
            if (steps.Length == 0)
            {
                return null;
            }

            string first = steps.Split('.')[0];
            string named = first.EndsWith("()", StringComparison.Ordinal)
                ? first.Substring(0, first.Length - 2)
                : first;
            string initialize = named + "Initialize";

            return inventory.Signatures.Any(s => s.MemberKind == MemberKind.Method
                && string.Equals(s.DeclaringType, root, StringComparison.Ordinal)
                && string.Equals(s.MemberName, initialize, StringComparison.Ordinal)
                && s.Parameters.Count == 1
                && string.Equals(
                    s.Parameters[0].TypeName,
                    TypeRoleEvidence.InjectedConnector,
                    StringComparison.Ordinal))
                ? initialize
                : null;
        }

        /// <summary>一歩の数で比べる。根の並びで先に見つけたものを残すので、同数では入れ替えない。</summary>
        private static bool Shorter(string steps, string found)
        {
            return Count(steps) < Count(found);
        }

        private static int Count(string steps)
        {
            return steps.Length == 0 ? 0 : steps.Split('.').Length;
        }
    }
}
