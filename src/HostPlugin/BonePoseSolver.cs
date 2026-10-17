using System.Collections.Generic;
using PEPlugin.Pmx;
using BonePose = PmxEditorMcp.VertexPose.BonePose;
using Mat = PmxEditorMcp.VertexPose.Mat;
using Quat = PmxEditorMcp.VertexPose.Quat;
using Vec = PmxEditorMcp.VertexPose.Vec;

namespace PmxEditorMcp
{
    internal sealed class BonePoseSolver
    {
        private readonly IPXPmx _model;

        private readonly IDictionary<IPXBone, int> _places = new Dictionary<IPXBone, int>();

        private readonly IDictionary<int, KeyValuePair<Quat, Vec>> _given;

        private readonly BonePose[] _done;

        private readonly bool[] _visiting;

        public BonePoseSolver(IPXPmx model, IDictionary<int, KeyValuePair<Quat, Vec>> given)
        {
            _model = model;
            _given = given;
            _done = new BonePose[model.Bone.Count];
            _visiting = new bool[model.Bone.Count];
            for (int at = 0; at < model.Bone.Count; at++)
            {
                _places[model.Bone[at]] = at;
            }
        }

        public bool TryPose(IPXBone bone, out BonePose pose, out string message)
        {
            pose = null;
            message = null;
            int at;
            if (bone == null || !_places.TryGetValue(bone, out at))
            {
                return true;
            }

            pose = _done[at];
            if (pose != null)
            {
                return true;
            }

            if (_visiting[at])
            {
                message = "ボーン " + at + " は、親か付与親をたどると自分へ戻る。";

                return false;
            }

            _visiting[at] = true;
            bool made = TryBuild(at, out pose, out message);
            _visiting[at] = false;
            if (made)
            {
                _done[at] = pose;
            }

            return made;
        }

        private bool TryBuild(int at, out BonePose pose, out string message)
        {
            pose = null;
            IPXBone bone = _model.Bone[at];
            KeyValuePair<Quat, Vec> own;
            if (!_given.TryGetValue(at, out own))
            {
                own = new KeyValuePair<Quat, Vec>(Quat.Identity, new Vec(0d, 0d, 0d));
            }

            BonePose parent;
            BonePose giver = null;
            if (!TryPose(bone.Parent, out parent, out message)
                || ((bone.IsAppendRotation || bone.IsAppendTranslation)
                    && !TryPose(bone.AppendParent, out giver, out message)))
            {
                return false;
            }

            Vec rest = Vec.Of(bone.Position);
            Quat local = own.Key;
            Vec move = own.Value;
            if (giver != null && bone.IsAppendRotation)
            {
                Quat given = bone.IsAppendLocal ? giver.Rotation.ToQuat() : giver.Local;
                if (bone.AppendRatio != 1f)
                {
                    given = VertexPose.Slerp(Quat.Identity, given, bone.AppendRatio);
                }

                local = Quat.Multiply(own.Key, given).Normalized();
            }

            if (giver != null && bone.IsAppendTranslation)
            {
                Vec given = bone.IsAppendLocal ? giver.World - Vec.Of(bone.AppendParent.Position) : giver.Move;
                move = (given * bone.AppendRatio) + own.Value;
            }

            Mat above = parent == null ? Mat.Identity : parent.Rotation;
            Vec aboveWorld = parent == null ? new Vec(0d, 0d, 0d) : parent.World;
            Vec aboveRest = parent == null ? new Vec(0d, 0d, 0d) : Vec.Of(bone.Parent.Position);
            Mat rotation = above * Mat.Of(local);
            Vec world = aboveWorld + above.Apply((rest - aboveRest) + move);
            pose = new BonePose(local, move, rotation, world, world - rotation.Apply(rest));

            return true;
        }
    }
}
