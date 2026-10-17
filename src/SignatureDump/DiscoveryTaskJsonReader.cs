using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>用途の作業の表をJSONから読む。</summary>
    public static class DiscoveryTaskJsonReader
    {
        private const string TasksName = "tasks";

        private const string TaskName = "task";

        private const string SearchesName = "searches";

        private const string ToolsName = "tools";

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(
                TasksName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(TaskName, JsonForm.Text()),
                        JsonForm.Member(
                            SearchesName, JsonForm.Array(JsonForm.Array(JsonForm.Text()))),
                        JsonForm.Member(ToolsName, JsonForm.Array(JsonForm.Text()))))));

        /// <summary>形が違えば <see cref="FormatException"/>。</summary>
        public static DiscoveryTaskTable Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            IDictionary<string, object> root = (IDictionary<string, object>)Form.Read(json);
            List<DiscoveryTask> tasks = new List<DiscoveryTask>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (IDictionary<string, object> members in
                ((object[])root[TasksName]).Cast<IDictionary<string, object>>())
            {
                string task = (string)members[TaskName];
                if (!seen.Add(task))
                {
                    throw new FormatException("作業が二度現れる: " + task);
                }

                tasks.Add(new DiscoveryTask(
                    task,
                    ((object[])members[SearchesName])
                        .Select(search => new DiscoverySearch(
                            ((object[])search).Cast<string>().ToArray()))
                        .ToList(),
                    Tools((object[])members[ToolsName])));
            }

            return new DiscoveryTaskTable(tasks);
        }

        private static IList<string> Tools(object[] items)
        {
            string[] tools = items.Cast<string>().ToArray();
            for (int at = 1; at < tools.Length; at++)
            {
                if (string.CompareOrdinal(tools[at - 1], tools[at]) >= 0)
                {
                    throw new FormatException("序数の昇順で並んでいない: " + tools[at]);
                }
            }

            return tools;
        }
    }
}
