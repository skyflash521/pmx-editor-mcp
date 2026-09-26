// 画面とリストの口と、その口へ渡すモーションの題材。使う口だけが値を持ち、それ以外は支えない。

using System;
using System.Collections.Generic;

namespace PmxEditorMcp.Tests
{
    /// <summary>ビューの表示の設定の題材。面の選択が画面に出るかだけを持つ。</summary>
    public sealed class FakeViewSetting : PEPlugin.View.IPEViewSettingConnector
    {
        /// <summary>選んだ面を画面に出すか。</summary>
        public bool Visible_SelectedFace { get; set; } = true;

        public int SelectedTabPage
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public System.Drawing.Color BackColor
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public System.Drawing.Color AmbientColor
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public System.Drawing.Color LightColor
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public PEPlugin.Pmd.IPEVector3 LightDirection
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool Visible_Bone
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool Visible_Vertex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool Visible_SelectedVertex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool Visible_UnvisibleVertex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool Visible_Normal
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool Visible_SelectedNormal
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool Visible_Body
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool Visible_SolidBody
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool Visible_Joint
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool Visible_WeightMap
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool OnlyWeighting
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public System.Drawing.Color NonWeightingVertexColor
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public float ModelSize
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public float Perspective
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public PEPlugin.View.AntiAliasingType AAType
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool ColorBlending
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public PEPlugin.View.FillMode FillMode
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public float VertexPointSize
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public System.Drawing.Color VertexPointColor
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public System.Drawing.Color SelectedVertexPointColor
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public float NormalLength
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public System.Drawing.Color NormalColor
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public System.Drawing.Color SelectedNormalColor
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public float JointPointSize
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public PEPlugin.View.ToonType ToonType
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool Edge
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public float EdgeSize
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public void InitializeLight()
        {
            throw new NotSupportedException();
        }

        public void InitializeViewSetting()
        {
            throw new NotSupportedException();
        }

        public bool LoadViewSetting(string path)
        {
            throw new NotSupportedException();
        }

        public void SaveViewSetting(string path)
        {
            throw new NotSupportedException();
        }

        public bool Focus()
        {
            throw new NotSupportedException();
        }

