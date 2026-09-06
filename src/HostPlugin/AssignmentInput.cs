using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>入れ子のリストへ加えるときの、親ごとの組。</summary>
    public sealed class AssignmentGroup
    {
        public AssignmentGroup(
            int? parentIndex = null,
            long? parentHandle = null,
            IList<long> handles = null,
            IList<int> refIndices = null)
        {
            ParentIndex = parentIndex;
            ParentHandle = parentHandle;
            Handles = handles;
            RefIndices = refIndices;
        }

        /// <summary>位置で指す親。ハンドルで指すときは null。</summary>
        public int? ParentIndex { get; }

        /// <summary>ハンドルで指す親。位置で指すときは null。</summary>
        public long? ParentHandle { get; }

        /// <summary>加える生成物のハンドル。所有するリストだけが持つ。</summary>
        public IList<long> Handles { get; }

        /// <summary>加える参照先の位置。指すだけのリストだけが持つ。</summary>
        public IList<int> RefIndices { get; }
    }

    /// <summary>解いた組の並び。</summary>
    public sealed class ResolvedAssignments
    {
        internal ResolvedAssignments(TargetForm parentForm, IList<AssignmentGroup> groups, int childCount)
        {
            ParentForm = parentForm;
            Groups = groups;
            ChildCount = childCount;
        }

        /// <summary>親をどちらで指したか。位置なら <see cref="TargetForm.Indices"/>。</summary>
        public TargetForm ParentForm { get; }

        /// <summary>要求に現れた順の組。</summary>
        public IList<AssignmentGroup> Groups { get; }

        /// <summary>全部の組の子の合計。</summary>
        public int ChildCount { get; }
    }

    /// <summary>加える子の並びの種類。リストが要素を所有するかどうかで決まる。</summary>
    public enum AssignmentChild
    {
        /// <summary>所有するリスト。生成物のハンドルで加える。</summary>
        Handles = 0,

        /// <summary>指すだけのリスト。参照先の位置で加える。</summary>
        RefIndices = 1,
    }

    /// <summary>
    /// 入れ子のリストへ加える要求を解く。親ごとの組で受け取り、親の指し方は要求の全体で1つに
    /// そろえ、同じ親を2つ以上の組へ書けない。検証は組をまたいで行い、1件でも不正なら1件も
    /// 加えない。
    /// </summary>
    public static class AssignmentInput
    {
        /// <summary>親ごとの組を並べる項目の名前。</summary>
        public const string Name = "assignments";

        /// <summary>
        /// <paramref name="groups"/> を解く。<paramref name="child"/> はそのリストが所有するかで
        /// 決まる子の並びの種類、<paramref name="parentAllowed"/> は受け付ける親の指し方
        /// (親の型がハンドルを発行しないリストは位置だけ)、<paramref name="parentListCount"/> は
        /// 親のリストの件数、<paramref name="isUsableHandle"/> はハンドルが使えるかを答えるもの、
        /// <paramref name="referencedCount"/> は参照先のリストの件数(所有するリストでは使わない)、
        /// <paramref name="childLimit"/> は全部の組の子の合計の上限である。
        /// </summary>
        public static bool TryResolve(
            IList<AssignmentGroup> groups,
            AssignmentChild child,
            TargetForm parentAllowed,
            int parentListCount,
            Func<long, bool> isUsableHandle,
            int referencedCount,
            int childLimit,
            out ResolvedAssignments resolved,
            out string code,
            out string message)
        {
            if (isUsableHandle == null)
            {
                throw new ArgumentNullException(nameof(isUsableHandle));
            }

            if (parentListCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(parentListCount), parentListCount, "0以上でなければならない。");
            }

            if (referencedCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(referencedCount), referencedCount, "0以上でなければならない。");
            }

            if (childLimit < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(childLimit), childLimit, "1以上でなければならない。");
            }

            resolved = null;
            code = null;
            message = null;
            if (groups == null || groups.Count == 0)
            {
                return Invalid(Name + " が空である。空だと加えるものが決まらない。", out code, out message);
            }

            int childCount = 0;

            return TryTakeParentForm(groups, parentAllowed, out TargetForm parentForm, out code, out message)
                && TryCheckShape(groups, child, out childCount, out code, out message)
                && TryCheckDuplicates(groups, parentForm, child, out code, out message)
                && TryCheckLimit(childCount, childLimit, out code, out message)
                && TryCheckParents(groups, parentForm, parentListCount, isUsableHandle, out code, out message)
                && TryCheckChildren(groups, child, isUsableHandle, referencedCount, out code, out message)
                && Take(parentForm, groups, childCount, out resolved);
        }

        /// <summary>親の指し方は要求の全体で1つとする。位置とハンドルが混じった要求は断る。</summary>
        private static bool TryTakeParentForm(
            IList<AssignmentGroup> groups,
            TargetForm allowed,
            out TargetForm form,
            out string code,
            out string message)
        {
            form = TargetForm.None;
            code = null;
            message = null;
            bool byIndex = false;
            bool byHandle = false;
            foreach (AssignmentGroup group in groups)
            {
                if (group == null)
                {
                    return Invalid(Name + " に中身の無い組がある。", out code, out message);
                }

                bool hasIndex = group.ParentIndex.HasValue;
                bool hasHandle = group.ParentHandle.HasValue;
                if (hasIndex && hasHandle)
                {
                    return Invalid(
                        Name + " の組が親を二重に指している。parentIndex と parentHandle は同時に持てない。",
                        out code,
                        out message);
                }

                if (!hasIndex && !hasHandle)
                {
                    return Invalid(
                        Name + " の組が親を指していない。parentIndex と parentHandle のどちらかを持つ。",
                        out code,
                        out message);
                }

                byIndex |= hasIndex;
                byHandle |= hasHandle;
            }

            if (byIndex && byHandle)
            {
                return Invalid(
                    Name + " が親の指し方を混ぜている。全部の組が parentIndex を持つか、"
                        + "全部の組が parentHandle を持つかのどちらかにする。",
                    out code,
                    out message);
            }

            form = byIndex ? TargetForm.Indices : TargetForm.Handles;
            if ((allowed & form) == form)
            {
                return true;
            }

            return Invalid(
                (form == TargetForm.Indices ? "parentIndex" : "parentHandle")
                    + " はこのツールでは指定できない。親の型がハンドルを発行しないためである。",
                out code,
                out message);
        }

        /// <summary>子の並びが区分どおりで、空でないことを見る。合計もここで数える。</summary>
        private static bool TryCheckShape(
            IList<AssignmentGroup> groups,
            AssignmentChild child,
            out int childCount,
            out string code,
            out string message)
        {
            childCount = 0;
            code = null;
            message = null;
            bool wantsHandles = child == AssignmentChild.Handles;
            foreach (AssignmentGroup group in groups)
            {
                bool hasHandles = group.Handles != null;
                bool hasRefIndices = group.RefIndices != null;
                if (hasHandles != wantsHandles || hasRefIndices == wantsHandles)
                {
                    return Invalid(
                        Name + " の組の子の並びが、このリストの区分と合わない。"
                            + (wantsHandles
                                ? "所有するリストは handles で加える。"
                                : "指すだけのリストは " + ReferenceInput.Name + " で加える。"),
                        out code,
                        out message);
                }

                int count = wantsHandles ? group.Handles.Count : group.RefIndices.Count;
                if (count == 0)
                {
                    return Invalid(
                        Name + " の組が子を1つも持たない。空の組は加えるものが決まらない。",
                        out code,
                        out message);
                }

                childCount += count;
            }

            return true;
        }

        /// <summary>
        /// 同じ親を二度以上指す要求と、組をまたいで同じハンドルを指す要求を断る。参照先の位置は
        /// 対象ではなく値なので、重なってよい。
        /// </summary>
        private static bool TryCheckDuplicates(
            IList<AssignmentGroup> groups,
            TargetForm parentForm,
            AssignmentChild child,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            HashSet<long> parents = new HashSet<long>();
            foreach (AssignmentGroup group in groups)
            {
                long key = parentForm == TargetForm.Indices
                    ? group.ParentIndex.Value
                    : group.ParentHandle.Value;
                if (!parents.Add(key))
                {
                    return Invalid(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} が同じ親を二度以上指している: {1}。1つの親へ複数を入れる要求は、"
                                + "その組の子の並びへ並べる。",
                            Name,
                            key),
                        out code,
                        out message);
                }
            }

            if (child != AssignmentChild.Handles)
            {
                return true;
            }

            HashSet<long> handles = new HashSet<long>();
            foreach (AssignmentGroup group in groups)
            {
                foreach (long handle in group.Handles)
                {
                    if (handles.Add(handle))
                    {
                        continue;
                    }

                    return Invalid(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} が同じハンドルを二度以上指している: {1}。加えるとハンドルは消えるので、"
                                + "同じものを複数の親へは配れない。",
                            Name,
                            handle),
                        out code,
                        out message);
                }
            }

            return true;
        }

        private static bool TryCheckLimit(int childCount, int childLimit, out string code, out string message)
        {
            code = null;
            message = null;
            if (childCount <= childLimit)
            {
                return true;
            }

            return Invalid(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} の子の合計が上限を超えている: {1} 件(上限は {2} 件)",
                    Name,
                    childCount,
                    childLimit),
                out code,
                out message);
        }

        /// <summary>親が実在することを見る。</summary>
        private static bool TryCheckParents(
            IList<AssignmentGroup> groups,
            TargetForm parentForm,
            int parentListCount,
            Func<long, bool> isUsableHandle,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            foreach (AssignmentGroup group in groups)
            {
                if (parentForm == TargetForm.Indices)
                {
                    int index = group.ParentIndex.Value;
                    if (index >= 0 && index < parentListCount)
                    {
                        continue;
                    }

                    code = ToolEnvelope.IndexOutOfRange;
                    message = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} の parentIndex が範囲の外にある: {1}(親のリストの件数は {2})",
                        Name,
                        index,
                        parentListCount);

                    return false;
                }

                long handle = group.ParentHandle.Value;
                if (isUsableHandle(handle))
                {
                    continue;
                }

                code = ToolEnvelope.InvalidHandle;
                message = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} の parentHandle に使えないハンドルがある: {1}",
                    Name,
                    handle);

                return false;
            }

            return true;
        }

        /// <summary>子のハンドルが使えること、参照先の位置が範囲の中にあることを見る。</summary>
        private static bool TryCheckChildren(
            IList<AssignmentGroup> groups,
            AssignmentChild child,
            Func<long, bool> isUsableHandle,
            int referencedCount,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            foreach (AssignmentGroup group in groups)
            {
                if (child != AssignmentChild.Handles)
                {
                    if (!ReferenceInput.CheckWithin(
                        group.RefIndices, referencedCount, ReferenceInput.Name, out code, out message))
                    {
                        return false;
                    }

                    continue;
                }

                foreach (long handle in group.Handles)
                {
                    if (isUsableHandle(handle))
                    {
                        continue;
                    }

                    code = ToolEnvelope.InvalidHandle;
                    message = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} に使えないハンドルがある: {1}",
                        Name,
                        handle);

                    return false;
                }
            }

            return true;
        }

        private static bool Take(
            TargetForm parentForm,
            IList<AssignmentGroup> groups,
            int childCount,
            out ResolvedAssignments resolved)
        {
            resolved = new ResolvedAssignments(parentForm, groups.ToArray(), childCount);

            return true;
        }

        private static bool Invalid(string text, out string code, out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = text;

            return false;
        }
    }
}
