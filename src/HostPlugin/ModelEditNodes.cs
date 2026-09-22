using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    /// <summary>
    /// 表示枠への一括登録と、表情枠の正規化を行うツール。
    /// </summary>
    public static class ModelEditNodes
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_nodes";

        /// <summary>どの枠にも載っていないボーンを、指した枠の末尾へ足す。</summary>
        public const string RegisterUnlistedBones = "registerUnlistedBones";

        /// <summary>どの枠にも載っていないモーフを、指した枠の末尾へ足す。</summary>
        public const string RegisterUnlistedMorphs = "registerUnlistedMorphs";

        public const string RegisterPickedBones = "registerPickedBones";

        public const string RegisterPickedMorphs = "registerPickedMorphs";

        public const string TargetIndicesName = "targetIndices";

        /// <summary>枠へ足す要素を、画面の選択で指す入力の名前。</summary>
        public const string TargetSelectedName = "targetSelected";

        /// <summary>表情の枠の中身を、モーフの並びの順にそろえる。</summary>
        public const string NormalizeExpressionNode = "normalizeExpressionNode";

        /// <summary>足した枠の中身の数を返す項目の名前。</summary>
        public const string AddedName = "added";

        /// <summary>変えた枠の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[]
                {
                    RegisterUnlistedBones,
                    RegisterUnlistedMorphs,
                    RegisterPickedBones,
                    RegisterPickedMorphs,
                    NormalizeExpressionNode,
                };
            }
        }

        /// <summary>ツールを表へ足す。<paramref name="builder"/> は新しい中身を作る相手を返す。</summary>
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
                TargetIndicesName,
                TargetSelectedName,
            };
            methods.Add(
                ToolName, edit.Method(known, (context, pmx) => Run(context, pmx, builder)));
        }

        private static ComposedEditResult Run(
            McpMethodContext context, object pmx, Func<object> builder)
        {
            IPXPmx model = (IPXPmx)pmx;
            IList<IPXNode> frames = ReferenceCleanup.Nodes(model).ToList();
            string operation;
            string code;
            string message;
            IList<int> chosen;
            IList<int> targets;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message)
                || !TargetInput.TryPositions(
                    context.Params,
                    TargetNames.Element,
                    frames.Count,
                    out chosen,
                    out code,
                    out message)
                || !ComposedInput.TryIndices(
                    context,
                    TargetIndicesName,
                    operation,
                    Picking,
                    Reach(model, operation),
                    out targets,
                    out code,
                    out message,
                    null,
                    TargetSelectedName,
                    context.Screen.Pick(Aimed(operation), Reach(model, operation))))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            if (Picking.Contains(operation, StringComparer.Ordinal)
                && !context.Params.ContainsKey(TargetIndicesName)
                && !context.Params.ContainsKey(TargetSelectedName))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    TargetIndicesName + " に、枠へ足す要素の位置を渡す。"
                        + TargetSelectedName + " に真を渡すと、画面の選択で指す。");
            }

            IList<IPXNode> picked = chosen.Select(at => frames[at]).ToList();
            int added = 0;
            int changed = 0;
            if (string.Equals(operation, NormalizeExpressionNode, StringComparison.Ordinal))
            {
                changed = picked.Count(node => Ordered(model, node));
            }
            else if (picked.Count > 0)
            {
                bool bones = Bones.Contains(operation, StringComparer.Ordinal);
                IPXNode into = picked.FirstOrDefault(
                    node => !bones || !ReferenceEquals(node, model.ExpressionNode));
                if (into == null)
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.InvalidArgument,
                        "表情の枠にはボーンを載せられない。ほかの枠も指す。");
                }

                added = Registered(
                    model,
                    into,
                    (IPXPmxBuilder)builder(),
                    bones,
                    Picking.Contains(operation, StringComparer.Ordinal) ? targets : null);
                changed = added == 0 ? 0 : 1;
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { AddedName, added },
                    { ChangedName, changed },
                });
        }

        private static IList<string> Picking
        {
            get { return new[] { RegisterPickedBones, RegisterPickedMorphs }; }
        }

        private static IList<string> Bones
        {
            get { return new[] { RegisterUnlistedBones, RegisterPickedBones }; }
        }

        /// <summary>その操作が指す相手の種類。相手を指さない操作では null。</summary>
        private static string Aimed(string operation)
        {
            if (!Picking.Contains(operation, StringComparer.Ordinal))
            {
                return null;
            }

            return Bones.Contains(operation, StringComparer.Ordinal)
                ? ElementKinds.Bone
                : ElementKinds.Morph;
        }

        private static int Reach(IPXPmx model, string operation)
        {
            if (!Picking.Contains(operation, StringComparer.Ordinal))
            {
                return 0;
            }

            return Bones.Contains(operation, StringComparer.Ordinal)
                ? model.Bone.Count
                : model.Morph.Count;
        }

        /// <summary>
        /// どの枠にも載っていない要素を、その枠の末尾へ足す。<paramref name="targets"/> を渡すと、
        /// その位置の要素だけを相手にする。足した数を返す。
        /// </summary>
        private static int Registered(
            IPXPmx model, IPXNode node, IPXPmxBuilder builder, bool bones, IList<int> targets)
        {
            HashSet<object> listed = new HashSet<object>(ReferenceComparer<object>.Instance);
            foreach (IPXNode held in ReferenceCleanup.Nodes(model))
            {
                foreach (IPXNodeItem item in held.Items)
                {
                    object target = Held(item);
                    if (target != null)
                    {
                        listed.Add(target);
                    }
                }
            }

            IList<object> reach = bones
                ? model.Bone.Cast<object>().ToList()
                : model.Morph.Cast<object>().ToList();
            int added = 0;
            foreach (object target in targets == null
                ? reach
                : targets.Select(at => reach[at]).ToList())
            {
                if (!listed.Add(target))
                {
                    continue;
                }

                node.Items.Add(bones
                    ? (IPXNodeItem)builder.BoneNodeItem((IPXBone)target)
                    : builder.MorphNodeItem((IPXMorph)target));
                added++;
            }

            return added;
        }

        /// <summary>枠の中身をモーフの並びの順にそろえる。並べ替えたなら真を返す。</summary>
        private static bool Ordered(IPXPmx model, IPXNode node)
        {
            IList<IPXNodeItem> before = node.Items.ToList();
            IList<IPXNodeItem> after = before
                .OrderBy(item => At(model, item))
                .ToList();
            if (before.SequenceEqual(after, ReferenceComparer<IPXNodeItem>.Instance))
            {
                return false;
            }

            node.Items.Clear();
            foreach (IPXNodeItem item in after)
            {
                node.Items.Add(item);
            }

            return true;
        }

        /// <summary>その中身が指すモーフの、並びの中の位置。モーフを指していなければ末尾より後ろ。</summary>
        private static int At(IPXPmx model, IPXNodeItem item)
        {
            IPXMorphNodeItem held = item as IPXMorphNodeItem;

            return held == null || held.Morph == null
                ? model.Morph.Count
                : model.Morph.IndexOf(held.Morph);
        }

        /// <summary>その中身が指している要素。どちらも指していなければ空。</summary>
        private static object Held(IPXNodeItem item)
        {
            IPXBoneNodeItem bone = item as IPXBoneNodeItem;
            if (bone != null)
            {
                return bone.Bone;
            }

            IPXMorphNodeItem morph = item as IPXMorphNodeItem;

            return morph == null ? null : (object)morph.Morph;
        }
    }
}
