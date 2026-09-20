// 画面とリストの口と、その口へ渡すモーションの題材。使う口だけが値を持ち、それ以外は支えない。

using System;
using System.Collections.Generic;

namespace PmxEditorMcp.Tests
{
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

        /// <summary>画面を描き直した回数。</summary>
        public int Repaints { get; private set; }

        /// <summary>VMDViewへ読み込んだモデル。</summary>
        public PEPlugin.Pmx.IPXPmx Loaded { get; private set; }

        /// <summary>VMDViewへ読み込んだモーション。</summary>
        public PEPlugin.Vmd.IPEVmd Motion { get; private set; }

        /// <summary>VMDViewの再生を始めた回数。</summary>
        public int Plays { get; private set; }

        /// <summary>VMDViewの再生を止めた回数。</summary>
        public int Stops { get; private set; }

        /// <summary>VMDViewが立ち上がっているか。</summary>
        public bool Booted { get; set; }

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
            throw new NotSupportedException();
        }

        public bool Focus()
        {
            throw new NotSupportedException();
        }

        public bool[] GetBodyVisibles()
        {
            throw new NotSupportedException();
        }

        public System.Drawing.Bitmap GetClientImage()
        {
            throw new NotSupportedException();
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

        public void SetCameraView(PEPlugin.Pmd.IPEVector3 target, PEPlugin.Pmd.IPEVector3 position, PEPlugin.Pmd.IPEVector3 upVector)
        {
            throw new NotSupportedException();
        }

        public void SetJointVisibles(bool[] v)
        {
            throw new NotSupportedException();
        }

        public void SetVertexIndices(int[] indices)
        {
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }

        public void UpdateModel_Bone()
        {
            throw new NotSupportedException();
        }

        public void UpdateModel_Joint()
        {
            throw new NotSupportedException();
        }

        public void UpdateModel_Material(int index)
        {
            throw new NotSupportedException();
        }

        public void UpdateModel_Vertex()
        {
            throw new NotSupportedException();
        }

        public void UpdateModel_Weight()
        {
            throw new NotSupportedException();
        }

        public bool[] BodyVisible
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public PEPlugin.Pmd.IPEVector3 CameraPosition
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

        public PEPlugin.Pmd.IPEVector3 CameraTarget
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

        public PEPlugin.Pmd.IPEVector3 CameraUpVector
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

        public int RedoCount
        {
            get
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

        public int UndoCount
        {
            get
            {
                throw new NotSupportedException();
            }
        }

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

        public System.Collections.Generic.IList<PEPlugin.Vmd.IPEVmdBoneKey> Bone
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public System.Collections.Generic.IList<PEPlugin.Vmd.IPEVmdCameraKey> Camera
        {
            get
            {
                throw new NotSupportedException();
            }
        }

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

        public System.Collections.Generic.IList<PEPlugin.Vmd.IPEVmdLightKey> Light
        {
            get
            {
                throw new NotSupportedException();
            }
        }

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

        public System.Collections.Generic.IList<PEPlugin.Vmd.IPEVmdMorphKey> Morph
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public System.Collections.Generic.IList<PEPlugin.Vmd.IPEVmdSelfShadowKey> SelfShadow
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public System.Collections.Generic.IList<PEPlugin.Vmd.IPEVmdVisibleIKKey> VisibleIK
        {
            get
            {
                throw new NotSupportedException();
            }
        }

    }
}
