using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    /// <summary>
    /// panel が 0 のまま表情枠に登録されているモーフ。MMD は、この状態のモーフに VMD のキーが
    /// 当たると落ちることがある。
    /// </summary>
    public static class HiddenExpressionMorphs
    {
        /// <summary>
        /// 表情枠が指している、panel が 0 のモーフ。モーフの並びの順で、同じモーフは1度だけ数える。
        /// PMXでないものには空を返す。
        /// </summary>
        public static IList<IPXMorph> Of(object pmx)
        {
            IPXPmx model = pmx as IPXPmx;

            return model == null ? new IPXMorph[0] : Ordered(model, Listed(model));
        }

        /// <summary>
        /// 表情枠が指している、panel が 0 のモーフの実体。モーフの並びに居るかは見ない。PMXでない
        /// ものには空を返す。
        /// </summary>
        public static ISet<object> Listed(object pmx)
        {
            IPXPmx model = pmx as IPXPmx;
            IEnumerable<object> hidden = model == null
                ? new object[0]
                : model.ExpressionNode.Items
                    .Where(item => item.IsMorph)
                    .Select(item => item.MorphItem.Morph)
                    .Where(morph => morph != null && morph.Panel == 0);

            return ReferenceCleanup.Held(hidden);
        }

        /// <summary>
        /// <paramref name="before"/> に無く、<paramref name="after"/> で <see cref="Of"/> に入るモーフの
        /// 警告。モーフの実体で突き合わせる。無ければ空を返す。
        /// </summary>
        public static IList<string> Arisen(ISet<object> before, object after)
        {
            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }

            IPXPmx model = after as IPXPmx;
            if (model == null)
            {
                return new string[0];
            }

            ISet<object> arisen = Listed(model);
            arisen.ExceptWith(before);
            if (arisen.Count == 0)
            {
                return new string[0];
            }

            IList<IPXMorph> named = Ordered(model, arisen);

            return named.Count == 0
                ? new string[0]
                : new[] { Warning(named.Select(morph => morph.Name).ToList()) };
        }

        private static IList<IPXMorph> Ordered(IPXPmx model, ISet<object> listed)
        {
            return listed.Count == 0
                ? new IPXMorph[0]
                : model.Morph.Where(morph => listed.Contains(morph)).ToList();
        }

        private static string Warning(IList<string> names)
        {
            return "panel が 0 の隠しモーフが表情枠に登録されている: "
                + string.Concat(names.Select(name => "「" + name + "」"))
                + "。MMD は、この状態のモーフに VMD のキーが当たると落ちることがある。表情枠から外すか、"
                + "panel を 1〜4 にする。";
        }
    }
}
