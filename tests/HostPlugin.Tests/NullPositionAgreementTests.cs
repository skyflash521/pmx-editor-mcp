using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class NullPositionAgreementTests
    {
        [Fact]
        public void EveryReferencedFieldTakesNullInTheDefinitionExactlyWhenTheGeneratedBindingAcceptsIt()
        {
            IDictionary<string, IList<bool>> defined = Defined();
            List<string> wrong = new List<string>();
            int checkedFields = 0;
            foreach (KeyValuePair<string, ToolFields> tool in GeneratedTools.Aggregations(new List<string>())
                .Where(t => t.Value.Writes))
            {
                foreach (ToolField field in tool.Value.Fields.Where(f => f.Referenced != null && !f.Listed))
                {
                    IList<bool> flags;
                    if (!defined.TryGetValue(tool.Key + " " + field.Name, out flags))
                    {
                        continue;
                    }

                    checkedFields++;
                    if (flags.Any(nullable => nullable == field.RefusesNull))
                    {
                        wrong.Add(tool.Key + " " + field.Name);
                    }
                }
            }

            Assert.True(checkedFields > 0, "照合した項目が無い。");
            Assert.True(wrong.Count == 0, "定義とホストの null の扱いが食い違う: " + string.Join(", ", wrong));
        }

        [Fact]
        public void EveryReferencedArgumentTakesNullInTheDefinitionExactlyWhenTheGeneratedBindingAcceptsIt()
        {
            IDictionary<string, IList<bool>> defined = Defined();
            List<string> wrong = new List<string>();
            int checkedArguments = 0;
            foreach (KeyValuePair<string, IList<ToolCall>> tool in GeneratedTools.Calls(new List<string>()))
            {
                foreach (ToolCall call in tool.Value)
                {
                    foreach (ToolArgument argument in call.Arguments.Where(a => a.Referenced != null))
                    {
                        IList<bool> flags;
                        if (!defined.TryGetValue(tool.Key + " " + argument.Name, out flags))
                        {
                            continue;
                        }

                        checkedArguments++;
                        if (flags.Any(nullable => nullable == argument.RefusesNull))
                        {
                            wrong.Add(tool.Key + " " + argument.Name + " (" + call.RowKey + ")");
                        }
                    }
                }
            }

            Assert.True(checkedArguments > 0, "照合した引数が無い。");
            Assert.True(wrong.Count == 0, "定義とホストの null の扱いが食い違う: " + string.Join(", ", wrong));
        }

        private static IDictionary<string, IList<bool>> Defined()
        {
            Dictionary<string, IList<bool>> found =
                new Dictionary<string, IList<bool>>(StringComparer.Ordinal);
            IDictionary<string, object> root = (IDictionary<string, object>)new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 1000,
            }.DeserializeObject(File.ReadAllText(SchemaPath()));
            foreach (IDictionary<string, object> tool in ((IEnumerable)root["tools"]).Cast<IDictionary<string, object>>())
            {
                Walk(tool["branches"], (string)tool["tool"], found);
            }

            return found;
        }

        private static void Walk(object node, string tool, IDictionary<string, IList<bool>> found)
        {
            IDictionary<string, object> item = node as IDictionary<string, object>;
            if (item != null)
            {
                object name;
                if (item.TryGetValue("name", out name)
                    && name is string
                    && !item.ContainsKey("shape")
                    && !item.ContainsKey("members")
                    && !item.ContainsKey("element")
                    && !item.ContainsKey("origin"))
                {
                    object nullable;
                    string key = tool + " " + name;
                    IList<bool> flags;
                    if (!found.TryGetValue(key, out flags))
                    {
                        flags = new List<bool>();
                        found[key] = flags;
                    }

                    flags.Add(item.TryGetValue("nullable", out nullable) && Equals(nullable, true));
                }

                foreach (object value in item.Values)
                {
                    Walk(value, tool, found);
                }

                return;
            }

            IEnumerable list = node as IEnumerable;
            if (list != null && !(node is string))
            {
                foreach (object value in list)
                {
                    Walk(value, tool, found);
                }
            }
        }

        private static string SchemaPath()
        {
            for (DirectoryInfo at = new DirectoryInfo(AppContext.BaseDirectory); at != null; at = at.Parent)
            {
                string path = Path.Combine(at.FullName, "catalog", "authored", "tool-schemas.json");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new FileNotFoundException("スキーマ正本が見つからない。");
        }
    }
}