        public System.Drawing.Point Location
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool Visible
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }
    }

    public sealed class FakePmxView : PEPlugin.View.IPXPmxViewConnector
    {
        /// <summary>種類ごとの、画面で選ばれている位置。</summary>
        public IDictionary<string, int[]> Selected { get; } =
            new Dictionary<string, int[]>(StringComparer.Ordinal)
            {
                { ElementKinds.Vertex, new int[0] },
                { ElementKinds.Face, new int[0] },
                { ElementKinds.Bone, new int[0] },
                { ElementKinds.Body, new int[0] },
                { ElementKinds.Joint, new int[0] },
            };

        /// <summary>描画を作り直した回数。</summary>
        public int Redraws { get; private set; }

        /// <summary>画面を映し直す頼みを落とすか。真なら描き直しの頼みが例外で終わる。</summary>
        public bool RefusesToPaint { get; set; }

        /// <summary>画面を描き直した回数。</summary>
        public int Repaints { get; private set; }

        /// <summary>区分だけを作り直した先を、頼まれた順に並べたもの。</summary>
        public IList<string> Remade { get; } = new List<string>();

        /// <summary>VMDViewへ読み込んだモデル。</summary>
        public PEPlugin.Pmx.IPXPmx Loaded { get; private set; }

        /// <summary>VMDViewへ読み込んだPMDのモデル。</summary>
        public PEPlugin.Pmd.IPEPmd Older { get; private set; }

        /// <summary>VMDViewへ読み込んだモーション。</summary>
        public PEPlugin.Vmd.IPEVmd Motion { get; private set; }

        /// <summary>VMDViewの再生を始めた回数。</summary>
        public int Plays { get; private set; }

        /// <summary>VMDViewの再生を止めた回数。</summary>
        public int Stops { get; private set; }

        /// <summary>VMDViewが立ち上がっているか。</summary>
        public bool Booted { get; set; }

        public int[] Narrowed { get; private set; } = new int[0];

        public int[] GetSelectedVertexIndices()
        {
            return Selected[ElementKinds.Vertex];
        }

        public void SetSelectedVertexIndices(int[] indices)
        {
            Selected[ElementKinds.Vertex] = indices;
        }

        public int[] GetSelectedFaceIndices()
        {
            return Selected[ElementKinds.Face];
        }

        public void SetSelectedFaceIndices(int[] indices)
        {
            Selected[ElementKinds.Face] = indices;
        }

        public int[] GetSelectedBoneIndices()
        {
            return Selected[ElementKinds.Bone];
        }

        public void SetSelectedBoneIndices(int[] indices)
        {
            Selected[ElementKinds.Bone] = indices;
        }

        public int[] GetSelectedBodyIndices()
        {
            return Selected[ElementKinds.Body];
        }

        public void SetSelectedBodyIndices(int[] indices)
        {
            Selected[ElementKinds.Body] = indices;
        }

        public int[] GetSelectedJointIndices()
        {
            return Selected[ElementKinds.Joint];
        }

        public void SetSelectedJointIndices(int[] indices)
        {
            Selected[ElementKinds.Joint] = indices;
        }

        public PEPlugin.Pmd.IPEVector3 CameraRotateCenter { get; set; }

        public bool IsVmdViewBootup
        {
            get { return Booted; }
        }

        public void UpdateModel()
        {
            Redraws++;
        }

        public void UpdateView()
        {
            if (RefusesToPaint)
            {
                throw new InvalidOperationException("画面を映し直せない。");
            }

            Repaints++;
        }

        public void BootupVmdView(PEPlugin.Pmx.IPXPmx pmx, PEPlugin.Vmd.IPEVmd vmd)
        {
            Loaded = pmx;
            Motion = vmd;
            Booted = true;
        }

        public void PlayVmdView()
        {
            Plays++;
        }

        public void StopVmdView()
        {
            Stops++;
            Motion = null;
        }

        public void BootupVmdView()
        {
            throw new NotSupportedException();
        }

        public void BootupVmdView(PEPlugin.Pmd.IPEPmd pmd, PEPlugin.Vmd.IPEVmd vmd)
        {
            Older = pmd;
            Motion = vmd;
            Booted = true;
        }

        public bool Focus()
        {
            throw new NotSupportedException();
        }

        public bool[] GetBodyVisibles()
        {
            throw new NotSupportedException();
        }

        /// <summary>画像を撮ったときの視点。撮るたびに置き換わる。</summary>
        public PEPlugin.Pmd.IPEVector3 ShotFrom { get; private set; }

        /// <summary>画像を撮った回数。</summary>
        public int Shots { get; private set; }

        /// <summary>最後に渡した画像。手放されたかをここで見る。</summary>
        public System.Drawing.Bitmap LastShot { get; private set; }

        public System.Drawing.Bitmap GetClientImage()
        {
            ShotFrom = CameraPositionSet;
            Shots++;
            LastShot = new System.Drawing.Bitmap(2, 2);

            return LastShot;
        }

        public bool[] GetJointVisibles()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.SDX.M GetProjectionMatrix(int screen)
        {
            throw new NotSupportedException();
        }

        public int[] GetVertexIndices()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEVector3[] GetViewAxis()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.SDX.M GetViewMatrix(int screen)
        {
            throw new NotSupportedException();
        }

        public void SetBodyVisibles(bool[] v)
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEVector3 CameraTargetSet { get; private set; }

        public PEPlugin.Pmd.IPEVector3 CameraPositionSet { get; private set; }

        public PEPlugin.Pmd.IPEVector3 CameraUpSet { get; private set; }

        public void SetCameraView(PEPlugin.Pmd.IPEVector3 target, PEPlugin.Pmd.IPEVector3 position, PEPlugin.Pmd.IPEVector3 upVector)
        {
            CameraTargetSet = target;
            CameraPositionSet = position;
            CameraUpSet = upVector;
        }

        public void SetJointVisibles(bool[] v)
        {
            throw new NotSupportedException();
        }

        public void SetVertexIndices(int[] indices)
        {
            Narrowed = indices;
        }

        public void SetVmeEvent(PEPlugin.Vme.IPEVme vme, int begin, int end)
        {
            throw new NotSupportedException();
        }

        public void SetVmeEvent(PEPlugin.Vme.IPEVmeResult result, int begin, int end)
        {
            throw new NotSupportedException();
        }

        public void UpdateModelSize(float scale)
        {
            throw new NotSupportedException();
        }

        public void UpdateModelSize(float scale, bool edge)
        {
            throw new NotSupportedException();
        }

        public void UpdateModel_Body()
        {
            Remade.Add(ElementKinds.Body);
        }

        public void UpdateModel_Bone()
        {
            Remade.Add(ElementKinds.Bone);
        }

        public void UpdateModel_Joint()
        {
            Remade.Add(ElementKinds.Joint);
        }

        public void UpdateModel_Material(int index)
        {
            throw new NotSupportedException();
        }

        public void UpdateModel_Vertex()
        {
            Remade.Add(ElementKinds.Vertex);
        }

        public void UpdateModel_Weight()
        {
            Remade.Add(ScreenRefresh.WeightKind);
        }

        public bool[] BodyVisible
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public PEPlugin.Pmd.IPEVector3 CameraPosition { get; set; }

        public PEPlugin.Pmd.IPEVector3 CameraTarget { get; set; }

        public PEPlugin.Pmd.IPEVector3 CameraUpVector { get; set; }

        public bool EnableCameraVmdView
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool EnableHandleEdit
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool IsShaderMode
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public System.Drawing.Point Location
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedBodyIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedJointIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool ShowBoneVmdView
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public System.Drawing.Size Size
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool Visible
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public System.Windows.Forms.FormWindowState WindowState
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

    }

    public sealed class FakeFormConnector : PEPlugin.Form.IPEFormConnector
    {
        /// <summary>作り直したリストの種類を、渡された順に控える。</summary>
        public IList<PEPlugin.Pmd.UpdateObject> Updated { get; } =
            new List<PEPlugin.Pmd.UpdateObject>();

        /// <summary>材質のリストで選ばれている位置。</summary>
        public int[] SelectedMaterials { get; set; } = new int[0];

        /// <summary>ボーンのリストで選ばれている位置。</summary>
        public int SelectedBoneIndex { get; set; }

        public void UpdateList(PEPlugin.Pmd.UpdateObject target)
        {
            Updated.Add(target);
        }

        public int[] GetSelectedMaterialIndices()
        {
            return SelectedMaterials;
        }

        public void SetSelectedMaterialIndices(int[] indices)
        {
            SelectedMaterials = indices;
        }

        public bool AppendPMDFile(string path)
        {
            throw new NotSupportedException();
        }

        public bool AppendXFile(string path)
        {
            throw new NotSupportedException();
        }

        public void Close()
        {
            throw new NotSupportedException();
        }

        public System.Drawing.Color[] GetBodyGroupColors()
        {
            throw new NotSupportedException();
        }

        public System.Drawing.Color[] GetBodyModeColors()
        {
            throw new NotSupportedException();
        }

        public System.Drawing.Color[] GetBoneKindColors()
        {
            throw new NotSupportedException();
        }

        public System.Drawing.Color[] GetExpressionCategoryColors()
        {
            throw new NotSupportedException();
        }

        public bool ImportXFile(string path)
        {
            throw new NotSupportedException();
        }

        public void InitializePMD()
        {
            throw new NotSupportedException();
        }

        public void InitializePMX()
        {
            throw new NotSupportedException();
        }

        public bool OpenPMDFile(string path)
        {
            throw new NotSupportedException();
        }

        public bool OpenPMXFile(string path)
        {
            throw new NotSupportedException();
        }

        public void Redo()
        {
            throw new NotSupportedException();
        }

        public void SaveMaterialName(string path)
        {
            throw new NotSupportedException();
        }

        public void SavePMDFile(string path)
        {
            throw new NotSupportedException();
        }

        public void SavePMXFile(string path)
        {
            throw new NotSupportedException();
        }

        public void Undo()
        {
            throw new NotSupportedException();
        }

        public int BodyItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public int BoneItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public bool EnableFacePage
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool EnableTexUpdateWatch
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int ExpressionItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public int ExpressionOffsetItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public int FaceItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public int FrameBoneItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public int FrameBone_BoneItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public int FrameExpressionItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public int IKItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public int IKLinkItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public int JointItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public System.Drawing.Point Location
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int MaterialItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public int MorphItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public int NodeElementItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public int NodeItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public bool PmxFormActivate
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int RedoCount { get; set; }

        public int SelectedBodyIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedExpressionIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedExpressionOffsetIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedFaceIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedFrameBoneIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedFrameBone_BoneIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedFrameExpressionIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedIKIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedIKLinkIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedJointIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedMaterialIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedTabPage
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedVertexIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool TopMost
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int UndoCount { get; set; }

        public int VertexItemsCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

    }

    public sealed class FakeVmd : PEPlugin.Vmd.IPEVmd
    {
        /// <summary>読み込んだモーションのファイルの道。読んでいなければ空。</summary>
        public string Path { get; private set; }

        /// <summary>種類ごとにキーを1つずつ持たせる。</summary>
        public void Fill()
        {
            Bone.Add(null);
            Morph.Add(null);
            VisibleIK.Add(null);
            Camera.Add(null);
            Light.Add(null);
            SelfShadow.Add(null);
        }

        public void FromFile(string path)
        {
            Path = path;
        }

        public void ClearKeys()
        {
            throw new NotSupportedException();
        }

        public object Clone()
        {
            throw new NotSupportedException();
        }

        public int GetBoneIndex(string name)
        {
            throw new NotSupportedException();
        }

        public string GetBoneName(int index)
        {
            throw new NotSupportedException();
        }

        public string[] GetBoneNames()
        {
            throw new NotSupportedException();
        }

        public int GetMorphIndex(string name)
        {
            throw new NotSupportedException();
        }

        public string GetMorphName(int index)
        {
            throw new NotSupportedException();
        }

        public string[] GetMorphNames()
        {
            throw new NotSupportedException();
        }

        public void Init(PEPlugin.Pmd.IPEPmd pmd)
        {
            throw new NotSupportedException();
        }

        public void Init(PEPlugin.Pmx.IPXPmx pmx)
        {
            throw new NotSupportedException();
        }

        public void NormalizeKeys()
        {
            throw new NotSupportedException();
        }

        public void SetBoneNames(string[] names)
        {
            throw new NotSupportedException();
        }

        public void SetModelNameForCameraLight()
        {
            throw new NotSupportedException();
        }

        public void SetMorphNames(string[] names)
        {
            throw new NotSupportedException();
        }

        public void SetNamesFromPmd(PEPlugin.Pmd.IPEPmd pmd)
        {
            throw new NotSupportedException();
        }

        public void ToFile(string path, bool trimKeys)
        {
            throw new NotSupportedException();
        }

        public void TrimBoneKeys()
        {
            throw new NotSupportedException();
        }

        public void TrimCameraKeys()
        {
            throw new NotSupportedException();
        }

        public void TrimKeys()
        {
            throw new NotSupportedException();
        }

        public void TrimLightKeys()
        {
            throw new NotSupportedException();
        }

        public void TrimMorphKeys()
        {
            throw new NotSupportedException();
        }

        public void TrimStartBlankKeys()
        {
            throw new NotSupportedException();
        }

        public System.Collections.Generic.IList<PEPlugin.Vmd.IPEVmdBoneKey> Bone { get; } =
            new System.Collections.Generic.List<PEPlugin.Vmd.IPEVmdBoneKey>();

        public System.Collections.Generic.IList<PEPlugin.Vmd.IPEVmdCameraKey> Camera { get; } =
            new System.Collections.Generic.List<PEPlugin.Vmd.IPEVmdCameraKey>();

        public string FilePath
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public System.Collections.Generic.IList<PEPlugin.Vmd.IPEVmdLightKey> Light { get; } =
            new System.Collections.Generic.List<PEPlugin.Vmd.IPEVmdLightKey>();

        public string ModelName
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public System.Collections.Generic.IList<PEPlugin.Vmd.IPEVmdMorphKey> Morph { get; } =
            new System.Collections.Generic.List<PEPlugin.Vmd.IPEVmdMorphKey>();

        public System.Collections.Generic.IList<PEPlugin.Vmd.IPEVmdSelfShadowKey> SelfShadow { get; } =
            new System.Collections.Generic.List<PEPlugin.Vmd.IPEVmdSelfShadowKey>();

        public System.Collections.Generic.IList<PEPlugin.Vmd.IPEVmdVisibleIKKey> VisibleIK { get; } =
            new System.Collections.Generic.List<PEPlugin.Vmd.IPEVmdVisibleIKKey>();

    }

    public sealed class FakeSubView : PEPlugin.View.IPESubViewConnector
    {
        public int Redrawn { get; private set; }

        public void UpdateView()
        {
            Redrawn++;
        }

        public System.Drawing.Bitmap GetClientImage()
        {
            throw new NotSupportedException();
        }

        public bool Focus()
        {
            throw new NotSupportedException();
        }

        public System.Drawing.Point Location
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public bool Visible
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public System.Drawing.Size Size
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public System.Windows.Forms.FormWindowState WindowState
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public bool[] GetBodyVisibles()
        {
            throw new NotSupportedException();
        }

        public bool[] GetJointVisibles()
        {
            throw new NotSupportedException();
        }

        public void SetBodyVisibles(bool[] visibles)
        {
            throw new NotSupportedException();
        }

        public void SetJointVisibles(bool[] visibles)
        {
            throw new NotSupportedException();
        }
    }

    public sealed class FakePartsSelect : PEPlugin.View.IPEPartsSelectConnector
    {
        public int[] Checked { get; set; } = new int[0];

        public int MaterialItemsCount { get; set; } = 1;

        public int BoneItemsCount { get; set; } = 1;

        public int ExpressionItemsCount { get; set; } = 1;

        public bool Visible { get; set; }

        public int[] GetCheckedMaterialIndices()
        {
            return Checked;
        }

        public void SetCheckedMaterialIndices(int[] indices)
        {
            Checked = indices;
        }

        public bool Focus()
        {
            throw new NotSupportedException();
        }

        public int[] GetCheckedBoneIndices()
        {
            throw new NotSupportedException();
        }

        public int[] GetCheckedExpressionIndices()
        {
            throw new NotSupportedException();
        }

        public void SetCheckedBoneIndices(int[] indices)
        {
            throw new NotSupportedException();
        }

        public void SetCheckedExpressionIndices(int[] indices)
        {
            throw new NotSupportedException();
        }

        public bool BoneSelected
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public System.Drawing.Point Location
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int RangeBegin
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int RangeEnd
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public PEPlugin.View.PartsSelectObject SelectObject
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedBoneIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedExpressionIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int SelectedMaterialIndex
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool SelectedPartsVisible
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

    }

    public sealed class FakeHostBuilder : PEPlugin.IPEBuilder
    {
        public FakeBuilder PmxBuilder { get; } = new FakeBuilder();

        /// <summary>PMDとして読んだファイルの道。読んでいなければ空。</summary>
        public string OlderPath { get; private set; }

        /// <summary>作って渡したVMD。</summary>
        public FakeVmd Motion { get; } = new FakeVmd();

        public PEPlugin.Vmd.IPEVmd CreateVmd()
        {
            return Motion;
        }

        public PEPlugin.IPXPmxBuilder Pmx
        {
            get { return PmxBuilder; }
        }

        public PEPlugin.Pmd.IPEBody CreateBody()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEBone CreateBone()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEExpression CreateExpression()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEExpressionOffset CreateExpressionOffset()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEFrameBone CreateFrameBone()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEHeader CreateHeader()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEIK CreateIK()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEJoint CreateJoint()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEMaterial CreateMaterial()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEPmd CreatePmd()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEPmd CreatePmd(string path)
        {
            OlderPath = path;

            return null;
        }

        public PEPlugin.Pmd.IPEVector2 CreateVector2()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEVector2 CreateVector2(float x, float y)
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEVector3 CreateVector3()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEVector3 CreateVector3(float x, float y, float z)
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEVector4 CreateVector4()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEVector4 CreateVector4(float x, float y, float z, float w)
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEVertex CreateVertex()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vmd.IPEVmd CreateVmd(PEPlugin.Pmd.IPEPmd pmd)
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vmd.IPEVmd CreateVmd(PEPlugin.Pmd.IPEPmd pmd, string vmdPath)
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vmd.IPEVmd CreateVmd(string[] boneNames, string[] morphNames)
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vmd.IPEVmdCameraKey CreateVmdBasCameraKey()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vmd.IPEVmdBoneKey CreateVmdBoneKey()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vmd.IPEVmdBonePoseState CreateVmdBonePoseState()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vmd.IPEVmdBonePoseState CreateVmdBonePoseState(PEPlugin.Vmd.IPEVmd vmd, int boneIndex)
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vmd.IPEVmdBonePoseState[] CreateVmdBonePoseState(PEPlugin.Vmd.IPEVmd vmd, int[] boneIndices)
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vmd.IPEVmdIPL CreateVmdIPL()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vmd.IPEVmdLightKey CreateVmdLightKey()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vmd.IPEVmdMorphKey CreateVmdMorphKey()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vmd.IPEVmdSelfShadowKey CreateVmdSelfShadowKey()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vme.IPEVme CreateVme()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vme.IPEVme CreateVme(PEPlugin.Pmd.IPEPmd pmd)
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vme.IPEVmeGroup CreateVmeGroup()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Vme.IPEVmePath CreateVmePath()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEXBody CreateXBody()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEXBone CreateXBone()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEXExpression CreateXExpression()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEXExpressionOffset CreateXExpressionOffset()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEXFace CreateXFace()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEXFrameBone CreateXFrameBone()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEXIK CreateXIK()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEXJoint CreateXJoint()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEXMaterial CreateXMaterial()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEXPmd CreateXPmd()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEXPmd CreateXPmd(PEPlugin.Pmd.IPEPmd pmd)
        {
            throw new NotSupportedException();
        }

        public PEPlugin.Pmd.IPEXVertex CreateXVertex()
        {
            throw new NotSupportedException();
        }

        public PEPlugin.IPEShortBuilder SC
        {
            get
            {
                throw new NotSupportedException();
            }
        }

    }
}
