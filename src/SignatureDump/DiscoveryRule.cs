using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    public sealed class DiscoverySearch
    {
        public DiscoverySearch(IList<string> terms)
        {
            if (terms == null)
            {
                throw new ArgumentNullException(nameof(terms));
            }

            Terms = new ReadOnlyCollection<string>(terms);
        }

        public IList<string> Terms { get; }
    }

    public sealed class DiscoveryTask
    {
        public DiscoveryTask(
            string task, IList<DiscoverySearch> searches, IList<string> required)
        {
            PropertyRecord.RequireText(task, nameof(task));
            if (searches == null)
            {
                throw new ArgumentNullException(nameof(searches));
            }

            if (required == null)
            {
                throw new ArgumentNullException(nameof(required));
            }

            Task = task;
            Searches = new ReadOnlyCollection<DiscoverySearch>(searches);
            Required = new ReadOnlyCollection<string>(required);
        }

        public string Task { get; }

        public IList<DiscoverySearch> Searches { get; }

        public IList<string> Required { get; }
    }

    public sealed class DiscoveryTaskTable
    {
        public DiscoveryTaskTable(IList<DiscoveryTask> tasks)
        {
            if (tasks == null)
            {
                throw new ArgumentNullException(nameof(tasks));
            }

            Tasks = new ReadOnlyCollection<DiscoveryTask>(tasks);
        }

        public IList<DiscoveryTask> Tasks { get; }
    }

    /// <summary>検索の語からツールを引き当てる規則。</summary>
    public static class DiscoveryRule
    {
        /// <summary>その検索が引き当てるツールの名前。</summary>
        public static IList<string> Matched(
            IDictionary<string, string> descriptions, IList<string> terms)
        {
            if (descriptions == null)
            {
                throw new ArgumentNullException(nameof(descriptions));
            }

            if (terms == null)
            {
                throw new ArgumentNullException(nameof(terms));
            }

            return new ReadOnlyCollection<string>(ToolMatch.Found(
                terms, descriptions.Select(d => new ToolMatch.Entry(d.Key, d.Value))));
        }

        /// <summary>その作業の検索をすべて投げて引き当てたツールの名前。</summary>
        public static IList<string> Found(
            IDictionary<string, string> descriptions, DiscoveryTask task)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);
            foreach (DiscoverySearch search in task.Searches)
            {
                foreach (string tool in Matched(descriptions, search.Terms))
                {
                    found.Add(tool);
                }
            }

            return new ReadOnlyCollection<string>(
                found.OrderBy(t => t, StringComparer.Ordinal).ToList());
        }
    }
}
