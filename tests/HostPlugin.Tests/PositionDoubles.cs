using System;
using System.Drawing;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using PEPlugin.Vmd;
using PEPlugin.Vme;
using PXCPlugin;
using PXCPlugin.Event;
using PXCPlugin.UIModel;

namespace PmxEditorMcp.Tests
{
    internal sealed class FakeVmeGroup : IPEVmeGroupBone, IPEVmeGroupMorph
    {
        public FakeVmeGroup(int count)
        {
            GroupCount = count;
        }

        public int GroupCount { get; }

        public int EventCount
        {
            get { throw new NotSupportedException(); }
        }

        public int LastFrame
        {
            get { throw new NotSupportedException(); }
        }

        public bool IsRunning
        {
            get { throw new NotSupportedException(); }
        }

        public void ClearGroup()
        {
            throw new NotSupportedException();
        }

        public void AddElement(IPEVmeEventElement el)
        {
            throw new NotSupportedException();
        }

        public void AddElements(params IPEVmeEventElement[] elArr)
        {
            throw new NotSupportedException();
        }

        public void RemoveElement(IPEVmeEventElement el)
        {
            throw new NotSupportedException();
        }

        public IPEVmeEventElement GetElement(int index)
        {
            throw new NotSupportedException();
        }

        public IPEVmeEventElement[] GetGroupElements()
        {
            throw new NotSupportedException();
        }

        public void ForEach<T>(Action<T> proc)
            where T : IPEVmeEventElement
        {
            throw new NotSupportedException();
        }

        public void ForEach<T>(Action<int, T> proc)
            where T : IPEVmeEventElement
        {
            throw new NotSupportedException();
        }

        public void AddBone(IPEVmeBone el)
        {
            throw new NotSupportedException();
        }

        public void AddBones(params IPEVmeBone[] elArr)
        {
            throw new NotSupportedException();
        }

        public void RemoveBone(IPEVmeBone el)
        {
            throw new NotSupportedException();
        }

        public IPEVmeBone[] GetGroupBones()
        {
            throw new NotSupportedException();
        }

        public IPEVmeBone GetBone(int boneIndex)
        {
            throw new NotSupportedException();
        }

        public void AddMorph(IPEVmeSingleValueElement el)
        {
            throw new NotSupportedException();
        }

        public void AddMorphs(params IPEVmeSingleValueElement[] elArr)
        {
            throw new NotSupportedException();
        }

        public void RemoveMorph(IPEVmeSingleValueElement el)
        {
            throw new NotSupportedException();
        }

        public IPEVmeSingleValueElement[] GetGroupMorphs()
        {
            throw new NotSupportedException();
        }

        public IPEVmeSingleValueElement GetMorph(int boneIndex)
        {
            throw new NotSupportedException();
        }

        public void ClearEvents()
        {
            throw new NotSupportedException();
        }

        public void SetEvent(int frame, PEVmeEvent ev, int id = 0)
        {
            throw new NotSupportedException();
        }

        public void SetEvent(int frame, Action ev, int id = 0)
        {
            throw new NotSupportedException();
        }

        public void SetEvent(int frameSt, int frameEd, PEVmeEvent ev, int id = 0)
        {
            throw new NotSupportedException();
        }

        public void SetEvent(int frameSt, int frameEd, Action ev, int id = 0)
        {
            throw new NotSupportedException();
        }

        public PEVmeEvent[] GetEvents(int frame, int id = 0)
        {
            throw new NotSupportedException();
        }

        public void RemoveEvent(PEVmeEvent ev)
        {
            throw new NotSupportedException();
        }

        public void RemoveEvent(int frame, int id = 0)
        {
            throw new NotSupportedException();
        }

        public void RunEvent(int frame, int id = 0)
        {
            throw new NotSupportedException();
        }
    }

    internal sealed class FakePoseState : IPEVmdBonePoseState
    {
        public void FromVmd(IPEVmd vmd, int boneIndex)
        {
            throw new NotSupportedException();
        }

