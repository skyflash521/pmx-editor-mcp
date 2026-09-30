using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public sealed class PositionInputTests
    {
        private const int HostLeast = 0;

        private static readonly Regex EntryPattern = new Regex(
            "(calls|aggregations|elements|preconditions)\\.Add\\(\"([a-z0-9_]+)\", ");

        private static readonly ISet<string> NoValueRefused = new HashSet<string>(StringComparer.Ordinal)
        {
            "model_update_faces vertex1",
            "model_update_faces vertex2",
            "model_update_faces vertex3",
            "model_update_iks target",
            "model_update_ik_links bone",
            "model_ik_link bone",
            "model_update_morph_offsets vertex",
            "model_update_morph_offsets bone",
            "model_update_morph_offsets morph",
            "model_update_node_items bone",
            "model_bone_morph_offset bone",
            "model_bone_node_item bone",
            "model_group_morph_offset morph",
            "model_uv_morph_offset vertex",
            "model_vertex_morph_offset vertex",
        };

        private static readonly Regex ReferencedFieldPattern = new Regex(
            "new ToolField\\(\"([A-Za-z0-9_]+)\", \"[^\"]*\", typeof\\(global::[^)]+\\), null, new ToolAccess\\(");

        private static readonly Regex ReferencedArgumentPattern = new Regex(
            "new ToolArgument\\(\"([A-Za-z0-9_]+)\", typeof\\(global::[^)]+\\), (?:true|false), new ToolAccess\\(");

        [Fact]
        public void EveryPositionInputDeclaresTheLowerBoundTheHostAccepts()
        {
            IDictionary<string, JsonObject> schemas = GeneratedToolDefinitions.Create()
                .ToDictionary(d => d.Name, d => SchemaRefs.Inlined(JsonNode.Parse(d.InputSchema)).AsObject(), StringComparer.Ordinal);
            List<string> wrong = new List<string>();
            int checkedForms = 0;
            foreach (KeyValuePair<string, ISet<string>> tool in NamedPositions())
            {
                foreach (string path in tool.Value)
                {
                    foreach (JsonObject form in FormsAt(schemas[tool.Key], path).Where(IsInteger))
                    {
                        checkedForms++;
                        Check(tool.Key + " " + path, form, wrong);
                    }
                }
            }

            foreach (KeyValuePair<string, ISet<string>> tool in ReferencedPositions())
            {
                JsonObject schema;
                if (!schemas.TryGetValue(tool.Key, out schema))
                {
                    continue;
                }

                foreach (string name in tool.Value)
                {
                    foreach (JsonObject form in PropertiesNamed(schema, name).Where(IsInteger))
                    {
                        checkedForms++;
                        Check(tool.Key + " " + name, form, wrong);
                    }
                }
            }

            Assert.True(checkedForms > 0, "位置として読む入力を1つも見つけられない。");
            Assert.True(wrong.Count == 0, wrong.Count + " 件:\n" + string.Join("\n", wrong.Distinct()));
        }

        [Fact]
        public void EveryReferencedPositionInputTakesNullExceptWhereTheSdkCannotHoldNoValue()
        {
            IDictionary<string, JsonObject> schemas = GeneratedToolDefinitions.Create()
                .ToDictionary(d => d.Name, d => SchemaRefs.Inlined(JsonNode.Parse(d.InputSchema)).AsObject(), StringComparer.Ordinal);
            List<string> wrong = new List<string>();
            int nullable = 0;
            int notNullable = 0;
            foreach (KeyValuePair<string, ISet<string>> tool in ReferencedPositions())
            {
                JsonObject schema;
                if (!schemas.TryGetValue(tool.Key, out schema))
                {
                    continue;
                }

                foreach (string name in tool.Value)
                {
                    bool expected = !NoValueRefused.Contains(tool.Key + " " + name);
                    foreach (JsonObject form in PropertiesNamed(schema, name).Where(IsInteger))
                    {
                        bool takesNull = form["type"] is JsonArray types
                            && types.Any(t => t.GetValue<string>() == "null");
                        if (takesNull != expected)
                        {
                            wrong.Add(tool.Key + " " + name + ": null を" + (expected ? "受け取る" : "受け取らない")
                                + "はずだが、ツール定義は " + form.ToJsonString());
                        }

                        if (takesNull)
                        {
                            nullable++;
                        }
                        else
                        {
                            notNullable++;
                        }
                    }
                }
            }

            Assert.True(nullable > 0, "null を受ける位置の入力を1つも見つけられない。");
            Assert.True(notNullable > 0, "null を受けない位置の入力を1つも見つけられない。");
            Assert.True(wrong.Count == 0, wrong.Count + " 件:\n" + string.Join("\n", wrong.Distinct()));
        }

        private static void Check(string input, JsonObject form, IList<string> wrong)
        {
            JsonNode minimum = form["minimum"];
            if (minimum == null || minimum.GetValue<double>() != HostLeast || form["exclusiveMinimum"] != null)
            {
                wrong.Add(input + ": ホストは " + HostLeast + " 以上の位置を受け取るが、ツール定義は " + form.ToJsonString());
            }
        }

        private static IDictionary<string, ISet<string>> NamedPositions()
        {
            Dictionary<string, ISet<string>> found = new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
            foreach (JsonNode tool in JsonNode.Parse(File.ReadAllText(Catalog("authored", "tool-schemas.json")))["tools"].AsArray())
            {
                string name = tool["tool"].GetValue<string>();
                HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonNode input in tool["branches"].AsArray().SelectMany(b => b["inputs"].AsArray()))
                {
                    if (input["injected"] == null || !input["injected"].GetValue<bool>())
                    {
                        Collect(input, input["name"].GetValue<string>(), null, true, paths);
                    }
                }

                found[name] = paths;
            }

            return found;
        }

        private static void Collect(JsonNode item, string path, string container, bool top, ISet<string> paths)
        {
            if (!IsHostInput(item))
            {
                return;
            }

            string name = item["name"] == null ? null : item["name"].GetValue<string>();
            bool number = item["shape"] != null && item["shape"].GetValue<string>() == "number";
            if (number
                && ((top && name == "offset")
                    || name == "parentIndex"
                    || (name == "start" && container != null && container.EndsWith("ange", StringComparison.Ordinal))))
            {
                paths.Add(path);
            }

            foreach (JsonNode member in item["members"] as JsonArray ?? new JsonArray())
            {
                Collect(member, path + "." + member["name"].GetValue<string>(), name, false, paths);
            }

            JsonNode element = item["element"];
            if (element != null
                && IsHostInput(element)
                && element["shape"] != null
                && element["shape"].GetValue<string>() == "number"
                && name != null
                && (name == "indices" || name.EndsWith("Indices", StringComparison.Ordinal)))
            {
                paths.Add(path + "[]");
            }
            else if (element != null)
            {
                Collect(element, path + "[]", name, false, paths);
            }
        }

        private static bool IsHostInput(JsonNode item)
        {
            return item["origin"] != null && item["origin"].GetValue<string>() == "hostInput";
        }

        private static IDictionary<string, ISet<string>> ReferencedPositions()
        {
            string text = File.ReadAllText(HostBindingPath());
            MatchCollection entries = EntryPattern.Matches(text);
            Dictionary<string, ISet<string>> found = new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
            for (int at = 0; at < entries.Count; at++)
            {
                int start = entries[at].Index;
                int end = at + 1 < entries.Count ? entries[at + 1].Index : text.Length;
                string body = text.Substring(start, end - start);
                string tool = entries[at].Groups[2].Value;
                ISet<string> names;
                if (!found.TryGetValue(tool, out names))
                {
                    names = new HashSet<string>(StringComparer.Ordinal);
                    found[tool] = names;
                }

                foreach (Match field in ReferencedFieldPattern.Matches(body).Cast<Match>()
                    .Concat(ReferencedArgumentPattern.Matches(body).Cast<Match>()))
                {
                    names.Add(field.Groups[1].Value);
                }
            }

            Assert.True(found.Values.Any(n => n.Count > 0), "ホストの結び付きから参照先を持つ項目を読めない。");

            return found;
        }

        private static IEnumerable<JsonObject> PropertiesNamed(JsonNode node, string name)
        {
            if (node is JsonArray many)
            {
                return many.SelectMany(n => PropertiesNamed(n, name));
            }

            if (!(node is JsonObject form))
            {
                return new JsonObject[0];
            }

            IEnumerable<JsonObject> here = form["properties"] is JsonObject properties && properties[name] != null
                ? Alternatives(properties[name])
                : new JsonObject[0];

            return here.Concat(form.Select(p => p.Value).SelectMany(v => PropertiesNamed(v, name)));
        }

        private static bool IsInteger(JsonObject form)
        {
            JsonNode type = form["type"];
            if (type is JsonArray types)
            {
                return types.Any(t => t.GetValue<string>() == "integer");
            }

            return type != null && type.GetValue<string>() == "integer";
        }

        private static IList<JsonObject> FormsAt(JsonObject schema, string path)
        {
            List<string> steps = new List<string>();
            foreach (string part in path.Split('.'))
            {
                string name = part;
                int elements = 0;
                while (name.EndsWith("[]", StringComparison.Ordinal))
                {
                    name = name.Substring(0, name.Length - 2);
                    elements++;
                }

                steps.Add("." + name);
                steps.AddRange(Enumerable.Repeat("[]", elements));
            }

            IEnumerable<JsonObject> current = Holders(schema);
            foreach (string step in steps)
            {
                current = step == "[]"
                    ? current.SelectMany(f => Alternatives(f["items"]))
                    : current.SelectMany(f => Alternatives(
                        f["properties"] is JsonObject properties ? properties[step.Substring(1)] : null));
            }

            return current.Where(f => f.Count > 0).ToList();
        }

        private static IEnumerable<JsonObject> Holders(JsonObject schema)
        {
            yield return schema;
            foreach (JsonObject nested in Alternatives(schema["anyOf"]).Concat(
                (schema["allOf"] as JsonArray ?? new JsonArray())
                    .Select(r => r["then"] as JsonObject)
                    .Where(t => t != null)
                    .SelectMany(t => new[] { t }.Concat(Alternatives(t["anyOf"])))))
            {
                yield return nested;
            }
        }

        private static IEnumerable<JsonObject> Alternatives(JsonNode node)
        {
            if (node is JsonArray many)
            {
                return many.SelectMany(Alternatives);
            }

            if (!(node is JsonObject form))
            {
                return new JsonObject[0];
            }

            JsonArray any = form["anyOf"] as JsonArray ?? form["oneOf"] as JsonArray;

            return any == null || form.ContainsKey("type") ? new[] { form } : any.SelectMany(Alternatives);
        }

        private static string HostBindingPath()
        {
            string path = typeof(PositionInputTests).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>()
                .Where(a => a.Key == "HostBindingPath")
                .Select(a => a.Value)
                .SingleOrDefault();
            Assert.True(path != null && File.Exists(path), "ホストのツールの結び付きが無い: " + path);

            return path;
        }

        private static string Catalog(string part, string name)
        {
            for (DirectoryInfo at = new DirectoryInfo(AppContext.BaseDirectory); at != null; at = at.Parent)
            {
                string path = Path.Combine(at.FullName, "catalog", part, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new FileNotFoundException("正本が見つからない: " + part + "/" + name);
        }
    }
}
