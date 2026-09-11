using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>接続の根から受け手の型へ至る道。根と、根から進む一歩の並びからなる。</summary>
    public sealed class ReceiverPath
    {
        public ReceiverPath(string root, string steps)
        {
            Root = root;
            Steps = steps;
        }

        /// <summary>辿り始める接続の根の型名。</summary>
        public string Root { get; }

        /// <summary>根から進む一歩の名前を点でつないだもの。根そのものでは空。</summary>
        public string Steps { get; }
    }

    /// <summary>
    /// 受け手の型へ至る道を、根ごとに辿って決める。道は列挙から導くので正本へ書かない。同じ型へ
    /// 道が複数あるときは、一歩の数が最も少ないものを採り、並ぶときは根の並びで先のものを採る。
    /// </summary>
    public static class ReceiverEvidence
    {
        /// <summary>
        /// 型ごとの道。辿り着けない型は持たない。<paramref name="types"/> は道を知りたい型の名前。
        /// </summary>
        public static IDictionary<string, ReceiverPath> Resolve(
            InventoryRecord inventory, IEnumerable<string> types)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            Dictionary<string, ReceiverPath> paths =
                new Dictionary<string, ReceiverPath>(StringComparer.Ordinal);
            foreach (string root in TypeRoleEvidence.ConnectionRoots)
            {
                IDictionary<string, string> reached =
                    TypeRoleEvidence.ReachableFromRoots(inventory, new[] { root });
                foreach (string type in types.Distinct(StringComparer.Ordinal))
                {
                    string steps;
                    if (!reached.TryGetValue(type, out steps))
                    {
                        continue;
                    }

                    ReceiverPath found;
                    if (!paths.TryGetValue(type, out found) || Shorter(steps, found.Steps))
                    {
                        paths[type] = new ReceiverPath(root, steps);
                    }
                }
            }

            return paths;
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