        public void FromPoseArray(PEVmdBonePose[] arr)
        {
            throw new NotSupportedException();
        }

        public PEVmdBonePose[] ToPoseArray()
        {
            throw new NotSupportedException();
        }

        public IPEVmdBoneKey[] ToBoneKeyArray(int boneIndex, int startOffset = 0)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public PEVmdBonePose GetPose(int frame)
        {
            throw new NotSupportedException();
        }
    }

    internal sealed class FakeSystemControl : IPXSystemControl
    {
        public int PEPluginCount
        {
            get { return PositionPremise.Counts["plugin.pe"]; }
        }

        public int CPluginCount
        {
            get { return PositionPremise.Counts["plugin.c"]; }
        }

        public int[] FindPEPlugins(string menuText, bool contains = true)
        {
            throw new NotSupportedException();
        }

        public PXPluginInfo GetPEPluginInfo(int n)
        {
            throw new NotSupportedException();
        }

        public void RunPEPlugin(int n)
        {
            throw new NotSupportedException();
        }

        public int[] FindCPlugins(string menuText, bool contains = true)
        {
            throw new NotSupportedException();
        }

        public PXPluginInfo GetCPluginInfo(int n)
        {
            throw new NotSupportedException();
        }

        public PXPluginInfo GetCPluginInfo(IPXCPlugin plugin)
        {
            throw new NotSupportedException();
        }

        public void RunCPlugin(int n)
        {
            throw new NotSupportedException();
        }

        public bool SetShareData(string key, int data)
        {
            throw new NotSupportedException();
        }

        public bool SetShareData(string key, string data)
        {
            throw new NotSupportedException();
        }

        public bool SetShareData(string key, byte[] data)
        {
            throw new NotSupportedException();
        }

        public int? GetShareValue(string key, bool clear = false)
        {
            throw new NotSupportedException();
        }

        public string GetShareText(string key, bool clear = false)
        {
            throw new NotSupportedException();
        }

        public byte[] GetShareBuffer(string key, bool clear = false)
        {
            throw new NotSupportedException();
        }

        public bool RemoveShareData(string key)
        {
            throw new NotSupportedException();
        }
    }

    internal sealed class FakeUiModel : IPXUIModel
    {
        public string Name
        {
            get { throw new NotSupportedException(); }
        }

        public bool Visible { get; set; }

        public int DrawMode { get; set; }

        public bool Light { get; set; }

        public bool Depth { get; set; }

        public bool TopMost { get; set; }

        public bool FixedDrawScale { get; set; }

        public void Release()
        {
            throw new NotSupportedException();
        }

        public void SetAutoRelease(IPXCPlugin plugin)
        {
            throw new NotSupportedException();
        }

        public void SetBillboard(int type)
        {
            throw new NotSupportedException();
        }

        public void SetWorld(M m)
        {
            throw new NotSupportedException();
        }

        public M GetWorld()
        {
            throw new NotSupportedException();
        }

        public void SetBone(int bx, V3 scale, Q rotate, V3 translate)
        {
            throw new NotSupportedException();
        }

        public void SetBoneScale(int bx, V3 scale)
        {
            throw new NotSupportedException();
        }

        public void SetBoneRotate(int bx, Q rotate)
        {
            throw new NotSupportedException();
        }

        public void SetBoneTranslate(int bx, V3 translate)
        {
            throw new NotSupportedException();
        }

        public void ResetBone()
        {
            throw new NotSupportedException();
        }

        public void SetMorph(int mx, float val)
        {
            throw new NotSupportedException();
        }

        public void ResetMorph()
        {
            throw new NotSupportedException();
        }

        public void UpdateTransform()
        {
            throw new NotSupportedException();
        }

        public V3 GetTransformedVertexPosition(int vx)
        {
            throw new NotSupportedException();
        }

        public V3 GetTransformedVertexNormal(int vx)
        {
            throw new NotSupportedException();
        }

