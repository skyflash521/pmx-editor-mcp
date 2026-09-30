using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PEPlugin.Pmd;
using PEPlugin.SDX;
using PEPlugin.View;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// TransformView の題材。エディタと同じく、ボーン・モーフの位置は一覧の範囲外なら受け付けず、数の欄は
    /// 値を文字にして持ち、読むときに文字から読み直す。
    /// </summary>
    internal sealed class FakeTransformView : IPETransformViewConnector
    {
        private readonly Func<int> _bones;

        private readonly Func<int> _morphs;

        private string[] _rotate = Written(new V3(0f, 0f, 0f));

        private string[] _translate = Written(new V3(0f, 0f, 0f));

        private string[] _scale = Written(new V3(0f, 0f, 0f));

        private string _morphValue = 0f.ToString();

        private int _bone = -1;

        private int _morph = -1;

        /// <summary>一覧が空の題材。</summary>
        public FakeTransformView()
            : this(() => 0, () => 0)
        {
        }

        /// <summary>ボーンとモーフの一覧に、渡した数だけ並ぶ題材。</summary>
        public FakeTransformView(Func<int> bones, Func<int> morphs)
        {
            _bones = bones;
            _morphs = morphs;
        }

        public int Updates { get; private set; }

        public List<string> Calls { get; } = new List<string>();

        public List<string> VpdTexts { get; } = new List<string>();

        public bool AcceptsVpd { get; set; } = true;

        public List<V3> Rotations { get; } = new List<V3>();

        public bool Visible { get; set; }

        public int SelectedBoneIndex
        {
            get { return _bone; }
            set { _bone = Listed(value, _bones()) ? value : _bone; }
        }

        public IPEVector3 BoneRotate_XYZ
        {
            get { return Read(_rotate); }
            set { _rotate = Written(value); }
        }

        public IPEVector3 BoneTranslate_XYZ
        {
            get { return Read(_translate); }
            set { _translate = Written(value); }
        }

        public IPEVector3 BoneScale_XYZ
        {
            get { return Read(_scale); }
            set { _scale = Written(value); }
        }

        public int SelectedMorphIndex
        {
            get { return _morph; }
            set { _morph = Listed(value, _morphs()) ? value : _morph; }
        }

        public float MorphValue
        {
            get
            {
                float read;
                float.TryParse(_morphValue, out read);

                return read;
            }

            set
            {
                _morphValue = value.ToString();
            }
        }

        public bool MorphChecker { get; set; }

        public Point Location { get; set; }

        public Size Size { get; set; }

        public FormWindowState WindowState { get; set; }

        public void UpdateView()
        {
            Updates++;
        }

        /// <summary>一覧の位置として受け付けるか。-1 は選んでいないことを表す。</summary>
        private static bool Listed(int at, int count)
        {
            return at >= -1 && at <= count - 1;
        }

        /// <summary>3つの欄へ書く文字。エディタと同じく、それぞれの数を既定の書式で文字にする。</summary>
        private static string[] Written(IPEVector3 value)
        {
            return new[] { value.X.ToString(), value.Y.ToString(), value.Z.ToString() };
        }

        /// <summary>3つの欄の文字を読み直した値。読めない欄は0になる。</summary>
        private static IPEVector3 Read(string[] fields)
        {
            float x;
            float y;
            float z;
            float.TryParse(fields[0], out x);
            float.TryParse(fields[1], out y);
            float.TryParse(fields[2], out z);

            return new V3(x, y, z);
        }

        public Bitmap GetClientImage()
        {
            throw new NotSupportedException();
        }

        public void ResetTransform()
        {
            Calls.Add("reset");
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
            Calls.Add("vpd");
            VpdTexts.Add(s);

            return AcceptsVpd;
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
