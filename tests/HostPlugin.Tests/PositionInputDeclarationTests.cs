using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class PositionInputDeclarationTests
    {
        private const string RangeStart = "start";

        private const string RangeCount = "count";

        [Fact]
        public void EveryDeclaredPositionInputIsRefusedAsOutOfRangeJustOutsideItsList()
        {
            List<string> wrong = new List<string>();
            IList<PositionDeclaration> declared = PositionDeclarations.Inputs();
            Assert.NotEmpty(declared);
            OnSta(() =>
            {
                foreach (PositionDeclaration input in declared)
                {
                    foreach (Probe probe in Probes(input))
                    {
                        string said = Run(input, probe);
                        if (said != null)
                        {
                            wrong.Add(input.Key + " (" + probe.Name + "): " + said);
                        }
                    }
                }
            });

            Assert.True(wrong.Count == 0, wrong.Count + " 件:\n" + string.Join("\n", wrong));
        }

        [Fact]
        public void EveryDeclaredPositionInputNamesAnInputOfItsTool()
        {
            IDictionary<string, IList<string>> inputs = InputPathsByTool();
            List<string> wrong = new List<string>();
            foreach (PositionDeclaration input in PositionDeclarations.Inputs())
            {
                IList<string> paths;
                if (!inputs.TryGetValue(input.Tool, out paths))
                {
                    wrong.Add(input.Key + ": ツール定義に無いツール");
                }
                else if (!paths.Contains(input.Input))
                {
                    wrong.Add(input.Key + ": ツール定義にその入力が無い");
                }

                if (!input.LowerOnly && (input.Of == null || !PositionPremise.Counts.ContainsKey(input.Of)))
                {
                    wrong.Add(input.Key + ": 件数の出所が前提のどの並びでもない: " + input.Of);
                }
            }

            Assert.True(wrong.Count == 0, wrong.Count + " 件:\n" + string.Join("\n", wrong));
        }

        [Fact]
        public void EveryIntegerInputIsEitherDeclaredAPositionOrListedAsNotOne()
        {
            ISet<string> declared = new HashSet<string>(
                PositionDeclarations.Inputs().Select(i => i.Key), StringComparer.Ordinal);
            ISet<string> notPositions = Keys("notPositions");
            ISet<string> population = IntegerInputs();
            List<string> wrong = new List<string>();
            foreach (string key in population)
            {
                int classes = (Declared(declared, key) ? 1 : 0) + (notPositions.Contains(key) ? 1 : 0);
                if (classes == 0)
                {
                    wrong.Add(key + ": 宣言も「位置でない入力」の一覧にも無い");
                }
                else if (classes > 1)
                {
                    wrong.Add(key + ": 複数の分類にある");
                }
            }

            foreach (string key in notPositions.Where(k => !population.Contains(k)))
            {
                wrong.Add(key + ": 一覧にあるが、整数の入力ではない");
            }

            Assert.True(
                wrong.Count == 0,
                wrong.Count + " 件(catalog/authored/position-inputs.json で分類する):\n" + string.Join("\n", wrong));
        }

        [Fact]
        public void EveryFileThatReadsPositionsIsEitherAToolWithDeclarationsOrAListedSharedFile()
        {
            ISet<string> toolsWithDeclarations = new HashSet<string>(
                PositionDeclarations.Inputs().Select(i => i.Tool), StringComparer.Ordinal);
            ISet<string> toolNames = new HashSet<string>(InputPathsByTool().Keys, StringComparer.Ordinal);
            IList<string> shared = PositionDeclarations.Readers();
            Regex reading = new Regex(@"\b(PositionInput\.Try\w+|ComposedInput\.TryPosition|ComposedInput\.TryIndices|TargetSelection\.TryResolve)\(");
            List<string> wrong = new List<string>();
            foreach (string path in Directory.GetFiles(
                Path.Combine(PositionDeclarations.RepositoryDirectory(), "src", "HostPlugin"), "*.cs"))
            {
                string file = Path.GetFileName(path);
                if (file == "PositionInput.cs" || !reading.IsMatch(File.ReadAllText(path)))
                {
                    continue;
                }

                string tool = ToolNameOf(file);
                if (toolNames.Contains(tool))
                {
                    if (!toolsWithDeclarations.Contains(tool))
                    {
                        wrong.Add(file + ": 位置を読むが、ツール " + tool + " の宣言が無い");
                    }
                }
                else if (!shared.Contains(file))
                {
                    wrong.Add(file + ": 位置を読むが、ツールの実装でも位置を読む共有のファイルの一覧にも無い");
                }
            }

            foreach (string file in shared.Where(f => !File.Exists(
                Path.Combine(PositionDeclarations.RepositoryDirectory(), "src", "HostPlugin", f))))
            {
                wrong.Add(file + ": 位置を読む共有のファイルの一覧にあるが、ファイルが無い");
            }

            Assert.True(wrong.Count == 0, wrong.Count + " 件:\n" + string.Join("\n", wrong));
        }

        [Fact]
        public void OnlyPositionInputProducesTheOutOfRangeCode()
        {
            Regex producing = new Regex(@"ToolEnvelope\.IndexOutOfRange|TOOL_INDEX_OUT_OF_RANGE|IndexOutOfRange\b");
            List<string> wrong = new List<string>();
            foreach (string path in Directory.GetFiles(
                Path.Combine(PositionDeclarations.RepositoryDirectory(), "src", "HostPlugin"), "*.cs"))
            {
                string file = Path.GetFileName(path);
                if (file != "PositionInput.cs" && file != "ToolEnvelope.cs" && producing.IsMatch(File.ReadAllText(path)))
                {
                    wrong.Add(file);
                }
            }

            Assert.True(
                wrong.Count == 0,
                "範囲外のコードを PositionInput の外で扱っている: " + string.Join(", ", wrong));
        }

        private static void OnSta(Action action)
        {
            Exception caught = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    caught = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (caught != null)
            {
                throw new InvalidOperationException("STAのスレッドで落ちた。", caught);
            }
        }

        private static IEnumerable<Probe> Probes(PositionDeclaration input)
        {
            if (input.LowerOnly)
            {
                yield return new Probe("-1", -1, true);
                yield return new Probe("0", 0, false);

                yield break;
            }

            int count = PositionPremise.Counts[input.Of];
            if (input.Span)
            {
                yield return new Probe("先頭が下", Range(-1, 1), true);
                yield return new Probe("先頭が上限ちょうど", Range(count, 1), true);
                yield return new Probe("末尾が上限ちょうど", Range(0, count + 1), true);
                yield return new Probe("末尾が最後", Range(count - 1, 1), false);

                yield break;
            }

            int ceiling = input.Insertion ? count + 1 : count;
            yield return new Probe("-1", -1, true);
            yield return new Probe("上限ちょうど", ceiling, true);
            yield return new Probe("上限+1", ceiling + 1, true);
            yield return new Probe("最後の位置", ceiling - 1, false);
        }

        private static IDictionary<string, object> Range(int start, int count)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { RangeStart, start },
                { RangeCount, count },
            };
        }

        private static string Run(PositionDeclaration input, Probe probe)
        {
            IDictionary<string, object> arguments = (IDictionary<string, object>)PositionDeclarations.Plain(input.With);
            IDictionary<string, ISet<string>> groups = PositionDeclarations.ChoiceGroupsOf(input.Tool);
            ISet<string> rivals;
            if (groups.TryGetValue(input.TopName, out rivals))
            {
                foreach (string rival in rivals.Where(r => r != input.TopName).ToList())
                {
                    arguments.Remove(rival);
                }
            }

            Assign(arguments, input.Input, probe.Value);
            using (PositionCaller caller = PositionCaller.Open(input.Tool, input.Needs))
            {
                IDictionary<string, object> envelope = caller.Call(
                    input.Tool, (IDictionary<string, object>)Resolved(arguments, caller));
                bool outside = !Equals(envelope["ok"], true)
                    && ComposedEditFixture.Code(envelope) == ToolEnvelope.IndexOutOfRange;
                if (probe.Outside && !outside)
                {
                    return Equals(envelope["ok"], true)
                        ? "範囲外なのに成功した"
                        : "範囲外なのに " + ComposedEditFixture.Code(envelope) + ": " + ComposedEditFixture.Message(envelope);
                }

                if (!probe.Outside && outside)
                {
                    return "範囲内なのに範囲外とされた: " + ComposedEditFixture.Message(envelope);
                }

                if (probe.Outside && !ComposedEditFixture.Message(envelope).Contains(input.LeafName))
                {
                    return "断る文が入力の名前 " + input.LeafName + " を含まない: " + ComposedEditFixture.Message(envelope);
                }
            }

            return null;
        }

        private static object Resolved(object given, PositionCaller caller)
        {
            IDictionary<string, object> map = given as IDictionary<string, object>;
            if (map != null)
            {
                return map.ToDictionary(p => p.Key, p => Resolved(p.Value, caller), StringComparer.Ordinal);
            }

            object[] list = given as object[];
            if (list != null)
            {
                return list.Select(i => Resolved(i, caller)).ToArray();
            }

            string token = given as string;

            return token != null && token.StartsWith("@", StringComparison.Ordinal) ? caller.Held(token) : given;
        }

        private static void Assign(IDictionary<string, object> arguments, string path, object value)
        {
            string[] parts = path.Split('.');
            IDictionary<string, object> holder = arguments;
            for (int at = 0; at < parts.Length; at++)
            {
                bool elements = parts[at].EndsWith("[]", StringComparison.Ordinal);
                string name = elements ? parts[at].Substring(0, parts[at].Length - 2) : parts[at];
                if (at == parts.Length - 1)
                {
                    holder[name] = elements ? new object[] { value } : value;

                    return;
                }

                object existing;
                holder.TryGetValue(name, out existing);
                IDictionary<string, object> child = elements
                    ? (existing as object[] ?? new object[0]).FirstOrDefault() as IDictionary<string, object>
                    : existing as IDictionary<string, object>;
                child = child ?? new Dictionary<string, object>(StringComparer.Ordinal);
                holder[name] = elements ? (object)new object[] { child } : child;
                holder = child;
            }
        }

        private static IDictionary<string, IList<string>> InputPathsByTool()
        {
            Dictionary<string, IList<string>> found = new Dictionary<string, IList<string>>(StringComparer.Ordinal);
            foreach (IDictionary<string, object> tool in PositionDeclarations.ToolSchemas())
            {
                List<string> paths = new List<string>();
                foreach (IDictionary<string, object> branch in ((IEnumerable)tool["branches"]).Cast<IDictionary<string, object>>())
                {
                    foreach (IDictionary<string, object> item in ((IEnumerable)branch["inputs"]).Cast<IDictionary<string, object>>())
                    {
                        if (!Injected(item))
                        {
                            Collect(item, (string)item["name"], paths);
                        }
                    }
                }

                found[(string)tool["tool"]] = paths.Distinct().ToList();
            }

            return found;
        }

        private static bool Injected(IDictionary<string, object> item)
        {
            object injected;

            return item.TryGetValue("injected", out injected) && Equals(injected, true);
        }

        private static void Collect(IDictionary<string, object> item, string path, IList<string> paths)
        {
            paths.Add(path);
            object members;
            if (item.TryGetValue("members", out members))
            {
                foreach (IDictionary<string, object> member in ((IEnumerable)members).Cast<IDictionary<string, object>>())
                {
                    Collect(member, path + "." + (string)member["name"], paths);
                }
            }

            object element;
            if (item.TryGetValue("element", out element))
            {
                Collect((IDictionary<string, object>)element, path + "[]", paths);
            }
        }

        private static ISet<string> Keys(string list)
        {
            return new HashSet<string>(PositionDeclarations.Listed(list), StringComparer.Ordinal);
        }

        private static bool Declared(ISet<string> declared, string key)
        {
            if (declared.Contains(key))
            {
                return true;
            }

            int space = key.IndexOf(' ');
            string tool = key.Substring(0, space);
            string name = key.Substring(space + 1);
            if (declared.Contains(tool + " args." + name) || declared.Contains(tool + " argsList[]." + name))
            {
                return true;
            }

            foreach (string member in new[] { "." + RangeStart, "." + RangeCount })
            {
                if (key.EndsWith(member, StringComparison.Ordinal)
                    && declared.Contains(key.Substring(0, key.Length - member.Length)))
                {
                    return true;
                }
            }

            return false;
        }

        private static ISet<string> IntegerInputs()
        {
            HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);
            foreach (IDictionary<string, object> read in PositionDeclarations.NumberReads())
            {
                if ((string)read["reads"] == "integer")
                {
                    found.Add((string)read["tool"] + " " + (string)read["input"]);
                }
            }

            foreach (KeyValuePair<string, IList<ToolCall>> tool in GeneratedTools.Calls(new List<string>()))
            {
                foreach (ToolArgument argument in tool.Value.SelectMany(c => c.Arguments)
                    .Where(a => !a.Injected && a.Referenced == null))
                {
                    if (argument.Type == typeof(int))
                    {
                        found.Add(tool.Key + " " + argument.Name);
                    }
                    else if (argument.Type == typeof(int[]))
                    {
                        found.Add(tool.Key + " " + argument.Name + "[]");
                    }

                    bool many = argument.Type.IsArray;
                    foreach (KeyValuePair<string, Type> member in MembersOf(argument))
                    {
                        string path = tool.Key + " " + argument.Name + (many ? "[]" : string.Empty) + "." + member.Key;
                        if (member.Value == typeof(int))
                        {
                            found.Add(path);
                        }
                        else if (member.Value == typeof(int[]))
                        {
                            found.Add(path + "[]");
                        }
                    }
                }
            }

            return found;
        }

        private static IEnumerable<KeyValuePair<string, Type>> MembersOf(ToolArgument argument)
        {
            Type type = argument.Type.IsArray ? argument.Type.GetElementType() : argument.Type;
            if (argument.Built != null)
            {
                return argument.Built.Members.Select(m => new KeyValuePair<string, Type>(m.Name, m.Type));
            }

            if (type == typeof(System.Drawing.Point))
            {
                return Ints("X", "Y");
            }

            if (type == typeof(System.Drawing.Size))
            {
                return Ints("Width", "Height");
            }

            return type == typeof(System.Drawing.Rectangle)
                ? Ints("X", "Y", "Width", "Height")
                : new KeyValuePair<string, Type>[0];
        }

        private static IEnumerable<KeyValuePair<string, Type>> Ints(params string[] names)
        {
            return names.Select(n => new KeyValuePair<string, Type>(n, typeof(int)));
        }

        private static string ToolNameOf(string file)
        {
            string stem = Path.GetFileNameWithoutExtension(file);

            return Regex.Replace(stem, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant();
        }

        private sealed class Probe
        {
            public Probe(string name, object value, bool outside)
            {
                Name = name;
                Value = value;
                Outside = outside;
            }

            public string Name { get; }

            public object Value { get; }

            public bool Outside { get; }
        }
    }
}
