using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PEPlugin.Pmd;
using PEPlugin.SDX;
using PEPlugin.View;

namespace PmxEditorMcp.Tests
{
    internal sealed class FakeTransformView : IPETransformViewConnector
    {
        public int Updates { get; private set; }

        public List<V3> Rotations { get; } = new List<V3>();

        public bool Visible { get; set; }

        public int SelectedBoneIndex { get; set; }

        public IPEVector3 BoneRotate_XYZ { get; set; }

        public IPEVector3 BoneTranslate_XYZ { get; set; }

        public IPEVector3 BoneScale_XYZ { get; set; }

        public int SelectedMorphIndex { get; set; }

        public float MorphValue { get; set; }

        public bool MorphChecker { get; set; }

        public Point Location { get; set; }

        public Size Size { get; set; }

        public FormWindowState WindowState { get; set; }

        public void UpdateView()
        {
            Updates++;
        }

        public Bitmap GetClientImage()
        {
            throw new NotSupportedException();
        }

        public void ResetTransform()
        {
            throw new NotSupportedException();
        }

        public void BoneRotate()
        {
            Rotations.Add(new V3(BoneRotate_XYZ));
        }

        public void BoneTranslate()
        {
            throw new NotSupportedException();
        }

        public void BoneScaling()
        {
            throw new NotSupportedException();
        }

        public bool SetVpd(string path)
        {
            throw new NotSupportedException();
        }

        public bool SetVpdFromText(string s)
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

        public void SetBodyVisibles(bool[] v)
        {
            throw new NotSupportedException();
        }

        public bool[] GetJointVisibles()
        {
            throw new NotSupportedException();
        }

        public void SetJointVisibles(bool[] v)
        {
            throw new NotSupportedException();
        }
    }
}
