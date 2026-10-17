using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.Tests
{
    internal sealed class PositionDeclaration
    {
        public string Tool { get; set; }

        public string Input { get; set; }

        public string Of { get; set; }

        public bool Insertion { get; set; }

        public bool Span { get; set; }

        public bool LowerOnly { get; set; }

        public string Needs { get; set; }

        public IDictionary<string, object> With { get; set; }

        public string Key
        {
            get { return Tool + " " + Input; }
        }

        public string LeafName
        {
            get
            {
                string last = Input.Split('.').Last();

                return last.EndsWith("[]", StringComparison.Ordinal) ? last.Substring(0, last.Length - 2) : last;
            }
        }

        public string TopName
        {
            get
            {
                string first = Input.Split('.')[0];

                return first.EndsWith("[]", StringComparison.Ordinal) ? first.Substring(0, first.Length - 2) : first;
            }
        }
    }

    internal static class PositionDeclarations
    {
        private const string FileName = "position-inputs.json";

        private static readonly Lazy<IList<IDictionary<string, object>>> Schemas =
            new Lazy<IList<IDictionary<string, object>>>(() =>
                ((IEnumerable)Parse(Path.Combine(CatalogDirectory("authored"), "tool-schemas.json"))["tools"])
                    .Cast<IDictionary<string, object>>().ToList());

        private static readonly Lazy<IList<IDictionary<string, object>>> Reads =
            new Lazy<IList<IDictionary<string, object>>>(() =>
                ((IEnumerable)Parse(Path.Combine(CatalogDirectory("observed"), "composed-number-reads.json"))["inputs"])
                    .Cast<IDictionary<string, object>>().ToList());

        public static IList<PositionDeclaration> Inputs()
        {
            IDictionary<string, object> root = Parse(Path.Combine(CatalogDirectory("authored"), FileName));

            return ((IEnumerable)root["inputs"]).Cast<IDictionary<string, object>>().Select(Read).ToList();
        }

        public static IList<string> Listed(string list)
        {
            IDictionary<string, object> root = Parse(Path.Combine(CatalogDirectory("authored"), FileName));

            return ((IEnumerable)root[list]).Cast<IDictionary<string, object>>()
                .Select(r => (string)r["tool"] + " " + (string)r["input"]).ToList();
        }

        public static IList<string> Readers()
        {
            IDictionary<string, object> root = Parse(Path.Combine(CatalogDirectory("authored"), FileName));

            return ((IEnumerable)root["readers"]).Cast<string>().ToList();
        }

        public static IDictionary<string, ISet<string>> ChoiceGroupsOf(string tool)
        {
            IDictionary<string, ISet<string>> groups = new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
            foreach (IDictionary<string, object> entry in ToolSchemas().Where(t => (string)t["tool"] == tool))
            {
                foreach (IDictionary<string, object> branch in ((IEnumerable)entry["branches"]).Cast<IDictionary<string, object>>())
                {
                    object choices;
                    if (!branch.TryGetValue("choices", out choices))
                    {
                        continue;
                    }

                    foreach (IDictionary<string, object> choice in ((IEnumerable)choices).Cast<IDictionary<string, object>>())
                    {
                        List<string> names = ((IEnumerable)choice["names"]).Cast<string>().ToList();
                        foreach (string name in names)
                        {
                            ISet<string> held;
                            if (!groups.TryGetValue(name, out held))
                            {
                                held = new HashSet<string>(StringComparer.Ordinal);
                                groups[name] = held;
                            }

                            foreach (string other in names)
                            {
                                held.Add(other);
                            }
                        }
                    }
                }
            }

            return groups;
        }

        public static IList<IDictionary<string, object>> ToolSchemas()
        {
            return Schemas.Value;
        }

        public static IList<IDictionary<string, object>> NumberReads()
        {
            return Reads.Value;
        }

        public static string RepositoryDirectory()
        {
            for (DirectoryInfo at = new DirectoryInfo(AppContext.BaseDirectory); at != null; at = at.Parent)
            {
                if (Directory.Exists(Path.Combine(at.FullName, "catalog")) && Directory.Exists(Path.Combine(at.FullName, "src")))
                {
                    return at.FullName;
                }
            }

            throw new DirectoryNotFoundException("リポジトリの根が見つからない。");
        }

        public static object Plain(object json)
        {
            IDictionary<string, object> map = json as IDictionary<string, object>;
            if (map != null)
            {
                Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> pair in map)
                {
                    copy[pair.Key] = Plain(pair.Value);
                }

                return copy;
            }

            ArrayList list = json as ArrayList;
            if (list != null)
            {
                return list.Cast<object>().Select(Plain).ToArray();
            }

            return json;
        }

        private static string CatalogDirectory(string part)
        {
            return Path.Combine(RepositoryDirectory(), "catalog", part);
        }

        private static IDictionary<string, object> Parse(string path)
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }
                .Deserialize<IDictionary<string, object>>(File.ReadAllText(path));
        }

        private static PositionDeclaration Read(IDictionary<string, object> row)
        {
            object with;
            object of;
            object to;
            object form;
            object needs;
            object lower;

            return new PositionDeclaration
            {
                Tool = (string)row["tool"],
                Input = (string)row["input"],
                Of = row.TryGetValue("of", out of) ? (string)of : null,
                Insertion = row.TryGetValue("to", out to) && (string)to == "end",
                Span = row.TryGetValue("form", out form) && (string)form == "range",
                LowerOnly = row.TryGetValue("lowerOnly", out lower) && Equals(lower, true),
                Needs = row.TryGetValue("needs", out needs) ? (string)needs : null,
                With = row.TryGetValue("with", out with)
                    ? (IDictionary<string, object>)Plain(with)
                    : new Dictionary<string, object>(StringComparer.Ordinal),
            };
        }
    }
}
