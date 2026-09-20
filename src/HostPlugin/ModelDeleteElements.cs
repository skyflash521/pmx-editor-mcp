using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した要素を並びから消し、それを指したままの口を片付けるツール。<c>related</c> が
    /// <c>cascade</c> のときは、消した要素だけが使っていた要素も同じ呼び出しで消える。
    /// </summary>
    public static class ModelDeleteElements
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_delete_elements";

        /// <summary>消した件数を返す項目の名前。</summary>
        public const string RemovedName = "removed";

        /// <summary>つられて消えた要素を、種類ごとに並べて返す項目の名前。</summary>
        public const string FollowingName = "following";

        /// <summary>つられて消えた要素の種類を返す項目の名前。</summary>
        public const string KindName = "kind";

        /// <summary>直した口の数を返す項目の名前。</summary>
        public const string RepairedName = "repaired";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            List<string> known = new List<string>(ElementScope.Names)
            {
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.All,
                ReferenceCleanup.RelatedName,
            };
            methods.Add(ToolName, edit.Method(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            ElementKind kind;
            IList<object> owners;
            string code;
            string message;
            object given;
            RelatedHandling handling;
            context.Params.TryGetValue(ReferenceCleanup.RelatedName, out given);
            if (!ElementScope.TryTake(context, pmx, out kind, out owners, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            if (!ReferenceCleanup.TryResolve(given, out handling, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            Dictionary<object, IList<int>> chosen = new Dictionary<object, IList<int>>();
            List<object> going = new List<object>();
            foreach (object owner in owners)
            {
                IList<int> positions;
                if (!ElementScope.TryPositions(
                    context, kind, owner, out positions, out code, out message))
                {
                    return ComposedEditResult.Refuse(code, message);
                }

                chosen.Add(owner, positions);
                IList<object> items = kind.Items(owner);
                going.AddRange(positions.Select(at => items[at]));
            }

            IDictionary<string, IList<object>> following = handling == RelatedHandling.Cascade
                ? ReferenceCleanup.Following(pmx, kind, going)
                : new Dictionary<string, IList<object>>(StringComparer.Ordinal);
            foreach (KeyValuePair<object, IList<int>> pair in chosen)
            {
                Drop(kind, pair.Key, pair.Value);
            }

            foreach (KeyValuePair<string, IList<object>> dragged in following)
            {
                Drag(pmx, dragged.Key, dragged.Value);
            }

            int repaired = handling == RelatedHandling.Keep ? 0 : ReferenceCleanup.Sweep(pmx);

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { RemovedName, going.Count },
                    { FollowingName, Counted(following) },
                    { RepairedName, repaired },
                });
        }

        private static void Drop(ElementKind kind, object owner, IList<int> positions)
        {
            IList<object> items = kind.Items(owner);
            kind.Replace(owner, items.Where((item, at) => !positions.Contains(at)).ToList());
        }

        private static void Drag(object pmx, string name, IList<object> going)
        {
            ElementKind kind;
            string message;
            if (!ElementKinds.TryResolve(name, out kind, out message))
            {
                throw new InvalidOperationException(message);
            }

            foreach (object owner in ElementKinds.Owners(pmx, kind))
            {
                IList<object> items = kind.Items(owner);
                kind.Replace(
                    owner, items.Where(item => !going.Any(g => ReferenceEquals(g, item))).ToList());
            }
        }

        private static object[] Counted(IDictionary<string, IList<object>> following)
        {
            return ElementKinds.Names
                .Where(following.ContainsKey)
                .Select(name => (object)new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { KindName, name },
                    { RemovedName, following[name].Count },
                })
                .ToArray();
        }
    }
}
