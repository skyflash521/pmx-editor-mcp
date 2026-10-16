using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ComposedOperationCatalogTests
    {
        [Fact]
        public void EveryOperationTheHostAcceptsIsOfferedToTheClient()
        {
            IDictionary<string, string[]> offered = Offered();
            List<string> wrong = new List<string>();
            foreach (Type type in typeof(ModelEditBones).Assembly.GetTypes())
            {
                PropertyInfo operations = type.GetProperty(
                    "Operations", BindingFlags.Public | BindingFlags.Static);
                FieldInfo tool = type.GetField("ToolName", BindingFlags.Public | BindingFlags.Static);
                if (operations == null || tool == null || !typeof(IList<string>).IsAssignableFrom(operations.PropertyType))
                {
                    continue;
                }

                string name = (string)tool.GetValue(null);
                IList<string> accepted = (IList<string>)operations.GetValue(null);
                string[] listed;
                if (!offered.TryGetValue(name, out listed))
                {
                    wrong.Add(name + ": operation の選択肢が観測データに無い");

                    continue;
                }

                if (!accepted.SequenceEqual(listed, StringComparer.Ordinal))
                {
                    wrong.Add(name + ": ホストは " + string.Join("・", accepted) + " を受け取るが、選択肢は "
                        + string.Join("・", listed));
                }
            }

            Assert.True(wrong.Count == 0, wrong.Count + " 件:\n" + string.Join("\n", wrong));
        }

        private static IDictionary<string, string[]> Offered()
        {
            IDictionary<string, object> reads = (IDictionary<string, object>)
                new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue }
                    .DeserializeObject(System.IO.File.ReadAllText(Reads(), System.Text.Encoding.UTF8));

            return ((object[])reads["inputs"])
                .Cast<IDictionary<string, object>>()
                .Where(entry => Equals(entry["input"], ComposedOperation.OperationName) && entry.ContainsKey("choices"))
                .ToDictionary(
                    entry => (string)entry["tool"],
                    entry => ((object[])entry["choices"]).Cast<string>().ToArray(),
                    StringComparer.Ordinal);
        }

        private static string Reads()
        {
            for (System.IO.DirectoryInfo at = new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                at != null;
                at = at.Parent)
            {
                string path = System.IO.Path.Combine(at.FullName, "catalog", "observed", "composed-text-reads.json");
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
            }

            throw new InvalidOperationException("composed-text-reads.json が見つからない。");
        }
    }
}
