using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using PmxEditorMcp.SignatureDump;
using Xunit;
using Xunit.Abstractions;

namespace PmxEditorMcp.Bridge.Tests
{
    public sealed class BridgeServerListingLimitTests
    {
        private static readonly TimeSpan TestWait = TimeSpan.FromSeconds(60);

        private const string LimitName = "limit";

        private const string MaximumName = "maximum";

        private const string ItemsName = "items";

        private const string TotalName = "total";

        private const string FieldsName = "fields";

        private const string HostOutputOrigin = "hostOutput";

        private const string HandleItem = "handle";

        private const string IndexItem = "index";

        private const string ParentIndexItem = "parentIndex";

        private const string ParentHandleItem = "parentHandle";

        private const string IndexInParentItem = "indexInParent";

        private const string ItemTypeItem = "itemType";

        private const string UnknownItemType = "知らない型";

        private const int ShortestEventTypeChars = 1;

        private static readonly string[] PointingItems =
        {
            HandleItem, IndexItem, ParentIndexItem, ParentHandleItem, IndexInParentItem,
        };

        private const string CountedTotal = "total";

        private const string ListTypePrefix = "System.Collections.Generic.IList<";

        private const int NullChars = 4;

        private const int EventQueueCapacity = 1000;

        private const string EventPollTool = "view_poll_events";

        private static readonly Lazy<Shapes> KnownShapes = new Lazy<Shapes>(() => new Shapes());

        private static readonly IDictionary<string, Listing> HostAssembledListings =
            new Dictionary<string, Listing>(StringComparer.Ordinal)
            {
                {
                    "model_find_referrers",
                    new Listing(new[] { CountedTotal }, 0, "referrerIndices", "0".Length, 1)
                },
                {
                    "model_find_material_vertices",
                    new Listing(new[] { CountedTotal }, 0, "vertexIndices", "0".Length, 1)
                },
                {
                    "model_find_bone_weights",
                    new Listing(
                        new[] { "count", CountedTotal },
                        "0".Length,
                        "bones",
                        "{\"index\":0,\"name\":\"\",\"vertexCount\":0,\"weightSum\":0,\"weightMax\":0}".Length)
                },
                {
                    "model_find_surface_intersections",
                    new Listing(
                        new[] { "count", "faceCount", "otherFaceCount", "totalLength", "maxDepth" },
                        "0".Length * 5,
                        "pairs",
                        ("{\"material\":0,\"face\":0,\"otherMaterial\":0,\"otherFace\":0,\"depth\":0,"
                            + "\"length\":0,\"start\":[0,0,0],\"end\":[0,0,0]}").Length)
                },
                {
                    "model_find_parts",
                    new Listing(
                        new[] { "count" },
                        "0".Length,
                        "parts",
                        ("{\"vertexRuns\":[{\"start\":0,\"count\":0}],\"vertexCount\":0,\"faceCount\":0,"
                            + "\"min\":[0,0,0],\"max\":[0,0,0]}").Length)
                },
                {
                    "view_pick_screen_point",
                    new Listing(
                        new[] { "count" },
                        "0".Length,
                        "hits",
                        ("{\"material\":0,\"face\":0,\"vertices\":[0,0,0],\"point\":[0,0,0],\"distance\":0,"
                            + "\"frontFacing\":true,\"vertex\":0,\"vertexDistance\":0}").Length)
                },
                {
                    "model_validate_pmx",
                    new Listing(
                        new[]
                        {
                            "unsoundFaces", "danglingFaces", "danglingWeights", "danglingBones",
                            "danglingMorphOffsets", "danglingNodeItems", "danglingPhysics",
                            "unnormalizedWeights", "duplicateFaces", "hiddenMorphsInExpressionFrame",
                            "found", "runsTotal",
                        },
                        "0".Length * 12,
                        "runs",
                        "{\"start\":0,\"count\":0}".Length)
                },
                {
                    "editor_find_operation",
                    new Listing(
                        new[] { CountedTotal, "editorVersion" },
                        EditorVersionChars(),
                        "matches",
                        "{\"form\":\"\",\"path\":[],\"route\":[]}".Length + ShortestFormChars())
                    {
                        Most = MostMatchesOfOneSearch(),
                    }
                },
                {
                    "view_poll_events",
                    new Listing(
                        new[] { "dropped", "remaining" },
                        "0".Length * 2,
                        "events",
                        "{\"seq\":0,\"type\":\"\",\"sourceHandle\":0,\"payload\":0}".Length + ShortestEventTypeChars,
                        1)
                    {
                        Most = EventQueueCapacity,
                    }
                },
            };