        public V3 GetTransformedBonePosition(int bx)
        {
            throw new NotSupportedException();
        }

        public M GetTransformedBoneMatrix(int bx)
        {
            throw new NotSupportedException();
        }

        public void UpdateMaterialColor(int mx, int type, V4 col)
        {
            throw new NotSupportedException();
        }

        public void UpdateMaterialEdge(int mx, bool edge, float size)
        {
            throw new NotSupportedException();
        }

        public void UpdateMaterialFlags(int mx, int type, bool flag)
        {
            throw new NotSupportedException();
        }

        public void SetBitmapTexture(int mx, Bitmap bmp, int mipmap = 1)
        {
            throw new NotSupportedException();
        }

        public void UpdateBitmapTexture(int mx, Bitmap bmp)
        {
            throw new NotSupportedException();
        }

        public IPXUIModelEventListener CreateEventListener(IPXEventConnector c, params int[] materials)
        {
            throw new NotSupportedException();
        }

        public void ReleaseEventListener(IPXUIModelEventListener listener)
        {
            throw new NotSupportedException();
        }
    }

    internal sealed class FakeEventConnector : IPXEventConnector
    {
        public object Connect(int n, object d)
        {
            throw new NotSupportedException();
        }

        public IPXViewEventListener CreateViewEventListener()
        {
            throw new NotSupportedException();
        }

        public void ReleaseViewEventListener(IPXViewEventListener l)
        {
            throw new NotSupportedException();
        }
    }

    internal sealed class FakeVisibleIkKey : IPEVmdVisibleIKKey
    {
        public int FrameIndex { get; set; }

        public bool Visible { get; set; }

        public IPEVmdIKEnable[] IK
        {
            get { throw new NotSupportedException(); }
        }

        public void SetIK(int[] indices, bool[] enable)
        {
            throw new NotSupportedException();
        }

        public int CompareTo(IPEVmdFrameKey other)
        {
            throw new NotSupportedException();
        }

        public int CompareTo(IPEVmdVisibleIKKey other)
        {
            throw new NotSupportedException();
        }

        public object Clone()
        {
            throw new NotSupportedException();
        }
    }

    internal sealed class FakeUiListener : IPXUIModelEventListener
    {
        public event EventHandler<PXEventArgs.UIModelMouse> MouseOver
        {
            add { throw new NotSupportedException(); }
            remove { throw new NotSupportedException(); }
        }

        public event EventHandler<PXEventArgs.UIModelMouse> MouseDown
        {
            add { throw new NotSupportedException(); }
            remove { throw new NotSupportedException(); }
        }

        public event EventHandler<PXEventArgs.UIModelMouse> MouseUp
        {
            add { throw new NotSupportedException(); }
            remove { throw new NotSupportedException(); }
        }

        public event EventHandler<PXEventArgs.UIModelMouse> MouseEnter
        {
            add { throw new NotSupportedException(); }
            remove { throw new NotSupportedException(); }
        }

        public event EventHandler<PXEventArgs.UIModelMouse> MouseLeave
        {
            add { throw new NotSupportedException(); }
            remove { throw new NotSupportedException(); }
        }

        public event EventHandler<PXEventArgs.UIModelMouseDrag> MouseDrag
        {
            add { throw new NotSupportedException(); }
            remove { throw new NotSupportedException(); }
        }

        public event EventHandler<PXEventArgs.UIModelMouseDrag> MouseDragEnd
        {
            add { throw new NotSupportedException(); }
            remove { throw new NotSupportedException(); }
        }

        public event EventHandler<PXEventArgs.UIModelMouse> MouseClick
        {
            add { throw new NotSupportedException(); }
            remove { throw new NotSupportedException(); }
        }

        public event EventHandler<PXEventArgs.UIModelMouse> MouseDoubleClick
        {
            add { throw new NotSupportedException(); }
            remove { throw new NotSupportedException(); }
        }
    }
}
