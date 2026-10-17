using System;
using System.Collections.Generic;
using PEPlugin.Pmd;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using PEPlugin.Vmd;

namespace PmxEditorMcp.Tests
{
    internal sealed class PoseVmd : IPEVmd
    {
        public Dictionary<int, string> BoneNames { get; } = new Dictionary<int, string>();

        public Dictionary<int, string> MorphNames { get; } = new Dictionary<int, string>();

        public IList<IPEVmdBoneKey> Bone { get; } = new List<IPEVmdBoneKey>();

        public IList<IPEVmdMorphKey> Morph { get; } = new List<IPEVmdMorphKey>();

        public IList<IPEVmdCameraKey> Camera { get; } = new List<IPEVmdCameraKey>();

        public IList<IPEVmdLightKey> Light { get; } = new List<IPEVmdLightKey>();

        public IList<IPEVmdSelfShadowKey> SelfShadow { get; } = new List<IPEVmdSelfShadowKey>();

        public IList<IPEVmdVisibleIKKey> VisibleIK { get; } = new List<IPEVmdVisibleIKKey>();

        public string ModelName
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public string FilePath
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public Dictionary<int, string> IndexedBoneNames { get; } = new Dictionary<int, string>();

        public Dictionary<int, string> IndexedMorphNames { get; } = new Dictionary<int, string>();

        public string GetBoneName(int index)
        {
            string name;

            return IndexedBoneNames.TryGetValue(index, out name) ? name : null;
        }

        public string GetMorphName(int index)
        {
            string name;

            return IndexedMorphNames.TryGetValue(index, out name) ? name : null;
        }

        public void SetBoneNames(string[] names)
        {
            throw new NotSupportedException();
        }

        public string[] GetBoneNames()
        {
            throw new NotSupportedException();
        }

        public int GetBoneIndex(string name)
        {
            foreach (KeyValuePair<int, string> pair in BoneNames)
            {
                if (pair.Value == name)
                {
                    return pair.Key;
                }
            }

            return -1;
        }

        public void SetMorphNames(string[] names)
        {
            throw new NotSupportedException();
        }

        public string[] GetMorphNames()
        {
            throw new NotSupportedException();
        }

        public int GetMorphIndex(string name)
        {
            foreach (KeyValuePair<int, string> pair in MorphNames)
            {
                if (pair.Value == name)
                {
                    return pair.Key;
                }
            }

            return -1;
        }

        public void SetNamesFromPmd(IPEPmd pmd)
        {
            throw new NotSupportedException();
        }

        public void Init(IPEPmd pmd)
        {
            throw new NotSupportedException();
        }

        public void Init(IPXPmx pmx)
        {
            throw new NotSupportedException();
        }

        public void ClearKeys()
        {
            throw new NotSupportedException();
        }

        public void NormalizeKeys()
        {
            throw new NotSupportedException();
        }

        public void TrimStartBlankKeys()
        {
            throw new NotSupportedException();
        }

        public void TrimKeys()
        {
            throw new NotSupportedException();
        }

        public void TrimBoneKeys()
        {
            throw new NotSupportedException();
        }

        public void TrimMorphKeys()
        {
            throw new NotSupportedException();
        }

        public void TrimCameraKeys()
        {
            throw new NotSupportedException();
        }

        public void TrimLightKeys()
        {
            throw new NotSupportedException();
        }

        public void SetModelNameForCameraLight()
        {
            throw new NotSupportedException();
        }

        public void FromFile(string path)
        {
            throw new NotSupportedException();
        }

        public void ToFile(string path, bool trimKeys)
        {
            throw new NotSupportedException();
        }

        public object Clone()
        {
            throw new NotSupportedException();
        }
    }

    internal sealed class PoseBoneKey : IPEVmdBoneKey
    {
        public int BoneIndex { get; set; }

        public int FrameIndex { get; set; }

        public IPEVector3 Translation { get; set; } = new V3(0f, 0f, 0f);

        public IPEQuaternion Rotation { get; set; } = new Q(0f, 0f, 0f, 1f);

        public IPEVmdIPL IplX { get; set; } = new PoseIpl();

        public IPEVmdIPL IplY { get; set; } = new PoseIpl();

        public IPEVmdIPL IplZ { get; set; } = new PoseIpl();

        public IPEVmdIPL IplR { get; set; } = new PoseIpl();

        public bool PhysicsOff { get; set; }

        public int CompareTo(IPEVmdFrameKey other)
        {
            throw new NotSupportedException();
        }

        public int CompareTo(IPEVmdBoneKey other)
        {
            throw new NotSupportedException();
        }

        public object Clone()
        {
            throw new NotSupportedException();
        }
    }

    internal sealed class PoseMorphKey : IPEVmdMorphKey
    {
        public int MorphIndex { get; set; }

        public int FrameIndex { get; set; }

        public float Value { get; set; }

        public int CompareTo(IPEVmdFrameKey other)
        {
            throw new NotSupportedException();
        }

        public int CompareTo(IPEVmdMorphKey other)
        {
            throw new NotSupportedException();
        }

        public object Clone()
        {
            throw new NotSupportedException();
        }
    }

    internal sealed class PoseIpl : IPEVmdIPL
    {
        public int X1 { get; set; } = 20;

        public int Y1 { get; set; } = 20;

        public int X2 { get; set; } = 107;

        public int Y2 { get; set; } = 107;

        public void SetLinear()
        {
            X1 = 20;
            Y1 = 20;
            X2 = 107;
            Y2 = 107;
        }

        public object Clone()
        {
            throw new NotSupportedException();
        }
    }
}
