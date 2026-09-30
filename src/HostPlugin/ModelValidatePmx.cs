using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    public static class ModelValidatePmx
    {
        public const string ToolName = "model_validate_pmx";

        public const string FoundName = "found";

        public const string RunsName = "runs";

        public const string RunsTotalName = "runsTotal";

        public const string OffsetName = "offset";

        public const string LimitName = "limit";

        public const string NextOffsetName = "nextOffset";

        public const string UnsoundFacesName = "unsoundFaces";

        public const string DanglingFacesName = "danglingFaces";

        public const string DanglingWeightsName = "danglingWeights";

        public const string DanglingBonesName = "danglingBones";

        public const string DanglingMorphOffsetsName = "danglingMorphOffsets";

        public const string DanglingNodeItemsName = "danglingNodeItems";

        public const string DanglingPhysicsName = "danglingPhysics";

        public const string UnnormalizedWeightsName = "unnormalizedWeights";

        public const string DuplicateFacesName = "duplicateFaces";

        public const string HiddenMorphsInExpressionFrameName = "hiddenMorphsInExpressionFrame";

        private static readonly JavaScriptSerializer Sizer = new JavaScriptSerializer();

        public static IList<string> Locatable
        {
            get
            {
                return new[]
                {
                    UnsoundFacesName,
                    DanglingFacesName,
                    DanglingWeightsName,
                    DanglingBonesName,
                    UnnormalizedWeightsName,
                    DuplicateFacesName,
                    HiddenMorphsInExpressionFrameName,
                };
            }
        }

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

            methods.Add(ToolName, edit.Read(new List<string> { RunsName, OffsetName, LimitName }, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            string asked;
            int offset;
            int limit;
            string code;
            string message;
            if (!TryRuns(context, out asked, out offset, out limit, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IPXPmx model = (IPXPmx)pmx;
            ISet<object> vertices = ReferenceCleanup.Held(model.Vertex.Cast<object>());
            ISet<object> materials = ReferenceCleanup.Held(model.Material.Cast<object>());
            ISet<object> bones = ReferenceCleanup.Held(model.Bone.Cast<object>());
            ISet<object> morphs = ReferenceCleanup.Held(model.Morph.Cast<object>());
            ISet<object> bodies = ReferenceCleanup.Held(model.Body.Cast<object>());

            IList<int> unsound = Unsound(model);
            IList<int> looseFaces = LooseFaces(model, vertices);
            IList<int> looseWeights = LooseWeights(model, bones);
            IList<int> unnormalized = Unnormalized(model);
            IList<int> doubled = Doubled(model);
            IList<IPXMorph> hidden = HiddenExpressionMorphs.Of(model);

            Dictionary<string, object> found = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { UnsoundFacesName, unsound.Count },
                { DanglingFacesName, looseFaces.Count },
                { DanglingWeightsName, looseWeights.Count },
                { DanglingBonesName, LooseBones(model, bones) },
                {
                    DanglingMorphOffsetsName,
                    LooseOffsets(model, vertices, materials, bones, morphs, bodies)
                },
                { DanglingNodeItemsName, LooseNodeItems(model, bones, morphs) },
                { DanglingPhysicsName, LoosePhysics(model, vertices, materials, bones, bodies) },
                { UnnormalizedWeightsName, unnormalized.Count },
                { DuplicateFacesName, doubled.Count },
                { HiddenMorphsInExpressionFrameName, hidden.Count },
            };
            found[FoundName] = found.Values.Sum(count => (int)count);
            if (asked == null)
            {
                return ComposedEditResult.Complete(found);
            }

            IDictionary<string, IList<int>> places = new Dictionary<string, IList<int>>(
                StringComparer.Ordinal)
            {
                { UnsoundFacesName, unsound },
                { DanglingFacesName, looseFaces },
                { DanglingWeightsName, looseWeights },
                { DanglingBonesName, BonesWithLoose(model, bones) },
                { UnnormalizedWeightsName, unnormalized },
                { DuplicateFacesName, doubled },
                { HiddenMorphsInExpressionFrameName, PositionsOf(model.Morph, hidden) },
            };
            IList<object> all = PositionRuns.Joined(places[asked]);
            Page<object> page;
            if (!Paging.TryTake(
                all,
                offset,
                limit,
                ResponseSize.ValueChars(context.BudgetChars),
                taken => Sizer.Serialize(Located(found, all.Count, offset, taken)).Length,
                out page))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.ResponseTooLarge, "値の枠に1件も収まらない。");
            }

            return ComposedEditResult.Complete(
                Located(found, all.Count, offset, page.Items), page.Warnings);
        }

        private static IDictionary<string, object> Located(
            IDictionary<string, object> found, int total, int offset, IList<object> taken)
        {
            Dictionary<string, object> value = new Dictionary<string, object>(
                found, StringComparer.Ordinal)
            {
                { RunsTotalName, total },
                { RunsName, taken.ToArray() },
            };
            if (offset + taken.Count < total)
            {
                value.Add(NextOffsetName, offset + taken.Count);
            }

            return value;
        }

        private static bool TryRuns(
            McpMethodContext context,
            out string asked,
            out int offset,
            out int limit,
            out string code,
            out string message)
        {
            asked = null;
            offset = 0;
            limit = int.MaxValue;
            if (!context.Params.ContainsKey(RunsName))
            {
                foreach (string name in new[] { OffsetName, LimitName })
                {
                    if (context.Params.ContainsKey(name))
                    {
                        code = ToolEnvelope.InvalidArgument;
                        message = name + " を渡せるのは " + RunsName + " を渡したときだけである。";

                        return false;
                    }
                }

                code = null;
                message = null;

                return true;
            }

            if (!ComposedInput.TryChoice(context, RunsName, Locatable, out asked, out code, out message))
            {
                return false;
            }

            if (!TryNumber(context, OffsetName, 0, ref offset, out message)
                || !TryNumber(context, LimitName, 1, ref limit, out message))
            {
                code = ToolEnvelope.InvalidArgument;

                return false;
            }

            return true;
        }

        private static bool TryNumber(
            McpMethodContext context, string name, int least, ref int taken, out string message)
        {
            message = null;
            object given;
            if (!context.Params.TryGetValue(name, out given) || given == null)
            {
                return true;
            }

            long number;
            if (!ValueInput.TryInteger(given, out number)
                || number < least
                || number > int.MaxValue)
            {
                message = name + " は " + least.ToString(CultureInfo.InvariantCulture)
                    + " 以上の整数でなければならない。";

                return false;
            }

            taken = (int)number;

            return true;
        }

        private static IList<int> PositionsOf(IList<IPXMorph> all, IList<IPXMorph> chosen)
        {
            return Enumerable.Range(0, all.Count)
                .Where(at => chosen.Any(morph => ReferenceEquals(morph, all[at])))
                .ToList();
        }

        private static IList<int> Unsound(IPXPmx model)
        {
            IList<IPXFace> faces = ViewSelection.Faces(model);

            return Enumerable.Range(0, faces.Count)
                .Where(at => !ReferenceCleanup.IsSoundFace(faces[at]))
                .ToList();
        }

        private static IList<int> LooseFaces(IPXPmx model, ISet<object> vertices)
        {
            IList<IPXFace> faces = ViewSelection.Faces(model);

            return Enumerable.Range(0, faces.Count)
                .Where(at =>
                    !ReferenceCleanup.Alive(faces[at].Vertex1, vertices)
                    || !ReferenceCleanup.Alive(faces[at].Vertex2, vertices)
                    || !ReferenceCleanup.Alive(faces[at].Vertex3, vertices))
                .ToList();
        }

        private static IList<int> LooseWeights(IPXPmx model, ISet<object> bones)
        {
            return Enumerable.Range(0, model.Vertex.Count)
                .Where(at => VertexWeights.Read(model.Vertex[at])
                    .Any(share => !ReferenceCleanup.Alive(share.Key, bones)))
                .ToList();
        }

        private static int LooseBones(IPXPmx model, ISet<object> bones)
        {
            return model.Bone.Sum(bone => LooseReferences(bone, bones));
        }

        private static IList<int> BonesWithLoose(IPXPmx model, ISet<object> bones)
        {
            return Enumerable.Range(0, model.Bone.Count)
                .Where(at => LooseReferences(model.Bone[at], bones) > 0)
                .ToList();
        }

        private static int LooseReferences(IPXBone bone, ISet<object> bones)
        {
            int found = 0;
            found += Loose(bone.Parent, bones) ? 1 : 0;
            found += Loose(bone.ToBone, bones) ? 1 : 0;
            found += Loose(bone.AppendParent, bones) ? 1 : 0;
            if (bone.IK == null)
            {
                return found;
            }

            found += bone.IsIK && !ReferenceCleanup.Alive(bone.IK.Target, bones) ? 1 : 0;
            found += bone.IK.Links.Count(link => !ReferenceCleanup.Alive(link.Bone, bones));

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
                : item.IsMorph
                    && (item.MorphItem.Morph == null
                        || ReferenceCleanup.Alive(item.MorphItem.Morph, morphs)))));
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
                    (anchor.Body != null && !bodies.Contains(anchor.Body))
                    || (anchor.Vertex != null && !vertices.Contains(anchor.Vertex)));
            }

            return found;
        }

        private static IList<int> Unnormalized(IPXPmx model)
        {
            return Enumerable.Range(0, model.Vertex.Count)
                .Where(at => !VertexWeights.IsSound(model.Vertex[at]))
                .ToList();
        }

        private static IList<int> Doubled(IPXPmx model)
        {
            List<int> found = new List<int>();
            int at = 0;
            IDictionary<IPXVertex, int> places = ModelCleanFaces.Places(model);
            foreach (IPXMaterial material in model.Material)
            {
                HashSet<string> met = new HashSet<string>(StringComparer.Ordinal);
                foreach (IPXFace face in material.Faces)
                {
                    if (!met.Add(ModelCleanFaces.Key(face, places)))
                    {
                        found.Add(at);
                    }

                    at++;
                }
            }

            return found;
        }
    }
}
