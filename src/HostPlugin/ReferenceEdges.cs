using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指す側の要素1つが、指される側の要素1つを指していることの知らせ。<paramref name="weight"/> は
    /// 重みを持つ辺での重みで、持たない辺では常に1である。
    /// </summary>
    public delegate void ReferenceVisit(object referrer, object target, float weight);

    /// <summary>
    /// 参照の辺1本。指す側の種類から指される側の種類へ向かう口をまとめ、指している組を並べる向きと、
    /// 指す先を付け替える向きの両方を持つ。
    /// </summary>
    public sealed class ReferenceEdge
    {
        private readonly Action<IPXPmx, ReferenceVisit> _walk;

        private readonly Action<IPXPmx, Func<object, object>> _retarget;

        private readonly Func<IPXPmx, ISet<object>, int> _mend;

        internal ReferenceEdge(
            string referrerKind,
            string targetKind,
            bool weighted,
            Action<IPXPmx, ReferenceVisit> walk,
            Action<IPXPmx, Func<object, object>> retarget,
            Func<IPXPmx, ISet<object>, int> mend)
        {
            ReferrerKind = referrerKind;
            TargetKind = targetKind;
            IsWeighted = weighted;
            _walk = walk;
            _retarget = retarget;
            _mend = mend;
        }

        /// <summary>指す側の種類の名前。</summary>
        public string ReferrerKind { get; }

        /// <summary>指される側の種類の名前。</summary>
        public string TargetKind { get; }

        /// <summary>重みを持つ辺か。持たない辺では重みが常に1になる。</summary>
        public bool IsWeighted { get; }

        /// <summary>指す先を付け替えられる辺か。ほかの要素を介して届く辺は付け替えられない。</summary>
        public bool CanRetarget
        {
            get { return _retarget != null; }
        }

        /// <summary>この辺で指している組を、指す側の並びの順に並べる。空の口は並ばない。</summary>
        public void Walk(object pmx, ReferenceVisit visit)
        {
            if (pmx == null)
            {
                throw new ArgumentNullException(nameof(pmx));
            }

            if (visit == null)
            {
                throw new ArgumentNullException(nameof(visit));
            }

            _walk((IPXPmx)pmx, visit);
        }

        /// <summary>
        /// この辺の口が指す先を、<paramref name="moved"/> が返す相手へ付け替える。空の口は空のまま
        /// 残す。付け替えられない辺では何もしない。
        /// </summary>
        public void Retarget(object pmx, Func<object, object> moved)
        {
            if (pmx == null)
            {
                throw new ArgumentNullException(nameof(pmx));
            }

            if (moved == null)
            {
                throw new ArgumentNullException(nameof(moved));
            }

            if (_retarget != null)
            {
                _retarget((IPXPmx)pmx, moved);
            }
        }

        /// <summary>
        /// この辺の口のうち、<paramref name="live"/> に居ない相手を指しているものを片付ける。片付け方は
        /// 辺ごとに違い、口を空にするもの・口を持つ要素ごと落とすもの・別の相手へ移すものがある。
        /// 片付けた数を返す。1件と数える単位も辺ごとに違い、落とした面・落としたオフセット・落とした
        /// 表示枠の項目・空にした口・直した頂点が、それぞれ1件である。
        /// </summary>
        public int Mend(object pmx, ISet<object> live)
        {
            if (pmx == null)
            {
                throw new ArgumentNullException(nameof(pmx));
            }

            if (live == null)
            {
                throw new ArgumentNullException(nameof(live));
            }

            return _mend == null ? 0 : _mend((IPXPmx)pmx, live);
        }
    }

    /// <summary>指す側の種類ごとに、指していた要素の位置を持つ組。</summary>
    public sealed class ReferrerSets
    {
        private readonly IDictionary<string, IList<int>> _found;

        internal ReferrerSets(IList<string> kinds, IDictionary<string, IList<int>> found)
        {
            Kinds = new ReadOnlyCollection<string>(kinds);
            _found = found;
        }

        /// <summary>指す側になれる種類の名前。辺の表が並べる順。</summary>
        public IList<string> Kinds { get; }

        /// <summary>その種類で指していた要素の位置。昇順で重なりを持たない。</summary>
        public IList<int> Of(string referrerKind)
        {
            if (referrerKind == null)
            {
                throw new ArgumentNullException(nameof(referrerKind));
            }

            IList<int> held;

            return _found.TryGetValue(referrerKind, out held)
                ? held
                : new ReadOnlyCollection<int>(new int[0]);
        }
    }

    /// <summary>
    /// 参照の辺の表。指される側の種類を選んで、その並びの要素を指している要素を引く。指す側が
    /// その種類の並びに居ないものは数えない。
    /// </summary>
    public static class ReferenceEdges
    {
        private static readonly IList<ReferenceEdge> Table = Build();

        /// <summary>辺のすべて。</summary>
        public static IList<ReferenceEdge> All
        {
            get { return Table; }
        }

        /// <summary>指される側になれる種類の名前。</summary>
        public static IList<string> TargetKinds
        {
            get
            {
                return new ReadOnlyCollection<string>(new[]
                {
                    ElementKinds.Vertex,
                    ElementKinds.Material,
                    ElementKinds.Bone,
                    ElementKinds.Morph,
                    ElementKinds.Body,
                });
            }
        }

        /// <summary>その種類を指している辺。指す側の種類の重なりは無い。</summary>
        public static IList<ReferenceEdge> Into(string targetKind)
        {
            if (targetKind == null)
            {
                throw new ArgumentNullException(nameof(targetKind));
            }

            return new ReadOnlyCollection<ReferenceEdge>(Table
                .Where(edge => string.Equals(edge.TargetKind, targetKind, StringComparison.Ordinal))
                .ToList());
        }

        /// <summary>その種類の要素を、位置の順に並べたもの。面はモデル全体の通し番号の順に並ぶ。</summary>
        public static IList<object> Listed(object pmx, string kind)
        {
            if (pmx == null)
            {
                throw new ArgumentNullException(nameof(pmx));
            }

            if (string.Equals(kind, ElementKinds.Face, StringComparison.Ordinal))
            {
                return ViewSelection.Faces((IPXPmx)pmx).Cast<object>().ToList();
            }

            ElementKind held;
            string message;
            if (!ElementKinds.TryResolve(kind, out held, out message))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, message);
            }

            return held.Items(pmx);
        }

        /// <summary>
        /// 指された位置の要素のどれか1つでも指している要素を、指す側の種類ごとにまとめる。
        /// <paramref name="minWeight"/> は重みを持つ辺のしきい値で、これより大きい重みだけを指す口と
        /// して数える。
        /// </summary>
        public static ReferrerSets Union(
            object pmx, string targetKind, IList<int> targets, float minWeight)
        {
            IList<string> kinds;
            IDictionary<int, IDictionary<string, SortedSet<int>>> found =
                Scan(pmx, targetKind, targets, minWeight, false, out kinds);

            return Made(kinds, found.Values.FirstOrDefault());
        }

        /// <summary>
        /// 指された位置の要素1つずつについて、それを指している要素を指す側の種類ごとにまとめる。
        /// 渡した位置と同じ順で並ぶ。
        /// </summary>
        public static IList<ReferrerSets> PerTarget(
            object pmx, string targetKind, IList<int> targets, float minWeight)
        {
            IList<string> kinds;
            IDictionary<int, IDictionary<string, SortedSet<int>>> found =
                Scan(pmx, targetKind, targets, minWeight, true, out kinds);

            return new ReadOnlyCollection<ReferrerSets>(targets
                .Select(at => Made(kinds, found.ContainsKey(at) ? found[at] : null))
                .ToList());
        }

        /// <summary>
        /// 指された位置の要素を指している要素を集める。<paramref name="perTarget"/> が真なら指される
        /// 側の位置ごとに分け、偽ならすべてを1つにまとめて位置 -1 へ置く。
        /// </summary>
        private static IDictionary<int, IDictionary<string, SortedSet<int>>> Scan(
            object pmx,
            string targetKind,
            IList<int> targets,
            float minWeight,
            bool perTarget,
            out IList<string> kinds)
        {
            if (pmx == null)
            {
                throw new ArgumentNullException(nameof(pmx));
            }

            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            IList<ReferenceEdge> edges = Into(targetKind);
            kinds = new ReadOnlyCollection<string>(edges
                .Select(edge => edge.ReferrerKind)
                .Distinct(StringComparer.Ordinal)
                .ToList());
            IDictionary<object, int> places = Placed(Listed(pmx, targetKind));
            HashSet<int> wanted = new HashSet<int>(targets);
            Dictionary<int, IDictionary<string, SortedSet<int>>> found =
                new Dictionary<int, IDictionary<string, SortedSet<int>>>();
            foreach (ReferenceEdge edge in edges)
            {
                IDictionary<object, int> referrers = Placed(Listed(pmx, edge.ReferrerKind));
                string kind = edge.ReferrerKind;
                bool weighted = edge.IsWeighted;
                edge.Walk(pmx, (referrer, target, weight) =>
                {
                    int at;
                    int held;
                    if ((weighted && !(weight > minWeight))
                        || target == null
                        || !places.TryGetValue(target, out at)
                        || !wanted.Contains(at)
                        || referrer == null
                        || !referrers.TryGetValue(referrer, out held))
                    {
                        return;
                    }

                    Put(found, perTarget ? at : -1, kind, held);
                });
            }

            return found;
        }

        private static void Put(
            IDictionary<int, IDictionary<string, SortedSet<int>>> found,
            int at,
            string kind,
            int referrer)
        {
            IDictionary<string, SortedSet<int>> held;
            if (!found.TryGetValue(at, out held))
            {
                held = new Dictionary<string, SortedSet<int>>(StringComparer.Ordinal);
                found.Add(at, held);
            }

            SortedSet<int> places;
            if (!held.TryGetValue(kind, out places))
            {
                places = new SortedSet<int>();
                held.Add(kind, places);
            }

            places.Add(referrer);
        }

        private static ReferrerSets Made(
            IList<string> kinds, IDictionary<string, SortedSet<int>> found)
        {
            Dictionary<string, IList<int>> held =
                new Dictionary<string, IList<int>>(StringComparer.Ordinal);
            foreach (string kind in kinds)
            {
                SortedSet<int> places;
                held.Add(
                    kind,
                    new ReadOnlyCollection<int>(
                        found != null && found.TryGetValue(kind, out places)
                            ? places.ToList()
                            : new List<int>()));
            }

            return new ReferrerSets(kinds, held);
        }

        private static IDictionary<object, int> Placed(IList<object> items)
        {
            Dictionary<object, int> made =
                new Dictionary<object, int>(ReferenceComparer<object>.Instance);
            for (int at = 0; at < items.Count; at++)
            {
                if (items[at] != null && !made.ContainsKey(items[at]))
                {
                    made.Add(items[at], at);
                }
            }

            return made;
        }

        private static IList<ReferenceEdge> Build()
        {
            return new ReadOnlyCollection<ReferenceEdge>(new List<ReferenceEdge>
            {
                new ReferenceEdge(
                    ElementKinds.Face,
                    ElementKinds.Vertex,
                    false,
                    WalkFaceVertex,
                    MoveFaceVertex,
                    MendFaceVertex),
                new ReferenceEdge(
                    ElementKinds.Material,
                    ElementKinds.Vertex,
                    false,
                    WalkMaterialVertex,
                    null,
                    null),
                new ReferenceEdge(
                    ElementKinds.Morph,
                    ElementKinds.Vertex,
                    false,
                    WalkMorphVertex,
                    MoveMorphVertex,
                    MendMorphVertex),
                new ReferenceEdge(
                    ElementKinds.SoftBody,
                    ElementKinds.Vertex,
                    false,
                    WalkSoftBodyVertex,
                    MoveSoftBodyVertex,
                    MendSoftBodyVertex),
                new ReferenceEdge(
                    ElementKinds.Morph,
                    ElementKinds.Material,
                    false,
                    WalkMorphMaterial,
                    MoveMorphMaterial,
                    MendMorphMaterial),
                new ReferenceEdge(
                    ElementKinds.SoftBody,
                    ElementKinds.Material,
                    false,
                    WalkSoftBodyMaterial,
                    MoveSoftBodyMaterial,
                    MendSoftBodyMaterial),
                new ReferenceEdge(
                    ElementKinds.Vertex,
                    ElementKinds.Bone,
                    true,
                    WalkVertexBone,
                    MoveVertexBone,
                    MendVertexBone),
                new ReferenceEdge(
                    ElementKinds.Bone,
                    ElementKinds.Bone,
                    false,
                    WalkBoneBone,
                    MoveBoneBone,
                    MendBoneBone),
                new ReferenceEdge(
                    ElementKinds.Morph,
                    ElementKinds.Bone,
                    false,
                    WalkMorphBone,
                    MoveMorphBone,
                    MendMorphBone),
                new ReferenceEdge(
                    ElementKinds.Node,
                    ElementKinds.Bone,
                    false,
                    WalkNodeBone,
                    MoveNodeBone,
                    MendNodeBone),
                new ReferenceEdge(
                    ElementKinds.Body,
                    ElementKinds.Bone,
                    false,
                    WalkBodyBone,
                    MoveBodyBone,
                    MendBodyBone),
                new ReferenceEdge(
                    ElementKinds.Morph,
                    ElementKinds.Morph,
                    false,
                    WalkMorphMorph,
                    MoveMorphMorph,
                    MendMorphMorph),
                new ReferenceEdge(
                    ElementKinds.Node,
                    ElementKinds.Morph,
                    false,
                    WalkNodeMorph,
                    MoveNodeMorph,
                    MendNodeMorph),
                new ReferenceEdge(
                    ElementKinds.Joint,
                    ElementKinds.Body,
                    false,
                    WalkJointBody,
                    MoveJointBody,
                    MendJointBody),
                new ReferenceEdge(
                    ElementKinds.Morph,
                    ElementKinds.Body,
                    false,
                    WalkMorphBody,
                    MoveMorphBody,
                    MendMorphBody),
                new ReferenceEdge(
                    ElementKinds.SoftBody,
                    ElementKinds.Body,
                    false,
                    WalkSoftBodyBody,
                    MoveSoftBodyBody,
                    MendSoftBodyBody),
            });
        }

        private static void Met(ReferenceVisit visit, object referrer, object target)
        {
            if (target != null)
            {
                visit(referrer, target, 1f);
            }
        }

        private static void WalkFaceVertex(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXFace face in ViewSelection.Faces(model))
            {
                Met(visit, face, face.Vertex1);
                Met(visit, face, face.Vertex2);
                Met(visit, face, face.Vertex3);
            }
        }

        private static void MoveFaceVertex(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXMaterial material in model.Material)
            {
                foreach (IPXFace face in material.Faces)
                {
                    face.Vertex1 = (IPXVertex)Landing(moved, face.Vertex1);
                    face.Vertex2 = (IPXVertex)Landing(moved, face.Vertex2);
                    face.Vertex3 = (IPXVertex)Landing(moved, face.Vertex3);
                }
            }
        }

        private static void WalkMaterialVertex(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXMaterial material in model.Material)
            {
                foreach (IPXFace face in material.Faces)
                {
                    Met(visit, material, face.Vertex1);
                    Met(visit, material, face.Vertex2);
                    Met(visit, material, face.Vertex3);
                }
            }
        }

        private static void WalkMorphVertex(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXMorph morph in model.Morph)
            {
                foreach (IPXMorphOffset offset in morph.Offsets)
                {
                    IPXVertexMorphOffset shifted = offset as IPXVertexMorphOffset;
                    if (shifted != null)
                    {
                        Met(visit, morph, shifted.Vertex);
                    }

                    IPXUVMorphOffset slid = offset as IPXUVMorphOffset;
                    if (slid != null)
                    {
                        Met(visit, morph, slid.Vertex);
                    }
                }
            }
        }

        private static void MoveMorphVertex(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXMorph morph in model.Morph)
            {
                foreach (IPXMorphOffset offset in morph.Offsets)
                {
                    IPXVertexMorphOffset shifted = offset as IPXVertexMorphOffset;
                    if (shifted != null)
                    {
                        shifted.Vertex = (IPXVertex)Landing(moved, shifted.Vertex);
                    }

                    IPXUVMorphOffset slid = offset as IPXUVMorphOffset;
                    if (slid != null)
                    {
                        slid.Vertex = (IPXVertex)Landing(moved, slid.Vertex);
                    }
                }
            }
        }

        private static void WalkSoftBodyVertex(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXSoftBody soft in model.SoftBody)
            {
                foreach (IPXVertex pin in soft.Pins)
                {
                    Met(visit, soft, pin);
                }

                foreach (IPXSoftBodyAnchor anchor in soft.Anchors)
                {
                    Met(visit, soft, anchor.Vertex);
                }
            }
        }

        private static void MoveSoftBodyVertex(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXSoftBody soft in model.SoftBody)
            {
                for (int at = 0; at < soft.Pins.Count; at++)
                {
                    soft.Pins[at] = (IPXVertex)Landing(moved, soft.Pins[at]);
                }

                foreach (IPXSoftBodyAnchor anchor in soft.Anchors)
                {
                    anchor.Vertex = (IPXVertex)Landing(moved, anchor.Vertex);
                }
            }
        }

        private static void WalkMorphMaterial(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXMorph morph in model.Morph)
            {
                foreach (IPXMorphOffset offset in morph.Offsets)
                {
                    IPXMaterialMorphOffset painted = offset as IPXMaterialMorphOffset;
                    if (painted == null)
                    {
                        continue;
                    }

                    if (painted.Material != null)
                    {
                        Met(visit, morph, painted.Material);

                        continue;
                    }

                    // 材質を指さない材質モーフのオフセットは全材質を指す。
                    foreach (IPXMaterial material in model.Material)
                    {
                        Met(visit, morph, material);
                    }
                }
            }
        }

        private static void MoveMorphMaterial(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXMorph morph in model.Morph)
            {
                foreach (IPXMorphOffset offset in morph.Offsets)
                {
                    IPXMaterialMorphOffset painted = offset as IPXMaterialMorphOffset;
                    if (painted != null)
                    {
                        painted.Material = (IPXMaterial)Landing(moved, painted.Material);
                    }
                }
            }
        }

        private static void WalkSoftBodyMaterial(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXSoftBody soft in model.SoftBody)
            {
                Met(visit, soft, soft.Material);
            }
        }

        private static void MoveSoftBodyMaterial(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXSoftBody soft in model.SoftBody)
            {
                soft.Material = (IPXMaterial)Landing(moved, soft.Material);
            }
        }

        private static void WalkVertexBone(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXVertex vertex in model.Vertex)
            {
                foreach (KeyValuePair<IPXBone, float> share in VertexWeights.All(vertex))
                {
                    if (share.Key != null)
                    {
                        visit(vertex, share.Key, share.Value);
                    }
                }
            }
        }

        private static void MoveVertexBone(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXVertex vertex in model.Vertex)
            {
                vertex.Bone1 = (IPXBone)Landing(moved, vertex.Bone1);
                vertex.Bone2 = (IPXBone)Landing(moved, vertex.Bone2);
                vertex.Bone3 = (IPXBone)Landing(moved, vertex.Bone3);
                vertex.Bone4 = (IPXBone)Landing(moved, vertex.Bone4);
            }
        }

        private static void WalkBoneBone(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXBone bone in model.Bone)
            {
                Met(visit, bone, bone.Parent);
                Met(visit, bone, bone.ToBone);
                Met(visit, bone, bone.AppendParent);
                if (bone.IK == null)
                {
                    continue;
                }

                Met(visit, bone, bone.IK.Target);
                foreach (IPXIKLink link in bone.IK.Links)
                {
                    Met(visit, bone, link.Bone);
                }
            }
        }

        private static void MoveBoneBone(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXBone bone in model.Bone)
            {
                bone.Parent = (IPXBone)Landing(moved, bone.Parent);
                bone.ToBone = (IPXBone)Landing(moved, bone.ToBone);
                bone.AppendParent = (IPXBone)Landing(moved, bone.AppendParent);
                if (bone.IK == null)
                {
                    continue;
                }

                bone.IK.Target = (IPXBone)Landing(moved, bone.IK.Target);
                foreach (IPXIKLink link in bone.IK.Links)
                {
                    link.Bone = (IPXBone)Landing(moved, link.Bone);
                }
            }
        }

        private static void WalkMorphBone(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXMorph morph in model.Morph)
            {
                foreach (IPXMorphOffset offset in morph.Offsets)
                {
                    IPXBoneMorphOffset posed = offset as IPXBoneMorphOffset;
                    if (posed != null)
                    {
                        Met(visit, morph, posed.Bone);
                    }
                }
            }
        }

        private static void MoveMorphBone(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXMorph morph in model.Morph)
            {
                foreach (IPXMorphOffset offset in morph.Offsets)
                {
                    IPXBoneMorphOffset posed = offset as IPXBoneMorphOffset;
                    if (posed != null)
                    {
                        posed.Bone = (IPXBone)Landing(moved, posed.Bone);
                    }
                }
            }
        }

        private static void WalkNodeBone(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXNode node in ReferenceCleanup.Nodes(model))
            {
                foreach (IPXNodeItem item in node.Items)
                {
                    IPXBoneNodeItem held = item as IPXBoneNodeItem;
                    if (held != null)
                    {
                        Met(visit, node, held.Bone);
                    }
                }
            }
        }

        private static void MoveNodeBone(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXNode node in ReferenceCleanup.Nodes(model))
            {
                foreach (IPXNodeItem item in node.Items)
                {
                    IPXBoneNodeItem held = item as IPXBoneNodeItem;
                    if (held != null)
                    {
                        held.Bone = (IPXBone)Landing(moved, held.Bone);
                    }
                }
            }
        }

        private static void WalkBodyBone(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXBody body in model.Body)
            {
                Met(visit, body, body.Bone);
            }
        }

        private static void MoveBodyBone(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXBody body in model.Body)
            {
                body.Bone = (IPXBone)Landing(moved, body.Bone);
            }
        }

        private static void WalkMorphMorph(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXMorph morph in model.Morph)
            {
                foreach (IPXMorphOffset offset in morph.Offsets)
                {
                    IPXGroupMorphOffset grouped = offset as IPXGroupMorphOffset;
                    if (grouped != null)
                    {
                        Met(visit, morph, grouped.Morph);
                    }
                }
            }
        }

        private static void MoveMorphMorph(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXMorph morph in model.Morph)
            {
                foreach (IPXMorphOffset offset in morph.Offsets)
                {
                    IPXGroupMorphOffset grouped = offset as IPXGroupMorphOffset;
                    if (grouped != null)
                    {
                        grouped.Morph = (IPXMorph)Landing(moved, grouped.Morph);
                    }
                }
            }
        }

        private static void WalkNodeMorph(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXNode node in ReferenceCleanup.Nodes(model))
            {
                foreach (IPXNodeItem item in node.Items)
                {
                    IPXMorphNodeItem held = item as IPXMorphNodeItem;
                    if (held != null)
                    {
                        Met(visit, node, held.Morph);
                    }
                }
            }
        }

        private static void MoveNodeMorph(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXNode node in ReferenceCleanup.Nodes(model))
            {
                foreach (IPXNodeItem item in node.Items)
                {
                    IPXMorphNodeItem held = item as IPXMorphNodeItem;
                    if (held != null)
                    {
                        held.Morph = (IPXMorph)Landing(moved, held.Morph);
                    }
                }
            }
        }

        private static void WalkJointBody(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXJoint joint in model.Joint)
            {
                Met(visit, joint, joint.BodyA);
                Met(visit, joint, joint.BodyB);
            }
        }

        private static void MoveJointBody(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXJoint joint in model.Joint)
            {
                joint.BodyA = (IPXBody)Landing(moved, joint.BodyA);
                joint.BodyB = (IPXBody)Landing(moved, joint.BodyB);
            }
        }

        private static void WalkMorphBody(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXMorph morph in model.Morph)
            {
                foreach (IPXMorphOffset offset in morph.Offsets)
                {
                    IPXImpulseMorphOffset pushed = offset as IPXImpulseMorphOffset;
                    if (pushed != null)
                    {
                        Met(visit, morph, pushed.Body);
                    }
                }
            }
        }

        private static void MoveMorphBody(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXMorph morph in model.Morph)
            {
                foreach (IPXMorphOffset offset in morph.Offsets)
                {
                    IPXImpulseMorphOffset pushed = offset as IPXImpulseMorphOffset;
                    if (pushed != null)
                    {
                        pushed.Body = (IPXBody)Landing(moved, pushed.Body);
                    }
                }
            }
        }

        private static void WalkSoftBodyBody(IPXPmx model, ReferenceVisit visit)
        {
            foreach (IPXSoftBody soft in model.SoftBody)
            {
                foreach (IPXSoftBodyAnchor anchor in soft.Anchors)
                {
                    Met(visit, soft, anchor.Body);
                }
            }
        }

        private static void MoveSoftBodyBody(IPXPmx model, Func<object, object> moved)
        {
            foreach (IPXSoftBody soft in model.SoftBody)
            {
                foreach (IPXSoftBodyAnchor anchor in soft.Anchors)
                {
                    anchor.Body = (IPXBody)Landing(moved, anchor.Body);
                }
            }
        }

        private static object Landing(Func<object, object> moved, object held)
        {
            return held == null ? null : moved(held);
        }

        private static int MendFaceVertex(IPXPmx model, ISet<object> live)
        {
            int repaired = 0;
            foreach (IPXMaterial material in model.Material)
            {
                for (int at = material.Faces.Count - 1; at >= 0; at--)
                {
                    IPXFace face = material.Faces[at];
                    if (ReferenceCleanup.Alive(face.Vertex1, live)
                        && ReferenceCleanup.Alive(face.Vertex2, live)
                        && ReferenceCleanup.Alive(face.Vertex3, live))
                    {
                        continue;
                    }

                    material.Faces.RemoveAt(at);
                    repaired++;
                }
            }

            return repaired;
        }

        private static int MendMorphVertex(IPXPmx model, ISet<object> live)
        {
            return Dropped(model, offset =>
            {
                IPXVertexMorphOffset moved = offset as IPXVertexMorphOffset;
                if (moved != null)
                {
                    return !ReferenceCleanup.Alive(moved.Vertex, live);
                }

                IPXUVMorphOffset slid = offset as IPXUVMorphOffset;

                return slid != null && !ReferenceCleanup.Alive(slid.Vertex, live);
            });
        }

        private static int MendSoftBodyVertex(IPXPmx model, ISet<object> live)
        {
            int repaired = 0;
            foreach (IPXSoftBody soft in model.SoftBody)
            {
                for (int at = soft.Pins.Count - 1; at >= 0; at--)
                {
                    if (ReferenceCleanup.Alive(soft.Pins[at], live))
                    {
                        continue;
                    }

                    soft.Pins.RemoveAt(at);
                    repaired++;
                }

                repaired += Anchored(soft, anchor => !ReferenceCleanup.Alive(anchor.Vertex, live));
            }

            return repaired;
        }

        private static int MendMorphMaterial(IPXPmx model, ISet<object> live)
        {
            return Dropped(model, offset =>
            {
                IPXMaterialMorphOffset painted = offset as IPXMaterialMorphOffset;

                return painted != null
                    && painted.Material != null
                    && !ReferenceCleanup.Alive(painted.Material, live);
            });
        }

        private static int MendSoftBodyMaterial(IPXPmx model, ISet<object> live)
        {
            int repaired = 0;
            foreach (IPXSoftBody soft in model.SoftBody)
            {
                if (soft.Material == null || ReferenceCleanup.Alive(soft.Material, live))
                {
                    continue;
                }

                soft.Material = null;
                repaired++;
            }

            return repaired;
        }

        private static int MendVertexBone(IPXPmx model, ISet<object> live)
        {
            return ReferenceCleanup.RepairWeights(model, live, model.Vertex);
        }

        private static int MendBoneBone(IPXPmx model, ISet<object> live)
        {
            int repaired = 0;
            foreach (IPXBone bone in model.Bone)
            {
                if (bone.Parent != null && !ReferenceCleanup.Alive(bone.Parent, live))
                {
                    bone.Parent = ReferenceCleanup.LandingBone(bone.Parent, live, null);
                    repaired++;
                }

                if (bone.ToBone != null && !ReferenceCleanup.Alive(bone.ToBone, live))
                {
                    bone.ToBone = null;
                    repaired++;
                }

                if (bone.AppendParent != null && !ReferenceCleanup.Alive(bone.AppendParent, live))
                {
                    bone.AppendParent = null;
                    bone.IsAppendRotation = false;
                    bone.IsAppendTranslation = false;
                    repaired++;
                }

                if (bone.IK == null)
                {
                    continue;
                }

                if (bone.IsIK && !ReferenceCleanup.Alive(bone.IK.Target, live))
                {
                    bone.IsIK = false;
                    bone.IK.Target = null;
                    repaired++;
                }
                else if (bone.IK.Target != null
                    && !ReferenceCleanup.Alive(bone.IK.Target, live))
                {
                    bone.IK.Target = null;
                    repaired++;
                }

                for (int at = bone.IK.Links.Count - 1; at >= 0; at--)
                {
                    if (ReferenceCleanup.Alive(bone.IK.Links[at].Bone, live))
                    {
                        continue;
                    }

                    bone.IK.Links.RemoveAt(at);
                    repaired++;
                }
            }

            return repaired;
        }

        private static int MendMorphBone(IPXPmx model, ISet<object> live)
        {
            return Dropped(model, offset =>
            {
                IPXBoneMorphOffset posed = offset as IPXBoneMorphOffset;

                return posed != null && !ReferenceCleanup.Alive(posed.Bone, live);
            });
        }

        private static int MendNodeBone(IPXPmx model, ISet<object> live)
        {
            return Unlisted(model, item => item.IsBone
                && !ReferenceCleanup.Alive(item.BoneItem.Bone, live));
        }

        private static int MendBodyBone(IPXPmx model, ISet<object> live)
        {
            int repaired = 0;
            foreach (IPXBody body in model.Body)
            {
                if (body.Bone == null || ReferenceCleanup.Alive(body.Bone, live))
                {
                    continue;
                }

                body.Bone = null;
                repaired++;
            }

            return repaired;
        }

        private static int MendMorphMorph(IPXPmx model, ISet<object> live)
        {
            return Dropped(model, offset =>
            {
                IPXGroupMorphOffset grouped = offset as IPXGroupMorphOffset;

                return grouped != null && !ReferenceCleanup.Alive(grouped.Morph, live);
            });
        }

        private static int MendNodeMorph(IPXPmx model, ISet<object> live)
        {
            return Unlisted(model, item => item.IsMorph
                && !ReferenceCleanup.Alive(item.MorphItem.Morph, live));
        }

        private static int MendJointBody(IPXPmx model, ISet<object> live)
        {
            int repaired = 0;
            foreach (IPXJoint joint in model.Joint)
            {
                if (joint.BodyA != null && !ReferenceCleanup.Alive(joint.BodyA, live))
                {
                    joint.BodyA = null;
                    repaired++;
                }

                if (joint.BodyB != null && !ReferenceCleanup.Alive(joint.BodyB, live))
                {
                    joint.BodyB = null;
                    repaired++;
                }
            }

            return repaired;
        }

        private static int MendMorphBody(IPXPmx model, ISet<object> live)
        {
            return Dropped(model, offset =>
            {
                IPXImpulseMorphOffset pushed = offset as IPXImpulseMorphOffset;

                return pushed != null && !ReferenceCleanup.Alive(pushed.Body, live);
            });
        }

        private static int MendSoftBodyBody(IPXPmx model, ISet<object> live)
        {
            int repaired = 0;
            foreach (IPXSoftBody soft in model.SoftBody)
            {
                repaired += Anchored(soft, anchor => !ReferenceCleanup.Alive(anchor.Body, live));
            }

            return repaired;
        }

        private static int Dropped(IPXPmx model, Func<IPXMorphOffset, bool> drop)
        {
            int repaired = 0;
            foreach (IPXMorph morph in model.Morph)
            {
                for (int at = morph.Offsets.Count - 1; at >= 0; at--)
                {
                    if (!drop(morph.Offsets[at]))
                    {
                        continue;
                    }

                    morph.Offsets.RemoveAt(at);
                    repaired++;
                }
            }

            return repaired;
        }

        private static int Unlisted(IPXPmx model, Func<IPXNodeItem, bool> drop)
        {
            int repaired = 0;
            foreach (IPXNode node in ReferenceCleanup.Nodes(model))
            {
                for (int at = node.Items.Count - 1; at >= 0; at--)
                {
                    if (!drop(node.Items[at]))
                    {
                        continue;
                    }

                    node.Items.RemoveAt(at);
                    repaired++;
                }
            }

            return repaired;
        }

        private static int Anchored(IPXSoftBody soft, Func<IPXSoftBodyAnchor, bool> drop)
        {
            int repaired = 0;
            for (int at = soft.Anchors.Count - 1; at >= 0; at--)
            {
                if (!drop(soft.Anchors[at]))
                {
                    continue;
                }

                soft.Anchors.RemoveAt(at);
                repaired++;
            }

            return repaired;
        }
    }
}
