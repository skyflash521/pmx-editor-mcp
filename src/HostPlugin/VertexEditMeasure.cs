using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public static class VertexEditMeasure
    {
        public const string ChangedVerticesName = "changedVertices";

        public const string ChangedBonesName = "changedBones";

        public const string ChangedBodiesName = "changedBodies";

        public const string ChangedJointsName = "changedJoints";

        private static readonly string[] EditRowKeys =
        {
            "PEPlugin.View.IPEVertexEditConnector.Move()",
            "PEPlugin.View.IPEVertexEditConnector.Move(PEPlugin.Pmd.IPEVector3)",
            "PEPlugin.View.IPEVertexEditConnector.Rotate()",
            "PEPlugin.View.IPEVertexEditConnector.Rotate(PEPlugin.Pmd.IPEVector3)",
            "PEPlugin.View.IPEVertexEditConnector.Scaling()",
            "PEPlugin.View.IPEVertexEditConnector.Scaling(PEPlugin.Pmd.IPEVector3)",
            "PEPlugin.View.IPEVertexEditConnector.RotateNormal()",
            "PEPlugin.View.IPEVertexEditConnector.RotateNormal(PEPlugin.Pmd.IPEVector3)",
            "PEPlugin.View.IPEVertexEditConnector.MoveNormalAxis()",
            "PEPlugin.View.IPEVertexEditConnector.MoveNormalAxis(System.Single)",
        };

        public static IDictionary<string, Func<object, object, IDictionary<string, object>>> ByRowKey()
        {
            Dictionary<string, Func<object, object, IDictionary<string, object>>> measures =
                new Dictionary<string, Func<object, object, IDictionary<string, object>>>(StringComparer.Ordinal);
            foreach (string key in EditRowKeys)
            {
                measures.Add(key, Changed);
            }

            return measures;
        }

        public static IDictionary<string, object> Changed(object before, object after)
        {
            IPXPmx earlier = (IPXPmx)before;
            IPXPmx later = (IPXPmx)after;
            int vertices = Math.Abs(earlier.Vertex.Count - later.Vertex.Count);
            for (int at = 0; at < Math.Min(earlier.Vertex.Count, later.Vertex.Count); at++)
            {
                if (!Same(earlier.Vertex[at].Position, later.Vertex[at].Position)
                    || !Same(earlier.Vertex[at].Normal, later.Vertex[at].Normal))
                {
                    vertices++;
                }
            }

            int bones = Math.Abs(earlier.Bone.Count - later.Bone.Count);
            for (int at = 0; at < Math.Min(earlier.Bone.Count, later.Bone.Count); at++)
            {
                if (!Same(earlier.Bone[at].Position, later.Bone[at].Position))
                {
                    bones++;
                }
            }

            int bodies = Math.Abs(earlier.Body.Count - later.Body.Count);
            for (int at = 0; at < Math.Min(earlier.Body.Count, later.Body.Count); at++)
            {
                if (!Same(earlier.Body[at].Position, later.Body[at].Position)
                    || !Same(earlier.Body[at].Rotation, later.Body[at].Rotation)
                    || !Same(earlier.Body[at].BoxSize, later.Body[at].BoxSize))
                {
                    bodies++;
                }
            }

            int joints = Math.Abs(earlier.Joint.Count - later.Joint.Count);
            for (int at = 0; at < Math.Min(earlier.Joint.Count, later.Joint.Count); at++)
            {
                if (!Same(earlier.Joint[at].Position, later.Joint[at].Position)
                    || !Same(earlier.Joint[at].Rotation, later.Joint[at].Rotation))
                {
                    joints++;
                }
            }

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ChangedVerticesName, vertices },
                { ChangedBonesName, bones },
                { ChangedBodiesName, bodies },
                { ChangedJointsName, joints },
            };
        }

        private static bool Same(V3 first, V3 second)
        {
            return first.X == second.X && first.Y == second.Y && first.Z == second.Z;
        }
    }
}
