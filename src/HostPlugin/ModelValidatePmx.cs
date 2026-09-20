using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    public static class ModelValidatePmx
    {
        public const string ToolName = "model_validate_pmx";

        public const string FoundName = "found";

        public const string UnsoundFacesName = "unsoundFaces";

        public const string DanglingFacesName = "danglingFaces";

        public const string DanglingWeightsName = "danglingWeights";

        public const string DanglingBonesName = "danglingBones";

        public const string DanglingMorphOffsetsName = "danglingMorphOffsets";

        public const string DanglingNodeItemsName = "danglingNodeItems";

        public const string DanglingPhysicsName = "danglingPhysics";

        public const string UnnormalizedWeightsName = "unnormalizedWeights";

        public const string DuplicateFacesName = "duplicateFaces";

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

            methods.Add(ToolName, edit.Read(new List<string>(), Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            ISet<object> vertices = ReferenceCleanup.Held(model.Vertex.Cast<object>());
            ISet<object> materials = ReferenceCleanup.Held(model.Material.Cast<object>());
            ISet<object> bones = ReferenceCleanup.Held(model.Bone.Cast<object>());
            ISet<object> morphs = ReferenceCleanup.Held(model.Morph.Cast<object>());
            ISet<object> bodies = ReferenceCleanup.Held(model.Body.Cast<object>());

            Dictionary<string, object> found = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UnsoundFacesName, Unsound(model) },
                { DanglingFacesName, LooseFaces(model, vertices) },
                { DanglingWeightsName, LooseWeights(model, bones) },
                { DanglingBonesName, LooseBones(model, bones) },
                {
                    DanglingMorphOffsetsName,
                    LooseOffsets(model, vertices, materials, bones, morphs, bodies)
                },
                { DanglingNodeItemsName, LooseNodeItems(model, bones, morphs) },
                { DanglingPhysicsName, LoosePhysics(model, vertices, materials, bones, bodies) },
                { UnnormalizedWeightsName, Unnormalized(model) },
                { DuplicateFacesName, Doubled(model) },
            };
            found[FoundName] = found.Values.Sum(count => (int)count);

            return ComposedEditResult.Complete(found);
        }

        private static int Unsound(IPXPmx model)
        {
            return ViewSelection.Faces(model).Count(face => !ReferenceCleanup.IsSoundFace(face));
        }

        private static int LooseFaces(IPXPmx model, ISet<object> vertices)
        {
            return ViewSelection.Faces(model).Count(face =>
                !ReferenceCleanup.Alive(face.Vertex1, vertices)
                || !ReferenceCleanup.Alive(face.Vertex2, vertices)
                || !ReferenceCleanup.Alive(face.Vertex3, vertices));
        }

        private static int LooseWeights(IPXPmx model, ISet<object> bones)
        {
            return model.Vertex.Count(vertex => VertexWeights.Read(vertex)
                .Any(share => !ReferenceCleanup.Alive(share.Key, bones)));
        }

        private static int LooseBones(IPXPmx model, ISet<object> bones)
        {
            int found = 0;
            foreach (IPXBone bone in model.Bone)
            {
                found += Loose(bone.Parent, bones) ? 1 : 0;
                found += Loose(bone.ToBone, bones) ? 1 : 0;
                found += Loose(bone.AppendParent, bones) ? 1 : 0;
                if (bone.IK == null)
                {
                    continue;
                }

                found += bone.IsIK && !ReferenceCleanup.Alive(bone.IK.Target, bones) ? 1 : 0;
                found += bone.IK.Links.Count(
                    link => !ReferenceCleanup.Alive(link.Bone, bones));
            }

            return found;
        }

        private static bool Loose(IPXBone held, ISet<object> bones)
        {
            return held != null && !bones.Contains(held);
        }

        private static int LooseOffsets(
            IPXPmx model,
            ISet<object> vertices,
            ISet<object> materials,
            ISet<object> bones,
            ISet<object> morphs,
            ISet<object> bodies)
        {
            return model.Morph.Sum(morph => morph.Offsets.Count(
                offset => !ReferenceCleanup.PointsAtLive(
                    offset, vertices, materials, bones, morphs, bodies)));
        }

        private static int LooseNodeItems(IPXPmx model, ISet<object> bones, ISet<object> morphs)
        {
            return ReferenceCleanup.Nodes(model).Sum(node => node.Items.Count(item => !(item.IsBone
                ? ReferenceCleanup.Alive(item.BoneItem.Bone, bones)
                : item.IsMorph && ReferenceCleanup.Alive(item.MorphItem.Morph, morphs))));
        }

        private static int LoosePhysics(
            IPXPmx model,
            ISet<object> vertices,
            ISet<object> materials,
            ISet<object> bones,
            ISet<object> bodies)
        {
            int found = model.Body.Count(body => body.Bone != null && !bones.Contains(body.Bone));
            foreach (IPXJoint joint in model.Joint)
            {
                found += joint.BodyA != null && !bodies.Contains(joint.BodyA) ? 1 : 0;
                found += joint.BodyB != null && !bodies.Contains(joint.BodyB) ? 1 : 0;
            }

            foreach (IPXSoftBody soft in model.SoftBody)
            {
                found += soft.Material != null && !materials.Contains(soft.Material) ? 1 : 0;
                found += soft.Pins.Count(pin => !ReferenceCleanup.Alive(pin, vertices));
                found += soft.Anchors.Count(anchor =>
                    !ReferenceCleanup.Alive(anchor.Body, bodies)
                    || !ReferenceCleanup.Alive(anchor.Vertex, vertices));
            }

            return found;
        }

        private static int Unnormalized(IPXPmx model)
        {
            return model.Vertex.Count(vertex => !VertexWeights.IsSound(vertex));
        }

        private static int Doubled(IPXPmx model)
        {
            int found = 0;
            IDictionary<IPXVertex, int> places = ModelCleanFaces.Places(model);
            foreach (IPXMaterial material in model.Material)
            {
                HashSet<string> met = new HashSet<string>(StringComparer.Ordinal);
                foreach (IPXFace face in material.Faces)
                {
                    found += met.Add(ModelCleanFaces.Key(face, places)) ? 0 : 1;
                }
            }

            return found;
        }
    }
}
