using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>モーションイベントのテンプレートを適用する対象を一つに定める組。</summary>
    public sealed class TemplateTarget
    {
        public TemplateTarget(string kind, string name = null, string path = null)
        {
            Kind = kind;
            Name = name;
            Path = path;
        }

        /// <summary>対象の種別。</summary>
        public string Kind { get; }

        /// <summary>対象の名前。種別の中で対象が1つだけのときは持たない。</summary>
        public string Name { get; }

        /// <summary>同じ対象の中に同じ種別の演算子への道が複数あるときの、その道の名前。</summary>
        public string Path { get; }

        /// <summary>組を1つの語で表す。並びの中で同じ対象を二度指していないかを見るのに使う。</summary>
        public override string ToString()
        {
            return Kind + "/" + (Name ?? string.Empty) + "/" + (Path ?? string.Empty);
        }
    }

    /// <summary>
    /// テンプレートを適用する対象の並びを解く。位置でもハンドルでも指せない対象なので、対象を一つに
    /// 定める組で受け取る。空にできず、同じ対象を二度以上指せず、要求に現れた順で解く。
    /// </summary>
    public static class TemplateTargets
    {
        /// <summary>対象の並びの項目の名前。</summary>
        public const string Name = "targets";

        /// <summary>
        /// <paramref name="targets"/> を解く。<paramref name="isKnown"/> は、その組が指す対象が
        /// 在るかを答えるものである。1件でも解けなければ何も適用しないので、ここで全件を見る。
        /// </summary>
        public static bool TryResolve(
            IList<TemplateTarget> targets,
            Func<TemplateTarget, bool> isKnown,
            out IList<TemplateTarget> resolved,
            out string code,
            out string message)
        {
            if (isKnown == null)
            {
                throw new ArgumentNullException(nameof(isKnown));
            }

            resolved = null;
            code = ToolEnvelope.InvalidArgument;
            message = null;
            if (targets == null || targets.Count == 0)
            {
                message = Name + " が空である。空だと適用する対象が決まらない。";

                return false;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (TemplateTarget target in targets)
            {
                if (target == null || string.IsNullOrWhiteSpace(target.Kind))
                {
                    message = Name + " の組が対象の種別を持たない。種別は空にできない。";

                    return false;
                }

                if (seen.Add(target.ToString()))
                {
                    continue;
                }

                message = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} が同じ対象を二度指している: {1}",
                    Name,
                    target);

                return false;
            }

            foreach (TemplateTarget target in targets)
            {
                if (isKnown(target))
                {
                    continue;
                }

                code = ToolEnvelope.NotApplicable;
                message = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} が指す対象が無い: {1}",
                    Name,
                    target);

                return false;
            }

            resolved = targets.ToArray();
            code = null;

            return true;
        }
    }
}
