using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin;
using PEPlugin.Pmd;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指したボーンの整理と生成と軸の設定を行うツール。
    /// </summary>
    public static class ModelEditBones
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_bones";

        /// <summary>同じ名前のボーンを先頭の1つへまとめる。</summary>
        public const string MergeSameName = "mergeSameName";

        /// <summary>子を持たないボーンを操作できない表示にする。</summary>
        public const string HideTipBones = "hideTipBones";

        /// <summary>表示先のボーン指定を、そのボーンまでの隔たりへ移す。</summary>
        public const string TipToOffset = "tipToOffset";

        /// <summary>表示先の隔たりを、いちばん近い子のボーン指定へ移す。</summary>
        public const string OffsetToTip = "offsetToTip";

        /// <summary>親より先に子が来ないよう並びを組み直す。</summary>
        public const string RelevelHierarchy = "relevelHierarchy";

        /// <summary>親を持たないボーンの上に、1つの親を足す。</summary>
        public const string AddRootParent = "addRootParent";

        /// <summary>指したボーンの上に、同じ位置の親を足す。</summary>
        public const string AddMultiStageParent = "addMultiStageParent";

        /// <summary>指したボーンの下に、同じ位置の子を足す。</summary>
        public const string AddMultiStageChild = "addMultiStageChild";

        /// <summary>指したボーンとその親の中間へ、ボーンを足す。</summary>
        public const string AddMiddle = "addMiddle";

        /// <summary>指したボーンの上に、そのボーンを付与の元にする親を足す。</summary>
        public const string AddAppendParent = "addAppendParent";

        /// <summary>指した頂点の重心へボーンを1つ足す。</summary>
        public const string AddAtVertices = "addAtVertices";

        /// <summary>指したボーンをIKの先とするIKボーンを足す。</summary>
        public const string MakeIk = "makeIk";

        /// <summary>名前の左右が逆のボーンの位置を、鏡像へそろえる。</summary>
        public const string MirrorPosition = "mirrorPosition";

        /// <summary>軸の制限の向きを、表示先への向きにする。</summary>
        public const string FixAxisToTip = "fixAxisToTip";

        /// <summary>ローカル軸を、表示先への向きと親からの向きで決める。</summary>
        public const string SetLocalAxis = "setLocalAxis";

        /// <summary>ローカル軸の指定を外す。</summary>
        public const string ResetLocalAxis = "resetLocalAxis";

        /// <summary>PMDのボーン種別を、いまの設定から決め直す。</summary>
        public const string SetPmdBoneKind = "setPmdBoneKind";

        /// <summary>軸を受け取る入力の名前。</summary>
        public const string AxisName = "axis";

        /// <summary>IKが辿るリンクの数を受け取る入力の名前。</summary>
        public const string LinkCountName = "linkCount";

        /// <summary>足したボーンの位置を返す項目の名前。</summary>
        public const string AddedName = "added";

        /// <summary>変えたボーンの数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>消えたボーンの数を返す項目の名前。</summary>
        public const string RemovedName = "removed";

        // ボーンの名前で左右を表す綴りは、PMXのモデルが従っている慣例である。
        private const string LeftSide = "左";

        private const string RightSide = "右";

        /// <summary>指した要素ではなく並び全体を相手にする操作。</summary>
        private static IList<string> Whole
        {
            get { return new[] { RelevelHierarchy, AddRootParent }; }
        }

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[]
                {
                    MergeSameName,
                    HideTipBones,
                    TipToOffset,
                    OffsetToTip,
                    RelevelHierarchy,
                    AddRootParent,
                    AddMultiStageParent,
                    AddMultiStageChild,
                    AddMiddle,
                    AddAppendParent,
                    AddAtVertices,
                    MakeIk,
                    MirrorPosition,
                    FixAxisToTip,
                    SetLocalAxis,
                    ResetLocalAxis,
                    SetPmdBoneKind,
                };
            }
        }

        /// <summary>ツールを表へ足す。<paramref name="builder"/> は新しい要素を作る相手を返す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit, Func<object> builder)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            List<string> known = new List<string>
            {
                ComposedOperation.OperationName,
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.All,
                AxisName,
                LinkCountName,
            };
            methods.Add(
                ToolName, edit.Method(known, (context, pmx) => Run(context, pmx, builder)));
        }

        private static ComposedEditResult Run(
            McpMethodContext context, object pmx, Func<object> builder)
        {
            IPXPmx model = (IPXPmx)pmx;
            string operation;
            string code;
            string message;
            IList<int> chosen;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            bool vertices = string.Equals(operation, AddAtVertices, StringComparison.Ordinal);
            string axis;
            int links;
            if (!TryChosen(context, model, operation, vertices, out chosen, out code, out message)
                || !ComposedInput.TryChoice(
                    context,
                    AxisName,
                    operation,
                    new[] { MirrorPosition },
                    ModelEditVertices.Axes,
                    out axis,
                    out code,
                    out message)
                || !ComposedInput.TryCount(
                    context,
                    LinkCountName,
                    operation,
                    new[] { MakeIk },
                    1,
                    out links,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            if (vertices)
            {
                return AtVertices(
                    model, (IPXPmxBuilder)builder(), chosen.Select(at => model.Vertex[at]).ToList());
            }

            IList<IPXBone> picked = chosen.Select(at => model.Bone[at]).ToList();
            switch (operation)
            {
                case MergeSameName:
                    return Merged(model, picked);

                case RelevelHierarchy:
                    return Releveled(model);

                case AddRootParent:
                    return Rooted(model, (IPXPmxBuilder)builder());

                case AddMultiStageParent:
                case AddMultiStageChild:
                case AddMiddle:
                case AddAppendParent:
                    return Staged(model, (IPXPmxBuilder)builder(), picked, operation);

                case MakeIk:
                    return Reaching(model, (IPXPmxBuilder)builder(), picked, links);

                default:
                    return Answer(new int[0], picked.Count(bone => Set(model, bone, operation, axis)), 0);
            }
        }

        /// <summary>並び全体を相手にする操作では、選択の指定を受け取らない。</summary>
        private static bool TryChosen(
            McpMethodContext context,
            IPXPmx model,
            string operation,
            bool vertices,
            out IList<int> chosen,
            out string code,
            out string message)
        {
            if (!Whole.Contains(operation, StringComparer.Ordinal))
            {
                return TargetInput.TryPositions(
                    context.Params,
                    TargetNames.Element,
                    vertices ? model.Vertex.Count : model.Bone.Count,
                    out chosen,
                    out code,
                    out message);
            }

            chosen = new int[0];

            return TargetInput.TryNoTarget(
                context.Params,
                TargetNames.Element,
                Operations.Where(held => !Whole.Contains(held, StringComparer.Ordinal)).ToList(),
                out code,
                out message);
        }

        /// <summary>変えたなら真を返す。</summary>
        private static bool Set(IPXPmx model, IPXBone bone, string operation, string axis)
        {
            switch (operation)
            {
                case HideTipBones:
                    return Hidden(model, bone);

                case TipToOffset:
                    return Loosened(bone);

                case OffsetToTip:
                    return Pointed(model, bone);

                case MirrorPosition:
                    return Mirrored(model, bone, axis);

                case FixAxisToTip:
                    return Fixed(model, bone);

                case SetLocalAxis:
                    return Framed(model, bone);

                case ResetLocalAxis:
                    return Unframed(bone);

                default:
                    return Sorted(bone);
            }
        }

        private static ComposedEditResult Merged(IPXPmx model, IList<IPXBone> picked)
        {
            Dictionary<IPXBone, IPXBone> moved =
                new Dictionary<IPXBone, IPXBone>(ReferenceComparer<IPXBone>.Instance);
            int changed = 0;
            foreach (IGrouping<string, IPXBone> group in picked
                .GroupBy(bone => bone.Name, StringComparer.Ordinal)
                .Where(g => g.Count() > 1))
            {
                foreach (IPXBone dropped in group.Skip(1))
                {
                    model.Bone.Remove(dropped);
                    moved[dropped] = group.First();
                }

                changed++;
            }

            ReferenceCleanup.Repoint(model, moved);

            return Answer(new int[0], changed, moved.Count);
        }

        /// <summary>親より先に子が来ないよう並びを組み直す。先に来ていた子の数を返す。</summary>
        private static ComposedEditResult Releveled(IPXPmx model)
        {
            IList<IPXBone> before = model.Bone.ToList();
            int early = before.Count(
                bone => bone.Parent != null && before.IndexOf(bone.Parent) > before.IndexOf(bone));
            if (early == 0)
            {
                return Answer(new int[0], 0, 0);
            }

            List<IPXBone> after = new List<IPXBone>();
            HashSet<IPXBone> taken = new HashSet<IPXBone>(ReferenceComparer<IPXBone>.Instance);
            foreach (IPXBone bone in before)
            {
                Place(after, taken, before, bone);
            }

            model.Bone.Clear();
            foreach (IPXBone bone in after)
            {
                model.Bone.Add(bone);
            }

            return Answer(new int[0], early, 0);
        }

        private static void Place(
            IList<IPXBone> after, ISet<IPXBone> taken, IList<IPXBone> before, IPXBone bone)
        {
            if (!taken.Add(bone))
            {
                return;
            }

            if (bone.Parent != null && before.Contains(bone.Parent))
            {
                Place(after, taken, before, bone.Parent);
            }

            after.Add(bone);
        }

        private static ComposedEditResult Rooted(IPXPmx model, IPXPmxBuilder builder)
        {
            IList<IPXBone> loose = model.Bone.Where(bone => bone.Parent == null).ToList();
            if (loose.Count == 0)
            {
                return Answer(new int[0], 0, 0);
            }

            IPXBone made = builder.Bone();
            made.Name = RootName;
            made.NameE = string.Empty;
            made.Visible = true;
            made.Controllable = true;
            made.IsRotation = true;
            model.Bone.Insert(0, made);
            foreach (IPXBone bone in loose)
            {
                bone.Parent = made;
            }

            return Answer(new[] { 0 }, loose.Count, 0);
        }

        private static ComposedEditResult Staged(
            IPXPmx model, IPXPmxBuilder builder, IList<IPXBone> picked, string operation)
        {
            List<int> added = new List<int>();
            foreach (IPXBone bone in picked)
            {
                IPXBone made = builder.Bone();
                made.Name = bone.Name + Tail(operation);
                made.NameE = string.Empty;
                made.Visible = true;
                made.Controllable = true;
                made.IsRotation = true;
                made.Position = Spot(bone, operation);
                if (string.Equals(operation, AddMultiStageChild, StringComparison.Ordinal))
                {
                    made.Parent = bone;
                    model.Bone.Insert(model.Bone.IndexOf(bone) + 1, made);
                }
                else
                {
                    made.Parent = bone.Parent;
                    if (string.Equals(operation, AddAppendParent, StringComparison.Ordinal))
                    {
                        made.AppendParent = bone;
                        made.IsAppendRotation = true;
                        made.AppendRatio = 1f;
                    }

                    bone.Parent = made;
                    model.Bone.Insert(model.Bone.IndexOf(bone), made);
                }

                added.Add(model.Bone.IndexOf(made));
            }

            return Answer(added, picked.Count, 0);
        }

        /// <summary>足すボーンを置く点。中間へ足すときだけ親との中ほどになる。</summary>
        private static V3 Spot(IPXBone bone, string operation)
        {
            return string.Equals(operation, AddMiddle, StringComparison.Ordinal)
                && bone.Parent != null
                ? Vectors.Between(bone.Position, bone.Parent.Position)
                : Vectors.Copied(bone.Position);
        }

        private static string Tail(string operation)
        {
            switch (operation)
            {
                case AddMultiStageParent:
                    return ParentTail;

                case AddMultiStageChild:
                    return ChildTail;

                case AddMiddle:
                    return MiddleTail;

                default:
                    return AppendTail;
            }
        }

        private static ComposedEditResult Reaching(
            IPXPmx model, IPXPmxBuilder builder, IList<IPXBone> picked, int links)
        {
            List<int> added = new List<int>();
            foreach (IPXBone bone in picked)
            {
                IPXBone made = builder.Bone();
                made.Name = bone.Name + IkTail;
                made.NameE = string.Empty;
                made.Position = Vectors.Copied(bone.Position);
                made.Visible = true;
                made.Controllable = true;
                made.IsRotation = true;
                made.IsTranslation = true;
                made.IsIK = true;
                made.IK.Target = bone;
                IPXBone above = bone.Parent;
                for (int at = 0; at < links && above != null; at++)
                {
                    made.IK.Links.Add(builder.IKLink(above));
                    above = above.Parent;
                }

                model.Bone.Add(made);
                added.Add(model.Bone.Count - 1);
            }

            return Answer(added, picked.Count, 0);
        }

        private static ComposedEditResult AtVertices(
            IPXPmx model, IPXPmxBuilder builder, IList<IPXVertex> picked)
        {
            if (picked.Count == 0)
            {
                return Answer(new int[0], 0, 0);
            }

            IPXBone made = builder.Bone();
            made.Name = VerticesName;
            made.NameE = string.Empty;
            made.Position = Vectors.Middle(picked.Select(vertex => vertex.Position));
            made.Visible = true;
            made.Controllable = true;
            made.IsRotation = true;
            model.Bone.Add(made);

            return Answer(new[] { model.Bone.Count - 1 }, 0, 0);
        }

        private static bool Hidden(IPXPmx model, IPXBone bone)
        {
            if (model.Bone.Any(held => ReferenceEquals(held.Parent, bone))
                || (!bone.Visible && !bone.Controllable))
            {
                return false;
            }

            bone.Visible = false;
            bone.Controllable = false;

            return true;
        }

        private static bool Loosened(IPXBone bone)
        {
            if (bone.ToBone == null)
            {
                return false;
            }

            bone.ToOffset = Vectors.Apart(bone.ToBone.Position, bone.Position, 1f);
            bone.ToBone = null;

            return true;
        }

        private static bool Pointed(IPXPmx model, IPXBone bone)
        {
            IPXBone found = null;
            float best = 0f;
            foreach (IPXBone child in model.Bone.Where(held => ReferenceEquals(held.Parent, bone)))
            {
                float apart = Vectors.Distance(bone.Position, child.Position);
                if (found == null || apart < best)
                {
                    found = child;
                    best = apart;
                }
            }

            if (found == null || ReferenceEquals(bone.ToBone, found))
            {
                return false;
            }

            bone.ToBone = found;
            bone.ToOffset = new V3(0f, 0f, 0f);

            return true;
        }

        private static bool Mirrored(IPXPmx model, IPXBone bone, string axis)
        {
            string turned = Turned(bone.Name);
            IPXBone twin = turned == null
                ? null
                : model.Bone.FirstOrDefault(
                    held => string.Equals(held.Name, turned, StringComparison.Ordinal));
            if (twin == null)
            {
                return false;
            }

            V3 across = Across(twin.Position, axis);
            if (Vectors.Same(bone.Position, across))
            {
                return false;
            }

            bone.Position = across;

            return true;
        }

        private static bool Fixed(IPXPmx model, IPXBone bone)
        {
            V3 along = Toward(bone);
            if (!Vectors.HasLength(along))
            {
                return false;
            }

            bone.IsFixAxis = true;
            bone.FixAxis = along;

            return true;
        }

        private static bool Framed(IPXPmx model, IPXBone bone)
        {
            V3 along = Toward(bone);
            if (!Vectors.HasLength(along))
            {
                return false;
            }

            V3 up = bone.Parent == null
                ? new V3(0f, 1f, 0f)
                : Vectors.Toward(bone.Position, bone.Parent.Position);
            V3 across = Vectors.Perpendicular(along, up);
            bone.IsLocalFrame = true;
            bone.SetLocalAxis(
                along, Vectors.HasLength(across) ? across : Vectors.Perpendicular(along, Aside(along)));

            return true;
        }

        private static bool Unframed(IPXBone bone)
        {
            if (!bone.IsLocalFrame)
            {
                return false;
            }

            bone.IsLocalFrame = false;

            return true;
        }

        private static bool Sorted(IPXBone bone)
        {
            bone.SetPMDBoneKind(Kind(bone));

            return true;
        }

        private static BoneKind Kind(IPXBone bone)
        {
            if (bone.IsIK)
            {
                return BoneKind.IK;
            }

            if (!bone.Visible)
            {
                return BoneKind.Unvisible;
            }

            if (bone.IsAppendRotation)
            {
                return BoneKind.RotateEffect;
            }

            if (bone.IsFixAxis)
            {
                return BoneKind.Twist;
            }

            return bone.IsTranslation ? BoneKind.RotateMove : BoneKind.Rotate;
        }

        /// <summary>
        /// そのボーンの表示先への向きを、長さを1にそろえて返す。表示先を持たないボーンでは長さを
        /// 持たない向きを返す。
        /// </summary>
        private static V3 Toward(IPXBone bone)
        {
            return bone.ToBone == null
                ? Vectors.Normalized(bone.ToOffset)
                : Vectors.Toward(bone.ToBone.Position, bone.Position);
        }

        private static V3 Across(V3 given, string axis)
        {
            switch (axis)
            {
                case ModelEditVertices.AxisX:
                    return new V3(-given.X, given.Y, given.Z);

                case ModelEditVertices.AxisY:
                    return new V3(given.X, -given.Y, given.Z);

                default:
                    return new V3(given.X, given.Y, -given.Z);
            }
        }

        /// <summary>その向きと平行でない向き。垂直な向きを作る手がかりにする。</summary>
        private static V3 Aside(V3 given)
        {
            return Math.Abs(given.X) < Math.Abs(given.Y)
                ? new V3(1f, 0f, 0f)
                : new V3(0f, 1f, 0f);
        }

        /// <summary>左右を入れ替えた名前。どちらも入っていない名前では空を返す。</summary>
        private static string Turned(string name)
        {
            if (name == null)
            {
                return null;
            }

            if (name.Contains(LeftSide))
            {
                return name.Replace(LeftSide, RightSide);
            }

            return name.Contains(RightSide) ? name.Replace(RightSide, LeftSide) : null;
        }

        private const string RootName = "全ての親";

        private const string VerticesName = "頂点";

        private const string ParentTail = "親";

        private const string ChildTail = "子";

        private const string MiddleTail = "中間";

        private const string AppendTail = "付与親";

        private const string IkTail = "IK";

        private static ComposedEditResult Answer(IList<int> added, int changed, int removed)
        {
            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { AddedName, added.Cast<object>().ToArray() },
                    { ChangedName, changed },
                    { RemovedName, removed },
                });
        }
    }
}
