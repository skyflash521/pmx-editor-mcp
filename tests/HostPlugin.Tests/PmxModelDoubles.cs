using System;
using System.Collections.Generic;
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
            return new FakeVertex
            {
                Position = Position,
                Normal = Normal,
                UV = UV,
                UVA1 = UVA1,
                UVA2 = UVA2,
                UVA3 = UVA3,
                UVA4 = UVA4,
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
                Diffuse = Diffuse,
                Specular = Specular,
                Power = Power,
                Ambient = Ambient,
                BothDraw = BothDraw,
                Shadow = Shadow,
                SelfShadowMap = SelfShadowMap,
                SelfShadow = SelfShadow,
                Edge = Edge,
                VertexColor = VertexColor,
                PrimitiveType = PrimitiveType,
                EdgeColor = EdgeColor,
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
            return new FakeIkLink(Bone) { IsLimit = IsLimit, Low = Low, High = High };
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

        /// <summary>簡易設定で入れたローカル軸のX。読み戻す口がSDKに無いのでここへ残す。</summary>
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

        public void SetPMDBoneKind(BoneKind kind)
        {
            PmdKind = kind;
        }

        public object Clone()
        {
            FakeBone made = new FakeBone(Name)
            {
                NameE = NameE,
                Position = Position,
                Parent = Parent,
                Level = Level,
                ToOffset = ToOffset,
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
                FixAxis = FixAxis,
                IsLocalFrame = IsLocalFrame,
                IsAfterPhysics = IsAfterPhysics,
                IsExternal = IsExternal,
                ExternalKey = ExternalKey,
            };
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
            return new FakeVertexMorphOffset(Vertex) { Offset = Offset };
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
            return new FakeUVMorphOffset(Vertex) { Offset = Offset };
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
            return new FakeBoneMorphOffset(Bone) { Translation = Translation, Rotation = Rotation };
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
                Diffuse = Diffuse,
                Specular = Specular,
                Power = Power,
                Ambient = Ambient,
                EdgeColor = EdgeColor,
                EdgeSize = EdgeSize,
                Tex = Tex,
                Sphere = Sphere,
                Toon = Toon,
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
                Velocity = Velocity,
                Torque = Torque,
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
                BoxSize = BoxSize,
                Position = Position,
                Rotation = Rotation,
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
                Position = Position,
                Rotation = Rotation,
                Limit_MoveLow = Limit_MoveLow,
                Limit_MoveHigh = Limit_MoveHigh,
                Limit_AngleLow = Limit_AngleLow,
                Limit_AngleHigh = Limit_AngleHigh,
                SpringConst_Move = SpringConst_Move,
                SpringConst_Rotate = SpringConst_Rotate,
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

        public IPXNode RootNode { get; } = new FakeNode("Root");

        public IPXNode ExpressionNode { get; } = new FakeNode("表情");

        public IList<IPXNode> Node { get; } = new List<IPXNode>();

        public IList<IPXBody> Body { get; } = new List<IPXBody>();

        public IList<IPXJoint> Joint { get; } = new List<IPXJoint>();

        public IList<IPXSoftBody> SoftBody { get; } = new List<IPXSoftBody>();

        public IPXPrimitiveBuilder Primitive
        {
            get { throw new NotSupportedException(); }
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
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }

        public object Clone()
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>新しい要素を作る相手の題材。値を持たない空の要素を返す。</summary>
    public sealed class FakeBuilder : IPXPmxBuilder
    {
        public IPXPmx Pmx()
        {
            return new FakePmx();
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

        public IPXMorph Morph()
        {
            return new FakeMorph();
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
}
