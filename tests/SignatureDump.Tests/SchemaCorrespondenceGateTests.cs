using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class SchemaCorrespondenceGateTests
    {
        private const string Tool = "model_list_vertices";

        private const string Vertex = "PEPlugin.Pmx.IPXVertex";

        private const string Key = Vertex + ".Move(System.Single)";

        /// <summary>ツールを持つ行を1つだけ持つ能力対応表。</summary>
        private static string MapJson(string signatureKey = Key)
        {
            return @"{ ""rows"": [" + Row(signatureKey) + "] }";
        }

        /// <summary>ツールを持つ行。名前は行が書かないので、引く表の側で与える。</summary>
        private static string Row(string signatureKey)
        {
            return @"{ ""signatureKey"": """ + signatureKey + @""",
                ""editKind"": ""read"", ""basis"": ""根拠。"",
                ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
                  ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] }";
        }

        /// <summary>行キーからツールの名前を引く表。行キーと名前を交互に並べる。</summary>
        private static IDictionary<string, string> Names(params string[] pairs)
        {
            Dictionary<string, string> names =
                new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < pairs.Length; index += 2)
            {
                names.Add(pairs[index], pairs[index + 1]);
            }

            return names;
        }

        /// <summary>用意がツールを1回呼ぶ行。呼ぶツールの名前を差し替えられる。</summary>
        private static string SetupRow(string called)
        {
            return @"{ ""signatureKey"": """ + Key + @""",
                ""editKind"": ""read"", ""basis"": ""根拠。"",
                ""postcondition"": [{ ""effectType"": ""valueRead"", ""effectKey"": """",
                  ""kind"": ""callLogOnly"", ""comparison"": ""exists"",
                  ""setup"": [{ ""tag"": ""callTool"", ""args"": {},
                    ""tool"": """ + called + @""" }] }] }";
        }

        /// <summary>器の中の入力がホストの入れる印を持つ形。</summary>
        private static string NestedInjectedSchemaJson()
        {
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [{ ""branch"": ""only"", ""inputs"": [
                  { ""name"": ""args"", ""origin"": ""hostInput"", ""required"": true,
                    ""members"": [{ ""name"": ""distance"", ""injected"": true }] }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>入力がホストの入れる印を持つ形。呼ぶ側は渡さない。</summary>
        private static string InjectedSchemaJson()
        {
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [{ ""branch"": ""only"", ""inputs"": [
                  { ""name"": ""distance"", ""injected"": true }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>入力の名前と応答の綴りを差し替えられる入出力の形。</summary>
        private static string SchemaJson(string inputs = @"""distance""", string shape = "number")
        {
            string items = inputs.Length == 0
                ? string.Empty
                : string.Join(
                    ",",
                    inputs.Replace("\"", string.Empty).Split(',')
                        .Select(n => @"{ ""name"": """ + n.Trim() + @""",
                          ""origin"": ""hostInput"", ""shape"": ""number"", ""required"": true }")
                        .ToArray());
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [{ ""branch"": ""only"", ""inputs"": [" + items + @"] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": """ + shape + @""" } }] }";
        }

        /// <summary>入力を組の中へ入れた形。集合を受け取るツールは引数を器の中へ置く。</summary>
        private static string NestedSchemaJson()
        {
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [{ ""branch"": ""only"", ""inputs"": [
                  { ""name"": ""args"", ""origin"": ""hostInput"", ""required"": true,
                    ""members"": [{ ""name"": ""distance"", ""required"": true }] }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>受け手の指し方と同じ名前のSDKの引数を、呼び分けの直下に置いた形。</summary>
        private static string SdkNamedSelectorSchemaJson(string name)
        {
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [{ ""branch"": ""only"", ""inputs"": [
                  { ""name"": """ + name + @""", ""required"": true }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>受け手の指し方を組の中へ入れた形。呼び分けの直下には現れない。</summary>
        private static string NestedSelectorSchemaJson()
        {
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [{ ""branch"": ""only"", ""inputs"": [
                  { ""name"": ""args"", ""origin"": ""hostInput"", ""required"": true,
                    ""members"": [
                      { ""name"": ""distance"",
                        ""required"": true },
                      { ""name"": ""all"", ""origin"": ""hostInput"", ""shape"": ""boolean"",
                        ""required"": true }] }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>ハンドルの受け手を1つ目の呼び分けだけが持つ形。</summary>
        private static string HandleBranchSchemaJson()
        {
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [
                  { ""branch"": ""handles"", ""inputs"": [
                    { ""name"": ""distance"",
                      ""required"": true },
                    { ""name"": ""handles"", ""origin"": ""hostInput"", ""required"": true,
                      ""element"": { ""origin"": ""hostInput"", ""shape"": ""number"" } }] },
                  { ""branch"": ""list"", ""inputs"": [
                    { ""name"": ""distance"",
                      ""required"": true }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>応答が名前つきの項目を持つ形。出力に現れる引数の行き先になる。</summary>
        private static string OutputSchemaJson(string member)
        {
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [{ ""branch"": ""only"", ""inputs"": [] }],
                ""output"": { ""origin"": ""hostOutput"", ""members"": [
                  { ""name"": """ + member + @""" }] } }] }";
        }

        /// <summary>呼び分けを2つ持つ形。2つ目の呼び分けが持つ入力を差し替えられる。</summary>
        private static string TwoBranchSchemaJson(string second)
        {
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [
                  { ""branch"": ""args"", ""inputs"": [
                    { ""name"": ""distance"",
                      ""required"": true },
                    { ""name"": ""all"", ""origin"": ""hostInput"", ""shape"": ""boolean"",
                      ""required"": true }] },
                  { ""branch"": ""list"", ""inputs"": [
                    { ""name"": ""distance"",
                      ""required"": true }" + second + @"] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>
        /// 発行する数を受け取る形。呼び分けを2つ持ち、`count` の入力を分岐ごとに差し替えられる。
        /// </summary>
        private static string IssuingSchemaJson(string first, string second = "")
        {
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [
                  { ""branch"": ""first"", ""inputs"": [
                    { ""name"": ""distance"",
                      ""required"": true },
                    { ""name"": ""count"", ""origin"": ""hostInput"", ""shape"": ""number"",
                      ""required"": true" + first + @" }] },
                  { ""branch"": ""second"", ""inputs"": [
                    { ""name"": ""distance"",
                      ""required"": true },
                    { ""name"": ""count"", ""origin"": ""hostInput"", ""shape"": ""number"",
                      ""required"": true" + second + @" }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>役割を1件だけ持つ型役割表。独立したツールを持つ役割だけが群とツール名を持つ。</summary>
        private static TypeRoleTable Roles(TypeRole role, bool issues = false)
        {
            bool independent = TypeRoleRecord.HasIndependentTool(role);
            return new TypeRoleTable(
                new[]
                {
                    new TypeRoleRecord(
                        Vertex,
                        role,
                        "根拠。",
                        "vertex",
                        "vertices",
                        independent ? CapabilityOwner.Model : CapabilityOwner.None),
                },
                new[]
                {
                    new HandleIssuanceRecord(
                        Key,
                        issues,
                        "根拠。"),
                },
                new ElementCollectionRecord[0]);
        }

        /// <summary>引数1件・戻り値ありのインスタンスメソッドのシグネチャ。</summary>
        private static IDictionary<string, SignatureRecord> Signatures(
            string valueType = "System.Single",
            bool isStatic = false,
            MemberKind memberKind = MemberKind.Method,
            ParameterDirection direction = ParameterDirection.In,
            string parameterName = "distance",
            string parameterType = "System.Single")
        {
            SignatureRecord signature = new SignatureRecord(
                Key,
                Vertex,
                memberKind,
                "Move",
                isStatic,
                0,
                new[]
                {
                    new ParameterRecord(parameterName, parameterType, direction, false),
                },
                valueType,
                false,
                false,
                OperationDirection.Read);

            return new Dictionary<string, SignatureRecord>(StringComparer.Ordinal)
            {
                { Key, signature },
            };
        }

        private static void Require(
            string schemas,
            string map = null,
            TypeRole role = TypeRole.Dto,
            IDictionary<string, SignatureRecord> signatures = null,
            bool issues = false,
            IDictionary<string, string> toolNames = null,
            AccessPathKind kind = AccessPathKind.Element)
        {
            SchemaCorrespondenceGate.Require(
                ToolMapJsonReader.Read(map ?? MapJson()),
                ToolSchemaJsonReader.Read(schemas),
                Roles(role, issues),
                signatures ?? Signatures(),
                toolNames ?? Names(Key, Tool),
                Paths(kind));
        }

        /// <summary>受け手の型からその道へ。リストの中の1件かどうかだけを変えられる。</summary>
        private static IDictionary<string, AccessPath> Paths(AccessPathKind kind)
        {
            bool listed = kind == AccessPathKind.Element;

            return new Dictionary<string, AccessPath>(StringComparer.Ordinal)
            {
                {
                    Vertex,
                    new AccessPath(kind, "PEPlugin.Pmx.IPXPmx.Vertex()", null, listed, Vertex)
                },
            };
        }

        /// <summary>発行する数を受け取るツール1件。`count` の入力を差し替えられる。</summary>
        private static string IssuingTool(string tool, string count)
        {
            return @"{ ""tool"": """ + tool + @""",
                ""branches"": [{ ""branch"": ""only"", ""inputs"": [
                  { ""name"": ""distance"",
                    ""required"": true },
                  { ""name"": ""count"", ""origin"": ""hostInput"", ""shape"": ""number"",
                    ""required"": true" + count + @" }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }";
        }

        [Fact]
        public void LeavesTheWrittenCountOfTheRowThatDoesNotIssue()
        {
            const string otherKey = Vertex + ".Scale(System.Single)";
            const string otherTool = "model_scale_vertices";
            IDictionary<string, SignatureRecord> signatures = Signatures();
            signatures[otherKey] = signatures[Key];

            SchemaCorrespondenceGate.Require(
                ToolMapJsonReader.Read(
                    @"{ ""rows"": [" + Row(Key) + "," + Row(otherKey) + "] }"),
                ToolSchemaJsonReader.Read(
                    @"{ ""tools"": ["
                        + IssuingTool(Tool, @", ""bounds"": { ""minimum"": 1 }") + ","
                        + IssuingTool(
                            otherTool, @", ""bounds"": { ""minimum"": 1, ""maximum"": 8166 }")
                        + "] }"),
                new TypeRoleTable(
                    new TypeRoleRecord[0],
                    new[]
                    {
                        new HandleIssuanceRecord(
                            Key, true, "根拠。"),
                        new HandleIssuanceRecord(otherKey, false, "根拠。"),
                    },
                    new ElementCollectionRecord[0]),
                signatures,
                Names(Key, Tool, otherKey, otherTool),
                Paths(AccessPathKind.Element));
        }

        [Theory]
        [InlineData("PEPlugin.Pmx.IPXPmx")]
        [InlineData("PXCPlugin.IPXCPluginConnector")]
        public void AcceptsAHostSuppliedArgumentTheSchemaDoesNotAskFor(string parameterType)
        {
            Require(
                InjectedSchemaJson(), signatures: Signatures(parameterType: parameterType));
        }

        [Theory]
        [InlineData("PEPlugin.Pmx.IPXPmx")]
        [InlineData("PXCPlugin.IPXCPluginConnector")]
        public void RejectsAHostSuppliedArgumentTheSchemaAsksFor(string parameterType)
        {
            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
                () => Require(
                    SchemaJson(), signatures: Signatures(parameterType: parameterType)));

            Assert.Contains("ホストが入れる", thrown.Message);
        }

        [Fact]
        public void RejectsAnArgumentTheSchemaMarksAsPutInByTheHost()
        {
            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
                () => Require(InjectedSchemaJson()));

            Assert.Contains("呼ぶ側が渡す", thrown.Message);
        }

        [Fact]
        public void RejectsAHostSuppliedArgumentTheSchemaAsksForInsideAContainer()
        {
            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
                () => Require(
                    NestedSchemaJson(),
                    signatures: Signatures(parameterType: "PEPlugin.Pmx.IPXPmx")));

            Assert.Contains("ホストが入れる", thrown.Message);
        }

        [Fact]
        public void RejectsAnArgumentInsideAContainerThatTheSchemaMarksAsPutInByTheHost()
        {
            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
                () => Require(NestedInjectedSchemaJson()));

            Assert.Contains("呼ぶ側が渡す", thrown.Message);
        }

        [Fact]
        public void AcceptsASetupThatCallsAToolTheSchemaHas()
        {
            Require(SchemaJson(), map: @"{ ""rows"": [" + SetupRow(Tool) + "] }");
        }

        [Fact]
        public void RejectsASetupThatCallsAToolTheSchemaDoesNotHave()
        {
            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
                () => Require(
                    SchemaJson(), map: @"{ ""rows"": [" + SetupRow("model_move_vertex") + "] }"));

            Assert.Contains("model_move_vertex", thrown.Message);
        }

        [Fact]
        public void AcceptsAnIssuingToolWhoseCountIsLeftToTheRule()
        {
            Require(IssuingSchemaJson(@", ""bounds"": { ""minimum"": 1 }"), issues: true);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RejectsAnIssuingToolThatWritesTheCountTheRuleDerives(bool inTheFirstBranch)
        {
            string written = @", ""bounds"": { ""minimum"": 1, ""maximum"": 8166 }";
            string schemas = inTheFirstBranch
                ? IssuingSchemaJson(written)
                : IssuingSchemaJson(@", ""bounds"": { ""minimum"": 1 }", written);

            Require(schemas);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(schemas, issues: true));

            Assert.Contains(
                "発行する数の上限は導く値なので書かない",
                error.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void AcceptsASchemaThatCoversTheArgumentsAndTheReturnValue()
        {
            Require(SchemaJson());
        }

        [Fact]
        public void RejectsAnArgumentWithoutAnInput()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(SchemaJson(inputs: @"""length""")));

            Assert.Contains("引数に対応する入力が無い", error.Message, StringComparison.Ordinal);
            Assert.Contains("distance", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsAnOutputWithoutAnOriginOnAVoidSignature()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                        ""branches"": [{ ""branch"": ""only"", ""inputs"": [
                          { ""name"": ""distance"", ""required"": true }] }],
                        ""output"": { } }] }",
                    signatures: Signatures(valueType: "System.Void")));

            Assert.Contains(
                "応答が出所を書いていない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsARowWhoseToolHasNoSchema()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(@"{ ""tools"": [] }"));

            Assert.Contains("入出力の形が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsAnOperationTargetWithoutATargetSelector()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(SchemaJson(), role: TypeRole.OperationTarget));

            Assert.Contains(
                "操作対象型の受け手を指す入力が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AcceptsAnOperationTargetThatTheModelHoldsOneOfWithoutATargetSelector()
        {
            Require(SchemaJson(), role: TypeRole.OperationTarget, kind: AccessPathKind.Child);
        }

        [Fact]
        public void AcceptsAnOperationTargetWithATargetSelector()
        {
            Require(SchemaJson(inputs: @"""distance"",""indices"""), role: TypeRole.OperationTarget);
        }

        [Fact]
        public void RejectsAHandleTargetWithoutHandles()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    SchemaJson(inputs: @"""distance"",""indices"""), role: TypeRole.HandleTarget));

            Assert.Contains(
                "ハンドル操作型の受け手を指す入力が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsAConnectorWithATargetSelector()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    SchemaJson(inputs: @"""distance"",""all"""), role: TypeRole.Connector));

            Assert.Contains(
                "コネクタ型なのに受け手を指す入力がある", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AConnectorMayTakeAnSdkArgumentNamedLikeATargetSelector()
        {
            Require(
                SdkNamedSelectorSchemaJson("indices"),
                role: TypeRole.Connector,
                signatures: Signatures(parameterName: "indices"));
        }

        [Fact]
        public void AnSdkArgumentNamedLikeATargetSelectorDoesNotPointTheReceiver()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    SdkNamedSelectorSchemaJson("indices"),
                    role: TypeRole.OperationTarget,
                    signatures: Signatures(parameterName: "indices")));

            Assert.Contains(
                "操作対象型の受け手を指す入力が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnSdkArgumentNamedLikeAHandleDoesNotPointTheReceiver()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    SdkNamedSelectorSchemaJson("handles"),
                    role: TypeRole.HandleTarget,
                    signatures: Signatures(parameterName: "handles")));

            Assert.Contains(
                "ハンドル操作型の受け手を指す入力が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AStaticMemberIsNotAskedForAReceiver()
        {
            Require(
                SchemaJson(),
                role: TypeRole.OperationTarget,
                signatures: Signatures(isStatic: true));
        }

        [Fact]
        public void AConstructorIsNotAskedForAReceiver()
        {
            Require(
                SchemaJson(),
                role: TypeRole.OperationTarget,
                signatures: Signatures(memberKind: MemberKind.Constructor));
        }

        [Fact]
        public void AnArgumentInsideAContainerIsFound()
        {
            Require(NestedSchemaJson());
        }

        [Fact]
        public void AnOutArgumentIsLookedForInTheResponse()
        {
            Require(
                OutputSchemaJson("distance"),
                signatures: Signatures(direction: ParameterDirection.Out));
        }

        [Fact]
        public void RejectsAnOutArgumentWithoutAResponseItem()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    OutputSchemaJson("length"),
                    signatures: Signatures(direction: ParameterDirection.Out)));

            Assert.Contains(
                "引数に対応する応答の項目が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ARefArgumentIsLookedForOnBothSides()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    SchemaJson(), signatures: Signatures(direction: ParameterDirection.Ref)));

            Assert.Contains(
                "引数に対応する応答の項目が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsABranchWithoutTheTargetSelector()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    TwoBranchSchemaJson(string.Empty), role: TypeRole.OperationTarget));

            Assert.Contains(
                "受け手を指す入力が無い呼び分けがある", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AcceptsATargetSelectorInEveryBranch()
        {
            Require(
                TwoBranchSchemaJson(
                    @", { ""name"": ""all"", ""origin"": ""hostInput"", ""shape"": ""boolean"",
                          ""required"": true }"),
                role: TypeRole.OperationTarget);
        }

        [Fact]
        public void ATargetSelectorInsideAContainerDoesNotCount()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(NestedSelectorSchemaJson(), role: TypeRole.OperationTarget));

            Assert.Contains(
                "受け手を指す入力が無い呼び分けがある", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsABranchWithoutHandlesForAHandleTarget()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(HandleBranchSchemaJson(), role: TypeRole.HandleTarget));

            Assert.Contains(
                "ハンドル操作型の受け手を指す入力が無い呼び分けがある",
                error.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsAValueReturningSignatureWhoseToolReturnsNothing()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(SchemaJson(shape: "null_value")));

            Assert.Contains("応答が値を持たない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AVoidSignatureMayReturnNothing()
        {
            Require(
                SchemaJson(shape: "null_value"), signatures: Signatures(valueType: "System.Void"));
        }

        [Fact]
        public void AGenericDeclaringTypeIsFoundByItsDefinitionName()
        {
            const string Open = "PEPlugin.Vme.IPEValue<T>";
            const string Closed = "PEPlugin.Vme.IPEValue<1>";
            string key = Open + ".Move(System.Single)";
            SignatureRecord signature = new SignatureRecord(
                key,
                Open,
                MemberKind.Method,
                "Move",
                false,
                0,
                new[]
                {
                    new ParameterRecord("distance", "System.Single", ParameterDirection.In, false),
                },
                "System.Single",
                false,
                false,
                OperationDirection.Read);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => SchemaCorrespondenceGate.Require(
                    ToolMapJsonReader.Read(MapJson(signatureKey: key)),
                    ToolSchemaJsonReader.Read(SchemaJson()),
                    new TypeRoleTable(
                        new[]
                        {
                            new TypeRoleRecord(
                                Closed,
                                TypeRole.OperationTarget,
                                "根拠。",
                                "value",
                                "values",
                                CapabilityOwner.Model),
                        },
                        new HandleIssuanceRecord[0],
                        new ElementCollectionRecord[0]),
                    new Dictionary<string, SignatureRecord>(StringComparer.Ordinal)
                    {
                        { key, signature },
                    },
                    Names(key, Tool),
                    new Dictionary<string, AccessPath>(StringComparer.Ordinal)
                    {
                        {
                            Closed,
                            new AccessPath(
                                AccessPathKind.Element,
                                "PEPlugin.Pmx.IPXPmx.Value()",
                                null,
                                true,
                                Closed)
                        },
                    }));

            Assert.Contains(
                "操作対象型の受け手を指す入力が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ARowWithoutAToolIsNotChecked()
        {
            string map = @"{ ""rows"": [{ ""signatureKey"": """ + Key + @""",
                ""editKind"": ""read"", ""basis"": ""根拠。"" }] }";

            SchemaCorrespondenceGate.Require(
                ToolMapJsonReader.Read(map),
                ToolSchemaJsonReader.Read(@"{ ""tools"": [] }"),
                Roles(TypeRole.OperationTarget),
                Signatures(),
                new Dictionary<string, string>(StringComparer.Ordinal),
                Paths(AccessPathKind.Element));
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            ToolMap map = ToolMapJsonReader.Read(MapJson());
            ToolSchemaTable schemas = ToolSchemaJsonReader.Read(SchemaJson());
            TypeRoleTable roles = Roles(TypeRole.Dto);
            IDictionary<string, SignatureRecord> signatures = Signatures();
            IDictionary<string, string> names = Names(Key, Tool);
            IDictionary<string, AccessPath> paths = Paths(AccessPathKind.Element);

            Assert.Throws<ArgumentNullException>(
                () => SchemaCorrespondenceGate.Require(
                    null, schemas, roles, signatures, names, paths));
            Assert.Throws<ArgumentNullException>(
                () => SchemaCorrespondenceGate.Require(map, null, roles, signatures, names, paths));
            Assert.Throws<ArgumentNullException>(
                () => SchemaCorrespondenceGate.Require(map, schemas, null, signatures, names, paths));
            Assert.Throws<ArgumentNullException>(
                () => SchemaCorrespondenceGate.Require(map, schemas, roles, null, names, paths));
            Assert.Throws<ArgumentNullException>(
                () => SchemaCorrespondenceGate.Require(map, schemas, roles, signatures, null, paths));
            Assert.Throws<ArgumentNullException>(
                () => SchemaCorrespondenceGate.Require(map, schemas, roles, signatures, names, null));
        }
    }
}
