using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin;
using PEPlugin.Pmd;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// PMXのデータの題材。SDKのインターフェースだけを実装し、値はそのまま持つ。要素どうしは
    /// Indexではなくオブジェクトで繋ぐので、並びから外しても指したままになるところまで本物と
    /// 同じ形になる。
    /// </summary>
    public sealed class FakeVertex : IPXVertex
    {
        public FakeVertex(float x = 0f, float y = 0f, float z = 0f)
        {
            Position = new V3(x, y, z);
            Normal = new V3(0f, 1f, 0f);
            UV = new V2(0f, 0f);
            UVA1 = new V4(0f, 0f, 0f, 0f);
            UVA2 = new V4(0f, 0f, 0f, 0f);
            UVA3 = new V4(0f, 0f, 0f, 0f);
            UVA4 = new V4(0f, 0f, 0f, 0f);
            SDEF_C = new V3(0f, 0f, 0f);
            SDEF_R0 = new V3(0f, 0f, 0f);
            SDEF_R1 = new V3(0f, 0f, 0f);
            EdgeScale = 1f;
        }

        /// <summary>ベクトルを持たない頂点。複製が値をすべて書き込む。</summary>
        private FakeVertex(bool bare)
        {
        }

        public V3 Position { get; set; }

        public V3 Normal { get; set; }

        public V2 UV { get; set; }

        public V4 UVA1 { get; set; }

        public V4 UVA2 { get; set; }

        public V4 UVA3 { get; set; }

        public V4 UVA4 { get; set; }

        public IPXBone Bone1 { get; set; }

        public IPXBone Bone2 { get; set; }

        public IPXBone Bone3 { get; set; }

        public IPXBone Bone4 { get; set; }

        public float Weight1 { get; set; }

        public float Weight2 { get; set; }

        public float Weight3 { get; set; }

        public float Weight4 { get; set; }

        public bool QDEF { get; set; }

        public bool SDEF { get; set; }

        public V3 SDEF_C { get; set; }

        public V3 SDEF_R0 { get; set; }

        public V3 SDEF_R1 { get; set; }

        public float EdgeScale { get; set; }

        public object Clone()
        {
            return new FakeVertex(bare: true)
            {
                Position = SdkVectors.Copy(Position),
                Normal = SdkVectors.Copy(Normal),
                UV = SdkVectors.Copy(UV),
                UVA1 = SdkVectors.Copy(UVA1),
                UVA2 = SdkVectors.Copy(UVA2),
                UVA3 = SdkVectors.Copy(UVA3),
                UVA4 = SdkVectors.Copy(UVA4),
                Bone1 = Bone1,
                Bone2 = Bone2,
                Bone3 = Bone3,
                Bone4 = Bone4,
                Weight1 = Weight1,
                Weight2 = Weight2,
                Weight3 = Weight3,
                Weight4 = Weight4,
                QDEF = QDEF,
                SDEF = SDEF,
                SDEF_C = SDEF_C,
                SDEF_R0 = SDEF_R0,
                SDEF_R1 = SDEF_R1,
                EdgeScale = EdgeScale,
            };
        }
    }

    public sealed class FakeFace : IPXFace
    {
        public FakeFace(IPXVertex first = null, IPXVertex second = null, IPXVertex third = null)
        {
            Vertex1 = first;
            Vertex2 = second;
            Vertex3 = third;
        }

        public IPXVertex Vertex1 { get; set; }

        public IPXVertex Vertex2 { get; set; }

        public IPXVertex Vertex3 { get; set; }

        public object Clone()
        {
            return new FakeFace(Vertex1, Vertex2, Vertex3);
        }
    }

    public sealed class FakeMaterial : IPXMaterial
    {
        public FakeMaterial(string name = "材質")
        {
            Name = name;
            NameE = string.Empty;
            Memo = string.Empty;
            Tex = string.Empty;
            Sphere = string.Empty;
            Toon = string.Empty;
            Diffuse = new V4(1f, 1f, 1f, 1f);
            Specular = new V3(0f, 0f, 0f);
            Ambient = new V3(0f, 0f, 0f);
            EdgeColor = new V4(0f, 0f, 0f, 1f);
        }

        public string Name { get; set; }

        public string NameE { get; set; }

        public string Memo { get; set; }

        public V4 Diffuse { get; set; }

        public V3 Specular { get; set; }

        public float Power { get; set; }

        public V3 Ambient { get; set; }

        public bool BothDraw { get; set; }

        public bool Shadow { get; set; }

        public bool SelfShadowMap { get; set; }

        public bool SelfShadow { get; set; }

        public bool Edge { get; set; }

        public bool VertexColor { get; set; }

        public PrimitiveType PrimitiveType { get; set; }

        public V4 EdgeColor { get; set; }

        public float EdgeSize { get; set; }

        public string Tex { get; set; }

        public string Sphere { get; set; }

        public SphereType SphereMode { get; set; }

        public string Toon { get; set; }

        public IList<IPXFace> Faces { get; } = new List<IPXFace>();

        public object Clone()
        {
            FakeMaterial made = new FakeMaterial(Name)
            {
                NameE = NameE,
                Memo = Memo,
                Diffuse = SdkVectors.Copy(Diffuse),
                Specular = SdkVectors.Copy(Specular),
                Power = Power,
                Ambient = SdkVectors.Copy(Ambient),
                BothDraw = BothDraw,
                Shadow = Shadow,
                SelfShadowMap = SelfShadowMap,
                SelfShadow = SelfShadow,
                Edge = Edge,
                VertexColor = VertexColor,
                PrimitiveType = PrimitiveType,
                EdgeColor = SdkVectors.Copy(EdgeColor),
                EdgeSize = EdgeSize,
                Tex = Tex,
                Sphere = Sphere,
                SphereMode = SphereMode,
                Toon = Toon,
            };
            foreach (IPXFace face in Faces)
            {
                made.Faces.Add((IPXFace)face.Clone());
            }

            return made;
        }
    }

    public sealed class FakeIkLink : IPXIKLink
    {
        public FakeIkLink(IPXBone bone = null)
        {
            Bone = bone;
            Low = new V3(0f, 0f, 0f);
            High = new V3(0f, 0f, 0f);
        }

        public IPXBone Bone { get; set; }

        public bool IsLimit { get; set; }

        public V3 Low { get; set; }

        public V3 High { get; set; }

        public object Clone()
        {
            return new FakeIkLink(Bone) { IsLimit = IsLimit, Low = SdkVectors.Copy(Low), High = SdkVectors.Copy(High) };
        }
    }

    public sealed class FakeIk : IPXIK
    {
        public IPXBone Target { get; set; }

        public int LoopCount { get; set; }

        public float Angle { get; set; }

        public IList<IPXIKLink> Links { get; } = new List<IPXIKLink>();

        public object Clone()
        {
            FakeIk made = new FakeIk { Target = Target, LoopCount = LoopCount, Angle = Angle };
            foreach (IPXIKLink link in Links)
            {
                made.Links.Add((IPXIKLink)link.Clone());
            }

            return made;
        }
    }

    public sealed class FakeBone : IPXBone
    {
        public FakeBone(string name = "ボーン")
        {
            Name = name;
            NameE = string.Empty;
            Position = new V3(0f, 0f, 0f);
            ToOffset = new V3(0f, 0f, 0f);
            FixAxis = new V3(1f, 0f, 0f);
            LocalAxisX = new V3(1f, 0f, 0f);
            LocalAxisZ = new V3(0f, 0f, 1f);
        }

        public string Name { get; set; }

        public string NameE { get; set; }

        public V3 Position { get; set; }

        public IPXBone Parent { get; set; }

        public int Level { get; set; }

        public V3 ToOffset { get; set; }

        public IPXBone ToBone { get; set; }

        public bool IsRotation { get; set; }

        public bool IsTranslation { get; set; }

        public bool Visible { get; set; }

        public bool Controllable { get; set; }

        public bool IsIK { get; set; }

        public bool IsAppendRotation { get; set; }

        public bool IsAppendTranslation { get; set; }

        public bool IsAppendLocal { get; set; }

        public IPXBone AppendParent { get; set; }

        public float AppendRatio { get; set; }

        public bool IsFixAxis { get; set; }

        public V3 FixAxis { get; set; }

        public bool IsLocalFrame { get; set; }

        public bool IsAfterPhysics { get; set; }

        public bool IsExternal { get; set; }

        public int ExternalKey { get; set; }

        public IPXIK IK { get; } = new FakeIk();

        public V3 LocalAxisX { get; private set; }

        /// <summary>簡易設定で入れたローカル軸のZ。</summary>
        public V3 LocalAxisZ { get; private set; }

        /// <summary>入れたPMDのボーン種別。</summary>
        public BoneKind PmdKind { get; private set; }

        public void GetLocalAxis(out V3 x, out V3 y, out V3 z)
        {
            x = LocalAxisX;
            z = LocalAxisZ;
            y = new V3(0f, 1f, 0f);
        }

        public void SetLocalAxis(V3 x, V3 z)
        {
            LocalAxisX = x;
            LocalAxisZ = z;
        }

        /// <summary>
        /// SDKと同じく、種別に合わせてフラグを入れ直す。回転・表示・操作を真、移動・IK・回転付与・
        /// 軸固定を偽にしてから、種別ごとに足す。
        /// </summary>
        public void SetPMDBoneKind(BoneKind kind)
        {
            PmdKind = kind;
            IsRotation = true;
            Visible = true;
            Controllable = true;
            IsTranslation = false;
            IsIK = false;
            IsAppendRotation = false;
            IsFixAxis = false;
            bool ik = false;
            switch (kind)
            {
                case BoneKind.RotateMove:
                    IsTranslation = true;
                    break;

                case BoneKind.IK:
                    ik = true;
                    break;

                case BoneKind.RotateEffect:
                    IsAppendRotation = true;
                    AppendRatio = 1f;
                    Level = 2;
                    break;

                case BoneKind.IKTo:
                case BoneKind.Unvisible:
                    Visible = false;
                    break;

                case BoneKind.Twist:
                    IsFixAxis = true;
                    break;

                case BoneKind.RotateRatio:
                    IsAppendRotation = true;
                    Visible = false;
                    AppendRatio = -0.01f;
                    break;
            }

            if (ik)
            {
                IsTranslation = true;
                IsIK = true;
                Level = 1;
            }
        }

        public object Clone()
        {
            FakeBone made = new FakeBone(Name)
            {
                NameE = NameE,
                Position = SdkVectors.Copy(Position),
                Parent = Parent,
                Level = Level,
                ToOffset = SdkVectors.Copy(ToOffset),
                ToBone = ToBone,
                IsRotation = IsRotation,
                IsTranslation = IsTranslation,
                Visible = Visible,
                Controllable = Controllable,
                IsIK = IsIK,
                IsAppendRotation = IsAppendRotation,
                IsAppendTranslation = IsAppendTranslation,
                IsAppendLocal = IsAppendLocal,
                AppendParent = AppendParent,
                AppendRatio = AppendRatio,
                IsFixAxis = IsFixAxis,
                FixAxis = SdkVectors.Copy(FixAxis),
                IsLocalFrame = IsLocalFrame,
                IsAfterPhysics = IsAfterPhysics,
                IsExternal = IsExternal,
                ExternalKey = ExternalKey,
            };
            made.LocalAxisX = SdkVectors.Copy(LocalAxisX);
            made.LocalAxisZ = SdkVectors.Copy(LocalAxisZ);
            made.PmdKind = PmdKind;
            made.IK.Target = IK.Target;
            made.IK.LoopCount = IK.LoopCount;
            made.IK.Angle = IK.Angle;
            foreach (IPXIKLink link in IK.Links)
            {
                made.IK.Links.Add((IPXIKLink)link.Clone());
            }

            return made;
        }
    }

    public sealed class FakeVertexMorphOffset : IPXVertexMorphOffset
    {
        public FakeVertexMorphOffset(IPXVertex vertex = null)
        {
            Vertex = vertex;
            Offset = new V3(0f, 0f, 0f);
        }

        public IPXVertex Vertex { get; set; }

        public V3 Offset { get; set; }

        public object Clone()
        {
            return new FakeVertexMorphOffset(Vertex) { Offset = SdkVectors.Copy(Offset) };
        }
    }

    public sealed class FakeUVMorphOffset : IPXUVMorphOffset
    {
        public FakeUVMorphOffset(IPXVertex vertex = null)
        {
            Vertex = vertex;
            Offset = new V4(0f, 0f, 0f, 0f);
        }

        public IPXVertex Vertex { get; set; }

        public V4 Offset { get; set; }

        public object Clone()
        {
            return new FakeUVMorphOffset(Vertex) { Offset = SdkVectors.Copy(Offset) };
        }
    }

    public sealed class FakeBoneMorphOffset : IPXBoneMorphOffset
    {
        public FakeBoneMorphOffset(IPXBone bone = null)
        {
            Bone = bone;
            Translation = new V3(0f, 0f, 0f);
            Rotation = new Q(0f, 0f, 0f, 1f);
        }

        public IPXBone Bone { get; set; }

        public V3 Translation { get; set; }

        public Q Rotation { get; set; }

        public object Clone()
        {
            return new FakeBoneMorphOffset(Bone) { Translation = SdkVectors.Copy(Translation), Rotation = SdkVectors.Copy(Rotation) };
        }
    }

    public sealed class FakeMaterialMorphOffset : IPXMaterialMorphOffset
    {
        public FakeMaterialMorphOffset(IPXMaterial material = null)
        {
            Material = material;
            Diffuse = new V4(1f, 1f, 1f, 1f);
            Specular = new V3(0f, 0f, 0f);
            Ambient = new V3(0f, 0f, 0f);
            EdgeColor = new V4(0f, 0f, 0f, 1f);
            Tex = new V4(1f, 1f, 1f, 1f);
            Sphere = new V4(1f, 1f, 1f, 1f);
            Toon = new V4(1f, 1f, 1f, 1f);
        }

        public IPXMaterial Material { get; set; }

        public int Op { get; set; }

        public V4 Diffuse { get; set; }

        public V3 Specular { get; set; }

        public float Power { get; set; }

        public V3 Ambient { get; set; }

        public V4 EdgeColor { get; set; }

        public float EdgeSize { get; set; }

        public V4 Tex { get; set; }

        public V4 Sphere { get; set; }

        public V4 Toon { get; set; }

        public void Clear(float v)
        {
            Diffuse = new V4(v, v, v, v);
            Specular = new V3(v, v, v);
            Power = v;
            Ambient = new V3(v, v, v);
            EdgeColor = new V4(v, v, v, v);
            EdgeSize = v;
            Tex = new V4(v, v, v, v);
            Sphere = new V4(v, v, v, v);
            Toon = new V4(v, v, v, v);
        }

        public object Clone()
        {
            return new FakeMaterialMorphOffset(Material)
            {
                Op = Op,
                Diffuse = SdkVectors.Copy(Diffuse),
                Specular = SdkVectors.Copy(Specular),
                Power = Power,
                Ambient = SdkVectors.Copy(Ambient),
                EdgeColor = SdkVectors.Copy(EdgeColor),
                EdgeSize = EdgeSize,
                Tex = SdkVectors.Copy(Tex),
                Sphere = SdkVectors.Copy(Sphere),
                Toon = SdkVectors.Copy(Toon),
            };
        }
    }

    public sealed class FakeGroupMorphOffset : IPXGroupMorphOffset
    {
        public FakeGroupMorphOffset(IPXMorph morph = null)
        {
            Morph = morph;
        }

        public IPXMorph Morph { get; set; }

        public float Ratio { get; set; }

        public object Clone()
        {
            return new FakeGroupMorphOffset(Morph) { Ratio = Ratio };
        }
    }

    public sealed class FakeImpulseMorphOffset : IPXImpulseMorphOffset
    {
        public FakeImpulseMorphOffset(IPXBody body = null)
        {
            Body = body;
            Velocity = new V3(0f, 0f, 0f);
            Torque = new V3(0f, 0f, 0f);
        }

        public IPXBody Body { get; set; }

        public bool Local { get; set; }

        public V3 Velocity { get; set; }

        public V3 Torque { get; set; }

        public object Clone()
        {
            return new FakeImpulseMorphOffset(Body)
            {
                Local = Local,
                Velocity = SdkVectors.Copy(Velocity),
                Torque = SdkVectors.Copy(Torque),
            };
        }
    }

    public sealed class FakeMorph : IPXMorph
    {
        public FakeMorph(string name = "モーフ", MorphKind kind = MorphKind.Vertex)
        {
            Name = name;
            NameE = string.Empty;
            Kind = kind;
        }

        public string Name { get; set; }

        public string NameE { get; set; }

        public int Panel { get; set; }

        public MorphKind Kind { get; set; }

        public IList<IPXMorphOffset> Offsets { get; } = new List<IPXMorphOffset>();

        public bool IsGroup
        {
            get { return Kind == MorphKind.Group; }
        }

        public bool IsVertex
        {
            get { return Kind == MorphKind.Vertex; }
        }

        public bool IsBone
        {
            get { return Kind == MorphKind.Bone; }
        }

        public bool IsUV
        {
            get
            {
                return Kind == MorphKind.UV || Kind == MorphKind.UVA1 || Kind == MorphKind.UVA2
                    || Kind == MorphKind.UVA3 || Kind == MorphKind.UVA4;
            }
        }

        public bool IsMaterial
        {
            get { return Kind == MorphKind.Material; }
        }

        public bool IsFlip
        {
            get { return Kind == MorphKind.Flip; }
        }

        public bool IsImpulse
        {
            get { return Kind == MorphKind.Impulse; }
        }

        public object Clone()
        {
            FakeMorph made = new FakeMorph(Name, Kind) { NameE = NameE, Panel = Panel };
            foreach (IPXMorphOffset offset in Offsets)
            {
                made.Offsets.Add((IPXMorphOffset)offset.Clone());
            }

            return made;
        }
    }

    public sealed class FakeBoneNodeItem : IPXBoneNodeItem
    {
        public FakeBoneNodeItem(IPXBone bone = null)
        {
            Bone = bone;
        }

        public IPXBone Bone { get; set; }

        public bool IsBone
        {
            get { return true; }
        }

        public bool IsMorph
        {
            get { return false; }
        }

        public IPXBoneNodeItem BoneItem
        {
            get { return this; }
        }

        public IPXMorphNodeItem MorphItem
        {
            get { return null; }
        }

        public object Clone()
        {
            return new FakeBoneNodeItem(Bone);
        }
    }

    public sealed class FakeMorphNodeItem : IPXMorphNodeItem
    {
        public FakeMorphNodeItem(IPXMorph morph = null)
        {
            Morph = morph;
        }

        public IPXMorph Morph { get; set; }

        public bool IsBone
        {
            get { return false; }
        }

        public bool IsMorph
        {
            get { return true; }
        }

        public IPXBoneNodeItem BoneItem
        {
            get { return null; }
        }

        public IPXMorphNodeItem MorphItem
        {
            get { return this; }
        }

        public object Clone()
        {
            return new FakeMorphNodeItem(Morph);
        }
    }

    public sealed class FakeNode : IPXNode
    {
        public FakeNode(string name = "表示枠")
        {
            Name = name;
            NameE = string.Empty;
        }

        public string Name { get; set; }

        public string NameE { get; set; }

        public IList<IPXNodeItem> Items { get; } = new List<IPXNodeItem>();

        public object Clone()
        {
            FakeNode made = new FakeNode(Name) { NameE = NameE };
            foreach (IPXNodeItem item in Items)
            {
                made.Items.Add((IPXNodeItem)item.Clone());
            }

            return made;
        }
    }

    public sealed class FakeBody : IPXBody
    {
        public FakeBody(string name = "剛体")
        {
            Name = name;
            NameE = string.Empty;
            Position = new V3(0f, 0f, 0f);
            Rotation = new V3(0f, 0f, 0f);
            BoxSize = new V3(1f, 1f, 1f);
            PassGroup = new bool[16];
        }

        public string Name { get; set; }

        public string NameE { get; set; }

        public IPXBone Bone { get; set; }

        public int Group { get; set; }

        public bool[] PassGroup { get; }

        public BodyBoxKind BoxKind { get; set; }

        public V3 BoxSize { get; set; }

        public V3 Position { get; set; }

        public V3 Rotation { get; set; }

        public float Mass { get; set; }

        public float PositionDamping { get; set; }

        public float RotationDamping { get; set; }

        public float Restitution { get; set; }

        public float Friction { get; set; }

        public BodyMode Mode { get; set; }

        public object Clone()
        {
            FakeBody made = new FakeBody(Name)
            {
                NameE = NameE,
                Bone = Bone,
                Group = Group,
                BoxKind = BoxKind,
                BoxSize = SdkVectors.Copy(BoxSize),
                Position = SdkVectors.Copy(Position),
                Rotation = SdkVectors.Copy(Rotation),
                Mass = Mass,
                PositionDamping = PositionDamping,
                RotationDamping = RotationDamping,
                Restitution = Restitution,
                Friction = Friction,
                Mode = Mode,
            };
            for (int at = 0; at < PassGroup.Length; at++)
            {
                made.PassGroup[at] = PassGroup[at];
            }

            return made;
        }
    }

    public sealed class FakeJoint : IPXJoint
    {
        public FakeJoint(string name = "Joint")
        {
            Name = name;
            NameE = string.Empty;
            Position = new V3(0f, 0f, 0f);
            Rotation = new V3(0f, 0f, 0f);
            Limit_MoveLow = new V3(0f, 0f, 0f);
            Limit_MoveHigh = new V3(0f, 0f, 0f);
            Limit_AngleLow = new V3(0f, 0f, 0f);
            Limit_AngleHigh = new V3(0f, 0f, 0f);
            SpringConst_Move = new V3(0f, 0f, 0f);
            SpringConst_Rotate = new V3(0f, 0f, 0f);
        }

        public string Name { get; set; }

        public string NameE { get; set; }

        public JointKind Kind { get; set; }

        public IPXBody BodyA { get; set; }

        public IPXBody BodyB { get; set; }

        public V3 Position { get; set; }

        public V3 Rotation { get; set; }

        public V3 Limit_MoveLow { get; set; }

        public V3 Limit_MoveHigh { get; set; }

        public V3 Limit_AngleLow { get; set; }

        public V3 Limit_AngleHigh { get; set; }

        public V3 SpringConst_Move { get; set; }

        public V3 SpringConst_Rotate { get; set; }

        public object Clone()
        {
            return new FakeJoint(Name)
            {
                NameE = NameE,
                Kind = Kind,
                BodyA = BodyA,
                BodyB = BodyB,
                Position = SdkVectors.Copy(Position),
                Rotation = SdkVectors.Copy(Rotation),
                Limit_MoveLow = SdkVectors.Copy(Limit_MoveLow),
                Limit_MoveHigh = SdkVectors.Copy(Limit_MoveHigh),
                Limit_AngleLow = SdkVectors.Copy(Limit_AngleLow),
                Limit_AngleHigh = SdkVectors.Copy(Limit_AngleHigh),
                SpringConst_Move = SdkVectors.Copy(SpringConst_Move),
                SpringConst_Rotate = SdkVectors.Copy(SpringConst_Rotate),
            };
        }
    }

    public sealed class FakeSoftBodyAnchor : IPXSoftBodyAnchor
    {
        public FakeSoftBodyAnchor(IPXBody body = null, IPXVertex vertex = null)
        {
            Body = body;
            Vertex = vertex;
        }

        public IPXBody Body { get; set; }

        public IPXVertex Vertex { get; set; }

        public bool Near { get; set; }

        public object Clone()
        {
            return new FakeSoftBodyAnchor(Body, Vertex) { Near = Near };
        }
    }

    public sealed class FakeSoftBody : IPXSoftBody
    {
        public FakeSoftBody(string name = "SoftBody")
        {
            Name = name;
            NameE = string.Empty;
            PassGroup = new bool[16];
        }

        public string Name { get; set; }

        public string NameE { get; set; }

        public SoftBodyShape Shape { get; set; }

        public IPXMaterial Material { get; set; }

        public int Group { get; set; }

        public bool[] PassGroup { get; }

        public bool GenerateBendingLinks { get; set; }

        public bool GenerateClusters { get; set; }

        public bool RandomizeConstraints { get; set; }

        public int BendingLinkDistance { get; set; }

        public int ClusterCount { get; set; }

        public float TotalMass { get; set; }

        public float Margin { get; set; }

        public int AeroModel { get; set; }

        public float VCF { get; set; }

        public float DP { get; set; }

        public float DG { get; set; }

        public float LF { get; set; }

        public float PR { get; set; }

        public float VC { get; set; }

        public float DF { get; set; }

        public float MT { get; set; }

        public float CHR { get; set; }

        public float KHR { get; set; }

        public float SHR { get; set; }

        public float AHR { get; set; }

        public float SRHR_CL { get; set; }

        public float SKHR_CL { get; set; }

        public float SSHR_CL { get; set; }

        public float SR_SPLT_CL { get; set; }

        public float SK_SPLT_CL { get; set; }

        public float SS_SPLT_CL { get; set; }

        public int V_IT { get; set; }

        public int P_IT { get; set; }

        public int D_IT { get; set; }

        public int C_IT { get; set; }

        public float LST { get; set; }

        public float AST { get; set; }

        public float VST { get; set; }

        public IList<IPXSoftBodyAnchor> Anchors { get; } = new List<IPXSoftBodyAnchor>();

        public IList<IPXVertex> Pins { get; } = new List<IPXVertex>();

        public object Clone()
        {
            FakeSoftBody made = new FakeSoftBody(Name)
            {
                NameE = NameE,
                Shape = Shape,
                Material = Material,
                Group = Group,
                GenerateBendingLinks = GenerateBendingLinks,
                GenerateClusters = GenerateClusters,
                RandomizeConstraints = RandomizeConstraints,
                BendingLinkDistance = BendingLinkDistance,
                ClusterCount = ClusterCount,
                TotalMass = TotalMass,
                Margin = Margin,
                AeroModel = AeroModel,
                VCF = VCF,
                DP = DP,
                DG = DG,
                LF = LF,
                PR = PR,
                VC = VC,
                DF = DF,
                MT = MT,
                CHR = CHR,
                KHR = KHR,
                SHR = SHR,
                AHR = AHR,
                SRHR_CL = SRHR_CL,
                SKHR_CL = SKHR_CL,
                SSHR_CL = SSHR_CL,
                SR_SPLT_CL = SR_SPLT_CL,
                SK_SPLT_CL = SK_SPLT_CL,
                SS_SPLT_CL = SS_SPLT_CL,
                V_IT = V_IT,
                P_IT = P_IT,
                D_IT = D_IT,
                C_IT = C_IT,
                LST = LST,
                AST = AST,
                VST = VST,
            };
            for (int at = 0; at < PassGroup.Length; at++)
            {
                made.PassGroup[at] = PassGroup[at];
            }

            foreach (IPXSoftBodyAnchor anchor in Anchors)
            {
                made.Anchors.Add((IPXSoftBodyAnchor)anchor.Clone());
            }

            foreach (IPXVertex pin in Pins)
            {
                made.Pins.Add(pin);
            }

            return made;
        }
    }

    public sealed class FakeHeader : IPXHeader
    {
        public float Version { get; set; } = 2.0f;

        public int StringEncode { get; set; }

        public int UVACount { get; set; }

        public object Clone()
        {
            return new FakeHeader
            {
                Version = Version,
                StringEncode = StringEncode,
                UVACount = UVACount,
            };
        }
    }

    public sealed class FakeModelInfo : IPXModelInfo
    {
        public string ModelName { get; set; } = string.Empty;

        public string ModelNameE { get; set; } = string.Empty;

        public string Comment { get; set; } = string.Empty;

        public string CommentE { get; set; } = string.Empty;

        public object Clone()
        {
            return new FakeModelInfo
            {
                ModelName = ModelName,
                ModelNameE = ModelNameE,
                Comment = Comment,
                CommentE = CommentE,
            };
        }
    }

    /// <summary>PMXそのものの題材。ファイルの読み書きは題材の役目ではないので持たない。</summary>
    public sealed class FakePmx : IPXPmx
    {
        public string FilePath { get; set; } = string.Empty;

        public IPXHeader Header { get; } = new FakeHeader();

        public IPXModelInfo ModelInfo { get; } = new FakeModelInfo();

        public IList<IPXVertex> Vertex { get; } = new List<IPXVertex>();

        public IList<IPXMaterial> Material { get; } = new List<IPXMaterial>();

        public IList<IPXBone> Bone { get; } = new List<IPXBone>();

        public IList<IPXMorph> Morph { get; } = new List<IPXMorph>();

        public IPXNode RootNode { get; internal set; } = new FakeNode("Root");

        public IPXNode ExpressionNode { get; internal set; } = new FakeNode("表情");

        public IList<IPXNode> Node { get; } = new List<IPXNode>();

        public IList<IPXBody> Body { get; } = new List<IPXBody>();

        public IList<IPXJoint> Joint { get; } = new List<IPXJoint>();

        public IList<IPXSoftBody> SoftBody { get; } = new List<IPXSoftBody>();

        public IPXPrimitiveBuilder Primitive
        {
            get { return new FakePrimitiveBuilder(); }
        }

        public void Clear()
        {
            Vertex.Clear();
            Material.Clear();
            Bone.Clear();
            Morph.Clear();
            Node.Clear();
            Body.Clear();
            Joint.Clear();
            SoftBody.Clear();
        }

        public void Normalize()
        {
        }

        public void FromFile(string path)
        {
            FilePath = path;
        }

        public void ToFile(string path)
        {
            throw new NotSupportedException();
        }

        public void FromStream(System.IO.Stream s)
        {
            throw new NotSupportedException();
        }

        public void ToStream(System.IO.Stream s)
        {
            s.WriteByte(0);
        }

        public object Clone()
        {
            return FakeEditorState.Duplicate(this);
        }
    }

    /// <summary>SDKの要素の複製がベクトルを別のオブジェクトにするのに合わせる写し。</summary>
    internal static class SdkVectors
    {
        public static V2 Copy(V2 v)
        {
            return v == null ? null : new V2(v.X, v.Y);
        }

        public static V3 Copy(V3 v)
        {
            return v == null ? null : new V3(v.X, v.Y, v.Z);
        }

        public static V4 Copy(V4 v)
        {
            return v == null ? null : new V4(v.X, v.Y, v.Z, v.W);
        }

        public static Q Copy(Q q)
        {
            return q == null ? null : new Q(q.X, q.Y, q.Z, q.W);
        }
    }

    /// <summary>
    /// エディタが持つ現在のPMXの題材の振る舞い。エディタは現在のPMXを位置で繋いだ形で持ち、
    /// 複製の要求には要素どうしを位置から繋ぎ直した別のオブジェクト一式を返し、反映では反映の
    /// 対象の要素を、渡された複製の要素の複製で置き換える(面だけは複製せずにそのまま入れ直す)。
    /// 置き換えた前の要素は現在のPMXから外れ、以後の反映を映さない。写すときに渡された複製を
    /// 整え(指す先の無い面・IKのリンク・
    /// モーフのオフセット・表示枠のボーン・SoftBodyの固定頂点を外し、オフセットが全部外れた
    /// モーフを消す)、写す頂点のウェイトを正規化する。PMXの外を指す要素は、位置を持たないので
    /// 写したあと何も指さない。
    /// </summary>
    public static class FakeEditorState
    {
        /// <summary>複製の要求に返す、要素どうしを繋ぎ直した別のオブジェクト一式。</summary>
        public static FakePmx Duplicate(IPXPmx source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            FakePmx made = new FakePmx();
            FakePmx held = source as FakePmx;
            made.FilePath = held == null ? string.Empty : held.FilePath;
            made.Header.Version = source.Header.Version;
            made.Header.StringEncode = source.Header.StringEncode;
            made.Header.UVACount = source.Header.UVACount;
            Carry(made, source, PmxUpdateObject.All, new Tables(source));

            return made;
        }

        /// <summary>
        /// 反映の行が <paramref name="passed"/> を受け取ったときに、<paramref name="state"/> へ
        /// 写す。<paramref name="part"/> が <see cref="PmxUpdateObject.All"/> なら全部を、ほかは
        /// その区分の並びだけを写す。<paramref name="index"/> は区分の中の位置で、-1 は区分の全体。
        /// </summary>
        public static void Reflect(FakePmx state, IPXPmx passed, PmxUpdateObject part, int index)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (passed == null)
            {
                throw new ArgumentNullException(nameof(passed));
            }

            if (part == PmxUpdateObject.None)
            {
                return;
            }

            Tables taken = new Tables(passed);
            if (Tidy(passed, part, taken))
            {
                taken = new Tables(passed);
            }

            if (part == PmxUpdateObject.All || part == PmxUpdateObject.Vertex)
            {
                int[] keys = new int[4];
                float[] values = new float[4];
                foreach (IPXVertex vertex in passed.Vertex)
                {
                    NormalizeWeight(vertex, passed.Bone, taken.Bone, keys, values);
                }
            }

            if (part == PmxUpdateObject.All || part == PmxUpdateObject.Header)
            {
                state.Header.Version = passed.Header.Version;
                state.Header.StringEncode = passed.Header.StringEncode;
                state.Header.UVACount = passed.Header.UVACount;
            }

            if (part == PmxUpdateObject.All || part == PmxUpdateObject.ModelInfo)
            {
                state.ModelInfo.ModelName = passed.ModelInfo.ModelName;
                state.ModelInfo.ModelNameE = passed.ModelInfo.ModelNameE;
                state.ModelInfo.Comment = passed.ModelInfo.Comment;
                state.ModelInfo.CommentE = passed.ModelInfo.CommentE;
            }

            if (index < 0 || part == PmxUpdateObject.All)
            {
                Carry(state, passed, part, taken);

                return;
            }

            Tables old = new Tables(state);
            switch (part)
            {
                case PmxUpdateObject.Vertex:
                    Put(state, s => s.Vertex, passed.Vertex, index);
                    break;

                case PmxUpdateObject.Material:
                    IList<IPXFace> faces = state.Material[index].Faces.ToList();
                    Put(state, s => s.Material, passed.Material, index);
                    Refill(state.Material[index].Faces, faces);
                    break;

                case PmxUpdateObject.Bone:
                    Put(state, s => s.Bone, passed.Bone, index);
                    break;

                case PmxUpdateObject.Morph:
                    Put(state, s => s.Morph, passed.Morph, index);
                    break;

                case PmxUpdateObject.Node:
                    if (index == 0)
                    {
                        Retire(state, state.RootNode, s => s.RootNode);
                        state.RootNode = (IPXNode)Fresh(passed.RootNode);
                    }
                    else if (index == 1)
                    {
                        Retire(state, state.ExpressionNode, s => s.ExpressionNode);
                        state.ExpressionNode = (IPXNode)Fresh(passed.ExpressionNode);
                    }
                    else
                    {
                        Put(state, s => s.Node, passed.Node, index - 2);
                    }

                    break;

                case PmxUpdateObject.Body:
                    Put(state, s => s.Body, passed.Body, index);
                    break;

                case PmxUpdateObject.Joint:
                    Put(state, s => s.Joint, passed.Joint, index);
                    break;

                case PmxUpdateObject.SoftBody:
                    Put(state, s => s.SoftBody, passed.SoftBody, index);
                    break;

                default:
                    Carry(state, passed, part, taken);
                    return;
            }

            Relink(state, taken, old);
        }

        /// <summary>
        /// 頂点のウェイトを、エディタが反映のときにかける正規化と同じ形へ直す。重みの無いボーンの
        /// 重みを0にし、SDEFはボーンの位置の小さい方を先に(R0とR1も入れ替えて)、ほかは重みの
        /// 大きい順に並べ、BDEF1・BDEF2・SDEFは和を1にそろえ、変形方式を重みの数から決め直す。
        /// </summary>
        public static void NormalizeWeight(IPXVertex vertex, IList<IPXBone> bones)
        {
            if (vertex == null)
            {
                throw new ArgumentNullException(nameof(vertex));
            }

            if (bones == null)
            {
                throw new ArgumentNullException(nameof(bones));
            }

            NormalizeWeight(vertex, bones, new Tables(bones).Bone);
        }

        private static void NormalizeWeight(
            IPXVertex vertex, IList<IPXBone> bones, IDictionary<object, int> table)
        {
            NormalizeWeight(vertex, bones, table, new int[4], new float[4]);
        }

        /// <summary>
        /// <paramref name="keys"/> と <paramref name="values"/> は4つずつの作業用の並びで、
        /// 呼ぶたびに書き潰す。
        /// </summary>
        private static void NormalizeWeight(
            IPXVertex vertex,
            IList<IPXBone> bones,
            IDictionary<object, int> table,
            int[] keys,
            float[] values)
        {
            Weigh(vertex.Bone1, vertex.Weight1, table, keys, values, 0);
            Weigh(vertex.Bone2, vertex.Weight2, table, keys, values, 1);
            Weigh(vertex.Bone3, vertex.Weight3, table, keys, values, 2);
            Weigh(vertex.Bone4, vertex.Weight4, table, keys, values, 3);
            bool sdef = vertex.SDEF;
            Deform deform = sdef ? Deform.Sdef : vertex.QDEF ? Deform.Qdef : Deform.Bdef2;
            V3 r0 = vertex.SDEF_R0;
            V3 r1 = vertex.SDEF_R1;
            if (deform == Deform.Sdef)
            {
                if (keys[0] > keys[1])
                {
                    int firstKey = keys[0];
                    float firstValue = values[0];
                    keys[0] = keys[1];
                    values[0] = values[1];
                    keys[1] = firstKey;
                    values[1] = firstValue;
                    V3 swapped = r0;
                    r0 = r1;
                    r1 = swapped;
                }
            }
            else
            {
                for (int at = 1; at < keys.Length; at++)
                {
                    int movingKey = keys[at];
                    float movingValue = values[at];
                    int to = at;
                    while (to > 0 && Math.Abs(values[to - 1]) < Math.Abs(movingValue))
                    {
                        keys[to] = keys[to - 1];
                        values[to] = values[to - 1];
                        to--;
                    }

                    keys[to] = movingKey;
                    values[to] = movingValue;
                }
            }

            deform = DeformOf(values, sdef, deform);
            if (deform == Deform.Sdef)
            {
                keys[2] = -1;
                values[2] = 0f;
                keys[3] = -1;
                values[3] = 0f;
            }

            if (deform != Deform.Bdef4 && deform != Deform.Qdef)
            {
                float sum = 0f;
                for (int at = 0; at < values.Length; at++)
                {
                    sum += values[at];
                }

                if (sum != 0f && sum != 1f)
                {
                    float scale = 1f / sum;
                    for (int at = 0; at < values.Length; at++)
                    {
                        values[at] = values[at] * scale;
                    }
                }
            }

            int used = deform == Deform.Bdef2 || deform == Deform.Sdef ? 2
                : deform == Deform.Bdef4 || deform == Deform.Qdef ? 4 : 1;
            for (int at = 0; at < used; at++)
            {
                if (keys[at] < 0)
                {
                    keys[at] = 0;
                    values[at] = 0f;
                }
            }

            deform = DeformOf(values, sdef, deform);
            vertex.Bone1 = BoneAt(bones, keys[0]);
            vertex.Bone2 = BoneAt(bones, keys[1]);
            vertex.Bone3 = BoneAt(bones, keys[2]);
            vertex.Bone4 = BoneAt(bones, keys[3]);
            vertex.Weight1 = values[0];
            vertex.Weight2 = values[1];
            vertex.Weight3 = values[2];
            vertex.Weight4 = values[3];
            vertex.SDEF = deform == Deform.Sdef;
            vertex.QDEF = deform == Deform.Qdef;
            vertex.SDEF_R0 = r0;
            vertex.SDEF_R1 = r1;
        }

        private enum Deform
        {
            Bdef1,

            Bdef2,

            Bdef4,

            Sdef,

            Qdef,
        }

        private static void Weigh(
            IPXBone bone, float value, IDictionary<object, int> table, int[] keys, float[] values, int slot)
        {
            int at;
            if (bone == null || !table.TryGetValue(bone, out at))
            {
                at = -1;
            }

            keys[slot] = at;
            values[slot] = at < 0 ? 0f : value;
        }

        private static Deform DeformOf(float[] values, bool sdef, Deform now)
        {
            int count = 0;
            for (int at = 0; at < values.Length; at++)
            {
                if (values[at] != 0f)
                {
                    count++;
                }
            }

            if (sdef && count != 1)
            {
                return Deform.Sdef;
            }

            if (now == Deform.Qdef && count != 1)
            {
                return Deform.Qdef;
            }

            return count <= 1 ? Deform.Bdef1 : count == 2 ? Deform.Bdef2 : Deform.Bdef4;
        }

        private static IPXBone BoneAt(IList<IPXBone> bones, int at)
        {
            return at >= 0 && at < bones.Count ? bones[at] : null;
        }

        /// <summary>
        /// <paramref name="source"/> の <paramref name="part"/> の並びを、要素を複製して
        /// <paramref name="target"/> へ置き、<paramref name="target"/> の全要素が指す先を位置で
        /// 繋ぎ直す。
        /// </summary>
        private static void Carry(FakePmx target, IPXPmx source, PmxUpdateObject part, Tables taken)
        {
            Tables old = new Tables(target);
            bool all = part == PmxUpdateObject.All;
            if (all || part == PmxUpdateObject.Vertex)
            {
                Replace(target, s => s.Vertex, source.Vertex);
            }

            if (all)
            {
                Replace(target, s => s.Material, source.Material);
            }
            else if (part == PmxUpdateObject.Material)
            {
                List<IList<IPXFace>> faces =
                    target.Material.Select(m => (IList<IPXFace>)m.Faces.ToList()).ToList();
                Replace(target, s => s.Material, source.Material);
                for (int at = 0; at < target.Material.Count; at++)
                {
                    Refill(target.Material[at].Faces, at < faces.Count ? faces[at] : new IPXFace[0]);
                }
            }
            else if (part == PmxUpdateObject.Face)
            {
                for (int at = 0; at < target.Material.Count && at < source.Material.Count; at++)
                {
                    int owner = at;
                    IList<IPXFace> faces = target.Material[at].Faces;
                    for (int face = 0; face < faces.Count; face++)
                    {
                        int place = face;
                        Retire(target, faces[face], s => Child(s.Material, owner, m => m.Faces, place));
                    }

                    Refill(
                        target.Material[at].Faces,
                        source.Material[at].Faces.Select(f => (IPXFace)Fresh(f)).ToList());
                }
            }

            if (all || part == PmxUpdateObject.Bone)
            {
                Replace(target, s => s.Bone, source.Bone);
            }

            if (all || part == PmxUpdateObject.Morph)
            {
                Replace(target, s => s.Morph, source.Morph);
            }

            if (all || part == PmxUpdateObject.Node)
            {
                Retire(target, target.RootNode, s => s.RootNode);
                Retire(target, target.ExpressionNode, s => s.ExpressionNode);
                target.RootNode = (IPXNode)Fresh(source.RootNode);
                target.ExpressionNode = (IPXNode)Fresh(source.ExpressionNode);
                Replace(target, s => s.Node, source.Node);
            }

            if (all || part == PmxUpdateObject.Body)
            {
                Replace(target, s => s.Body, source.Body);
            }

            if (all || part == PmxUpdateObject.Joint)
            {
                Replace(target, s => s.Joint, source.Joint);
            }

            if (all || part == PmxUpdateObject.SoftBody)
            {
                Replace(target, s => s.SoftBody, source.SoftBody);
            }

            if (all)
            {
                target.ModelInfo.ModelName = source.ModelInfo.ModelName;
                target.ModelInfo.ModelNameE = source.ModelInfo.ModelNameE;
                target.ModelInfo.Comment = source.ModelInfo.Comment;
                target.ModelInfo.CommentE = source.ModelInfo.CommentE;
            }

            Relink(target, taken, old);
        }

        /// <summary>
        /// 並びを空にして、<paramref name="source"/> の要素の複製を並べる。エディタは反映の対象の
        /// 並びを複製で置き換えるので、前に並んでいた要素は並びから外れる。
        /// </summary>
        private static void Replace<T>(
            FakePmx state, Func<FakePmx, IList<T>> select, IList<T> source)
            where T : class
        {
            IList<T> list = select(state);
            List<T> made = new List<T>(source.Count);
            foreach (T item in source)
            {
                made.Add((T)Fresh(item));
            }

            T[] gone = list.ToArray();
            if (typeof(T) == typeof(IPXVertex) || typeof(T) == typeof(IPXBody)
                || typeof(T) == typeof(IPXJoint))
            {
                Places.GetOrCreateValue(state).Pending.Add(places =>
                {
                    for (int at = 0; at < gone.Length; at++)
                    {
                        int place = at;
                        if (gone[at] != null)
                        {
                            places[gone[at]] = s => Child(select(s), place);
                        }
                    }
                });
            }
            else
            {
                for (int at = 0; at < gone.Length; at++)
                {
                    int place = at;
                    Retire(state, gone[at], s => Child(select(s), place));
                }
            }

            list.Clear();
            foreach (T item in made)
            {
                list.Add(item);
            }
        }

        /// <summary>並びのその位置を、<paramref name="source"/> の同じ位置の要素の複製で置き換える。</summary>
        private static void Put<T>(
            FakePmx state, Func<FakePmx, IList<T>> select, IList<T> source, int index)
            where T : class
        {
            IList<T> list = select(state);
            Retire(state, list[index], s => Child(select(s), index));
            list[index] = (T)Fresh(source[index]);
        }

        /// <summary>
        /// <paramref name="held"/> が並びから外れたあとも、それが並んでいた位置に、いまの
        /// <paramref name="state"/> で並んでいる要素を返す。並びから外れていなければそのまま返し、
        /// その位置にもう要素が無ければ null を返す。
        /// </summary>
        public static T Now<T>(FakePmx state, T held)
            where T : class
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            Func<FakePmx, object> place;
            if (held == null || !Places.GetOrCreateValue(state).Flushed().TryGetValue(held, out place))
            {
                return held;
            }

            return (T)place(state);
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FakePmx, Retired> Places =
            new System.Runtime.CompilerServices.ConditionalWeakTable<FakePmx, Retired>();

        /// <summary>
        /// 並びから外れた要素から、それが並んでいた位置を引く表。頂点・剛体・Jointは、外れた
        /// 並びを <see cref="Pending"/> に取っておき、表を初めて引くときに書き込む。
        /// </summary>
        private sealed class Retired
        {
            public Dictionary<object, Func<FakePmx, object>> Places { get; } =
                new Dictionary<object, Func<FakePmx, object>>();

            public List<Action<Dictionary<object, Func<FakePmx, object>>>> Pending { get; } =
                new List<Action<Dictionary<object, Func<FakePmx, object>>>>();

            public Dictionary<object, Func<FakePmx, object>> Flushed()
            {
                foreach (Action<Dictionary<object, Func<FakePmx, object>>> write in Pending)
                {
                    write(Places);
                }

                Pending.Clear();

                return Places;
            }
        }

        /// <summary>
        /// 並びから外れる要素の位置を覚える。要素が持つ面・オフセット・表示枠の項目・IKの
        /// リンク・アンカーも、持ち主の位置の下の位置として覚える。
        /// </summary>
        private static void Retire(FakePmx state, object old, Func<FakePmx, object> place)
        {
            if (old == null)
            {
                return;
            }

            Retired retired = Places.GetOrCreateValue(state);
            if (old is IPXVertex || old is IPXBody || old is IPXJoint)
            {
                retired.Pending.Add(places => places[old] = place);

                return;
            }

            retired.Places[old] = place;
            RetireChildren(retired.Places, old, place);
        }

        private static void RetireChildren(
            Dictionary<object, Func<FakePmx, object>> places, object old, Func<FakePmx, object> place)
        {
            System.Collections.IList children =
                old is IPXMaterial ? (System.Collections.IList)((IPXMaterial)old).Faces
                : old is IPXMorph ? (System.Collections.IList)((IPXMorph)old).Offsets
                : old is IPXNode ? (System.Collections.IList)((IPXNode)old).Items
                : old is IPXBone ? (System.Collections.IList)((IPXBone)old).IK.Links
                : old is IPXSoftBody ? (System.Collections.IList)((IPXSoftBody)old).Anchors
                : null;
            if (children == null)
            {
                return;
            }

            for (int at = 0; at < children.Count; at++)
            {
                int index = at;
                if (children[at] == null || places.ContainsKey(children[at]))
                {
                    continue;
                }

                places[children[at]] = s => ChildOf(place(s), index);
            }
        }

        private static object ChildOf(object owner, int index)
        {
            System.Collections.IList children =
                owner is IPXMaterial ? (System.Collections.IList)((IPXMaterial)owner).Faces
                : owner is IPXMorph ? (System.Collections.IList)((IPXMorph)owner).Offsets
                : owner is IPXNode ? (System.Collections.IList)((IPXNode)owner).Items
                : owner is IPXBone ? (System.Collections.IList)((IPXBone)owner).IK.Links
                : owner is IPXSoftBody ? (System.Collections.IList)((IPXSoftBody)owner).Anchors
                : null;

            return children != null && index < children.Count ? children[index] : null;
        }

        private static object Child<T>(IList<T> list, int index)
        {
            return index < list.Count ? (object)list[index] : null;
        }

        private static object Child<T, TChild>(
            IList<T> owners, int owner, Func<T, IList<TChild>> children, int index)
        {
            return owner < owners.Count ? Child(children(owners[owner]), index) : null;
        }

        private static void Refill<T>(IList<T> list, IList<T> items)
        {
            List<T> made = items.ToList();
            list.Clear();
            foreach (T item in made)
            {
                list.Add(item);
            }
        }

        /// <summary>
        /// 渡された複製を、エディタが写す前に整える形へ直す。指す先が <paramref name="taken"/> の
        /// 並びに無い面・IKのリンク・オフセット・表示枠のボーン・固定頂点を外し、オフセットが全部
        /// 外れたモーフを消す。モーフを消したら真を返す。
        /// </summary>
        private static bool Tidy(IPXPmx work, PmxUpdateObject part, Tables taken)
        {
            bool removed = false;
            bool all = part == PmxUpdateObject.All;
            if (all || part == PmxUpdateObject.Vertex || part == PmxUpdateObject.Face)
            {
                foreach (IPXMaterial material in work.Material)
                {
                    for (int at = material.Faces.Count - 1; at >= 0; at--)
                    {
                        IPXFace face = material.Faces[at];
                        if (!Holds(taken.Vertex, face.Vertex1) || !Holds(taken.Vertex, face.Vertex2)
                            || !Holds(taken.Vertex, face.Vertex3))
                        {
                            material.Faces.RemoveAt(at);
                        }
                    }
                }
            }

            if (all || part == PmxUpdateObject.Bone)
            {
                foreach (IPXBone bone in work.Bone.Where(b => b.IsIK))
                {
                    if (!Holds(taken.Bone, bone.IK.Target))
                    {
                        bone.IsIK = false;
                        continue;
                    }

                    for (int at = bone.IK.Links.Count - 1; at >= 0; at--)
                    {
                        if (!Holds(taken.Bone, bone.IK.Links[at].Bone))
                        {
                            bone.IK.Links.RemoveAt(at);
                        }
                    }
                }
            }

            if (all || part == PmxUpdateObject.Vertex || part == PmxUpdateObject.Bone
                || part == PmxUpdateObject.Morph)
            {
                for (int at = work.Morph.Count - 1; at >= 0; at--)
                {
                    IPXMorph morph = work.Morph[at];
                    if (morph.IsMaterial || morph.IsFlip || morph.IsImpulse || morph.Offsets.Count == 0)
                    {
                        continue;
                    }

                    for (int offset = morph.Offsets.Count - 1; offset >= 0; offset--)
                    {
                        if (Dangling(morph.Offsets[offset], taken))
                        {
                            morph.Offsets.RemoveAt(offset);
                        }
                    }

                    if (morph.Offsets.Count == 0)
                    {
                        work.Morph.RemoveAt(at);
                        removed = true;
                    }
                }
            }

            if (all || part == PmxUpdateObject.Bone || part == PmxUpdateObject.Morph
                || part == PmxUpdateObject.Node)
            {
                foreach (IPXNode node in new[] { work.RootNode, work.ExpressionNode }.Concat(work.Node))
                {
                    for (int at = node.Items.Count - 1; at >= 0; at--)
                    {
                        IPXNodeItem item = node.Items[at];
                        if (item.IsBone && !Holds(taken.Bone, item.BoneItem.Bone))
                        {
                            node.Items.RemoveAt(at);
                        }
                    }
                }
            }

            if (all || part == PmxUpdateObject.SoftBody)
            {
                foreach (IPXSoftBody soft in work.SoftBody)
                {
                    for (int at = soft.Pins.Count - 1; at >= 0; at--)
                    {
                        if (!Holds(taken.Vertex, soft.Pins[at]))
                        {
                            soft.Pins.RemoveAt(at);
                        }
                    }
                }
            }

            return removed;
        }

        private static bool Dangling(IPXMorphOffset offset, Tables taken)
        {
            IPXGroupMorphOffset group = offset as IPXGroupMorphOffset;
            if (group != null)
            {
                return !Holds(taken.Morph, group.Morph);
            }

            IPXVertexMorphOffset vertex = offset as IPXVertexMorphOffset;
            if (vertex != null)
            {
                return !Holds(taken.Vertex, vertex.Vertex);
            }

            IPXBoneMorphOffset bone = offset as IPXBoneMorphOffset;
            if (bone != null)
            {
                return !Holds(taken.Bone, bone.Bone);
            }

            IPXUVMorphOffset uv = offset as IPXUVMorphOffset;

            return uv != null && !Holds(taken.Vertex, uv.Vertex);
        }

        private static bool Holds(IDictionary<object, int> table, object element)
        {
            return element != null && table.ContainsKey(element);
        }

        /// <summary>
        /// 要素を複製し、ベクトルも別のオブジェクトにする。指す先は元のまま残し、あとで
        /// <see cref="Relink"/> が繋ぎ直す。
        /// </summary>
        private static object Fresh(object element)
        {
            object made = ((ICloneable)element).Clone();
            IPXVertex vertex = made as IPXVertex;
            if (vertex != null)
            {
                vertex.SDEF_C = SdkVectors.Copy(vertex.SDEF_C);
                vertex.SDEF_R0 = SdkVectors.Copy(vertex.SDEF_R0);
                vertex.SDEF_R1 = SdkVectors.Copy(vertex.SDEF_R1);
            }

            IPXBone bone = made as IPXBone;
            if (bone != null && !bone.IsIK)
            {
                bone.IK.Target = null;
                bone.IK.LoopCount = 0;
                bone.IK.Angle = 0f;
                bone.IK.Links.Clear();
            }

            return made;
        }

        /// <summary>
        /// <paramref name="model"/> の全要素が指す先を、位置で繋ぎ直す。指す先が
        /// <paramref name="taken"/> か <paramref name="old"/> の並びにあれば、その位置にいまある
        /// 要素へ、どちらにも無ければ何も指さない形へ直す。
        /// </summary>
        private static void Relink(FakePmx model, Tables taken, Tables old)
        {
            Func<IPXVertex, IPXVertex> vertex =
                v => (IPXVertex)Mapped(v, taken.Vertex, old.Vertex, model.Vertex);
            Func<IPXMaterial, IPXMaterial> material =
                m => (IPXMaterial)Mapped(m, taken.Material, old.Material, model.Material);
            Func<IPXBone, IPXBone> bone = b => (IPXBone)Mapped(b, taken.Bone, old.Bone, model.Bone);
            Func<IPXMorph, IPXMorph> morph =
                m => (IPXMorph)Mapped(m, taken.Morph, old.Morph, model.Morph);
            Func<IPXBody, IPXBody> body = b => (IPXBody)Mapped(b, taken.Body, old.Body, model.Body);
            foreach (IPXVertex v in model.Vertex)
            {
                v.Bone1 = bone(v.Bone1);
                v.Bone2 = bone(v.Bone2);
                v.Bone3 = bone(v.Bone3);
                v.Bone4 = bone(v.Bone4);
            }

            foreach (IPXMaterial m in model.Material)
            {
                foreach (IPXFace face in m.Faces)
                {
                    face.Vertex1 = vertex(face.Vertex1);
                    face.Vertex2 = vertex(face.Vertex2);
                    face.Vertex3 = vertex(face.Vertex3);
                }
            }

            foreach (IPXBone b in model.Bone)
            {
                b.Parent = bone(b.Parent);
                b.ToBone = bone(b.ToBone);
                b.AppendParent = bone(b.AppendParent);
                b.IK.Target = bone(b.IK.Target);
                foreach (IPXIKLink link in b.IK.Links)
                {
                    link.Bone = bone(link.Bone);
                }
            }

            foreach (IPXMorph m in model.Morph)
            {
                foreach (IPXMorphOffset offset in m.Offsets)
                {
                    Relink(offset, vertex, material, bone, morph, body);
                }
            }

            foreach (IPXNode node in new[] { model.RootNode, model.ExpressionNode }.Concat(model.Node))
            {
                foreach (IPXNodeItem item in node.Items)
                {
                    if (item.IsBone)
                    {
                        item.BoneItem.Bone = bone(item.BoneItem.Bone);
                    }
                    else if (item.IsMorph)
                    {
                        item.MorphItem.Morph = morph(item.MorphItem.Morph);
                    }
                }
            }

            foreach (IPXBody b in model.Body)
            {
                b.Bone = bone(b.Bone);
            }

            foreach (IPXJoint j in model.Joint)
            {
                j.BodyA = body(j.BodyA);
                j.BodyB = body(j.BodyB);
            }

            foreach (IPXSoftBody s in model.SoftBody)
            {
                s.Material = material(s.Material);
                foreach (IPXSoftBodyAnchor anchor in s.Anchors)
                {
                    anchor.Body = body(anchor.Body);
                    anchor.Vertex = vertex(anchor.Vertex);
                }

                for (int at = 0; at < s.Pins.Count; at++)
                {
                    s.Pins[at] = vertex(s.Pins[at]);
                }
            }
        }

        private static void Relink(
            IPXMorphOffset offset,
            Func<IPXVertex, IPXVertex> vertex,
            Func<IPXMaterial, IPXMaterial> material,
            Func<IPXBone, IPXBone> bone,
            Func<IPXMorph, IPXMorph> morph,
            Func<IPXBody, IPXBody> body)
        {
            IPXVertexMorphOffset moved = offset as IPXVertexMorphOffset;
            if (moved != null)
            {
                moved.Vertex = vertex(moved.Vertex);
            }

            IPXUVMorphOffset uv = offset as IPXUVMorphOffset;
            if (uv != null)
            {
                uv.Vertex = vertex(uv.Vertex);
            }

            IPXBoneMorphOffset posed = offset as IPXBoneMorphOffset;
            if (posed != null)
            {
                posed.Bone = bone(posed.Bone);
            }

            IPXMaterialMorphOffset tinted = offset as IPXMaterialMorphOffset;
            if (tinted != null)
            {
                tinted.Material = material(tinted.Material);
            }

            IPXGroupMorphOffset grouped = offset as IPXGroupMorphOffset;
            if (grouped != null)
            {
                grouped.Morph = morph(grouped.Morph);
            }

            IPXImpulseMorphOffset pushed = offset as IPXImpulseMorphOffset;
            if (pushed != null)
            {
                pushed.Body = body(pushed.Body);
            }
        }

        private static object Mapped<T>(
            T pointed,
            IDictionary<object, int> taken,
            IDictionary<object, int> old,
            IList<T> now)
            where T : class
        {
            if (pointed == null)
            {
                return null;
            }

            int at;
            if (!taken.TryGetValue(pointed, out at) && !old.TryGetValue(pointed, out at))
            {
                return null;
            }

            return at < now.Count ? now[at] : null;
        }

        /// <summary>
        /// 要素から並びの中の位置を引く表。要素は同一性で見分ける。作った時点の並びを写し取り、
        /// 表は初めて引かれたときに作る。
        /// </summary>
        private sealed class Tables
        {
            private readonly Lazy<IDictionary<object, int>> _vertex;

            private readonly Lazy<IDictionary<object, int>> _material;

            private readonly Lazy<IDictionary<object, int>> _bone;

            private readonly Lazy<IDictionary<object, int>> _morph;

            private readonly Lazy<IDictionary<object, int>> _body;

            public Tables(IList<IPXBone> bones)
            {
                _vertex = _material = _morph = _body = Table(new object[0]);
                _bone = Table(bones);
            }

            public Tables(IPXPmx pmx)
            {
                _vertex = Table(pmx.Vertex);
                _material = Table(pmx.Material);
                _bone = Table(pmx.Bone);
                _morph = Table(pmx.Morph);
                _body = Table(pmx.Body);
            }

            public IDictionary<object, int> Vertex
            {
                get { return _vertex.Value; }
            }

            public IDictionary<object, int> Material
            {
                get { return _material.Value; }
            }

            public IDictionary<object, int> Bone
            {
                get { return _bone.Value; }
            }

            public IDictionary<object, int> Morph
            {
                get { return _morph.Value; }
            }

            public IDictionary<object, int> Body
            {
                get { return _body.Value; }
            }

            private static Lazy<IDictionary<object, int>> Table<T>(IList<T> list)
            {
                T[] held = list.ToArray();

                return new Lazy<IDictionary<object, int>>(
                    () =>
                    {
                        Dictionary<object, int> made =
                            new Dictionary<object, int>(held.Length, Identity.Instance);
                        for (int at = 0; at < held.Length; at++)
                        {
                            if (held[at] != null && !made.ContainsKey(held[at]))
                            {
                                made.Add(held[at], at);
                            }
                        }

                        return made;
                    },
                    System.Threading.LazyThreadSafetyMode.None);
            }
        }

        private sealed class Identity : IEqualityComparer<object>
        {
            public static readonly Identity Instance = new Identity();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }

    /// <summary>新しい要素を作る相手の題材。値を持たない空の要素を返す。</summary>
    public sealed class FakeBuilder : IPXPmxBuilder
    {
        public bool SystemNodesInList { get; set; }

        public IPXPmx Pmx()
        {
            FakePmx made = new FakePmx();
            if (SystemNodesInList)
            {
                made.Node.Add(made.RootNode);
                made.Node.Add(made.ExpressionNode);
            }

            return made;
        }

        public IPXVertex Vertex()
        {
            return new FakeVertex();
        }

        public IPXFace Face()
        {
            return new FakeFace();
        }

        public IPXMaterial Material()
        {
            return new FakeMaterial();
        }

        public IPXBone Bone()
        {
            return new FakeBone();
        }

        public IPXIKLink IKLink()
        {
            return new FakeIkLink();
        }

        public IPXIKLink IKLink(IPXBone bone)
        {
            return new FakeIkLink(bone);
        }

        public IPXIKLink IKLink(IPXBone bone, V3 low, V3 high)
        {
            return new FakeIkLink(bone) { IsLimit = true, Low = low, High = high };
        }

        /// <summary>作るモーフの種類。SDKの既定は頂点モーフ。</summary>
        public MorphKind MadeMorphKind { get; set; } = MorphKind.Vertex;

        /// <summary>SDKと同じく、パネルを4(その他)にしたモーフを作る。</summary>
        public IPXMorph Morph()
        {
            return new FakeMorph(kind: MadeMorphKind) { Panel = 4 };
        }

        public IPXVertexMorphOffset VertexMorphOffset()
        {
            return new FakeVertexMorphOffset();
        }

        public IPXVertexMorphOffset VertexMorphOffset(IPXVertex vertex, V3 offset)
        {
            return new FakeVertexMorphOffset(vertex) { Offset = offset };
        }

        public IPXUVMorphOffset UVMorphOffset()
        {
            return new FakeUVMorphOffset();
        }

        public IPXUVMorphOffset UVMorphOffset(IPXVertex vertex, V4 offset)
        {
            return new FakeUVMorphOffset(vertex) { Offset = offset };
        }

        public IPXBoneMorphOffset BoneMorphOffset()
        {
            return new FakeBoneMorphOffset();
        }

        public IPXBoneMorphOffset BoneMorphOffset(IPXBone bone, V3 translation, Q rotation)
        {
            return new FakeBoneMorphOffset(bone)
            {
                Translation = translation,
                Rotation = rotation,
            };
        }

        public IPXMaterialMorphOffset MaterialMorphOffset()
        {
            return new FakeMaterialMorphOffset();
        }

        public IPXGroupMorphOffset GroupMorphOffset()
        {
            return new FakeGroupMorphOffset();
        }

        public IPXGroupMorphOffset GroupMorphOffset(IPXMorph morph, float ratio)
        {
            return new FakeGroupMorphOffset(morph) { Ratio = ratio };
        }

        public IPXImpulseMorphOffset ImpulseMorphOffset()
        {
            return new FakeImpulseMorphOffset();
        }

        public IPXImpulseMorphOffset ImpulseMorphOffset(
            IPXBody body, bool local, V3 velocity, V3 torque)
        {
            return new FakeImpulseMorphOffset(body)
            {
                Local = local,
                Velocity = velocity,
                Torque = torque,
            };
        }

        public IPXNode Node()
        {
            return new FakeNode();
        }

        public IPXBoneNodeItem BoneNodeItem()
        {
            return new FakeBoneNodeItem();
        }

        public IPXBoneNodeItem BoneNodeItem(IPXBone bone)
        {
            return new FakeBoneNodeItem(bone);
        }

        public IPXMorphNodeItem MorphNodeItem()
        {
            return new FakeMorphNodeItem();
        }

        public IPXMorphNodeItem MorphNodeItem(IPXMorph morph)
        {
            return new FakeMorphNodeItem(morph);
        }

        public IPXBody Body()
        {
            return new FakeBody();
        }

        public IPXJoint Joint()
        {
            return new FakeJoint();
        }

        public IPXSoftBody SoftBody()
        {
            return new FakeSoftBody();
        }

        public IPXSoftBodyAnchor SoftBodyAnchor()
        {
            return new FakeSoftBodyAnchor();
        }
    }

    public sealed class FakePrimitiveBuilder : IPXPrimitiveBuilder, PXCPlugin.IPXCPrimitiveBuilder
    {
        public void AddPlane(int material, V3 pos, float width, float height, int uc, int vc, int dir, bool uv, IPXBone bone)
        {
            throw new NotSupportedException();
        }

        public void AddBox(int material, V3 pos, float width, float height, float depth, IPXBone bone)
        {
            throw new NotSupportedException();
        }

        public void AddSphere(int material, V3 pos, float r, int slices, int stacks, IPXBone bone)
        {
            throw new NotSupportedException();
        }

        public void AddCylinder(int material, V3 pos, float r1, float r2, float length, int slices, int stacks, IPXBone bone)
        {
            throw new NotSupportedException();
        }

        public void AddTorus(int material, V3 pos, float r1, float r2, int sides, int rings, IPXBone bone)
        {
            throw new NotSupportedException();
        }

        public void AddText(int material, V3 pos, System.Drawing.Font font, string text, float d, float ex, IPXBone bone)
        {
            throw new NotSupportedException();
        }

        public void AddPlane(IPXPmx pmx, int material, V3 pos, float width, float height, int uc, int vc, int dir, bool uv, IPXBone bone)
        {
            throw new NotSupportedException();
        }

        public void AddBox(IPXPmx pmx, int material, V3 pos, float width, float height, float depth, IPXBone bone)
        {
            throw new NotSupportedException();
        }

        public void AddSphere(IPXPmx pmx, int material, V3 pos, float r, int slices, int stacks, IPXBone bone)
        {
            throw new NotSupportedException();
        }

        public void AddCylinder(IPXPmx pmx, int material, V3 pos, float r1, float r2, float length, int slices, int stacks, IPXBone bone)
        {
            throw new NotSupportedException();
        }

        public void AddTorus(IPXPmx pmx, int material, V3 pos, float r1, float r2, int sides, int rings, IPXBone bone)
        {
            throw new NotSupportedException();
        }

        public void AddText(IPXPmx pmx, int material, V3 pos, System.Drawing.Font font, string text, float d, float ex, IPXBone bone)
        {
            throw new NotSupportedException();
        }
    }
}
