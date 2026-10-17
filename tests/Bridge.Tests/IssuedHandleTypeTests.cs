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
    public sealed class IssuedHandleTypeTests
    {
        private const string ObjectType = "System.Object";

        private const string ListTypePrefix = "System.Collections.Generic.IList<";

        private static readonly Regex CallPattern = new Regex(
            "new ToolCall\\(\"([^\"]+)\", .*?DangerKind\\.\\w+, new ToolArgument\\[\\] \\{[^}]*\\}, "
                + "new ToolArgument\\[\\] \\{[^}]*\\}, (?:typeof\\(global::([^)]+)\\)|null)"
                + "(?:, typeof\\(global::([^)]+)\\))?");

        [Fact]
        public void EveryToolIssuesHandlesOfTheTypeTheEditorReturns()
        {
            IDictionary<string, string> returned = EditorReturns();
            List<string> wrong = new List<string>();
            int issuing = 0;
            foreach (Match call in CallPattern.Matches(File.ReadAllText(HostBindingPath())).Cast<Match>())
            {
                if (!call.Groups[3].Success)
                {
                    continue;
                }

                issuing++;
                string rowKey = call.Groups[1].Value;
                string declared = call.Groups[2].Value;
                string issued = call.Groups[3].Value;
                string actual;
                if (string.Equals(declared, ObjectType, StringComparison.Ordinal))
                {
                    if (!returned.TryGetValue(rowKey, out actual))
                    {
                        wrong.Add(rowKey + ": 戻り値の綴りが object なのに、エディタが返す型の表に無い");

                        continue;
                    }
                }
                else
                {
                    actual = Contained(declared);
                }

                if (!string.Equals(issued, actual, StringComparison.Ordinal))
                {
                    wrong.Add(rowKey + ": 払い出す型 " + issued + " / エディタが返す型 " + actual);
                }
            }

            Assert.True(issuing > 0, "ハンドルを払い出す呼び出しを結び付きから読めない。");
            Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        }

        private static string Contained(string type)
        {
            if (type.EndsWith("[]", StringComparison.Ordinal))
            {
                return type.Substring(0, type.Length - 2);
            }

            return type.StartsWith(ListTypePrefix, StringComparison.Ordinal) && type.EndsWith(">", StringComparison.Ordinal)
                ? type.Substring(ListTypePrefix.Length, type.Length - ListTypePrefix.Length - 1)
                : type;
        }

        private static IDictionary<string, string> EditorReturns()
        {
            return JsonNode.Parse(File.ReadAllText(Catalog("observed", "clone-returns.json")))["rows"].AsArray()
                .ToDictionary(
                    r => r["signatureKey"].GetValue<string>(),
                    r => r["returns"].GetValue<string>(),
                    StringComparer.Ordinal);
        }

        private static string HostBindingPath()
        {
            string path = typeof(IssuedHandleTypeTests).Assembly
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
