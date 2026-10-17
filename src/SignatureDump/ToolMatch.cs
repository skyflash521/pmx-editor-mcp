using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>探す語をツールの名前と説明文へ当てる規則。</summary>
    public static class ToolMatch
    {
        public sealed class Entry
        {
            public Entry(string name, string description)
            {
                if (name == null)
                {
                    throw new ArgumentNullException(nameof(name));
                }

                Name = name;
                Description = description;
            }

            public string Name { get; }

            public string Description { get; }
        }

        public static IList<string> Found(IList<string> texts, IEnumerable<Entry> entries)
        {
            if (texts == null)
            {
                throw new ArgumentNullException(nameof(texts));
            }

            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            List<string> found = new List<string>();
            foreach (Entry entry in entries)
            {
                if (texts.Any(
                    text => TextMatch.Contains(entry.Name, text)
                        || TextMatch.Contains(entry.Description, text)))
                {
                    found.Add(entry.Name);
                }
            }

            found.Sort(StringComparer.Ordinal);

            return found;
        }
    }
}
