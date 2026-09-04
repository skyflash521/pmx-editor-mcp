using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolMapGateTests
    {
        private const string Key = "PEPlugin.Pmx.IPXPmxConnector.GetCurrentState()";

        private const string MapJson = @"{ ""rows"": [
  { ""signatureKey"": """ + Key + @""",
    ""capabilityIds"": [""CAP-001""],
    ""rowKind"": ""commonContract"",
    ""editKind"": ""read"",
    ""direction"": ""read"",
    ""basis"": ""現在のPMXの複製を返すだけである。"",
    ""assignment"": ""internalFlow"",
    ""target"": ""stateRead"",
    ""slotBinding"": { ""return"": ""pmxClone"", ""receiver"": ""owningObject"",
                       ""parameters"": {} } }
] }";

        private const string AssignmentsJson = @"{ ""assignments"": [
  { ""signatureKey"": """ + Key + @""",
    ""assignment"": ""internalFlow"",
    ""target"": ""stateRead"",
    ""slotBinding"": { ""return"": ""pmxClone"", ""receiver"": ""owningObject"",
                       ""parameters"": {} },
    ""basis"": ""現在のPMXの複製を得る呼び出しそのものである。"" }
] }";

        private static SignatureRecord Signature(
            OperationDirection direction, IList<ParameterRecord> parameters)
        {
            return new SignatureRecord(
                Key,
                "PEPlugin.Pmx.IPXPmxConnector",
                MemberKind.Method,
                "GetCurrentState",
                false,
                0,
                parameters ?? new ParameterRecord[0],
                "PEPlugin.Pmx.IPXPmx",
                false,
                false,
                direction);
        }

        private static void Require(
            string mapJson = MapJson,
            string assignmentsJson = AssignmentsJson,
            ISet<string> provided = null,
            IDictionary<string, ISet<string>> owners = null,
            IDictionary<string, DangerKind> dangers = null,
            OperationDirection direction = OperationDirection.Read,
            IDictionary<string, string> notes = null,
            ISet<string> updateKinds = null,
            ISet<string> elementNouns = null,
            ISet<string> typeNames = null,
            IList<ParameterRecord> parameters = null)
        {
            ToolMapGate.Require(
                ToolMapJsonReader.Read(mapJson),
                new ToolMapEvidence(
                    provided ?? new HashSet<string>(new[] { Key }, StringComparer.Ordinal),
                    owners ?? new Dictionary<string, ISet<string>>(StringComparer.Ordinal)
                    {
                        { Key, new HashSet<string>(new[] { "CAP-001" }, StringComparer.Ordinal) },
                    },
                    new Dictionary<string, SignatureRecord>(StringComparer.Ordinal)
                    {
                        { Key, Signature(direction, parameters) },
                    },
                    dangers ?? new Dictionary<string, DangerKind>(StringComparer.Ordinal),
                    notes ?? new Dictionary<string, string>(StringComparer.Ordinal),
                    updateKinds ?? new HashSet<string>(
                        new[] { "Vertex", "Bone" }, StringComparer.Ordinal),
                    elementNouns ?? new HashSet<string>(
                        new[] { "vertex" }, StringComparer.Ordinal),
                    typeNames ?? new HashSet<string>(
                        new[] { "PEPlugin.SDX.V3" }, StringComparer.Ordinal)),
                CommonAssignmentJsonReader.Read(assignmentsJson));
        }

        /// <summary>判定を1つだけ持つ直接ディスパッチ行の表。</summary>
        private static string Dispatch(string judgement)
        {
            return @"{ ""rows"": [
  { ""signatureKey"": """ + Key + @""",
    ""capabilityIds"": [""CAP-001""],
    ""rowKind"": ""directDispatch"",
    ""editKind"": ""read"",
    ""direction"": ""read"",
    ""basis"": ""現在のPMXの複製を返すだけである。"",
    ""tool"": ""model_list_vertices"",
    ""postcondition"": [" + judgement + @"] }
] }";
        }

        /// <summary>要素型とサンプル値の型を指す用意の操作を持つ表。</summary>
        private static string SetupMap(string elementType, string sample)
        {
            return Dispatch(
                @"{ ""effectType"": ""valueRead"", ""effectKey"": ""name"",
                    ""kind"": ""callLogOnly"", ""comparison"": ""exists"",
                    ""setup"": [
                      { ""tag"": ""addElement"", ""elementType"": """ + elementType + @""",
                        ""out"": ""added"" },
                      { ""tag"": ""callTool"", ""tool"": ""model_update_vertices"",
                        ""args"": { ""value"": ""sample:" + sample + @""" } }
                    ] }");
        }

        /// <summary>2つの能力が指す行。IDは昇順に並べず、並べ直しが効いているかを見る。</summary>
        private static string TwoCapabilities(string note)
        {
            const string Anchor = @"""capabilityIds"": [""CAP-001""],";
            Assert.Contains(Anchor, MapJson, StringComparison.Ordinal);
            string rewritten = MapJson.Replace(
                Anchor, @"""capabilityIds"": [""CAP-002"", ""CAP-001""],");
            return note == null
                ? rewritten
                : rewritten.Replace(@"""basis"":", @"""note"": """ + note + @""", ""basis"":");
        }

        private static IDictionary<string, ISet<string>> Owners(params string[] ids)
        {
            return new Dictionary<string, ISet<string>>(StringComparer.Ordinal)
            {
                { Key, new HashSet<string>(ids, StringComparer.Ordinal) },
            };
        }

        /// <summary>事後条件が期待でSDKの引数を1つ指す表。</summary>
        private static string SdkArgumentMap(string argument)
        {
            return Dispatch(
                @"{ ""effectType"": ""stateWritten"", ""effectKey"": ""name"", ""kind"": ""readback"",
                    ""observerTool"": ""model_list_vertices"", ""observerArgs"": {},
                    ""valuePath"": ""name"", ""comparison"": ""equals"",
                    ""expected"": ""sdkArg:" + argument + @""" }");
        }

        /// <summary>事後条件が観測ツールの束縛で参照元を1つ指す行を持つ表。</summary>
        private static string ObserverArgumentMap(string reference)
        {
            return Dispatch(
                @"{ ""effectType"": ""stateWritten"", ""effectKey"": ""name"", ""kind"": ""readback"",
                    ""observerTool"": ""model_list_vertices"",
                    ""observerArgs"": { ""index"": """ + reference + @""" },
                    ""valuePath"": ""name"", ""comparison"": ""exists"" }");
        }

        /// <summary>事後条件が用意の操作の引数で参照元を1つ指す行を持つ表。</summary>
        private static string SetupArgumentMap(string reference)
        {
            return Dispatch(
                @"{ ""effectType"": ""valueRead"", ""effectKey"": ""name"",
                    ""kind"": ""callLogOnly"", ""comparison"": ""exists"",
                    ""setup"": [{ ""tag"": ""callTool"", ""tool"": ""model_list_vertices"",
                      ""args"": { ""index"": """ + reference + @""" } }] }");
        }

        /// <summary>ファイルの生成を見る判定を持つ行の表。</summary>
        private static string FileMap(string effectKey)
        {
            return Dispatch(
                @"{ ""effectType"": ""fileWritten"", ""effectKey"": """ + effectKey + @""",
                    ""kind"": ""file"", ""comparison"": ""exists"" }");
        }

        private static IList<ParameterRecord> Parameters(string name)
        {
            return new[] { new ParameterRecord(name, "System.Int32", ParameterDirection.In, false) };
        }

        private static IDictionary<string, string> Note(string text)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal) { { "CAP-001", text } };
        }

        /// <summary>行の種別に依らない項目のうしろへ、書き足したい項目を挟んだ表を作る。</summary>
        private static string WithMember(string member)
        {
            const string Anchor = @"""basis"":";
            Assert.Contains(Anchor, MapJson, StringComparison.Ordinal);
            return MapJson.Replace(Anchor, member + ", " + Anchor);
        }

        private static string WithUpdateSpec(string update)
        {
            const string Anchor = @"""editKind"": ""read""";
            Assert.Contains(Anchor, MapJson, StringComparison.Ordinal);
            string spec = update == null
                ? @"{ ""refresh"": [] }"
                : @"{ ""update"": """ + update + @""", ""refresh"": [] }";
            return WithMember(@"""updateSpec"": " + spec)
                .Replace(Anchor, @"""editKind"": ""duplicateEdit""");
        }

        [Fact]
        public void AcceptsARowThatAgreesWithEveryCanon()
        {
            Require();
        }

        [Fact]
        public void RejectsARowForASignatureThatIsNotProvided()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(provided: new HashSet<string>(StringComparer.Ordinal)));

            Assert.Contains("提供対象でない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsCapabilityIdsThatDoNotMatchTheLedger()
        {
            Assert.Throws<InvalidOperationException>(() => Require(
                owners: new Dictionary<string, ISet<string>>(StringComparer.Ordinal)
                {
                    { Key, new HashSet<string>(new[] { "CAP-002" }, StringComparer.Ordinal) },
                }));
        }

        [Fact]
        public void RejectsARowWhoseSignatureNoLedgerRowPointsAt()
        {
            Assert.Throws<InvalidOperationException>(() => Require(
                owners: new Dictionary<string, ISet<string>>(StringComparer.Ordinal)));
        }

        [Fact]
        public void RejectsADirectionThatTheSignatureDoesNotDecide()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(direction: OperationDirection.Write));

            Assert.Contains("操作の向き", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsARowThatOmitsTheDangerKindOfADangerousSignature()
        {
            Assert.Throws<InvalidOperationException>(() => Require(
                dangers: new Dictionary<string, DangerKind>(StringComparer.Ordinal)
                {
                    { Key, DangerKind.Reset },
                }));
        }

        [Fact]
        public void RejectsADangerKindOnASignatureThatIsNotDangerous()
        {
            Assert.Throws<InvalidOperationException>(
                () => Require(mapJson: WithMember(@"""dangerKind"": ""reset""")));
        }

        [Fact]
        public void RejectsADangerKindThatIsNotTheOneTheRuleDecides()
        {
            Assert.Throws<InvalidOperationException>(() => Require(
                mapJson: WithMember(@"""dangerKind"": ""reset"""),
                dangers: new Dictionary<string, DangerKind>(StringComparer.Ordinal)
                {
                    { Key, DangerKind.Overwrite },
                }));
        }

        [Fact]
        public void AcceptsADangerKindThatMatchesTheRule()
        {
            Require(
                mapJson: WithMember(@"""dangerKind"": ""reset"""),
                dangers: new Dictionary<string, DangerKind>(StringComparer.Ordinal)
                {
                    { Key, DangerKind.Reset },
                });
        }

        [Fact]
        public void RejectsAMissingNoteOnARowFromACapabilityThatHasOne()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(notes: Note("一次資料で利用非推奨")));

            Assert.Contains("契約注記", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsANoteThatIsNotWhatTheLedgerWrites()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    mapJson: WithMember(@"""note"": ""利用非推奨"""),
                    notes: Note("一次資料で利用非推奨")));

            Assert.Contains("台帳の契約注記と合わない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AcceptsANoteThatIsWhatTheLedgerWrites()
        {
            Require(
                mapJson: WithMember(@"""note"": ""一次資料で利用非推奨"""),
                notes: Note("一次資料で利用非推奨"));
        }

        [Fact]
        public void LeavesTheNoteFreeOnARowWhoseCapabilitiesHaveNone()
        {
            Require(mapJson: WithMember(@"""note"": ""覚え書き"""));
        }

        [Fact]
        public void RejectsAnUpdateKindThatTheEnumerationDoesNotHave()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(mapJson: WithUpdateSpec("Materiaru")));

            Assert.Contains("列挙型に無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AcceptsAnUpdateKindThatTheEnumerationHas()
        {
            Require(mapJson: WithUpdateSpec("Vertex"));
        }

        [Fact]
        public void AcceptsARowThatReflectsTheWholeModel()
        {
            Require(mapJson: WithUpdateSpec(null));
        }

        [Fact]
        public void RejectsACommonContractRowThatTheSpecialRuleTableDoesNotHave()
        {
            Assert.Throws<InvalidOperationException>(
                () => Require(assignmentsJson: @"{ ""assignments"": [] }"));
        }

        [Fact]
        public void RejectsASpecialRuleTableItemThatHasNoRow()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(mapJson: @"{ ""rows"": [] }"));

            Assert.Contains("対応する共通契約割当行が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsAnAssignmentThatDoesNotMatchTheSpecialRuleTable()
        {
            Assert.Throws<InvalidOperationException>(() => Require(
                mapJson: MapJson
                    .Replace(@"""assignment"": ""internalFlow""", @"""assignment"": ""tool""")
                    .Replace(@"""target"": ""stateRead""", @"""target"": ""model_list_vertices""")));
        }

        [Fact]
        public void RejectsATargetThatDoesNotMatchTheSpecialRuleTable()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(mapJson: MapJson.Replace(
                    @"""target"": ""stateRead""", @"""target"": ""connect""")));

            Assert.Contains("対象名", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsABindingThatDoesNotMatchTheSpecialRuleTable()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(mapJson: MapJson.Replace(
                    @"""return"": ""pmxClone""", @"""return"": ""residentObject""")));

            Assert.Contains("束縛", error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("取得経路。契約注記: 一次資料で利用非推奨。別の制約")]
        [InlineData("契約注記:一次資料で利用非推奨。別の制約")]
        public void TakesTheContractNoteFromThePrefixToTheEndOfTheRemarks(string remarks)
        {
            IDictionary<string, string> notes = ToolMapEvidence.ContractNotes(new[]
            {
                Capability("CAP-001", "Undo記録の制御"),
                Capability("CAP-002", remarks),
                Capability("CAP-003", null),
            });

            Assert.Equal(new[] { "CAP-002" }, notes.Keys);
            Assert.Equal("一次資料で利用非推奨。別の制約", notes["CAP-002"]);
        }

        [Theory]
        [InlineData("契約注記: 一つ目。契約注記: 二つ目")]
        [InlineData("取得経路。契約注記:")]
        [InlineData("取得経路。契約注記:   ")]
        public void RejectsAContractNoteThatWouldSlipPastTheCheck(string remarks)
        {
            Assert.Throws<InvalidOperationException>(
                () => ToolMapEvidence.ContractNotes(new[] { Capability("CAP-002", remarks) }));
        }

        [Fact]
        public void RequiresEveryCapabilityOfARowThatSeveralOfThemPointAt()
        {
            Assert.Throws<InvalidOperationException>(() => Require(
                mapJson: TwoCapabilities(null),
                owners: Owners("CAP-001")));

            Require(mapJson: TwoCapabilities(null), owners: Owners("CAP-001", "CAP-002"));
        }

        [Fact]
        public void ConnectsTheNotesOfSeveralCapabilitiesInTheOrderOfTheirIds()
        {
            Require(
                mapJson: TwoCapabilities("一つ目。二つ目"),
                owners: Owners("CAP-001", "CAP-002"),
                notes: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "CAP-002", "二つ目" },
                    { "CAP-001", "一つ目" },
                });
        }

        [Fact]
        public void WritesANoteThatSeveralCapabilitiesShareOnlyOnce()
        {
            Require(
                mapJson: TwoCapabilities("同じ制約"),
                owners: Owners("CAP-001", "CAP-002"),
                notes: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "CAP-001", "同じ制約" },
                    { "CAP-002", "同じ制約" },
                });
        }

        [Fact]
        public void AcceptsASetupThatNamesWordsTheOtherCanonsHave()
        {
            Require(
                mapJson: SetupMap("vertex", "PEPlugin.SDX.V3"),
                assignmentsJson: @"{ ""assignments"": [] }");
        }

        [Fact]
        public void RejectsAnElementTypeThatTheTypeRoleTableDoesNotHave()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    mapJson: SetupMap("chouten", "PEPlugin.SDX.V3"),
                    assignmentsJson: @"{ ""assignments"": [] }"));

            Assert.Contains("型役割表に無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsASampledTypeThatThePublicApiDoesNotHave()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    mapJson: SetupMap("vertex", "PEPlugin.SDX.V9"),
                    assignmentsJson: @"{ ""assignments"": [] }"));

            Assert.Contains("公開API列挙に無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsAnSdkArgumentThatTheSignatureDoesNotHave()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    mapJson: SdkArgumentMap("value"),
                    assignmentsJson: @"{ ""assignments"": [] }"));

            Assert.Contains("SDKの引数がシグネチャに無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AcceptsAnSdkArgumentThatTheSignatureHas()
        {
            Require(
                mapJson: SdkArgumentMap("st_x"),
                assignmentsJson: @"{ ""assignments"": [] }",
                parameters: Parameters("st_x"));
        }

        [Fact]
        public void RejectsAnSdkArgumentBoundInTheObserverThatTheSignatureDoesNotHave()
        {
            Assert.Throws<InvalidOperationException>(() => Require(
                mapJson: ObserverArgumentMap("sdkArg:missing"),
                assignmentsJson: @"{ ""assignments"": [] }",
                parameters: Parameters("st_x")));

            Require(
                mapJson: ObserverArgumentMap("sdkArg:st_x"),
                assignmentsJson: @"{ ""assignments"": [] }",
                parameters: Parameters("st_x"));
        }

        [Fact]
        public void RejectsAnSdkArgumentBoundInASetupCallThatTheSignatureDoesNotHave()
        {
            Assert.Throws<InvalidOperationException>(() => Require(
                mapJson: SetupArgumentMap("sdkArg:missing"),
                assignmentsJson: @"{ ""assignments"": [] }",
                parameters: Parameters("st_x")));

            Require(
                mapJson: SetupArgumentMap("sdkArg:st_x"),
                assignmentsJson: @"{ ""assignments"": [] }",
                parameters: Parameters("st_x"));
        }

        [Fact]
        public void RejectsAFileJudgementWhoseEffectKeyIsNotAnArgumentOfTheSignature()
        {
            Assert.Throws<InvalidOperationException>(() => Require(
                mapJson: FileMap("missing"),
                assignmentsJson: @"{ ""assignments"": [] }",
                parameters: Parameters("path")));

            Require(
                mapJson: FileMap("path"),
                assignmentsJson: @"{ ""assignments"": [] }",
                parameters: Parameters("path"));
        }

        [Fact]
        public void KeepsOnlyTheProvidedCapabilitiesAmongTheOwners()
        {
            IDictionary<string, ISet<string>> owners = ToolMapEvidence.ProvidedOwners(
                new Dictionary<string, ISet<string>>(StringComparer.Ordinal)
                {
                    {
                        Key,
                        new HashSet<string>(
                            new[] { "CAP-001", "CAP-002", "CAP-003" }, StringComparer.Ordinal)
                    },
                },
                new[]
                {
                    Capability("CAP-001", null),
                    Capability("CAP-002", null, CapabilityStatus.NotSupported),
                    Capability("CAP-003", null, CapabilityStatus.NeedsInvestigation),
                });

            Assert.Equal(new[] { "CAP-001" }, owners[Key]);
        }

        [Fact]
        public void RejectsNullInputs()
        {
            Assert.Throws<ArgumentNullException>(() => ToolMapEvidence.ContractNotes(null));
            Assert.Throws<ArgumentNullException>(() => ToolMapEvidence.ProvidedOwners(
                null, new CapabilityRecord[0]));
            Assert.Throws<ArgumentNullException>(() => ToolMapEvidence.ProvidedOwners(
                new Dictionary<string, ISet<string>>(StringComparer.Ordinal), null));
        }

        private static CapabilityRecord Capability(
            string id, string remarks, CapabilityStatus status = CapabilityStatus.Provided)
        {
            return new CapabilityRecord(
                id,
                "PMXデータ",
                "IPXPmxConnector.GetCurrentState",
                CapabilityTargetKind.Single,
                new[] { "IPXPmxConnector.GetCurrentState" },
                status,
                CapabilityOwner.Model,
                remarks);
        }
    }
}
