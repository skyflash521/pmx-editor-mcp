using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>用途の作業が、検索でその作業に要るツールをちょうど引き当てるかを照合する。</summary>
    public static class DiscoveryGate
    {
        /// <summary>規則に反していれば <see cref="InvalidOperationException"/>。</summary>
        public static void Require(
            DiscoveryTaskTable tasks, IDictionary<string, string> descriptions)
        {
            if (tasks == null)
            {
                throw new ArgumentNullException(nameof(tasks));
            }

            if (descriptions == null)
            {
                throw new ArgumentNullException(nameof(descriptions));
            }

            foreach (DiscoveryTask task in tasks.Tasks)
            {
                foreach (string tool in task.Required.Where(t => !descriptions.ContainsKey(t)))
                {
                    throw new InvalidOperationException(
                        "要るツールがツールの一覧に無い: " + task.Task + "(" + tool + ")");
                }

                IList<string> found = DiscoveryRule.Found(descriptions, task);
                RequireSame(task, found);
            }
        }

        private static void RequireSame(DiscoveryTask task, IList<string> found)
        {
            string[] missing = task.Required
                .Where(t => !found.Contains(t, StringComparer.Ordinal))
                .ToArray();
            if (missing.Length != 0)
            {
                throw new InvalidOperationException(
                    "検索が引き当てないツールがある: " + task.Task
                        + "(" + string.Join("・", missing) + ")");
            }

            string[] extra = found
                .Where(t => !task.Required.Contains(t, StringComparer.Ordinal))
                .ToArray();
            if (extra.Length != 0)
            {
                throw new InvalidOperationException(
                    "検索が要らないツールを引き当てる: " + task.Task
                        + "(" + string.Join("・", extra) + ")");
            }
        }
    }
}