        private static readonly ISet<string> AcceptedUnderivedReasons = new HashSet<string>(StringComparer.Ordinal);

        private readonly ITestOutputHelper _output;

        public BridgeServerListingLimitTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData(null)]
        [InlineData(BridgeBudget.MinimumChars)]
        [InlineData(BridgeBudget.MaximumChars)]
        public async Task EveryListingTakesAtLeastTheSmallestItemsThatFitInOneResponseOfTheBudget(int? budgetChars)
        {
            IDictionary<string, int> published = await MaximaAsync(
                budgetChars?.ToString(CultureInfo.InvariantCulture));
            int valueChars = (budgetChars ?? BridgeBudget.DefaultChars) - WarningRoomChars();

            List<string> wrong = new List<string>();
            Dictionary<string, string> underived = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> one in published)
            {
                string tool = one.Key.Substring(0, one.Key.IndexOf('/'));
                string reason;
                Listing listing = KnownShapes.Value.Of(tool, out reason);
                if (listing == null)
                {
                    underived[tool] = reason;
                    continue;
                }

                int fitting = listing.FittingCount(valueChars);
                int wanted = listing.Most.HasValue ? Math.Min(fitting, listing.Most.Value) : fitting;
                if (one.Value < wanted)
                {
                    wrong.Add(one.Key + ": maximum " + one.Value + "(1件 " + listing.ItemChars
                        + " 文字の最も小さい項目は " + fitting + " 件が1回に収まり、1回で返しうるのは "
                        + (listing.Most.HasValue ? listing.Most.Value.ToString(CultureInfo.InvariantCulture) : "上限なし")
                        + ")");
                }
            }

            _output.WriteLine("1件の最も小さい形を導けない一覧 " + underived.Count + " 件");
            foreach (KeyValuePair<string, string> one in underived.OrderBy(u => u.Key, StringComparer.Ordinal))
            {
                _output.WriteLine(one.Key + ": " + one.Value);
            }

