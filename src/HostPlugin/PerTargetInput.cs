using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>対象の集合へ配る入力の取り方。</summary>
    public enum PerTargetForm
    {
        /// <summary>どちらも受け取らない。</summary>
        None = 0,

        /// <summary>全要素へ同じ組を配る。</summary>
        Shared = 1,

        /// <summary>対象と同じ長さの組の並び。</summary>
        PerTarget = 2,
    }

    /// <summary>解いた入力。</summary>
    public sealed class ResolvedPerTargetInput
    {
        internal ResolvedPerTargetInput(PerTargetForm form, object shared, IList<object> perTarget)
        {
            Form = form;
            Shared = shared;
            PerTarget = perTarget;
        }

        /// <summary>どちらの取り方で解いたか。</summary>
        public PerTargetForm Form { get; }

        /// <summary>全要素へ配る組。ほかの取り方では null。</summary>
        public object Shared { get; }

        /// <summary>要素ごとの組の並び。ほかの取り方では null。</summary>
        public IList<object> PerTarget { get; }

        /// <summary><paramref name="index"/> 番目の対象へ配る組。</summary>
        public object For(int index)
        {
            if (Form == PerTargetForm.None)
            {
                throw new InvalidOperationException("受け取らない取り方では配る組が無い。");
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "0以上でなければならない。");
            }

            if (Form == PerTargetForm.Shared)
            {
                return Shared;
            }

            if (index >= PerTarget.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index), index, "対象の並びの外を指している。");
            }

            return PerTarget[index];
        }
    }

    /// <summary>
    /// 対象の集合へ配る入力を解く。全要素へ同じ組を配る取り方と、対象と同じ長さの並びで配る
    /// 取り方は相互排他で、両方持つことも、どちらも持たないことも引数が不正とする。解決をここに
    /// 集めるのは、更新の値でもメソッドの引数でも同じ断り方にするためである。
    /// </summary>
    public static class PerTargetInput
    {
        /// <summary>更新の値を配る項目の名前。</summary>
        public static PerTargetNames Values { get; } = new PerTargetNames("value", "values");

        /// <summary>メソッドの引数を配る項目の名前。</summary>
        public static PerTargetNames Args { get; } = new PerTargetNames("args", "argsList");

        /// <summary>
        /// <paramref name="shared"/> と <paramref name="perTarget"/> のどちらで配るかを解く。
        /// 持たない側には null を渡す。<paramref name="takesInput"/> が偽のツール——入力に現れる
        /// 引数を1つも持たないもの——は、どちらも受け取らない。<paramref name="targetCount"/> は
        /// 解決した対象の件数で、並びで配るときはこれと同じ長さでなければならない。
        /// </summary>
        public static bool TryResolve(
            object shared,
            IList<object> perTarget,
            int targetCount,
            PerTargetNames names,
            bool takesInput,
            out ResolvedPerTargetInput resolved,
            out string code,
            out string message)
        {
            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            if (targetCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetCount), targetCount, "0以上でなければならない。");
            }

            resolved = null;
            code = null;
            message = null;
            bool hasShared = shared != null;
            bool hasPerTarget = perTarget != null;
            if (!takesInput)
            {
                if (!hasShared && !hasPerTarget)
                {
                    resolved = new ResolvedPerTargetInput(PerTargetForm.None, null, null);

                    return true;
                }

                return Invalid(
                    "このツールは " + names.Shared + " も " + names.PerTarget
                        + " も受け取らない。渡す引数が無いためである。",
                    out code,
                    out message);
            }

            if (hasShared && hasPerTarget)
            {
                return Invalid(
                    names.Shared + " と " + names.PerTarget + " は同時に持てない。どちらか1つを指定する。",
                    out code,
                    out message);
            }

            if (!hasShared && !hasPerTarget)
            {
                return Invalid(
                    names.Shared + " と " + names.PerTarget + " のどちらも無い。どちらか1つを指定する。",
                    out code,
                    out message);
            }

            if (hasShared)
            {
                resolved = new ResolvedPerTargetInput(PerTargetForm.Shared, shared, null);

                return true;
            }

            if (perTarget.Count != targetCount)
            {
                return Invalid(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} の長さが対象の件数と違う: {1} 件に対して {2} 件。",
                        names.PerTarget,
                        targetCount,
                        perTarget.Count),
                    out code,
                    out message);
            }

            resolved = new ResolvedPerTargetInput(
                PerTargetForm.PerTarget, null, perTarget.ToArray());

            return true;
        }

        private static bool Invalid(string text, out string code, out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = text;

            return false;
        }
    }

    /// <summary>配る入力の2つの項目の名前。</summary>
    public sealed class PerTargetNames
    {
        public PerTargetNames(string shared, string perTarget)
        {
            if (string.IsNullOrWhiteSpace(shared))
            {
                throw new ArgumentException("空にできない。", nameof(shared));
            }

            if (string.IsNullOrWhiteSpace(perTarget))
            {
                throw new ArgumentException("空にできない。", nameof(perTarget));
            }

            Shared = shared;
            PerTarget = perTarget;
        }

        /// <summary>全要素へ同じ組を配る項目の名前。</summary>
        public string Shared { get; }

        /// <summary>要素ごとの組の並びを配る項目の名前。</summary>
        public string PerTarget { get; }
    }
}
