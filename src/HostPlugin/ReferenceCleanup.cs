using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    /// <summary>消した要素を指していた口の始末の仕方。</summary>
    public enum RelatedHandling
    {
        /// <summary>何も触らない。指す先を失った口はそのまま残る。</summary>
        Keep,

        /// <summary>指す先を失った口を直す。ウェイトは残るボーンへ移し、直せない口は落とす。</summary>
        Repair,

        /// <summary>直すことに加えて、消した要素だけが使っていた要素も一緒に消す。</summary>
        Cascade,
    }

    /// <summary>
    /// 並びから消えた要素を指したままの口を片付ける。PMXの要素はIndexではなくオブジェクトの参照で
    /// 繋がるので、並びから外しただけでは他の要素が指したままになる。指す先を失った口は、その口が
    /// 空を取れるなら空にし、取れないなら口を持つ要素ごと落とす。頂点のウェイトだけは落とせない
    /// ので、残っている祖先のボーンへ移し、同じボーンが重なったら重みを足してまとめる。
    /// </summary>
    public static class ReferenceCleanup
    {
        /// <summary>始末の仕方を受け取る入力の名前。</summary>
        public const string RelatedName = "related";

        /// <summary>何も触らない。</summary>
        public const string Keep = "keep";

        /// <summary>指す先を失った口を直す。</summary>
        public const string Repair = "repair";

        /// <summary>直すことに加えて、消した要素だけが使っていた要素も消す。</summary>
        public const string Cascade = "cascade";

        // SDEFは2つのボーンの間を補間するPMXの変形方式である。
        private const int SdefBones = 2;

        /// <summary>受け取れる値。スキーマが並べる順。</summary>
        public static IList<string> Names
        {
            get
            {
                return new ReadOnlyCollection<string>(new[] { Keep, Repair, Cascade });
            }
        }

        /// <summary>入力から始末の仕方を読む。省かれていれば <see cref="RelatedHandling.Repair"/>。</summary>
        public static bool TryResolve(object given, out RelatedHandling handling, out string message)
        {
            handling = RelatedHandling.Repair;
            message = null;
            if (given == null)
            {
                return true;
            }

            switch (given as string)
            {
                case Keep:
                    handling = RelatedHandling.Keep;

                    return true;

                case Repair:
                    handling = RelatedHandling.Repair;

                    return true;

                case Cascade:
                    handling = RelatedHandling.Cascade;

                    return true;

                default:
                    message = RelatedName + " は次のどれかでなければならない: "
                        + string.Join("・", Names.ToArray());

                    return false;
            }
        }

        /// <summary>
        /// 指した要素を消したとき、それだけが使っていたために一緒に消える要素を、種類の名前ごとに
        /// 集める。消す前のPMXを渡す。連なって不要になる要素も含み、指した要素そのものは返さない。
        /// </summary>
        public static IDictionary<string, IList<object>> Following(
            object pmx, ElementKind kind, ICollection<object> removed)
        {
            if (pmx == null)
            {
                throw new ArgumentNullException(nameof(pmx));
            }

            if (kind == null)
            {
                throw new ArgumentNullException(nameof(kind));
            }

            if (removed == null)
            {
                throw new ArgumentNullException(nameof(removed));
            }

            IPXPmx model = (IPXPmx)pmx;
            HashSet<object> seed = Held(removed);
            HashSet<object> materials = Seeded(kind, ElementKinds.Material, seed);
            HashSet<object> vertices = Seeded(kind, ElementKinds.Vertex, seed);
            HashSet<object> morphs = Seeded(kind, ElementKinds.Morph, seed);
            HashSet<object> bodies = Seeded(kind, ElementKinds.Body, seed);
            HashSet<object> joints = Held(new object[0]);

            for (bool grew = true; grew;)
            {
                grew = Grew(vertices, VerticesOnlyUsedBy(model, materials))
                    | Grew(materials, MaterialsLosingEveryFace(model, vertices))
                    | Grew(morphs, MorphsLosingEveryOffset(model, materials, vertices, morphs, bodies))
                    | Grew(joints, JointsAttachedTo(model, bodies));
            }

            Dictionary<string, IList<object>> following =
                new Dictionary<string, IList<object>>(StringComparer.Ordinal);
            Add(following, ElementKinds.Vertex, model.Vertex.Cast<object>(), Apart(vertices, seed));
            Add(following, ElementKinds.Material, model.Material.Cast<object>(), Apart(materials, seed));
            Add(following, ElementKinds.Morph, model.Morph.Cast<object>(), Apart(morphs, seed));
            Add(following, ElementKinds.Joint, model.Joint.Cast<object>(), Apart(joints, seed));

            return following;
        }

        /// <summary>3つの頂点が揃っているか。揃っていない面はPMXの面として意味を持たない。</summary>
        public static bool IsSoundFace(IPXFace face)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return face.Vertex1 != null
                && face.Vertex2 != null
                && face.Vertex3 != null
                && !ReferenceEquals(face.Vertex1, face.Vertex2)
                && !ReferenceEquals(face.Vertex2, face.Vertex3)
                && !ReferenceEquals(face.Vertex3, face.Vertex1);
        }

        /// <summary>3つの頂点が揃っていない面を落とす。落とした数を返す。</summary>
        public static int DropUnsoundFaces(object pmx)
        {
            if (pmx == null)
            {
                throw new ArgumentNullException(nameof(pmx));
            }

            int dropped = 0;
            foreach (IPXMaterial material in ((IPXPmx)pmx).Material)
            {
                for (int at = material.Faces.Count - 1; at >= 0; at--)
                {
                    if (IsSoundFace(material.Faces[at]))
                    {
                        continue;
                    }

                    material.Faces.RemoveAt(at);
                    dropped++;
                }
            }

            return dropped;
        }

        /// <summary>
        /// 頂点を指したままの口を、別の頂点へ付け替える。表に無い頂点を指す口はそのままにする。
        /// </summary>
        public static void Repoint(object pmx, IDictionary<IPXVertex, IPXVertex> moved)
        {
            if (pmx == null)
            {
                throw new ArgumentNullException(nameof(pmx));
            }

            if (moved == null)
            {
                throw new ArgumentNullException(nameof(moved));
            }

            IPXPmx model = (IPXPmx)pmx;
            foreach (IPXMaterial material in model.Material)
            {
                foreach (IPXFace face in material.Faces)
                {
                    face.Vertex1 = Moved(moved, face.Vertex1);
                    face.Vertex2 = Moved(moved, face.Vertex2);
                    face.Vertex3 = Moved(moved, face.Vertex3);
                }
            }

            foreach (IPXMorph morph in model.Morph)
            {
                foreach (IPXMorphOffset offset in morph.Offsets)
                {
                    IPXVertexMorphOffset shifted = offset as IPXVertexMorphOffset;
                    if (shifted != null)
                    {
                        shifted.Vertex = Moved(moved, shifted.Vertex);
                    }

                    IPXUVMorphOffset slid = offset as IPXUVMorphOffset;
                    if (slid != null)
                    {
                        slid.Vertex = Moved(moved, slid.Vertex);
                    }
                }
            }

            foreach (IPXSoftBody soft in model.SoftBody)
            {
                for (int at = 0; at < soft.Pins.Count; at++)
                {
                    soft.Pins[at] = Moved(moved, soft.Pins[at]);
                }

                foreach (IPXSoftBodyAnchor anchor in soft.Anchors)
                {
                    anchor.Vertex = Moved(moved, anchor.Vertex);
                }
            }
        }

        /// <summary>
        /// 材質を指したままの口を、別の材質へ付け替える。表に無い材質を指す口はそのままにする。
        /// </summary>
        public static void Repoint(object pmx, IDictionary<IPXMaterial, IPXMaterial> moved)
        {
            if (pmx == null)
            {
                throw new ArgumentNullException(nameof(pmx));
            }

            if (moved == null)
            {
                throw new ArgumentNullException(nameof(moved));
            }

            IPXPmx model = (IPXPmx)pmx;
            foreach (IPXMorph morph in model.Morph)
            {
                foreach (IPXMorphOffset offset in morph.Offsets)
                {
                    IPXMaterialMorphOffset painted = offset as IPXMaterialMorphOffset;
                    if (painted != null)
                    {
                        painted.Material = Moved(moved, painted.Material);
                    }
                }
            }

            foreach (IPXSoftBody soft in model.SoftBody)
            {
                soft.Material = Moved(moved, soft.Material);
            }
        }

        private static T Moved<T>(IDictionary<T, T> moved, T held)
            where T : class
        {
            T found;

            return held != null && moved.TryGetValue(held, out found) ? found : held;
        }

        /// <summary>
        /// PMX全体を見て、並びに居ない要素を指したままの口を直す。直した口の数を返す。
        /// </summary>
        public static int Sweep(object pmx)
        {
            if (pmx == null)
            {
                throw new ArgumentNullException(nameof(pmx));
            }

            IPXPmx model = (IPXPmx)pmx;
            HashSet<object> vertices = Held(model.Vertex.Cast<object>());
            HashSet<object> materials = Held(model.Material.Cast<object>());
            HashSet<object> bones = Held(model.Bone.Cast<object>());
            HashSet<object> morphs = Held(model.Morph.Cast<object>());
            HashSet<object> bodies = Held(model.Body.Cast<object>());

            int repaired = SweepFaces(model, vertices);
            repaired += SweepWeights(model, bones);
            repaired += SweepBones(model, bones);
            repaired += SweepMorphs(model, vertices, materials, bones, morphs, bodies);
            repaired += SweepNodes(model, bones, morphs);
            repaired += SweepPhysics(model, vertices, materials, bones, bodies);

            return repaired;
        }

        private static int SweepFaces(IPXPmx model, HashSet<object> vertices)
        {
            int repaired = 0;
            foreach (IPXMaterial material in model.Material)
            {
                for (int at = material.Faces.Count - 1; at >= 0; at--)
                {
                    IPXFace face = material.Faces[at];
                    if (Alive(face.Vertex1, vertices)
                        && Alive(face.Vertex2, vertices)
                        && Alive(face.Vertex3, vertices))
                    {
                        continue;
                    }

                    material.Faces.RemoveAt(at);
                    repaired++;
                }
            }

            return repaired;
        }

        private static int SweepWeights(IPXPmx model, HashSet<object> bones)
        {
            int repaired = 0;
            IPXBone fallback = model.Bone.Count == 0 ? null : model.Bone[0];
            foreach (IPXVertex vertex in model.Vertex)
            {
                IPXBone[] held = { vertex.Bone1, vertex.Bone2, vertex.Bone3, vertex.Bone4 };
                float[] weights = { vertex.Weight1, vertex.Weight2, vertex.Weight3, vertex.Weight4 };
                if (!held.Any(bone => bone != null && !bones.Contains(bone)))
                {
                    continue;
                }

                List<IPXBone> kept = new List<IPXBone>();
                List<float> shares = new List<float>();
                for (int at = 0; at < held.Length; at++)
                {
                    IPXBone bone = LandingBone(held[at], bones, fallback);
                    if (bone == null)
                    {
                        continue;
                    }

                    int found = kept.FindIndex(b => ReferenceEquals(b, bone));
                    if (found < 0)
                    {
                        kept.Add(bone);
                        shares.Add(weights[at]);
                    }
                    else
                    {
                        shares[found] += weights[at];
                    }
                }

                Write(vertex, kept, shares);
                if (kept.Count < SdefBones)
                {
                    vertex.SDEF = false;
                }

                if (kept.Count == 0)
                {
                    vertex.QDEF = false;
                }

                repaired++;
            }

            return repaired;
        }

        private static IPXBone LandingBone(IPXBone bone, HashSet<object> bones, IPXBone fallback)
        {
            if (bone == null || bones.Contains(bone))
            {
                return bone;
            }

            for (IPXBone above = bone.Parent; above != null; above = above.Parent)
            {
                if (bones.Contains(above))
                {
                    return above;
                }
            }

            return fallback;
        }

        private static void Write(IPXVertex vertex, IList<IPXBone> bones, IList<float> weights)
        {
            vertex.Bone1 = At(bones, 0);
            vertex.Bone2 = At(bones, 1);
            vertex.Bone3 = At(bones, 2);
            vertex.Bone4 = At(bones, 3);
            vertex.Weight1 = Share(weights, 0);
            vertex.Weight2 = Share(weights, 1);
            vertex.Weight3 = Share(weights, 2);
            vertex.Weight4 = Share(weights, 3);
        }

        private static IPXBone At(IList<IPXBone> bones, int at)
        {
            return at < bones.Count ? bones[at] : null;
        }

        private static float Share(IList<float> weights, int at)
        {
            return at < weights.Count ? weights[at] : 0f;
        }

        private static int SweepBones(IPXPmx model, HashSet<object> bones)
        {
            int repaired = 0;
            foreach (IPXBone bone in model.Bone)
            {
                if (bone.Parent != null && !bones.Contains(bone.Parent))
                {
                    bone.Parent = LandingBone(bone.Parent, bones, null);
                    repaired++;
                }

                if (bone.ToBone != null && !bones.Contains(bone.ToBone))
                {
                    bone.ToBone = null;
                    repaired++;
                }

                if (bone.AppendParent != null && !bones.Contains(bone.AppendParent))
                {
                    bone.AppendParent = null;
                    bone.IsAppendRotation = false;
                    bone.IsAppendTranslation = false;
                    repaired++;
                }

                if (bone.IsIK && !Alive(bone.IK.Target, bones))
                {
                    bone.IsIK = false;
                    repaired++;
                }

                for (int at = bone.IK.Links.Count - 1; at >= 0; at--)
                {
                    if (Alive(bone.IK.Links[at].Bone, bones))
                    {
                        continue;
                    }

                    bone.IK.Links.RemoveAt(at);
                    repaired++;
                }
            }

            return repaired;
        }

        private static int SweepMorphs(
            IPXPmx model,
            HashSet<object> vertices,
            HashSet<object> materials,
            HashSet<object> bones,
            HashSet<object> morphs,
            HashSet<object> bodies)
        {
            int repaired = 0;
            foreach (IPXMorph morph in model.Morph)
            {
                for (int at = morph.Offsets.Count - 1; at >= 0; at--)
                {
                    if (PointsAtLive(morph.Offsets[at], vertices, materials, bones, morphs, bodies))
                    {
                        continue;
                    }

                    morph.Offsets.RemoveAt(at);
                    repaired++;
                }
            }

            return repaired;
        }

        private static bool PointsAtLive(
            IPXMorphOffset offset,
            HashSet<object> vertices,
            HashSet<object> materials,
            HashSet<object> bones,
            HashSet<object> morphs,
            HashSet<object> bodies)
        {
            IPXVertexMorphOffset moved = offset as IPXVertexMorphOffset;
            if (moved != null)
            {
                return Alive(moved.Vertex, vertices);
            }

            IPXUVMorphOffset slid = offset as IPXUVMorphOffset;
            if (slid != null)
            {
                return Alive(slid.Vertex, vertices);
            }

            IPXBoneMorphOffset posed = offset as IPXBoneMorphOffset;
            if (posed != null)
            {
                return Alive(posed.Bone, bones);
            }

            IPXGroupMorphOffset grouped = offset as IPXGroupMorphOffset;
            if (grouped != null)
            {
                return Alive(grouped.Morph, morphs);
            }

            IPXImpulseMorphOffset pushed = offset as IPXImpulseMorphOffset;
            if (pushed != null)
            {
                return Alive(pushed.Body, bodies);
            }

            IPXMaterialMorphOffset painted = offset as IPXMaterialMorphOffset;

            // 材質を指さない材質モーフのオフセットは全材質を指すので、空のままでよい。
            return painted == null || painted.Material == null
                || materials.Contains(painted.Material);
        }

        private static int SweepNodes(
            IPXPmx model, HashSet<object> bones, HashSet<object> morphs)
        {
            int repaired = 0;
            foreach (IPXNode node in EveryNode(model))
            {
                for (int at = node.Items.Count - 1; at >= 0; at--)
                {
                    IPXNodeItem item = node.Items[at];
                    bool held = item.IsBone
                        ? Alive(item.BoneItem.Bone, bones)
                        : item.IsMorph && Alive(item.MorphItem.Morph, morphs);
                    if (held)
                    {
                        continue;
                    }

                    node.Items.RemoveAt(at);
                    repaired++;
                }
            }

            return repaired;
        }

        private static IEnumerable<IPXNode> EveryNode(IPXPmx model)
        {
            if (model.RootNode != null)
            {
                yield return model.RootNode;
            }

            if (model.ExpressionNode != null)
            {
                yield return model.ExpressionNode;
            }

            foreach (IPXNode node in model.Node)
            {
                yield return node;
            }
        }

        private static int SweepPhysics(
            IPXPmx model,
            HashSet<object> vertices,
            HashSet<object> materials,
            HashSet<object> bones,
            HashSet<object> bodies)
        {
            int repaired = 0;
            foreach (IPXBody body in model.Body)
            {
                if (body.Bone == null || bones.Contains(body.Bone))
                {
                    continue;
                }

                body.Bone = null;
                repaired++;
            }

            foreach (IPXJoint joint in model.Joint)
            {
                if (joint.BodyA != null && !bodies.Contains(joint.BodyA))
                {
                    joint.BodyA = null;
                    repaired++;
                }

                if (joint.BodyB != null && !bodies.Contains(joint.BodyB))
                {
                    joint.BodyB = null;
                    repaired++;
                }
            }

            foreach (IPXSoftBody soft in model.SoftBody)
            {
                if (soft.Material != null && !materials.Contains(soft.Material))
                {
                    soft.Material = null;
                    repaired++;
                }

                for (int at = soft.Pins.Count - 1; at >= 0; at--)
                {
                    if (Alive(soft.Pins[at], vertices))
                    {
                        continue;
                    }

                    soft.Pins.RemoveAt(at);
                    repaired++;
                }

                for (int at = soft.Anchors.Count - 1; at >= 0; at--)
                {
                    IPXSoftBodyAnchor anchor = soft.Anchors[at];
                    if (Alive(anchor.Body, bodies) && Alive(anchor.Vertex, vertices))
                    {
                        continue;
                    }

                    soft.Anchors.RemoveAt(at);
                    repaired++;
                }
            }

            return repaired;
        }

        private static HashSet<object> Seeded(
            ElementKind kind, string name, HashSet<object> seed)
        {
            return string.Equals(kind.Name, name, StringComparison.Ordinal)
                ? Held(seed)
                : Held(new object[0]);
        }

        private static bool Grew(HashSet<object> held, IEnumerable<object> found)
        {
            bool grew = false;
            foreach (object item in found)
            {
                grew |= held.Add(item);
            }

            return grew;
        }

        private static IList<object> Apart(HashSet<object> held, HashSet<object> seed)
        {
            return held.Where(item => !seed.Contains(item)).ToList();
        }

        private static IEnumerable<object> VerticesOnlyUsedBy(
            IPXPmx model, HashSet<object> materials)
        {
            HashSet<object> elsewhere = Held(new object[0]);
            HashSet<object> here = Held(new object[0]);
            foreach (IPXMaterial material in model.Material)
            {
                HashSet<object> side = materials.Contains(material) ? here : elsewhere;
                foreach (IPXFace face in material.Faces)
                {
                    side.Add(face.Vertex1);
                    side.Add(face.Vertex2);
                    side.Add(face.Vertex3);
                }
            }

            return model.Vertex
                .Cast<object>()
                .Where(v => here.Contains(v) && !elsewhere.Contains(v));
        }

        private static IEnumerable<object> MaterialsLosingEveryFace(
            IPXPmx model, HashSet<object> vertices)
        {
            return model.Material
                .Where(m => m.Faces.Count > 0
                    && m.Faces.All(f => vertices.Contains(f.Vertex1)
                        || vertices.Contains(f.Vertex2)
                        || vertices.Contains(f.Vertex3)))
                .Cast<object>();
        }

        private static IEnumerable<object> MorphsLosingEveryOffset(
            IPXPmx model,
            HashSet<object> materials,
            HashSet<object> vertices,
            HashSet<object> morphs,
            HashSet<object> bodies)
        {
            HashSet<object> liveVertices = Without(model.Vertex.Cast<object>(), vertices);
            HashSet<object> liveMaterials = Without(model.Material.Cast<object>(), materials);
            HashSet<object> liveMorphs = Without(model.Morph.Cast<object>(), morphs);
            HashSet<object> liveBodies = Without(model.Body.Cast<object>(), bodies);
            HashSet<object> bones = Held(model.Bone.Cast<object>());

            return model.Morph
                .Where(m => m.Offsets.Count > 0
                    && m.Offsets.All(o => !PointsAtLive(
                        o, liveVertices, liveMaterials, bones, liveMorphs, liveBodies)))
                .Cast<object>();
        }

        private static IEnumerable<object> JointsAttachedTo(IPXPmx model, HashSet<object> bodies)
        {
            return model.Joint
                .Where(j => bodies.Contains(j.BodyA) || bodies.Contains(j.BodyB))
                .Cast<object>();
        }

        private static HashSet<object> Without(
            IEnumerable<object> all, HashSet<object> going)
        {
            return Held(all.Where(item => !going.Contains(item)));
        }

        private static void Add(
            IDictionary<string, IList<object>> following,
            string name,
            IEnumerable<object> all,
            IList<object> chosen)
        {
            HashSet<object> held = Held(chosen);
            IList<object> taken = all.Where(held.Contains).ToList();
            if (taken.Count > 0)
            {
                following.Add(name, taken);
            }
        }

        private static bool Alive(object item, HashSet<object> live)
        {
            return item != null && live.Contains(item);
        }

        private static HashSet<object> Held(IEnumerable<object> items)
        {
            HashSet<object> held = new HashSet<object>(ReferenceComparer<object>.Instance);
            foreach (object item in items)
            {
                if (item != null)
                {
                    held.Add(item);
                }
            }

            return held;
        }
    }
}
