using System;
using System.Drawing;
using System.Windows.Forms;
using PEPlugin.Pmd;
using PEPlugin.View;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class TransformViewSyncTests
    {
        [Fact]
        public void AnOpenTransformViewIsUpdated()
        {
            FakeTransformView view = new FakeTransformView { Visible = true };

            TransformViewSync.Refresh(view);

            Assert.Equal(1, view.Updates);
        }

        [Fact]
        public void AHiddenTransformViewIsLeftAlone()
        {
            FakeTransformView view = new FakeTransformView { Visible = false };

            TransformViewSync.Refresh(view);

            Assert.Equal(0, view.Updates);
        }

        [Fact]
        public void NoTransformViewIsNothingToDo()
        {
            TransformViewSync.Refresh(null);
        }

        private sealed class FakeTransformView : IPETransformViewConnector
        {
            public int Updates { get; private set; }

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
                throw new NotSupportedException();
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
}
