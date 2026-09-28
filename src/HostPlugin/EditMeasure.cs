using System;
using System.Collections.Generic;
using PEPlugin.Form;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public static class EditMeasure
    {
        public const string ChangedVerticesName = "changedVertices";

        public const string ChangedBonesName = "changedBones";

        public const string ChangedBodiesName = "changedBodies";

        public const string ChangedJointsName = "changedJoints";

        public const string UndoCountName = "undoCount";

        public const string RedoCountName = "redoCount";

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

        private static readonly string[] PlacingRowKeys =
        {
            "PEPlugin.View.IPEVertexEditConnector.Move()",
            "PEPlugin.View.IPEVertexEditConnector.Move(PEPlugin.Pmd.IPEVector3)",
            "PEPlugin.View.IPEVertexEditConnector.Rotate()",
            "PEPlugin.View.IPEVertexEditConnector.Rotate(PEPlugin.Pmd.IPEVector3)",
            "PEPlugin.View.IPEVertexEditConnector.Scaling()",
            "PEPlugin.View.IPEVertexEditConnector.Scaling(PEPlugin.Pmd.IPEVector3)",
        };

        private static readonly KeyValuePair<string, string>[] CountedKinds =
        {
            new KeyValuePair<string, string>(ElementKinds.Vertex, ChangedVerticesName),
            new KeyValuePair<string, string>(ElementKinds.Bone, ChangedBonesName),
            new KeyValuePair<string, string>(ElementKinds.Body, ChangedBodiesName),
            new KeyValuePair<string, string>(ElementKinds.Joint, ChangedJointsName),
        };

        private static readonly string[] HistoryRowKeys =
        {
            "PEPlugin.Form.IPEFormConnector.Undo()",
            "PEPlugin.Form.IPEFormConnector.Redo()",
        };

        public static IDictionary<string, Func<object, object, object, IDictionary<string, object>>> ByRowKey()
        {
            Dictionary<string, Func<object, object, object, IDictionary<string, object>>> measures =
                new Dictionary<string, Func<object, object, object, IDictionary<string, object>>>(StringComparer.Ordinal);
            foreach (string key in EditRowKeys)
            {
                measures.Add(key, (receiver, before, after) => Changed(before, after));
            }

            foreach (string key in HistoryRowKeys)
            {
                measures.Add(key, Stepped);
            }

            return measures;
        }

        /// <summary>
        /// 位置を動かす行で、呼ぶ前に選ばれていたのに1件も変わらなかった種類ごとの警告。ほかの行では空を返す。
        /// </summary>
        public static IList<string> Unmoved(
            string rowKey, IDictionary<string, object> measured, IDictionary<string, int> picked)
        {
            if (measured == null)
            {
                throw new ArgumentNullException(nameof(measured));
            }

            if (picked == null)
            {
                throw new ArgumentNullException(nameof(picked));
            }

            List<string> warnings = new List<string>();
            if (!Places(rowKey))
            {
                return warnings;
            }

            foreach (KeyValuePair<string, string> kind in CountedKinds)
            {
                int chosen;
                object changed;
                if (picked.TryGetValue(kind.Key, out chosen)
                    && chosen > 0
                    && measured.TryGetValue(kind.Value, out changed)
                    && Equals(changed, 0))
                {
                    warnings.Add(
                        "選ばれていた " + kind.Key + " は " + chosen + " 件とも変わらなかった。PMXView の選択対象から"
                        + "外れている種類は動かない。種類をまたいで動かすなら model_place_elements を使う。");
                }
            }

            return warnings;
        }

        public static bool Places(string rowKey)
        {
            return Array.IndexOf(PlacingRowKeys, rowKey) >= 0;
        }

        public static IDictionary<string, int> Picked(ScreenTargets screen, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            return new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { ElementKinds.Vertex, screen.Taken(ElementKinds.Vertex, model.Vertex.Count).Count },
                { ElementKinds.Bone, screen.Taken(ElementKinds.Bone, model.Bone.Count).Count },
                { ElementKinds.Body, screen.Taken(ElementKinds.Body, model.Body.Count).Count },
                { ElementKinds.Joint, screen.Taken(ElementKinds.Joint, model.Joint.Count).Count },
            };
        }

        private static IDictionary<string, object> Stepped(object receiver, object before, object after)
        {
            IPEFormConnector form = (IPEFormConnector)receiver;
            IDictionary<string, object> made = Changed(before, after);
            made.Add(UndoCountName, form.UndoCount);
            made.Add(RedoCountName, form.RedoCount);

            return made;
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
