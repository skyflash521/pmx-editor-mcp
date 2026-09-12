using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>用途の作業の表をJSONから読む。</summary>
    public static class DiscoveryTaskJsonReader
    {
        private const string TasksName = "tasks";

        private const string TaskName = "task";

        private const string SearchesName = "searches";

        private const string ToolsName = "tools";

        /// <summary>形が違えば <see cref="FormatException"/>。</summary>
        public static DiscoveryTaskTable Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            Dictionary<string, object> root = Members(Parse(json), TasksName);
            List<DiscoveryTask> tasks = new List<DiscoveryTask>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (object item in Array(root[TasksName], TasksName))
            {
                Dictionary<string, object> members =
                    Members(item, TaskName, SearchesName, ToolsName);
                string task = Text(members[TaskName], TaskName);
                if (!seen.Add(task))
                {
                    throw new FormatException("作業が二度現れる: " + task);
                }

                tasks.Add(new DiscoveryTask(task, Searches(members), Tools(members)));
            }

            if (tasks.Count == 0)
            {
                throw new FormatException(TasksName + " は1件以上でなければならない。");
            }

            return new DiscoveryTaskTable(tasks);
        }

        private static IList<DiscoverySearch> Searches(Dictionary<string, object> members)
        {
            List<DiscoverySearch> searches = new List<DiscoverySearch>();
            foreach (object item in Array(members[SearchesName], SearchesName))
            {
                string[] terms = Array(item, SearchesName)
                    .Select(t => Text(t, SearchesName))
                    .ToArray();
                if (terms.Length == 0)
                {
                    throw new FormatException("検索の語が無い。");
                }

                searches.Add(new DiscoverySearch(terms));
            }

            if (searches.Count == 0)
            {
                throw new FormatException(SearchesName + " は1件以上でなければならない。");
            }

            return searches;
        }

        private static IList<string> Tools(Dictionary<string, object> members)
        {
            string[] tools = Array(members[ToolsName], ToolsName)
                .Select(t => Text(t, ToolsName))
                .ToArray();
            if (tools.Length == 0)
            {
                throw new FormatException(ToolsName + " は1件以上でなければならない。");
            }

            for (int at = 1; at < tools.Length; at++)
            {
                if (string.CompareOrdinal(tools[at - 1], tools[at]) >= 0)
                {
                    throw new FormatException("序数の昇順で並んでいない: " + tools[at]);
                }
            }

            return tools;
        }

        private static object Parse(string json)
        {
            try
            {
                return new JavaScriptSerializer().DeserializeObject(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("JSONとして読めない。", exception);
            }
        }

        private static object[] Array(object value, string name)
        {
            object[] items = value as object[];
            if (items == null)
            {
                throw new FormatException(name + " は項目の並びでなければならない。");
            }

            return items;
        }

        private static Dictionary<string, object> Members(object value, params string[] names)
        {
            Dictionary<string, object> members = value as Dictionary<string, object>;
            if (members == null)
            {
                throw new FormatException("項目の組でなければならない。");
            }

            foreach (string name in names)
            {
                if (!members.ContainsKey(name))
                {
                    throw new FormatException("項目が無い: " + name);
                }
            }

            foreach (string name in members.Keys)
            {
                if (!names.Contains(name, StringComparer.Ordinal))
                {
                    throw new FormatException("知らない項目がある: " + name);
                }
            }

            return members;
        }

        private static string Text(object value, string name)
        {
            string text = value as string;
            if (string.IsNullOrEmpty(text))
            {
                throw new FormatException(name + " は空でない文字列でなければならない。");
            }

            return text;
        }
    }
}
