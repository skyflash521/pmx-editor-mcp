using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    /// <summary>状態検証の1項目。<see cref="Positions"/> は当たった要素の位置で、位置を持たない項目では null。</summary>
    public sealed class StateItem
    {
        public StateItem(string name, int count, IList<int> positions)
        {
            Name = name;
            Count = count;
            Positions = positions;
        }

        public string Name { get; }

        public int Count { get; }

        public IList<int> Positions { get; }
    }

    /// <summary>
    /// エディタの「PMXデータの状態検証」と同じ項目を同じ判定で数える。エディタが範囲外の番号として
    /// 数える参照は、null か並びに居ない要素への参照として数え、エディタが -1 を許す参照では null を
    /// 許す。エディタの範囲外の番号は、SDKの複製では null として見える。
    /// </summary>
    public static class PmxStateCheck
    {
        public const string BadUvaCountName = "badUvaCount";

        public const string EmptyModelNameName = "emptyModelName";

        public const string EmptyCommentName = "emptyComment";

        public const string InvalidNormalsName = "invalidNormals";

        public const string InvalidWeightsName = "invalidWeights";

        public const string EmptySdefValuesName = "emptySdefValues";

        public const string SdefBadBoneReferencesName = "sdefBadBoneReferences";

        public const string UnusedVerticesName = "unusedVertices";

        public const string BadFaceVertexReferencesName = "badFaceVertexReferences";

        public const string BadFacesName = "badFaces";

        public const string DuplicateFacesName = "duplicateFaces";

        public const string UnnamedMaterialsName = "unnamedMaterials";

        public const string DuplicateMaterialNamesName = "duplicateMaterialNames";

        public const string MaterialsWithoutFacesName = "materialsWithoutFaces";

        public const string MissingTexturesName = "missingTextures";

        public const string MissingSphereTexturesName = "missingSphereTextures";

        public const string SphereModeNoneName = "sphereModeNone";

        public const string MissingToonTexturesName = "missingToonTextures";

        public const string NoBonesName = "noBones";

        public const string UnnamedBonesName = "unnamedBones";

        public const string DuplicateBoneNamesName = "duplicateBoneNames";

        public const string CyclicBonesName = "cyclicBones";

        public const string BonesDeformedBeforeParentName = "bonesDeformedBeforeParent";

        public const string BadParentBonesName = "badParentBones";

        public const string BonesDeformedBeforeAppendParentName = "bonesDeformedBeforeAppendParent";

        public const string BadAppendParentBonesName = "badAppendParentBones";

        public const string BadToBonesName = "badToBones";

        public const string MissingAppendParentBonesName = "missingAppendParentBones";

        public const string BadIkTargetsName = "badIkTargets";

        public const string BadIkLinksName = "badIkLinks";

        public const string BonesNotInFrameName = "bonesNotInFrame";

        public const string BonesInFramesTwiceName = "bonesInFramesTwice";

        public const string UnnamedMorphsName = "unnamedMorphs";

        public const string DuplicateMorphNamesName = "duplicateMorphNames";

        public const string BadMorphOffsetsName = "badMorphOffsets";

        public const string MorphsNotInFrameName = "morphsNotInFrame";

        public const string MorphsInFramesTwiceName = "morphsInFramesTwice";

        public const string UnnamedFramesName = "unnamedFrames";

        public const string DuplicateFrameNamesName = "duplicateFrameNames";

        public const string FramesWithBadBonesName = "framesWithBadBones";

        public const string FramesWithBadMorphsName = "framesWithBadMorphs";

        public const string BoneInExpressionFrameName = "boneInExpressionFrame";

        public const string MissingSystemFramesName = "missingSystemFrames";

        public const string UnnamedBodiesName = "unnamedBodies";

        public const string DuplicateBodyNamesName = "duplicateBodyNames";

        public const string BodiesWithBadBoneName = "bodiesWithBadBone";

        public const string UnnamedJointsName = "unnamedJoints";

        public const string DuplicateJointNamesName = "duplicateJointNames";

        public const string JointsWithBadBodyAName = "jointsWithBadBodyA";

        public const string JointsWithBadBodyBName = "jointsWithBadBodyB";

        public const string UnnamedSoftBodiesName = "unnamedSoftBodies";

        public const string DuplicateSoftBodyNamesName = "duplicateSoftBodyNames";

        public const string SoftBodiesWithBadMaterialName = "softBodiesWithBadMaterial";

        public const string SoftBodiesWithBadBodyName = "softBodiesWithBadBody";

        public const string SoftBodiesWithBadVertexName = "softBodiesWithBadVertex";

        public const string SoftBodiesPinnedOnAnchorName = "softBodiesPinnedOnAnchor";

        public const string SoftBodiesOutsideMaterialName = "softBodiesOutsideMaterial";

        private const int MaxUvaCount = 4;

        private const int SystemToonCount = 10;

        /// <summary>位置を持つ項目の名前。エディタの状態検証が並べる順。</summary>
        public static IList<string> Locatable
        {
            get
            {
                return new[]
                {
                    InvalidNormalsName, InvalidWeightsName, EmptySdefValuesName, SdefBadBoneReferencesName,
                    UnusedVerticesName, BadFaceVertexReferencesName, BadFacesName, DuplicateFacesName,
                    UnnamedMaterialsName, DuplicateMaterialNamesName, MaterialsWithoutFacesName,
                    MissingTexturesName, MissingSphereTexturesName, SphereModeNoneName, MissingToonTexturesName,
                    UnnamedBonesName, DuplicateBoneNamesName, CyclicBonesName, BonesDeformedBeforeParentName,
                    BadParentBonesName, BonesDeformedBeforeAppendParentName, BadAppendParentBonesName,
                    BadToBonesName, MissingAppendParentBonesName, BadIkTargetsName, BadIkLinksName,
                    BonesNotInFrameName, BonesInFramesTwiceName, UnnamedMorphsName, DuplicateMorphNamesName,
                    BadMorphOffsetsName, MorphsNotInFrameName, MorphsInFramesTwiceName, UnnamedFramesName,
                    DuplicateFrameNamesName, FramesWithBadBonesName, FramesWithBadMorphsName,
                    UnnamedBodiesName, DuplicateBodyNamesName, BodiesWithBadBoneName, UnnamedJointsName,
                    DuplicateJointNamesName, JointsWithBadBodyAName, JointsWithBadBodyBName,
                    UnnamedSoftBodiesName, DuplicateSoftBodyNamesName, SoftBodiesWithBadMaterialName,
                    SoftBodiesWithBadBodyName, SoftBodiesWithBadVertexName, SoftBodiesPinnedOnAnchorName,
                    SoftBodiesOutsideMaterialName,
                };
            }
        }

        /// <summary>
        /// 全項目を、エディタの状態検証が並べる順に返す。数はエディタが表示する数と同じで、要素を
        /// 二度以上数える項目では <see cref="StateItem.Positions"/> の件数と違うことがある。
        /// </summary>
        public static IList<StateItem> Of(IPXPmx model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            List<StateItem> items = new List<StateItem>();
            Places places = new Places(model);
            Flag(items, BadUvaCountName, model.Header.UVACount < 0 || MaxUvaCount < model.Header.UVACount);
            Flag(items, EmptyModelNameName, string.IsNullOrEmpty(model.ModelInfo.ModelName));
            Flag(items, EmptyCommentName, string.IsNullOrEmpty(model.ModelInfo.Comment));
            Vertices(items, model, places);
            Faces(items, model, places);
            Materials(items, model, places);
            FrameScan scan = new FrameScan(model, places);
            Bones(items, model, places, scan);
            Morphs(items, model, places, scan);
            Frames(items, model, places, scan);
            Bodies(items, model, places);
            Joints(items, model, places);
            SoftBodies(items, model, places);

            return items;
        }

        private static void Vertices(List<StateItem> items, IPXPmx model, Places places)
        {
            List<int> normals = new List<int>();
            List<int> weights = new List<int>();
            List<int> empty = new List<int>();
            List<int> sdefBones = new List<int>();
            for (int at = 0; at < model.Vertex.Count; at++)
            {
                IPXVertex vertex = model.Vertex[at];
                if (IsInvalidNormal(vertex))
                {
                    normals.Add(at);
                }

                IPXBone[] bones = { vertex.Bone1, vertex.Bone2, vertex.Bone3, vertex.Bone4 };
                Deform deform = DeformOf(vertex);
                if (bones.Take(SlotsChecked(deform)).Any(bone => !places.Has(places.Bones, bone)))
                {
                    weights.Add(at);
                }

                if (deform != Deform.Sdef)
                {
                    continue;
                }

                if (Zero(vertex.SDEF_C) && Zero(vertex.SDEF_R0) && Zero(vertex.SDEF_R1))
                {
                    empty.Add(at);
                }

                if (!places.Has(places.Bones, vertex.Bone1) || !places.Has(places.Bones, vertex.Bone2))
                {
                    sdefBones.Add(at);
                }
            }

            HashSet<IPXVertex> used2 = new HashSet<IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXFace face in places.Faces)
            {
                Use(used2, face.Vertex1);
                Use(used2, face.Vertex2);
                Use(used2, face.Vertex3);
            }

            List<int> unused = Enumerable.Range(0, model.Vertex.Count)
                .Where(at => !used2.Contains(model.Vertex[at]))
                .ToList();
            Listed(items, InvalidNormalsName, normals);
            Listed(items, InvalidWeightsName, weights);
            Listed(items, EmptySdefValuesName, empty);
            Listed(items, SdefBadBoneReferencesName, sdefBones);
            Listed(items, UnusedVerticesName, unused);
        }

        /// <summary>エディタの変形方式。0でない重みが1つなら、SDEF と QDEF の印があっても BDEF1 になる。</summary>
        private static Deform DeformOf(IPXVertex vertex)
        {
            int weighted = new[] { vertex.Weight1, vertex.Weight2, vertex.Weight3, vertex.Weight4 }
                .Count(weight => weight != 0f);
            if (vertex.SDEF && weighted != 1)
            {
                return Deform.Sdef;
            }

            if (vertex.QDEF && weighted != 1)
            {
                return Deform.Qdef;
            }

            return weighted <= 1 ? Deform.Bdef1 : weighted == 2 ? Deform.Bdef2 : Deform.Bdef4;
        }

        /// <summary>参照を確かめる枠の数。エディタは QDEF の頂点の参照を確かめない。</summary>
        private static int SlotsChecked(Deform deform)
        {
            switch (deform)
            {
                case Deform.Bdef1:
                    return 1;

                case Deform.Bdef2:
                case Deform.Sdef:
                    return 2;

                case Deform.Bdef4:
                    return 4;

                default:
                    return 0;
            }
        }

        private static bool IsInvalidNormal(IPXVertex vertex)
        {
            float x = vertex.Normal.X;
            float y = vertex.Normal.Y;
            float z = vertex.Normal.Z;

            return (x == 0f && y == 0f && z == 0f)
                || float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z)
                || float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z);
        }

        private static void Faces(List<StateItem> items, IPXPmx model, Places places)
        {
            int badReferences = 0;
            List<int> referring = new List<int>();
            List<int> bad = new List<int>();
            List<int> doubled = new List<int>();
            HashSet<string> met = new HashSet<string>(StringComparer.Ordinal);
            int at = 0;
            foreach (IPXMaterial material in model.Material)
            {
                bool drawsFaces = !IsOnly(material, PrimitiveType.Line) && !IsOnly(material, PrimitiveType.Point);
                foreach (IPXFace face in material.Faces)
                {
                    int[] corners = { places.Of(places.Vertices, face.Vertex1), places.Of(places.Vertices, face.Vertex2), places.Of(places.Vertices, face.Vertex3) };
                    int outside = corners.Count(corner => corner < 0);
                    badReferences += outside;
                    if (outside > 0)
                    {
                        referring.Add(at);
                    }

                    if (outside > 0
                        || (drawsFaces
                            && (corners[0] == corners[1] || corners[0] == corners[2] || corners[1] == corners[2])))
                    {
                        bad.Add(at);
                    }

                    if (!met.Add(string.Join(",", corners)))
                    {
                        doubled.Add(at);
                    }

                    at++;
                }
            }

            items.Add(new StateItem(BadFaceVertexReferencesName, badReferences, referring));
            Listed(items, BadFacesName, bad);
            Listed(items, DuplicateFacesName, doubled);
        }

        /// <summary>
        /// 材質の描画の印が、その描き方の印1つだけか。エディタは印の組がちょうどその1つのときだけ、面の
        /// 頂点の重なりを不正に数えない。
        /// </summary>
        private static bool IsOnly(IPXMaterial material, PrimitiveType type)
        {
            return material.PrimitiveType == type
                && !material.BothDraw
                && !material.Shadow
                && !material.SelfShadowMap
                && !material.SelfShadow
                && !material.Edge
                && !material.VertexColor;
        }

        private static void Materials(List<StateItem> items, IPXPmx model, Places places)
        {
            IList<IPXMaterial> materials = model.Material;
            string folder = Folder(model.FilePath);
            List<int> texture = new List<int>();
            List<int> sphere = new List<int>();
            List<int> sphereMode = new List<int>();
            List<int> toon = new List<int>();
            for (int at = 0; at < materials.Count; at++)
            {
                IPXMaterial material = materials[at];
                if (!string.IsNullOrEmpty(material.Tex) && !File.Exists(folder + material.Tex))
                {
                    texture.Add(at);
                }

                if (!string.IsNullOrEmpty(material.Sphere))
                {
                    if (!File.Exists(folder + material.Sphere))
                    {
                        sphere.Add(at);
                    }

                    if (material.SphereMode == SphereType.None)
                    {
                        sphereMode.Add(at);
                    }
                }

                if (!string.IsNullOrEmpty(material.Toon)
                    && !IsSystemToon(material.Toon)
                    && !File.Exists(folder + material.Toon))
                {
                    toon.Add(at);
                }
            }

            Named(items, UnnamedMaterialsName, DuplicateMaterialNamesName, materials.Select(m => m.Name).ToList());
            Listed(items, MaterialsWithoutFacesName, Where(materials, material => material.Faces.Count <= 0));
            Listed(items, MissingTexturesName, texture);
            Listed(items, MissingSphereTexturesName, sphere);
            Listed(items, SphereModeNoneName, sphereMode);
            Listed(items, MissingToonTexturesName, toon);
        }

        private static string Folder(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetDirectoryName(filePath) + "\\";
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }

        private static bool IsSystemToon(string name)
        {
            if (string.Equals(name, "toon0.bmp", StringComparison.Ordinal))
            {
                return true;
            }

            for (int at = 1; at <= SystemToonCount; at++)
            {
                if (string.Equals(name, "toon" + at.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ".bmp", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Bones(List<StateItem> items, IPXPmx model, Places places, FrameScan scan)
        {
            IList<IPXBone> bones = model.Bone;
            List<int> early = new List<int>();
            List<int> badParent = new List<int>();
            List<int> appendEarly = new List<int>();
            List<int> badAppend = new List<int>();
            List<int> badTo = new List<int>();
            List<int> missingAppend = new List<int>();
            List<int> badTarget = new List<int>();
            List<int> badLink = new List<int>();
            for (int at = 0; at < bones.Count; at++)
            {
                IPXBone bone = bones[at];
                Ordered(bone, at, bone.Parent, places, badParent, early);
                Ordered(bone, at, bone.AppendParent, places, badAppend, appendEarly);
                if (bone.ToBone != null && !places.Has(places.Bones, bone.ToBone))
                {
                    badTo.Add(at);
                }

                if ((bone.IsAppendRotation || bone.IsAppendTranslation) && !places.Has(places.Bones, bone.AppendParent))
                {
                    missingAppend.Add(at);
                }

                if (!bone.IsIK || bone.IK == null)
                {
                    continue;
                }

                if (!places.Has(places.Bones, bone.IK.Target))
                {
                    badTarget.Add(at);
                }

                if (bone.IK.Links.Any(link => !places.Has(places.Bones, link.Bone)))
                {
                    badLink.Add(at);
                }
            }

            Flag(items, NoBonesName, bones.Count == 0);
            Named(items, UnnamedBonesName, DuplicateBoneNamesName, bones.Select(b => b.Name).ToList());
            Listed(items, CyclicBonesName, Cyclic(bones, places));
            Listed(items, BonesDeformedBeforeParentName, early);
            Listed(items, BadParentBonesName, badParent);
            Listed(items, BonesDeformedBeforeAppendParentName, appendEarly);
            Listed(items, BadAppendParentBonesName, badAppend);
            Listed(items, BadToBonesName, badTo);
            Listed(items, MissingAppendParentBonesName, missingAppend);
            Listed(items, BadIkTargetsName, badTarget);
            Listed(items, BadIkLinksName, badLink);
            Listed(items, BonesNotInFrameName, Enumerable.Range(0, bones.Count)
                .Where(at => bones[at].Visible && !scan.BoneRegistered.Contains(at))
                .ToList());
            items.Add(new StateItem(BonesInFramesTwiceName, scan.BonesTwice.Count, Distinct(scan.BonesTwice)));
        }

        /// <summary>
        /// 親か付与親の参照を確かめる。並びに居ないか自分を指すなら <paramref name="bad"/> へ、居れば
        /// 親より先に変形するときに <paramref name="early"/> へ入れる。エディタは物理後でないボーンを
        /// 物理後のボーンより先に、同じ側では変形階層の小さい順に、同じ変形階層では並びの順に変形する。
        /// </summary>
        private static void Ordered(
            IPXBone bone, int at, IPXBone referenced, Places places, List<int> bad, List<int> early)
        {
            if (referenced == null)
            {
                return;
            }

            int place = places.Of(places.Bones, referenced);
            if (place < 0 || place == at)
            {
                bad.Add(at);

                return;
            }

            if (referenced.IsAfterPhysics && !bone.IsAfterPhysics)
            {
                early.Add(at);
            }
            else if (referenced.IsAfterPhysics == bone.IsAfterPhysics
                && (referenced.Level > bone.Level || (referenced.Level == bone.Level && place > at)))
            {
                early.Add(at);
            }
        }

        /// <summary>親を辿ると自分へ戻るボーン。エディタと同じく、辿る途中で既に数えたボーンに当たったら止める。</summary>
        private static IList<int> Cyclic(IList<IPXBone> bones, Places places)
        {
            int count = bones.Count;
            int[] parents = bones.Select(bone => bone.Parent == null
                ? -1
                : places.Bones.TryGetValue(bone.Parent, out int found) ? found : count).ToArray();
            List<int> cyclic = new List<int>();
            HashSet<int> seen = new HashSet<int>();
            for (int at = 0; at < count; at++)
            {
                int current = at;
                for (int step = 0; step <= count; step++)
                {
                    int parent = parents[current];
                    if (seen.Contains(parent) || seen.Contains(at))
                    {
                        break;
                    }

                    if (parent == at)
                    {
                        cyclic.Add(at);
                        seen.Add(at);

                        break;
                    }

                    if (parent < 0 || count <= parent)
                    {
                        break;
                    }

                    current = parent;
                }
            }

            return cyclic;
        }

        private static void Morphs(List<StateItem> items, IPXPmx model, Places places, FrameScan scan)
        {
            IList<IPXMorph> morphs = model.Morph;
            Named(items, UnnamedMorphsName, DuplicateMorphNamesName, morphs.Select(m => m.Name).ToList());
            Listed(items, BadMorphOffsetsName, Where(morphs, morph => morph.Offsets.Any(offset => IsBadOffset(morph, offset, places))));
            Listed(items, MorphsNotInFrameName, Enumerable.Range(0, morphs.Count)
                .Where(at => !scan.MorphRegistered.Contains(at))
                .ToList());
            items.Add(new StateItem(MorphsInFramesTwiceName, scan.MorphsTwice.Count, Distinct(scan.MorphsTwice)));
        }

        private static bool IsBadOffset(IPXMorph morph, IPXMorphOffset offset, Places places)
        {
            if (morph.IsVertex || morph.IsUV)
            {
                IPXVertexMorphOffset moved = offset as IPXVertexMorphOffset;
                IPXUVMorphOffset slid = offset as IPXUVMorphOffset;

                return !places.Has(places.Vertices, moved != null ? moved.Vertex : slid?.Vertex);
            }

            if (morph.IsBone)
            {
                return !places.Has(places.Bones, (offset as IPXBoneMorphOffset)?.Bone);
            }

            if (morph.IsImpulse)
            {
                return !places.Has(places.Bodies, (offset as IPXImpulseMorphOffset)?.Body);
            }

            if (morph.IsMaterial)
            {
                IPXMaterial painted = (offset as IPXMaterialMorphOffset)?.Material;

                return painted != null && !places.Has(places.Materials, painted);
            }

            return (morph.IsGroup || morph.IsFlip)
                && !places.Has(places.Morphs, (offset as IPXGroupMorphOffset)?.Morph);
        }

        private static void Frames(List<StateItem> items, IPXPmx model, Places places, FrameScan scan)
        {
            IList<IPXNode> frames = scan.Frames;
            List<int> badBones = new List<int>();
            List<int> badMorphs = new List<int>();
            for (int at = 0; at < frames.Count; at++)
            {
                foreach (IPXNodeItem item in frames[at].Items)
                {
                    if (item.IsBone && !places.Has(places.Bones, item.BoneItem.Bone))
                    {
                        badBones.Add(at);

                        break;
                    }

                    if (!item.IsBone && item.IsMorph && !places.Has(places.Morphs, item.MorphItem.Morph))
                    {
                        badMorphs.Add(at);

                        break;
                    }
                }
            }

            Named(items, UnnamedFramesName, DuplicateFrameNamesName, frames.Select(f => f.Name).ToList());
            Listed(items, FramesWithBadBonesName, badBones);
            Listed(items, FramesWithBadMorphsName, badMorphs);
            Flag(
                items,
                BoneInExpressionFrameName,
                model.ExpressionNode != null && model.ExpressionNode.Items.Any(item => !item.IsMorph));
            Flag(items, MissingSystemFramesName, model.RootNode == null || model.ExpressionNode == null);
        }

        private static void Bodies(List<StateItem> items, IPXPmx model, Places places)
        {
            IList<IPXBody> bodies = model.Body;
            Named(items, UnnamedBodiesName, DuplicateBodyNamesName, bodies.Select(b => b.Name).ToList());
            Listed(items, BodiesWithBadBoneName, Where(bodies, body => body.Bone != null && !places.Has(places.Bones, body.Bone)));
        }

        private static void Joints(List<StateItem> items, IPXPmx model, Places places)
        {
            IList<IPXJoint> joints = model.Joint;
            Named(items, UnnamedJointsName, DuplicateJointNamesName, joints.Select(j => j.Name).ToList());
            Listed(items, JointsWithBadBodyAName, Where(joints, joint => !places.Has(places.Bodies, joint.BodyA)));
            Listed(items, JointsWithBadBodyBName, Where(joints, joint => !places.Has(places.Bodies, joint.BodyB)));
        }

        /// <summary>
        /// SoftBody の項目。エディタと同じく、剛体・頂点の参照の不正と、アンカーと重なる Pin は、当たった
        /// アンカーや Pin の数だけ数える。
        /// </summary>
        private static void SoftBodies(List<StateItem> items, IPXPmx model, Places places)
        {
            IList<IPXSoftBody> softs = model.SoftBody;
            List<int> badMaterial = new List<int>();
            List<int> badBody = new List<int>();
            List<int> badVertex = new List<int>();
            List<int> pinned = new List<int>();
            List<int> outside = new List<int>();
            for (int at = 0; at < softs.Count; at++)
            {
                IPXSoftBody soft = softs[at];
                HashSet<IPXVertex> covered = null;
                if (soft.Material != null)
                {
                    if (!places.Has(places.Materials, soft.Material))
                    {
                        badMaterial.Add(at);
                    }

                    covered = new HashSet<IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
                    foreach (IPXFace face in soft.Material.Faces)
                    {
                        Use(covered, face.Vertex1);
                        Use(covered, face.Vertex2);
                        Use(covered, face.Vertex3);
                    }
                }

                bool vertexCounted = false;
                bool outsideCounted = false;
                HashSet<IPXVertex> anchored = new HashSet<IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
                foreach (IPXSoftBodyAnchor anchor in soft.Anchors)
                {
                    if (anchor.Vertex != null)
                    {
                        anchored.Add(anchor.Vertex);
                        if (covered != null && !covered.Contains(anchor.Vertex))
                        {
                            outside.Add(at);
                            outsideCounted = true;
                        }
                    }

                    if (!places.Has(places.Bodies, anchor.Body))
                    {
                        badBody.Add(at);
                    }

                    if (!places.Has(places.Vertices, anchor.Vertex))
                    {
                        badVertex.Add(at);
                        vertexCounted = true;
                    }
                }

                foreach (IPXVertex pin in soft.Pins)
                {
                    if (pin != null && anchored.Contains(pin))
                    {
                        pinned.Add(at);
                    }

                    if (!outsideCounted && covered != null && pin != null && !covered.Contains(pin))
                    {
                        outside.Add(at);
                        outsideCounted = true;
                    }

                    if (!vertexCounted && !places.Has(places.Vertices, pin))
                    {
                        badVertex.Add(at);
                    }
                }
            }

            Named(items, UnnamedSoftBodiesName, DuplicateSoftBodyNamesName, softs.Select(s => s.Name).ToList());
            Listed(items, SoftBodiesWithBadMaterialName, badMaterial);
            Counted(items, SoftBodiesWithBadBodyName, badBody);
            Counted(items, SoftBodiesWithBadVertexName, badVertex);
            Counted(items, SoftBodiesPinnedOnAnchorName, pinned);
            Counted(items, SoftBodiesOutsideMaterialName, outside);
        }

        private static void Named(List<StateItem> items, string empty, string doubled, IList<string> names)
        {
            Dictionary<string, int> first = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int at = 0; at < names.Count; at++)
            {
                if (names[at] != null && !first.ContainsKey(names[at]))
                {
                    first.Add(names[at], at);
                }
            }

            Listed(items, empty, Enumerable.Range(0, names.Count).Where(at => string.IsNullOrEmpty(names[at])).ToList());
            Listed(items, doubled, Enumerable.Range(0, names.Count)
                .Where(at => !string.IsNullOrEmpty(names[at]) && first[names[at]] != at)
                .ToList());
        }

        private static IList<int> Where<T>(IList<T> list, Func<T, bool> chosen)
        {
            return Enumerable.Range(0, list.Count).Where(at => chosen(list[at])).ToList();
        }

        private static void Flag(List<StateItem> items, string name, bool found)
        {
            items.Add(new StateItem(name, found ? 1 : 0, null));
        }

        private static void Listed(List<StateItem> items, string name, IList<int> positions)
        {
            items.Add(new StateItem(name, positions.Count, positions));
        }

        private static void Counted(List<StateItem> items, string name, IList<int> hits)
        {
            items.Add(new StateItem(name, hits.Count, Distinct(hits)));
        }

        private static IList<int> Distinct(IEnumerable<int> positions)
        {
            return positions.Where(at => at >= 0).Distinct().OrderBy(at => at).ToList();
        }

        private static bool Zero(PEPlugin.SDX.V3 given)
        {
            return given == null || (given.X == 0f && given.Y == 0f && given.Z == 0f);
        }

        private static void Use(ISet<IPXVertex> used, IPXVertex vertex)
        {
            if (vertex != null)
            {
                used.Add(vertex);
            }
        }

        private enum Deform
        {
            Bdef1,
            Bdef2,
            Bdef4,
            Sdef,
            Qdef,
        }

        private sealed class FrameScan
        {
            public FrameScan(IPXPmx model, Places places)
            {
                Frames = ReferenceCleanup.Nodes(model).Where(node => node != null).ToList();
                foreach (IPXNode frame in Frames)
                {
                    foreach (IPXNodeItem item in frame.Items)
                    {
                        if (item.IsBone)
                        {
                            int bone = places.Of(places.Bones, item.BoneItem.Bone);
                            if (!BoneRegistered.Add(bone))
                            {
                                BonesTwice.Add(bone);
                            }
                        }
                        else if (item.IsMorph)
                        {
                            int morph = places.Of(places.Morphs, item.MorphItem.Morph);
                            if (!MorphRegistered.Add(morph))
                            {
                                MorphsTwice.Add(morph);
                            }
                        }
                    }
                }
            }

            public IList<IPXNode> Frames { get; }

            public ISet<int> BoneRegistered { get; } = new HashSet<int>();

            public ISet<int> MorphRegistered { get; } = new HashSet<int>();

            public IList<int> BonesTwice { get; } = new List<int>();

            public IList<int> MorphsTwice { get; } = new List<int>();
        }

        private sealed class Places
        {
            public Places(IPXPmx model)
            {
                Vertices = Table(model.Vertex);
                Materials = Table(model.Material);
                Bones = Table(model.Bone);
                Morphs = Table(model.Morph);
                Bodies = Table(model.Body);
                Faces = model.Material.SelectMany(material => material.Faces).ToList();
            }

            public IDictionary<IPXVertex, int> Vertices { get; }

            public IDictionary<IPXMaterial, int> Materials { get; }

            public IDictionary<IPXBone, int> Bones { get; }

            public IDictionary<IPXMorph, int> Morphs { get; }

            public IDictionary<IPXBody, int> Bodies { get; }

            public IList<IPXFace> Faces { get; }

            public bool Has<T>(IDictionary<T, int> table, T item)
                where T : class
            {
                return item != null && table.ContainsKey(item);
            }

            /// <summary>並びの位置。null なら -1、並びに居なければ -2。</summary>
            public int Of<T>(IDictionary<T, int> table, T item)
                where T : class
            {
                if (item == null)
                {
                    return -1;
                }

                return table.TryGetValue(item, out int found) ? found : -2;
            }

            private static IDictionary<T, int> Table<T>(IList<T> list)
                where T : class
            {
                Dictionary<T, int> table = new Dictionary<T, int>(ReferenceComparer<T>.Instance);
                for (int at = 0; at < list.Count; at++)
                {
                    if (list[at] != null && !table.ContainsKey(list[at]))
                    {
                        table.Add(list[at], at);
                    }
                }

                return table;
            }
        }
    }
}