            string[] unaccepted = underived
                .Where(u => !AcceptedUnderivedReasons.Contains(u.Value))
                .Select(u => u.Key + ": " + u.Value)
                .OrderBy(u => u, StringComparer.Ordinal)
                .ToArray();
            Assert.True(
                unaccepted.Length == 0,
                "1件の最も小さい形を導けない一覧が " + unaccepted.Length + " 件ある:\n" + string.Join("\n", unaccepted));
            Assert.True(
                wrong.Count == 0,
                "1回に収まる件数より maximum が小さい一覧が " + wrong.Count + " 件ある:\n"
                    + string.Join("\n", wrong.Take(20)));
        }

        private static int EditorVersionChars()
        {
            using JsonDocument structure = JsonDocument.Parse(File.ReadAllText(Catalog("observed", "ui-structure.json")));

            return structure.RootElement.GetProperty("editorVersion").GetString().Length + 2;
        }

        [Fact]
        public void TheEventPollCountBoundInTheCatalogIsTheQueueCapacity()
        {
            using JsonDocument schemas = JsonDocument.Parse(File.ReadAllText(Catalog("authored", "tool-schemas.json")));
            JsonElement poll = schemas.RootElement.GetProperty("tools").EnumerateArray()
                .Single(t => t.GetProperty("tool").GetString() == EventPollTool);
            int[] bounds = poll.GetProperty("branches").EnumerateArray()
                .SelectMany(b => b.GetProperty("inputs").EnumerateArray())
                .Where(i => i.GetProperty("name").GetString() == LimitName)
                .Select(i => i.GetProperty("bounds").GetProperty(MaximumName).GetInt32())
                .ToArray();

            Assert.Equal(new[] { EventQueueCapacity }, bounds.Distinct().ToArray());
        }

        private static int MostMatchesOfOneSearch()
        {
            using JsonDocument structure = JsonDocument.Parse(File.ReadAllText(Catalog("observed", "ui-structure.json")));
            List<string[]> matchables = new List<string[]>();
            foreach (JsonElement window in structure.RootElement.GetProperty("windows").EnumerateArray())
            {
                matchables.Add(new[] { TextOf(window, "title") });
                AddParts(window.GetProperty("root"), matchables);
                if (window.TryGetProperty("messages", out JsonElement said))
                {
                    matchables.AddRange(said.EnumerateArray().Select(m => new[] { m.GetString() }));
                }
            }

            return matchables.Count(m => m.Any(t => !string.IsNullOrEmpty(t)));
        }

        private static void AddParts(JsonElement node, IList<string[]> matchables)
        {
            if (!node.TryGetProperty("children", out JsonElement children))
            {
                return;
            }

            foreach (JsonElement child in children.EnumerateArray())
            {
                matchables.Add(new[] { TextOf(child, "text"), TextOf(child, "toolTip") });
                AddParts(child, matchables);
            }
        }

        private static string TextOf(JsonElement node, string name)
        {
            return node.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static int ShortestFormChars()
        {
            using JsonDocument structure = JsonDocument.Parse(File.ReadAllText(Catalog("observed", "ui-structure.json")));

            return structure.RootElement.GetProperty("windows").EnumerateArray()
                .Min(w => w.GetProperty("form").GetString().Length);
        }

        private static string HostBindings()
        {
            string path = typeof(BridgeServerListingLimitTests).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>()
                .Where(a => a.Key == "HostBindingPath")
                .Select(a => a.Value)
                .SingleOrDefault();
            Assert.True(path != null && File.Exists(path), "ホストのツールの結び付きが無い: " + path);

            return File.ReadAllText(path);
        }

        private static int WarningRoomChars()
        {
            using JsonDocument contract = JsonDocument.Parse(File.ReadAllText(Catalog("authored", "common-contract.json")));

            return contract.RootElement.GetProperty("budgets").GetProperty("warningRoomChars").GetInt32();
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

        private static async Task<IDictionary<string, int>> MaximaAsync(string budgetChars)
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client =
                await BridgeToolsTests.StartBridgeAsync(null, budgetChars, limit.Token);

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: limit.Token);
            Dictionary<string, int> found = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (McpClientTool tool in tools)
            {
                using JsonDocument inlined =
                    JsonDocument.Parse(SchemaRefs.Inlined(tool.ProtocolTool.InputSchema.GetRawText()));
                Collect(tool.Name, inlined.RootElement, found);
            }

            return found;
        }

        private static void Collect(string at, JsonElement schema, IDictionary<string, int> found)
        {
            if (schema.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (JsonElement item in schema.EnumerateArray())
                {
                    Collect(at + "/" + index.ToString(CultureInfo.InvariantCulture), item, found);
                    index++;
                }

                return;
            }

            if (schema.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (JsonProperty member in schema.EnumerateObject())
            {
                if (string.Equals(member.Name, "properties", StringComparison.Ordinal)
                    && member.Value.ValueKind == JsonValueKind.Object
                    && member.Value.TryGetProperty(LimitName, out JsonElement counted)
                    && counted.ValueKind == JsonValueKind.Object
                    && counted.TryGetProperty(MaximumName, out JsonElement maximum))
                {
                    found[at + "/properties/limit"] = maximum.GetDouble() >= int.MaxValue
                        ? int.MaxValue
                        : (int)maximum.GetDouble();
                }

                Collect(at + "/" + member.Name, member.Value, found);
            }
        }

        private enum Growth
        {
            Fixed,
            Sequenced,
            Paired,
        }

        private sealed class Way
        {
            public Way(int itemChars, Growth growth)
            {
                ItemChars = itemChars;
                Growth = growth;
            }

            public int ItemChars { get; }

            public Growth Growth { get; }

            public long Widened(int count)
            {
                switch (Growth)
                {
                    case Growth.Sequenced:
                        long widened = 0;
                        for (long at = 10; at < count; at *= 10)
                        {
                            widened += count - at;
                        }

                        return widened;

                    case Growth.Paired:
                        return PairedWidened(count);

                    default:
                        return 0;
                }
            }

            private static long PairedWidened(int count)
            {
                long left = count;
                long widened = 0;
                for (int digits = 2; left > 0; digits++)
                {
                    long pairs = 0;
                    for (int first = 1; first < digits; first++)
                    {
                        pairs = checked(pairs + WithDigits(first) * WithDigits(digits - first));
                    }

                    long taken = Math.Min(left, pairs);
                    widened += taken * (digits - 2);
                    left -= taken;
                }

                return widened;
            }

            private static long WithDigits(int digits)
            {
                if (digits > 18)
                {
                    return long.MaxValue / 1000;
                }

                long upper = 1;
                for (int at = 0; at < digits; at++)
                {
                    upper *= 10;
                }

                return digits == 1 ? 10 : upper - upper / 10;
            }
        }

        private sealed class Listing
        {
            public Listing(
                IList<string> wrapperNames, int wrapperValueChars, string pageName, int itemChars, int sequenced = 0)
                : this(
                    wrapperNames,
                    wrapperValueChars,
                    pageName,
                    new[] { new Way(itemChars, sequenced == 1 ? Growth.Sequenced : Growth.Fixed) })
            {
            }

            public Listing(IList<string> wrapperNames, int wrapperValueChars, string pageName, IList<Way> ways)
            {
                WrapperNames = wrapperNames;
                WrapperValueChars = wrapperValueChars;
                PageName = pageName;
                Ways = ways;
            }

            public IList<string> WrapperNames { get; }

            public int WrapperValueChars { get; }

            public string PageName { get; }

            public IList<Way> Ways { get; }

            public int? Most { get; init; }

            public int ItemChars
            {
                get { return Ways.Min(w => w.ItemChars); }
            }

            public int FittingCount(int valueChars)
            {
                return Ways.Max(way => FittingCount(way, valueChars));
            }

            public int FittingCount(Way way, int valueChars)
            {
                int count = 0;
                while (Chars(way, count + 1) <= valueChars)
                {
                    count++;
                }

                return count;
            }

            private long Chars(Way way, int count)
            {
                long wrapper = 2 + WrapperValueChars;
                foreach (string name in WrapperNames.Concat(new[] { PageName }))
                {
                    wrapper += name.Length + 3 + 1;
                }

                if (WrapperNames.Contains(CountedTotal))
                {
                    wrapper += count.ToString(CultureInfo.InvariantCulture).Length;
                }

                wrapper += 2 - 1;

                return wrapper + (long)count * way.ItemChars + Math.Max(0, count - 1) + way.Widened(count);
            }
        }

        private sealed class Shapes
        {
            private static readonly Regex Head = new Regex("^対象 \\S+ / 動作 (\\S+) / 出所 (\\S+)$");

            private readonly IDictionary<string, JsonElement> _schemas;

            private readonly IDictionary<string, KeyValuePair<string, string>> _sources;

            private readonly IDictionary<string, string> _roles;

            private readonly IDictionary<string, int> _components;

            private readonly ShippedToolDefinitionTests.SdkMetadata _sdk;

            private readonly string _bindings = HostBindings();

            private readonly Dictionary<string, SdkDeclared> _declared =
                new Dictionary<string, SdkDeclared>(StringComparer.Ordinal);

            public Shapes()
            {
                using (JsonDocument schemas = JsonDocument.Parse(File.ReadAllText(Catalog("authored", "tool-schemas.json"))))
                {
                    _schemas = schemas.RootElement.GetProperty("tools").EnumerateArray()
                        .ToDictionary(t => t.GetProperty("tool").GetString(), t => t.Clone(), StringComparer.Ordinal);
                }

                using (JsonDocument roles = JsonDocument.Parse(File.ReadAllText(Catalog("authored", "type-roles.json"))))
                {
                    _roles = roles.RootElement.GetProperty("types").EnumerateArray()
                        .ToDictionary(
                            t => t.GetProperty("typeName").GetString(),
                            t => t.GetProperty("role").GetString(),
                            StringComparer.Ordinal);
                }

                using (JsonDocument contract = JsonDocument.Parse(File.ReadAllText(Catalog("authored", "common-contract.json"))))
                {
                    _components = contract.RootElement.GetProperty("components").EnumerateArray()
                        .ToDictionary(
                            c => c.GetProperty("typeName").GetString(),
                            c => c.GetProperty("count").GetInt32(),
                            StringComparer.Ordinal);
                }

                _sources = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal);
                foreach (GeneratedToolDefinition definition in GeneratedToolDefinitions.Create())
                {
                    Match match = Head.Match(definition.Description.Split('\n')[0]);
                    if (match.Success)
                    {
                        _sources[definition.Name] = new KeyValuePair<string, string>(
                            match.Groups[2].Value, match.Groups[1].Value);
                    }
                }

                IList<string> paths = ShippedToolDefinitionTests.SdkAssemblyPaths();
                _sdk = new ShippedToolDefinitionTests.SdkMetadata(paths);
                foreach (string path in paths)
                {
                    ReadDeclared(path);
                }
            }

            private static JsonElement Answer(JsonElement output)
            {
                JsonElement answer = output;
                while (!answer.TryGetProperty("members", out _)
                    && IsHostOutput(answer)
                    && answer.TryGetProperty("element", out JsonElement element)
                    && IsHostOutput(element))
                {
                    answer = element;
                }

                return answer;
            }

            private static bool IsHostOutput(JsonElement item)
            {
                return item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("origin", out JsonElement origin)
                    && origin.GetString() == HostOutputOrigin;
            }

            public Listing Of(string tool, out string reason)
            {
                reason = null;
                Listing assembled;
                if (HostAssembledListings.TryGetValue(tool, out assembled))
                {
                    return assembled;
                }

                JsonElement schema;
                if (!_schemas.TryGetValue(tool, out schema))
                {
                    reason = "正本にツールが無い";

                    return null;
                }

                JsonElement items = default;
                bool found = false;
                if (Answer(schema.GetProperty("output")).TryGetProperty("members", out JsonElement members))
                {
                    foreach (JsonElement member in members.EnumerateArray())
                    {
                        if (member.GetProperty("name").GetString() == ItemsName)
                        {
                            items = member;
                            found = true;
                        }
                    }
                }

                if (!found || !items.TryGetProperty("element", out JsonElement element))
                {
                    reason = "出力の直下に items が無い";

                    return null;
                }

                KeyValuePair<string, string> source;
                bool sourced = _sources.TryGetValue(tool, out source);
                if (element.TryGetProperty("members", out JsonElement parts))
                {
                    string returned = sourced ? ReturnType(source.Key, source.Value) : null;
                    IList<Way> ways = ObjectItemWays(
                        tool,
                        schema,
                        parts,
                        sourced ? source.Key : null,
                        returned == null ? null : ElementType(returned.TrimEnd('&')),
                        out reason);

                    return ways == null ? null : new Listing(new[] { CountedTotal }, 0, ItemsName, ways);
                }

                int? itemChars = ValueItemChars(
                    element, sourced ? source : (KeyValuePair<string, string>?)null, out reason);

                return itemChars == null
                    ? null
                    : new Listing(
                        new[] { CountedTotal },
                        0,
                        ItemsName,
                        itemChars.Value,
                        tool.Contains("indices", StringComparison.Ordinal) ? 1 : 0);
            }

            private int ItemTypeChars(string tool)
            {
                return ItemTypes(tool).Concat(new[] { UnknownItemType }).Min(t => t.Length) + 2;
            }

            private IEnumerable<string> ItemTypes(string tool)
            {
                Match entry = Regex.Match(
                    _bindings, "\\.Add\\(\"" + Regex.Escape(tool) + "\", [^\\n]*");

                return entry.Success
                    ? Regex.Matches(entry.Value, "new ToolItem\\(\"([a-z0-9_]+)\"").Select(m => m.Groups[1].Value)
                    : new string[0];
            }

            private IList<Way> ObjectItemWays(
                string tool,
                JsonElement schema,
                JsonElement parts,
                string source,
                string returnedElement,
                out string reason)
            {
                reason = null;
                Dictionary<string, int> hosted = new Dictionary<string, int>(StringComparer.Ordinal);
                List<int> chosen = new List<int>();
                foreach (JsonElement part in parts.EnumerateArray())
                {
                    string name = part.GetProperty("name").GetString();
                    bool host = part.TryGetProperty("origin", out JsonElement origin)
                        && origin.GetString() == HostOutputOrigin;
                    int? value = host ? CatalogValueChars(part) : SdkMemberChars(source, name, out reason);
                    if (value == null && !host && returnedElement != null)
                    {
                        value = SdkMemberChars(returnedElement, name, out reason);
                    }
                    if (value == null)
                    {
                        reason = reason ?? "ホストが返す項目 " + name + " の形が正本に無い";

                        return null;
                    }

                    if (host && name == ItemTypeItem)
                    {
                        value = ItemTypeChars(tool);
                    }

                    int chars = name.Length + 3 + value.Value;
                    if (host)
                    {
                        hosted[name] = chars;
                    }
                    else
                    {
                        chosen.Add(chars);
                    }
                }

                List<int> fixedItems = hosted
                    .Where(p => !PointingItems.Contains(p.Key, StringComparer.Ordinal))
                    .Select(p => p.Value)
                    .ToList();
                List<KeyValuePair<List<int>, Growth>> pointings = new List<KeyValuePair<List<int>, Growth>>();
                foreach (string alone in new[] { HandleItem, IndexItem })
                {
                    if (hosted.TryGetValue(alone, out int chars))
                    {
                        pointings.Add(new KeyValuePair<List<int>, Growth>(new List<int> { chars }, Growth.Sequenced));
                    }
                }

                if (hosted.TryGetValue(IndexInParentItem, out int inParent))
                {
                    foreach (string parent in new[] { ParentIndexItem, ParentHandleItem })
                    {
                        if (hosted.TryGetValue(parent, out int chars))
                        {
                            pointings.Add(new KeyValuePair<List<int>, Growth>(
                                new List<int> { chars, inParent }, Growth.Paired));
                        }
                    }
                }

                if (pointings.Count == 0)
                {
                    pointings.Add(new KeyValuePair<List<int>, Growth>(new List<int>(), Growth.Fixed));
                }

                bool narrowed = schema.GetProperty("branches").EnumerateArray()
                    .SelectMany(b => b.GetProperty("inputs").EnumerateArray())
                    .Any(i => i.GetProperty("name").GetString() == FieldsName);
                List<Way> ways = new List<Way>();
                foreach (KeyValuePair<List<int>, Growth> pointing in pointings)
                {
                    List<int> taken = fixedItems.Concat(pointing.Key).ToList();
                    if (narrowed)
                    {
                        if (taken.Count == 0 && chosen.Count > 0)
                        {
                            taken.Add(chosen.Min());
                        }
                    }
                    else
                    {
                        taken.AddRange(chosen);
                    }

                    ways.Add(new Way(2 + taken.Sum() + Math.Max(0, taken.Count - 1), pointing.Value));
                }

                return ways;
            }

            private int? ValueItemChars(JsonElement element, KeyValuePair<string, string>? source, out string reason)
            {
                reason = null;
                int? catalog = CatalogValueChars(element);
                if (catalog != null)
                {
                    return catalog;
                }

                if (source == null)
                {
                    reason = "出所の無いツール";

                    return null;
                }

                string returned = ReturnType(source.Value.Key, source.Value.Value);
                if (returned == null)
                {
                    reason = "出所の型に呼び先のメソッドが見つからない";

                    return null;
                }

                string elementType = ElementType(returned);
                if (elementType == null)
                {
                    reason = "呼び先の戻り値が並びでない: " + returned;

                    return null;
                }

                return TypeChars(elementType, out reason);
            }

            private int? SdkMemberChars(string source, string name, out string reason)
            {
                reason = null;
                if (source == null)
                {
                    reason = "出所の無いツール";

                    return null;
                }

                IList<string> types = _sdk.PropertyTypes(source, name);
                if (types.Count == 0)
                {
                    reason = "出所の型にプロパティ " + name + " が無い";

                    return null;
                }

                int? least = null;
                foreach (string type in types)
                {
                    int? chars = TypeChars(type, out reason);
                    if (chars == null)
                    {
                        return null;
                    }

                    least = least == null ? chars : Math.Min(least.Value, chars.Value);
                }

                return least;
            }

            private int? TypeChars(string type, out string reason)
            {
                reason = null;
                string plain = type.TrimEnd('&');
                if (plain.StartsWith("System.Nullable<", StringComparison.Ordinal))
                {
                    int? inner = TypeChars(plain.Substring(16, plain.Length - 17), out reason);

                    return inner == null ? null : Math.Min(inner.Value, NullChars);
                }

                if (ElementType(plain) != null)
                {
                    return "[]".Length;
                }

                string role;
                if (_roles.TryGetValue(plain, out role) && (role == "operationTarget" || role == "handleTarget"))
                {
                    return "0".Length;
                }

                bool? valueType = _sdk.IsValueType(plain);
                int nullable = valueType == false ? NullChars : int.MaxValue;
                if (_sdk.IsEnum(plain))
                {
                    return Math.Min(nullable, _sdk.EnumNames(plain).Where(n => n != "value__").Min(n => n.Length) + 2);
                }

                int count;
                if (_components.TryGetValue(plain, out count))
                {
                    return Math.Min(nullable, count * 2 + 1);
                }

                switch (plain)
                {
                    case "System.Boolean":
                        return "true".Length;
                    case "System.Byte":
                    case "System.Int32":
                    case "System.Single":
                    case "System.Double":
                    case "System.Object":
                        return "0".Length;
                    case "System.String":
                        return "\"\"".Length;
                    case "System.Version":
                    case "System.Drawing.Font":
                    case "System.Drawing.Brush":
                    case "System.Drawing.Bitmap":
                        return NullChars;
                    case "System.Drawing.Color":
                    case "System.Drawing.Rectangle":
                        return "[0,0,0,0]".Length;
                    case "System.Drawing.Size":
                    case "System.Drawing.Point":
                        return "[0,0]".Length;
                    default:
                        reason = "ホストが値として写す型でない: " + plain;

                        return null;
                }
            }

            private static string ElementType(string type)
            {
                if (type.EndsWith("[]", StringComparison.Ordinal))
                {
                    return type.Substring(0, type.Length - 2);
                }

                if (type.StartsWith(ListTypePrefix, StringComparison.Ordinal) && type.EndsWith(">", StringComparison.Ordinal))
                {
                    return type.Substring(ListTypePrefix.Length, type.Length - ListTypePrefix.Length - 1);
                }

                return null;
            }

            private static int? CatalogValueChars(JsonElement item)
            {
                int? chars;
                if (item.TryGetProperty("element", out _))
                {
                    chars = "[]".Length;
                }
                else if (item.TryGetProperty("members", out JsonElement members))
                {
                    List<int> each = new List<int>();
                    foreach (JsonElement member in members.EnumerateArray())
                    {
                        int? value = CatalogValueChars(member);
                        if (value == null)
                        {
                            return null;
                        }

                        each.Add(member.GetProperty("name").GetString().Length + 3 + value.Value);
                    }

                    chars = 2 + each.Sum() + Math.Max(0, each.Count - 1);
                }
                else if (item.TryGetProperty("shape", out JsonElement shape))
                {
                    switch (shape.GetString())
                    {
                        case "number":
                            chars = "0".Length;
                            break;
                        case "text":
                            chars = "\"\"".Length;
                            break;
                        case "boolean":
                        case "null_value":
                            chars = NullChars;
                            break;
                        default:
                            chars = null;
                            break;
                    }
                }
                else
                {
                    chars = null;
                }

                if (item.TryGetProperty("nullable", out JsonElement nullable) && nullable.ValueKind == JsonValueKind.True)
                {
                    chars = chars == null ? NullChars : Math.Min(chars.Value, NullChars);
                }

                return chars;
            }

            private string ReturnType(string type, string action)
            {
                string wanted = action.Replace("_", string.Empty).ToLowerInvariant();
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                Queue<string> pending = new Queue<string>(new[] { type });
                while (pending.Count > 0)
                {
                    string next = pending.Dequeue();
                    SdkDeclared declared;
                    if (!seen.Add(next) || !_declared.TryGetValue(next, out declared))
                    {
                        continue;
                    }

                    string returned;
                    if (declared.Returns.TryGetValue(wanted, out returned))
                    {
                        return returned;
                    }

                    foreach (string inherited in declared.Inherited)
                    {
                        pending.Enqueue(inherited);
                    }
                }

                return null;
            }

            private void ReadDeclared(string path)
            {
                using (FileStream stream = File.OpenRead(path))
                using (PEReader image = new PEReader(stream))
                {
                    MetadataReader reader = image.GetMetadataReader();
                    ShippedToolDefinitionTests.SdkTypeNames names = new ShippedToolDefinitionTests.SdkTypeNames();
                    foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
                    {
                        TypeDefinition definition = reader.GetTypeDefinition(handle);
                        string name = names.GetTypeFromDefinition(reader, handle, 0);
                        if (_declared.ContainsKey(name))
                        {
                            continue;
                        }

                        List<string> inherited = definition.GetInterfaceImplementations()
                            .Select(i => names.Of(reader, reader.GetInterfaceImplementation(i).Interface))
                            .ToList();
                        if (!definition.BaseType.IsNil)
                        {
                            inherited.Add(names.Of(reader, definition.BaseType));
                        }

                        Dictionary<string, string> returns = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (MethodDefinition method in definition.GetMethods().Select(reader.GetMethodDefinition))
                        {
                            if ((method.Attributes & MethodAttributes.SpecialName) != 0)
                            {
                                continue;
                            }

                            string key = reader.GetString(method.Name).Replace("_", string.Empty).ToLowerInvariant();
                            if (!returns.ContainsKey(key))
                            {
                                returns[key] = method.DecodeSignature(names, null).ReturnType;
                            }
                        }

                        _declared[name] = new SdkDeclared(inherited, returns);
                    }
                }
            }
        }

        private sealed class SdkDeclared
        {
            public SdkDeclared(IList<string> inherited, IDictionary<string, string> returns)
            {
                Inherited = inherited;
                Returns = returns;
            }

            public IList<string> Inherited { get; }

            public IDictionary<string, string> Returns { get; }
        }
    }
}
