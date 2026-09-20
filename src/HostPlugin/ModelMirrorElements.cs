using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public static class ModelMirrorElements
    {
        public const string ToolName = "model_mirror_elements";

        public const string CopyTargets = "copyTargets";

        public const string MirrorModel = "mirrorModel";

        public static IList<string> Operations
        {
            get { return new[] { CopyTargets, MirrorModel }; }
        }

        public const string TargetsName = "targets";

        public const string KindName = "kind";

        public static IList<string> Kinds
        {
            get
            {
                return new[]
                {
                    ElementKinds.Vertex,
                    ElementKinds.Face,
                    ElementKinds.Bone,
                    ElementKinds.Body,
                    ElementKinds.Joint,
                };
            }
        }

        public const string AddedName = "added";

        public const string ChangedName = "changed";

        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            List<string> known = new List<string>
            {
                ComposedOperation.OperationName,
                TargetsName,
            };
            methods.Add(ToolName, edit.Method(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            string operation;
            string code;
            string message;
            if (!ComposedOperation.TryTake(
                context, Operations, out operation, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            if (string.Equals(operation, MirrorModel, StringComparison.Ordinal))
            {
                if (context.Params.ContainsKey(TargetsName))
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.InvalidArgument,
                        TargetsName + " を渡せるのは " + CopyTargets + " のときだけである。");
                }

                return Answer(0, Whole(model));
            }

            IList<KeyValuePair<string, IList<int>>> targets;
            if (!TryTargets(context, model, out targets, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            return Answer(Added(model, targets), 0);
        }

        private static int Added(
            IPXPmx model, IList<KeyValuePair<string, IList<int>>> targets)
        {
            IDictionary<IPXBone, IPXBone> bones = Named(model.Bone.ToList());
            int added = 0;
            IList<int> wanted = Picked(targets, ElementKinds.Bone);
            if (wanted.Count > 0)
            {
                IDictionary<IPXBone, IPXBone> copies = Doubled(model.Bone, wanted, Bone);
                Overlaid(bones, copies);
                Rewire(copies.Values, bones);
                added += copies.Count;
            }

            IDictionary<IPXVertex, IPXVertex> vertices = Doubled(
                model.Vertex,
                Picked(targets, ElementKinds.Vertex),
                vertex => Vertex(vertex, bones));
            added += vertices.Count;
            added += Deformed(model, vertices);
            added += Faced(model, Picked(targets, ElementKinds.Face), vertices);
            IDictionary<IPXBody, IPXBody> bodies = Named(model.Body.ToList());
            wanted = Picked(targets, ElementKinds.Body);
            if (wanted.Count > 0)
            {
                IDictionary<IPXBody, IPXBody> copies =
                    Doubled(model.Body, wanted, body => Body(body, bones));
                Overlaid(bodies, copies);
                added += copies.Count;
            }

            added += Doubled(
                model.Joint,
                Picked(targets, ElementKinds.Joint),
                joint => Joint(joint, bodies)).Count;

            return added;
        }

        private static IDictionary<T, T> Doubled<T>(
            IList<T> items, IList<int> picked, Func<T, T> mirror)
            where T : class
        {
            Dictionary<T, T> made = new Dictionary<T, T>(ReferenceComparer<T>.Instance);
            foreach (int at in picked)
            {
                T held = items[at];
                if (made.ContainsKey(held))
                {
                    continue;
                }

                T copy = mirror(held);
                made.Add(held, copy);
                items.Add(copy);
            }

            return made;
        }

        /// <summary>写しのある要素の繋ぎ先を、名前で対にした相手から写しへ置き換える。</summary>
        private static void Overlaid<T>(IDictionary<T, T> paired, IDictionary<T, T> copies)
            where T : class
        {
            foreach (KeyValuePair<T, T> pair in copies)
            {
                paired[pair.Key] = pair.Value;
            }
        }

        /// <summary>名前の左右が対になる要素どうしを、元から相手への対として並べる。</summary>
        private static IDictionary<T, T> Named<T>(IList<T> items)
            where T : class
        {
            Dictionary<string, T> byName = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (T item in items)
            {
                string name = Name(item);
                if (name != null && !byName.ContainsKey(name))
                {
                    byName.Add(name, item);
                }
            }

            Dictionary<T, T> made = new Dictionary<T, T>(ReferenceComparer<T>.Instance);
            foreach (T item in items)
            {
                string name = Name(item);
                string other = Flipped(name, false);
                T found;
                if (!string.Equals(name, other, StringComparison.Ordinal)
                    && byName.TryGetValue(other, out found)
                    && !made.ContainsKey(item))
                {
                    made.Add(item, found);
                }
            }

            return made;
        }

        private static IPXBone Bone(IPXBone bone)
        {
            IPXBone made = (IPXBone)bone.Clone();
            made.Name = Flipped(bone.Name, true);
            made.Position = Aside(bone.Position);
            made.ToOffset = Aside(bone.ToOffset);
            made.Parent = bone.Parent;
            made.ToBone = bone.ToBone;
            made.AppendParent = bone.AppendParent;
            if (made.IK != null && bone.IK != null)
            {
                made.IK.Target = bone.IK.Target;
                for (int at = 0; at < made.IK.Links.Count && at < bone.IK.Links.Count; at++)
                {
                    made.IK.Links[at].Bone = bone.IK.Links[at].Bone;
                }
            }

            return made;
        }

        private static void Rewire(
            IEnumerable<IPXBone> copies, IDictionary<IPXBone, IPXBone> bones)
        {
            foreach (IPXBone made in copies)
            {
                made.Parent = Instead(bones, made.Parent, true);
                made.ToBone = Instead(bones, made.ToBone, false);
                made.AppendParent = Instead(bones, made.AppendParent, false);
                if (made.IK == null)
                {
                    continue;
                }

                made.IK.Target = Instead(bones, made.IK.Target, true);
                foreach (IPXIKLink link in made.IK.Links)
                {
                    link.Bone = Instead(bones, link.Bone, true);
                    if (!link.IsLimit)
                    {
                        continue;
                    }

                    V3 low = link.Low;
                    V3 high = link.High;
                    link.Low = new V3(low.X, -high.Y, -high.Z);
                    link.High = new V3(high.X, -low.Y, -low.Z);
                }
            }
        }

        /// <summary>
        /// その参照の写しがあれば写しへ移す。<paramref name="keeping"/> が偽のとき、写しの無い参照は
        /// 外す。
        /// </summary>
        private static IPXBone Instead(
            IDictionary<IPXBone, IPXBone> bones, IPXBone held, bool keeping)
        {
            IPXBone found;
            if (held != null && bones.TryGetValue(held, out found))
            {
                return found;
            }

            return keeping ? held : null;
        }

        private static IPXVertex Vertex(IPXVertex vertex, IDictionary<IPXBone, IPXBone> bones)
        {
            IPXVertex made = (IPXVertex)vertex.Clone();
            made.Position = Aside(vertex.Position);
            made.Normal = Aside(vertex.Normal);
            made.SDEF_C = Aside(vertex.SDEF_C);
            made.SDEF_R0 = Aside(vertex.SDEF_R0);
            made.SDEF_R1 = Aside(vertex.SDEF_R1);
            VertexWeights.Write(
                made,
                VertexWeights.All(vertex)
                    .Select(share => new KeyValuePair<IPXBone, float>(
                        Instead(bones, share.Key, true), share.Value))
                    .ToList());
            if (made.SDEF && made.Bone1 != null && made.Bone2 != null)
            {
                made.SDEF_C = Projected(made);
            }

            return made;
        }

        private static int Faced(
            IPXPmx model, IList<int> picked, IDictionary<IPXVertex, IPXVertex> vertices)
        {
            if (picked.Count == 0)
            {
                return 0;
            }

            IList<IPXFace> faces = ViewSelection.Faces(model);
            IList<int> owners = ViewSelection.Owners(model);
            HashSet<int> chosen = new HashSet<int>(picked);
            int added = 0;
            foreach (int at in chosen.OrderBy(place => place))
            {
                IPXFace held = faces[at];
                if (!ViewSelection.Corners(held).All(vertices.ContainsKey))
                {
                    continue;
                }

                IPXFace made = (IPXFace)held.Clone();
                made.Vertex1 = vertices[held.Vertex1];
                made.Vertex2 = vertices[held.Vertex3];
                made.Vertex3 = vertices[held.Vertex2];
                model.Material[owners[at]].Faces.Add(made);
                added++;
            }

            return added;
        }

        /// <summary>
        /// SDEFの中心を、2つのボーンを結ぶ線の上へ頂点を落とした点にする。2つのボーンが同じ位置に
        /// あるときはいまの中心をそのまま返す。
        /// </summary>
        private static V3 Projected(IPXVertex vertex)
        {
            V3 from = vertex.Bone1.Position;
            V3 along = Vectors.Toward(vertex.Bone2.Position, from);
            if (!Vectors.HasLength(along))
            {
                return vertex.SDEF_C;
            }

            return Vectors.Add(
                from,
                Vectors.Scale(along, Vectors.Dot(along, Vectors.Apart(vertex.Position, from, 1f))));
        }

        private static int Deformed(
            IPXPmx model, IDictionary<IPXVertex, IPXVertex> vertices)
        {
            if (vertices.Count == 0)
            {
                return 0;
            }

            int added = 0;
            foreach (IPXMorph morph in model.Morph)
            {
                List<IPXMorphOffset> made = new List<IPXMorphOffset>();
                foreach (IPXMorphOffset offset in morph.Offsets)
                {
                    IPXMorphOffset copy = Offset(offset, vertices);
                    if (copy != null)
                    {
                        made.Add(copy);
                    }
                }

                foreach (IPXMorphOffset copy in made)
                {
                    morph.Offsets.Add(copy);
                    added++;
                }
            }

            return added;
        }

        /// <summary>写しを指すオフセットを返す。写していない頂点を指すオフセットでは null を返す。</summary>
        private static IPXMorphOffset Offset(
            IPXMorphOffset offset, IDictionary<IPXVertex, IPXVertex> vertices)
        {
            IPXVertexMorphOffset moved = offset as IPXVertexMorphOffset;
            if (moved != null && moved.Vertex != null && vertices.ContainsKey(moved.Vertex))
            {
                IPXVertexMorphOffset copy = (IPXVertexMorphOffset)moved.Clone();
                copy.Vertex = vertices[moved.Vertex];
                copy.Offset = Aside(moved.Offset);

                return copy;
            }

            IPXUVMorphOffset slid = offset as IPXUVMorphOffset;
            if (slid != null && slid.Vertex != null && vertices.ContainsKey(slid.Vertex))
            {
                IPXUVMorphOffset copy = (IPXUVMorphOffset)slid.Clone();
                copy.Vertex = vertices[slid.Vertex];

                return copy;
            }

            return null;
        }

        private static IPXBody Body(IPXBody body, IDictionary<IPXBone, IPXBone> bones)
        {
            IPXBody made = (IPXBody)body.Clone();
            made.Name = Flipped(body.Name, true);
            made.Position = Aside(body.Position);
            made.Rotation = Turned(body.Rotation);
            made.Bone = Instead(bones, body.Bone, true);

            return made;
        }

        private static IPXJoint Joint(IPXJoint joint, IDictionary<IPXBody, IPXBody> bodies)
        {
            IPXJoint made = (IPXJoint)joint.Clone();
            made.Name = Flipped(joint.Name, true);
            made.Position = Aside(joint.Position);
            made.Rotation = Turned(joint.Rotation);
            Limit(
                made,
                joint.Limit_MoveLow,
                joint.Limit_MoveHigh,
                joint.Limit_AngleLow,
                joint.Limit_AngleHigh);
            made.BodyA = Instead(bodies, joint.BodyA);
            made.BodyB = Instead(bodies, joint.BodyB);

            return made;
        }

        private static void Limit(
            IPXJoint made, V3 moveLow, V3 moveHigh, V3 angleLow, V3 angleHigh)
        {
            made.Limit_MoveLow = new V3(-moveHigh.X, moveLow.Y, moveLow.Z);
            made.Limit_MoveHigh = new V3(-moveLow.X, moveHigh.Y, moveHigh.Z);
            made.Limit_AngleLow = new V3(angleLow.X, -angleHigh.Y, -angleHigh.Z);
            made.Limit_AngleHigh = new V3(angleHigh.X, -angleLow.Y, -angleLow.Z);
        }

        private static IPXBody Instead(IDictionary<IPXBody, IPXBody> bodies, IPXBody held)
        {
            IPXBody found;

            return held != null && bodies.TryGetValue(held, out found) ? found : held;
        }

        private static int Whole(IPXPmx model)
        {
            int changed = 0;
            foreach (IPXVertex vertex in model.Vertex)
            {
                vertex.Position = Aside(vertex.Position);
                vertex.Normal = Aside(vertex.Normal);
                vertex.SDEF_R0 = Aside(vertex.SDEF_R0);
                vertex.SDEF_R1 = Aside(vertex.SDEF_R1);
                changed++;
            }

            foreach (IPXMaterial material in model.Material)
            {
                foreach (IPXFace face in material.Faces)
                {
                    IPXVertex second = face.Vertex2;
                    face.Vertex2 = face.Vertex3;
                    face.Vertex3 = second;
                    changed++;
                }
            }

            foreach (IPXBone bone in model.Bone)
            {
                bone.Name = Flipped(bone.Name, false);
                bone.Position = Aside(bone.Position);
                bone.ToOffset = Aside(bone.ToOffset);
                if (bone.IK != null)
                {
                    foreach (IPXIKLink link in bone.IK.Links)
                    {
                        V3 low = link.Low;
                        V3 high = link.High;
                        link.Low = new V3(low.X, -high.Y, -high.Z);
                        link.High = new V3(high.X, -low.Y, -low.Z);
                    }
                }

                changed++;
            }

            changed += Shifted(model);
            foreach (IPXBody body in model.Body)
            {
                body.Name = Flipped(body.Name, false);
                body.Position = Aside(body.Position);
                body.Rotation = Turned(body.Rotation);
                changed++;
            }

            foreach (IPXJoint joint in model.Joint)
            {
                joint.Name = Flipped(joint.Name, false);
                joint.Position = Aside(joint.Position);
                joint.Rotation = Turned(joint.Rotation);
                Limit(
                    joint,
                    joint.Limit_MoveLow,
                    joint.Limit_MoveHigh,
                    joint.Limit_AngleLow,
                    joint.Limit_AngleHigh);
                changed++;
            }

            return changed;
        }

        private static int Shifted(IPXPmx model)
        {
            int changed = 0;
            foreach (IPXMorph morph in model.Morph)
            {
                foreach (IPXMorphOffset offset in morph.Offsets)
                {
                    IPXVertexMorphOffset moved = offset as IPXVertexMorphOffset;
                    if (moved != null)
                    {
                        moved.Offset = Aside(moved.Offset);
                        changed++;
                    }

                    IPXBoneMorphOffset posed = offset as IPXBoneMorphOffset;
                    if (posed != null)
                    {
                        posed.Translation = Aside(posed.Translation);
                        changed++;
                    }

                    IPXImpulseMorphOffset pushed = offset as IPXImpulseMorphOffset;
                    if (pushed != null)
                    {
                        pushed.Velocity = Aside(pushed.Velocity);
                        changed++;
                    }
                }
            }

            return changed;
        }

        private static V3 Aside(V3 given)
        {
            return given == null ? null : new V3(-given.X, given.Y, given.Z);
        }

        private static V3 Turned(V3 given)
        {
            return given == null ? null : new V3(given.X, -given.Y, -given.Z);
        }

        /// <summary>
        /// 名前の左右を入れ替える。<paramref name="marking"/> が真のとき、左右を持たない名前には
        /// 写しと分かる印を頭へ足す。
        /// </summary>
        private static string Flipped(string name, bool marking)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            char[] letters = name.ToCharArray();
            int last = letters.Length - 1;
            if (letters[0] == '左' || letters[0] == '右')
            {
                letters[0] = letters[0] == '左' ? '右' : '左';
            }
            else if (letters[last] == '左' || letters[last] == '右')
            {
                letters[last] = letters[last] == '左' ? '右' : '左';
            }
            else
            {
                return marking ? Mark + name : name;
            }

            return new string(letters);
        }

        private const string Mark = "M-";

        private static string Name(object item)
        {
            IPXBone bone = item as IPXBone;
            if (bone != null)
            {
                return bone.Name;
            }

            IPXBody body = item as IPXBody;

            return body == null ? null : body.Name;
        }

        private static IList<int> Picked(
            IList<KeyValuePair<string, IList<int>>> targets, string kind)
        {
            foreach (KeyValuePair<string, IList<int>> target in targets)
            {
                if (string.Equals(target.Key, kind, StringComparison.Ordinal))
                {
                    return target.Value;
                }
            }

            return new int[0];
        }

        private static bool TryTargets(
            McpMethodContext context,
            IPXPmx model,
            out IList<KeyValuePair<string, IList<int>>> targets,
            out string code,
            out string message)
        {
            targets = null;
            code = ToolEnvelope.InvalidArgument;
            message = null;
            object given;
            context.Params.TryGetValue(TargetsName, out given);
            object[] items = given as object[];
            if (items == null || items.Length == 0)
            {
                message = TargetsName + " は、写す要素を種類ごとに指した組を1つ以上並べたもので"
                    + "なければならない。";

                return false;
            }

            List<KeyValuePair<string, IList<int>>> made =
                new List<KeyValuePair<string, IList<int>>>();
            foreach (object item in items)
            {
                IDictionary<string, object> held = item as IDictionary<string, object>;
                object name;
                string kind = null;
                if (held != null && held.TryGetValue(KindName, out name))
                {
                    kind = name as string;
                }

                if (kind == null || !Kinds.Contains(kind, StringComparer.Ordinal))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = TargetsName + " の " + KindName + " は次のどれかでなければならない: "
                        + string.Join("・", Kinds.ToArray());

                    return false;
                }

                if (made.Any(t => string.Equals(t.Key, kind, StringComparison.Ordinal)))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = TargetsName + " へ同じ種類を二度並べている: " + kind;

                    return false;
                }

                IList<int> positions;
                if (!TargetInput.TryPositions(
                    held,
                    TargetNames.Element,
                    ViewSelection.Count(model, kind),
                    out positions,
                    out code,
                    out message))
                {
                    return false;
                }

                made.Add(new KeyValuePair<string, IList<int>>(kind, positions));
            }

            code = null;
            targets = made;

            return true;
        }

        private static ComposedEditResult Answer(int added, int changed)
        {
            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { AddedName, added },
                    { ChangedName, changed },
                });
        }
    }
}
