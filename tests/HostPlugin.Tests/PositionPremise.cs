using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;

namespace PmxEditorMcp.Tests
{
    internal static class PositionPremise
    {
        public static readonly IDictionary<string, int> Counts = new Dictionary<string, int>
        {
            { "vertex", 11 },
            { "material", 3 },
            { "face", 13 },
            { "bone", 5 },
            { "morph", 7 },
            { "node", 8 },
            { "body", 17 },
            { "joint", 9 },
            { "softBody", 10 },
            { "material.face", 4 },
            { "bone.ikLink", 2 },
            { "morph.morphOffset", 12 },
            { "node.nodeItem", 14 },
            { "softBody.softBodyAnchor", 15 },
            { "vmd.boneName", 28 },
            { "vmd.morphName", 29 },
            { "vme.bone", 18 },
            { "vme.morph", 19 },
            { "vmeResult.bone", 20 },
            { "vmeResult.morph", 21 },
            { "vmeGroup.element", 22 },
            { "vmePath.point", 27 },
            { "ui.bone", 30 },
            { "ui.vertex", 31 },
            { "ui.morph", 32 },
            { "ui.material", 33 },
            { "plugin.registered", StubSystemConnector.RegisteredPlugins },
            { "plugin.registeredC", StubSystemConnector.RegisteredCPlugins },
            { "plugin.pe", 25 },
            { "plugin.c", 26 },
        };

        private const int ImplicitNodes = 2;

        public static void Fill(FakePmx pmx)
        {
            for (int at = 0; at < Counts["vertex"]; at++)
            {
                pmx.Vertex.Add(new FakeVertex(at, 0f, 0f));
            }

            int[] faces = { 4, 4, 5 };
            for (int owner = 0; owner < Counts["material"]; owner++)
            {
                FakeMaterial material = new FakeMaterial("材質" + owner);
                for (int at = 0; at < faces[owner]; at++)
                {
                    material.Faces.Add(new FakeFace(pmx.Vertex[at], pmx.Vertex[at + 1], pmx.Vertex[at + 2]));
                }

                pmx.Material.Add(material);
            }

            for (int at = 0; at < Counts["bone"]; at++)
            {
                pmx.Bone.Add(new FakeBone("ボーン" + at));
            }

            foreach (IPXBone owner in pmx.Bone)
            {
                FakeBone ik = (FakeBone)owner;
                ik.IsIK = true;
                ik.IK.Target = pmx.Bone[0];
                for (int at = 0; at < Counts["bone.ikLink"]; at++)
                {
                    ik.IK.Links.Add(new FakeIkLink(pmx.Bone[(at + 1) % pmx.Bone.Count]));
                }
            }

            for (int at = 0; at < Counts["morph"]; at++)
            {
                pmx.Morph.Add(new FakeMorph("モーフ" + at));
            }

            foreach (IPXMorph owner in pmx.Morph)
            {
                for (int at = 0; at < Counts["morph.morphOffset"]; at++)
                {
                    owner.Offsets.Add(new FakeVertexMorphOffset(pmx.Vertex[at % pmx.Vertex.Count]));
                }
            }

            for (int at = 0; at < Counts["node"] - ImplicitNodes; at++)
            {
                pmx.Node.Add(new FakeNode("枠" + at));
            }

            foreach (IPXNode owner in new[] { pmx.RootNode, pmx.ExpressionNode }.Concat(pmx.Node))
            {
                for (int at = 0; at < Counts["node.nodeItem"]; at++)
                {
                    owner.Items.Add(new FakeBoneNodeItem(pmx.Bone[at % pmx.Bone.Count]));
                }
            }

            for (int at = 0; at < Counts["body"]; at++)
            {
                pmx.Body.Add(new FakeBody("剛体" + at));
            }

            for (int at = 0; at < Counts["joint"]; at++)
            {
                pmx.Joint.Add(new FakeJoint("Joint" + at));
            }

            for (int at = 0; at < Counts["softBody"]; at++)
            {
                pmx.SoftBody.Add(new FakeSoftBody("SoftBody" + at));
            }

            foreach (IPXSoftBody owner in pmx.SoftBody)
            {
                for (int at = 0; at < Counts["softBody.softBodyAnchor"]; at++)
                {
                    owner.Anchors.Add(new FakeSoftBodyAnchor(pmx.Body[0], pmx.Vertex[at % pmx.Vertex.Count]));
                }
            }
        }

        public static FakePmx UiModelSource()
        {
            FakePmx pmx = new FakePmx();
            for (int at = 0; at < Counts["ui.vertex"]; at++)
            {
                pmx.Vertex.Add(new FakeVertex(at, 0f, 0f));
            }

            for (int at = 0; at < Counts["ui.material"]; at++)
            {
                pmx.Material.Add(new FakeMaterial("材質" + at));
            }

            for (int at = 0; at < Counts["ui.bone"]; at++)
            {
                pmx.Bone.Add(new FakeBone("ボーン" + at));
            }

            for (int at = 0; at < Counts["ui.morph"]; at++)
            {
                pmx.Morph.Add(new FakeMorph("モーフ" + at));
            }

            return pmx;
        }
    }
}
