using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PEPlugin;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    /// <summary>
    /// 要素の種類1つ。<c>kind</c> が取る値ごとに、並びの在りかと、要素の作り方と複製の仕方を持つ。
    /// 並びを持つ相手は、PMXが直に並べる種類ではPMXそのもの、そうでなければ親の要素である。
    /// </summary>
    public sealed class ElementKind
    {
        private readonly Func<object, IList<object>> _items;

        private readonly Action<object, IList<object>> _replace;

        private readonly Func<object, object> _create;

        private readonly Func<object, object> _clone;

        internal ElementKind(
            string name,
            ElementKind owner,
            Func<object, IList<object>> items,
            Action<object, IList<object>> replace,
            Func<object, object> create,
            Func<object, object> clone)
        {
            Name = name;
            Owner = owner;
            _items = items;
            _replace = replace;
            _create = create;
            _clone = clone;
        }

        /// <summary><c>kind</c> が取る値。</summary>
        public string Name { get; }

        /// <summary>並びを持つ親の種類。PMXが直に並べる種類では null。</summary>
        public ElementKind Owner { get; }

        /// <summary>その相手が並べている要素。並びそのものではなく写しを返す。</summary>
        public IList<object> Items(object owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            return _items(owner);
        }

        /// <summary>その相手の並びを、渡した順の並びで置き換える。</summary>
        public void Replace(object owner, IList<object> items)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            _replace(owner, items);
        }

        /// <summary>
        /// この種類の要素を1つ作る。ほかの要素を指して初めて意味を持つ種類は、指す先の無い要素が
        /// モデルへ書き戻すときに落ちるので作れず、null を返す。
        /// </summary>
        public object Create(object builder, object owner)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return _create == null ? null : _create(builder);
        }

        /// <summary>その要素の複製。</summary>
        public object CloneOf(object item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return _clone(item);
        }
    }

    /// <summary>
    /// 組み立てたツールが <c>kind</c> で受け取る要素の種類の表。名前から種類を引き、知らない名前は
    /// 受け取れる名前を並べて断る。
    /// </summary>
    public static class ElementKinds
    {
        /// <summary>種類を受け取る入力の名前。</summary>
        public const string KindName = "kind";

        /// <summary>PMXが直に並べる頂点。</summary>
        public const string Vertex = "vertex";

        /// <summary>材質が並べる面。</summary>
        public const string Face = "face";

        /// <summary>PMXが直に並べる材質。</summary>
        public const string Material = "material";

        /// <summary>PMXが直に並べるボーン。</summary>
        public const string Bone = "bone";

        /// <summary>ボーンのIKが並べるリンク。</summary>
        public const string IkLink = "ikLink";

        /// <summary>PMXが直に並べるモーフ。</summary>
        public const string Morph = "morph";

        /// <summary>モーフが並べるオフセット。</summary>
        public const string MorphOffset = "morphOffset";

        /// <summary>PMXが直に並べる表示枠。</summary>
        public const string Node = "node";

        /// <summary>表示枠が並べる要素。</summary>
        public const string NodeItem = "nodeItem";

        /// <summary>PMXが直に並べる剛体。</summary>
        public const string Body = "body";

        /// <summary>PMXが直に並べるJoint。</summary>
        public const string Joint = "joint";

        /// <summary>PMXが直に並べるSoftBody。</summary>
        public const string SoftBody = "softBody";

        /// <summary>SoftBodyが並べるアンカー。</summary>
        public const string SoftBodyAnchor = "softBodyAnchor";

        private static readonly IList<ElementKind> Table = Build();

        /// <summary>受け取れる種類の名前。スキーマが並べる順。</summary>
        public static IList<string> Names
        {
            get
            {
                return new ReadOnlyCollection<string>(Table.Select(k => k.Name).ToList());
            }
        }

        /// <summary>名前から種類を引く。知らない名前なら偽で、断る説明を渡す。</summary>
        public static bool TryResolve(object given, out ElementKind kind, out string message)
        {
            kind = null;
            message = null;
            string name = given as string;
            if (name != null)
            {
                kind = Table.FirstOrDefault(k => string.Equals(k.Name, name, StringComparison.Ordinal));
            }

            if (kind != null)
            {
                return true;
            }

            message = KindName + " は次のどれかでなければならない: "
                + string.Join("・", Names.ToArray());

            return false;
        }

        /// <summary>その種類の並びを持つ相手を、PMXから集める。親を持たない種類ではPMX自身。</summary>
        public static IList<object> Owners(object pmx, ElementKind kind)
        {
            if (pmx == null)
            {
                throw new ArgumentNullException(nameof(pmx));
            }

            if (kind == null)
            {
                throw new ArgumentNullException(nameof(kind));
            }

            if (kind.Owner == null)
            {
                return new object[] { pmx };
            }

            List<object> owners = new List<object>();
            foreach (object holder in Owners(pmx, kind.Owner))
            {
                owners.AddRange(kind.Owner.Items(holder));
            }

            return owners;
        }

        private static IList<ElementKind> Build()
        {
            ElementKind material = Rooted(
                Material,
                pmx => ((IPXPmx)pmx).Material,
                builder => builder.Material());
            ElementKind bone = Rooted(
                Bone,
                pmx => ((IPXPmx)pmx).Bone,
                builder => builder.Bone());
            ElementKind morph = Rooted(
                Morph,
                pmx => ((IPXPmx)pmx).Morph,
                builder => builder.Morph());
            ElementKind node = new ElementKind(
                Node,
                null,
                pmx => ReferenceCleanup.Nodes(pmx).Cast<object>().ToList(),
                PutNodes,
                builder => ((IPXPmxBuilder)builder).Node(),
                Copy);
            ElementKind softBody = Rooted(
                SoftBody,
                pmx => ((IPXPmx)pmx).SoftBody,
                builder => builder.SoftBody());

            return new ReadOnlyCollection<ElementKind>(new List<ElementKind>
            {
                Rooted(Vertex, pmx => ((IPXPmx)pmx).Vertex, builder => builder.Vertex()),
                Owned(Face, material, owner => ((IPXMaterial)owner).Faces),
                material,
                bone,
                Owned(IkLink, bone, owner => ((IPXBone)owner).IK.Links),
                morph,
                Owned(MorphOffset, morph, owner => ((IPXMorph)owner).Offsets),
                node,
                Owned(NodeItem, node, owner => ((IPXNode)owner).Items),
                Rooted(Body, pmx => ((IPXPmx)pmx).Body, builder => builder.Body()),
                Rooted(Joint, pmx => ((IPXPmx)pmx).Joint, builder => builder.Joint()),
                softBody,
                Owned(SoftBodyAnchor, softBody, owner => ((IPXSoftBody)owner).Anchors),
            });
        }

        private static ElementKind Rooted<T>(
            string name, Func<object, IList<T>> list, Func<IPXPmxBuilder, T> create)
        {
            return new ElementKind(
                name,
                null,
                Taken(list),
                Put(list),
                builder => create((IPXPmxBuilder)builder),
                Copy);
        }

        private static ElementKind Owned<T>(
            string name, ElementKind owner, Func<object, IList<T>> list)
        {
            return new ElementKind(name, owner, Taken(list), Put(list), null, Copy);
        }

        private static Func<object, IList<object>> Taken<T>(Func<object, IList<T>> list)
        {
            return owner => list(owner).Cast<object>().ToList();
        }

        private static Action<object, IList<object>> Put<T>(Func<object, IList<T>> list)
        {
            return (owner, items) =>
            {
                IList<T> held = list(owner);
                held.Clear();
                foreach (object item in items)
                {
                    held.Add((T)item);
                }
            };
        }

        /// <summary>
        /// 枠の並びを置き換える。先頭の2つはモデルがリストとは別に持つ枠で、リストへは入らない。
        /// その2つが先頭に居ない並びは <see cref="ArgumentException"/> で断る。
        /// </summary>
        private static void PutNodes(object owner, IList<object> items)
        {
            IPXPmx model = (IPXPmx)owner;
            object[] ahead = { model.ExpressionNode, model.RootNode };
            for (int at = 0; at < ahead.Length; at++)
            {
                if (items.Count <= at || !ReferenceEquals(items[at], ahead[at]))
                {
                    throw new ArgumentException(
                        "先頭の" + ahead.Length
                            + "件はモデルがリストとは別に持つ枠で、取り除くことも動かすこともできない。",
                        nameof(items));
                }
            }

            IList<IPXNode> held = model.Node;
            held.Clear();
            for (int at = ahead.Length; at < items.Count; at++)
            {
                held.Add((IPXNode)items[at]);
            }
        }

        private static object Copy(object item)
        {
            return ((ICloneable)item).Clone();
        }
    }

    /// <summary>
    /// <c>kind</c> と親の指定を読み、その種類の並びを持つ相手を解く。親を持たない種類ではPMX自身
    /// ひとつが相手で、親の指定は受け取らない。
    /// </summary>
    public static class ElementScope
    {
        /// <summary>ここが読む項目の名前。</summary>
        public static IList<string> Names
        {
            get
            {
                return new ReadOnlyCollection<string>(new List<string>
                {
                    ElementKinds.KindName,
                    TargetNames.Parent.Indices,
                    TargetNames.Parent.Range,
                    TargetNames.Parent.All,
                });
            }
        }

        /// <summary>
        /// 解いた相手を渡す。<paramref name="kind"/> が親を持つ種類のときだけ、親の指定を読む。
        /// 解けなければ偽で、断る内容を渡す。
        /// </summary>
        public static bool TryTake(
            McpMethodContext context,
            object pmx,
            out ElementKind kind,
            out IList<object> owners,
            out string code,
            out string message)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (pmx == null)
            {
                throw new ArgumentNullException(nameof(pmx));
            }

            kind = null;
            owners = null;
            code = ToolEnvelope.InvalidArgument;
            object given;
            context.Params.TryGetValue(ElementKinds.KindName, out given);
            if (!ElementKinds.TryResolve(given, out kind, out message))
            {
                return false;
            }

            if (context.Params.ContainsKey(TargetNames.Parent.Handles))
            {
                message = TargetNames.Parent.Handles + " は受け取らない。親は位置で指す。";

                return false;
            }

            TargetRequest request;
            if (!TargetInput.TryTake(
                context.Params, TargetNames.Parent, false, out request, out code, out message))
            {
                return false;
            }

            IList<object> all = ElementKinds.Owners(pmx, kind);
            if (kind.Owner == null)
            {
                if (TargetSelection.Points(request))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = kind.Name + " はPMXが直に並べる種類なので、親の指定を受け取らない。";

                    return false;
                }

                owners = all;

                return true;
            }

            ResolvedTargets resolved;
            if (!TargetSelection.TryResolve(
                request,
                TargetForm.Indices | TargetForm.Range | TargetForm.All,
                all.Count,
                id => false,
                out resolved,
                out code,
                out message,
                TargetNames.Parent))
            {
                return false;
            }

            owners = resolved.Indices.Select(at => all[at]).ToList();

            return true;
        }

        /// <summary>
        /// <paramref name="owner"/> 番目の材質の面の並びの中で、指した面の位置を解く。
        /// <paramref name="counts"/> は材質ごとの面の数で、材質の並びの順に渡す。画面の選択で
        /// 指すときは、モデル全体で数えた選択のうちその材質のものを採る。選択がほかの材質にだけ
        /// あるときは、この材質からは1つも採らない。
        /// </summary>
        public static bool TryFacePositions(
            McpMethodContext context,
            IList<int> counts,
            int owner,
            out IList<int> positions,
            out string code,
            out string message)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (counts == null)
            {
                throw new ArgumentNullException(nameof(counts));
            }

            ScreenPick picked = context.Screen.PickFaces(counts, owner);
            if (TargetInput.TryPositions(
                context.Params,
                TargetNames.Element,
                counts[owner],
                out positions,
                out code,
                out message,
                picked))
            {
                return true;
            }

            if (code == ToolEnvelope.NotApplicable && context.Screen.TakenFaces(counts).Count != 0)
            {
                positions = new int[0];
                code = null;
                message = null;

                return true;
            }

            return false;
        }

        /// <summary>
        /// その相手の並びの中で、指した要素の位置を解く。位置は昇順で重なりを持たない。解けなければ
        /// 偽で、断る内容を渡す。
        /// </summary>
        public static bool TryPositions(
            McpMethodContext context,
            object pmx,
            ElementKind kind,
            object owner,
            out IList<int> positions,
            out string code,
            out string message)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (pmx == null)
            {
                throw new ArgumentNullException(nameof(pmx));
            }

            if (kind == null)
            {
                throw new ArgumentNullException(nameof(kind));
            }

            positions = null;
            int count = kind.Items(owner).Count;
            if (kind.Owner != null && string.Equals(kind.Name, ElementKinds.Face, StringComparison.Ordinal))
            {
                IList<object> owners = kind.Owner.Items(pmx);
                int at = Enumerable.Range(0, owners.Count)
                    .FirstOrDefault(i => ReferenceEquals(owners[i], owner));

                return TryFacePositions(
                    context,
                    owners.Select(o => kind.Items(o).Count).ToList(),
                    at,
                    out positions,
                    out code,
                    out message);
            }

            ScreenPick picked = kind.Owner == null ? context.Screen.Pick(kind.Name, count) : null;
            if (picked == null && context.Params.ContainsKey(TargetNames.Element.Selected))
            {
                code = ToolEnvelope.InvalidArgument;
                message = kind.Owner == null
                    ? TargetNames.Element.Selected + " で指せる種類ではない: " + kind.Name
                        + "。画面が選べるのは "
                        + string.Join("・", ScreenTargets.Kinds.ToArray()) + " である。"
                    : TargetNames.Element.Selected + " が指す位置はモデル全体で数えるが、"
                        + kind.Name + " は " + kind.Owner.Name + " ごとに数える並びである。"
                        + TargetNames.Element.Indices + "・" + TargetNames.Element.Range + "・"
                        + TargetNames.Element.All + " で指す。";

                return false;
            }

            return TargetInput.TryPositions(
                context.Params,
                TargetNames.Element,
                count,
                out positions,
                out code,
                out message,
                picked);
        }

    }
}
