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

        /// <summary>ツールを1件割り当てた行を持つ能力対応表。</summary>
        private static string MapJson(string tool = Tool, string signatureKey = Key)
        {
            return @"{ ""rows"": [{ ""signatureKey"": """ + signatureKey + @""",
                ""capabilityIds"": [""CAP-001""], ""rowKind"": ""directDispatch"",
                ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                ""tool"": """ + tool + @""",
                ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
                  ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] }] }";
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
                    ""members"": [{ ""name"": ""distance"", ""origin"": ""sdkIn"",
                      ""shape"": ""number"", ""required"": true }] }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>受け手の指し方を組の中へ入れた形。呼び分けの直下には現れない。</summary>
        private static string NestedSelectorSchemaJson()
        {
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [{ ""branch"": ""only"", ""inputs"": [
                  { ""name"": ""args"", ""origin"": ""hostInput"", ""required"": true,
                    ""members"": [
                      { ""name"": ""distance"", ""origin"": ""sdkIn"", ""shape"": ""number"",
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
                    { ""name"": ""distance"", ""origin"": ""sdkIn"", ""shape"": ""number"",
                      ""required"": true },
                    { ""name"": ""handles"", ""origin"": ""hostInput"", ""required"": true,
                      ""minItems"": 1, ""maxItems"": 8166,
                      ""element"": { ""origin"": ""hostInput"", ""shape"": ""number"" } }] },
                  { ""branch"": ""list"", ""inputs"": [
                    { ""name"": ""distance"", ""origin"": ""sdkIn"", ""shape"": ""number"",
                      ""required"": true }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>応答が名前つきの項目を持つ形。出力に現れる引数の行き先になる。</summary>
        private static string OutputSchemaJson(string member)
        {
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [{ ""branch"": ""only"", ""inputs"": [] }],
                ""output"": { ""origin"": ""hostOutput"", ""members"": [
                  { ""name"": """ + member + @""", ""origin"": ""sdkOut"",
                    ""shape"": ""number"" }] } }] }";
        }

        /// <summary>呼び分けを2つ持つ形。2つ目の呼び分けが持つ入力を差し替えられる。</summary>
        private static string TwoBranchSchemaJson(string second)
        {
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [
                  { ""branch"": ""args"", ""inputs"": [
                    { ""name"": ""distance"", ""origin"": ""sdkIn"", ""shape"": ""number"",
                      ""required"": true },
                    { ""name"": ""all"", ""origin"": ""hostInput"", ""shape"": ""boolean"",
                      ""required"": true }] },
                  { ""branch"": ""list"", ""inputs"": [
                    { ""name"": ""distance"", ""origin"": ""sdkIn"", ""shape"": ""number"",
                      ""required"": true }" + second + @"] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>役割を1件だけ持つ型役割表。独立したツールを持つ役割だけが群とツール名を持つ。</summary>
        private static TypeRoleTable Roles(TypeRole role)
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
                        "接続の経路。",
                        independent ? CapabilityOwner.Model : CapabilityOwner.None,
                        independent
                            ? new Dictionary<ToolVerb, string> { { ToolVerb.List, Tool } }
                            : new Dictionary<ToolVerb, string>()),
                },
                new HandleIssuanceRecord[0],
                new ElementCollectionRecord[0]);
        }

        /// <summary>引数1件・戻り値ありのインスタンスメソッドのシグネチャ。</summary>
        private static IDictionary<string, SignatureRecord> Signatures(
            string valueType = "System.Single",
            bool isStatic = false,
            MemberKind memberKind = MemberKind.Method,
            ParameterDirection direction = ParameterDirection.In)
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
                    new ParameterRecord("distance", "System.Single", direction, false),
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
            IDictionary<string, SignatureRecord> signatures = null)
        {
            SchemaCorrespondenceGate.Require(
                ToolMapJsonReader.Read(map ?? MapJson()),
                ToolSchemaJsonReader.Read(schemas),
                Roles(role),
                signatures ?? Signatures());
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
        public void RejectsARowWhoseSignatureIsNotInTheEnumeration()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    SchemaJson(),
                    signatures: new Dictionary<string, SignatureRecord>(StringComparer.Ordinal)));

            Assert.Contains("公開APIの列挙に無い", error.Message, StringComparison.Ordinal);
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

        /// <summary>
        /// 型役割表は総称型を引数の数で書き、列挙は型引数の名前で書くので、役割の引き当ては同じ鍵へ
        /// 写してから行う。写さずに引くと、受け手の検査が黙って素通りする。
        /// </summary>
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
                                "接続の経路。",
                                CapabilityOwner.Model,
                                new Dictionary<ToolVerb, string>
                                {
                                    { ToolVerb.List, "model_list_values" },
                                }),
                        },
                        new HandleIssuanceRecord[0],
                        new ElementCollectionRecord[0]),
                    new Dictionary<string, SignatureRecord>(StringComparer.Ordinal)
                    {
                        { key, signature },
                    }));

            Assert.Contains(
                "操作対象型の受け手を指す入力が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ARowWithoutAToolIsNotChecked()
        {
            string map = @"{ ""rows"": [{ ""signatureKey"": """ + Key + @""",
                ""capabilityIds"": [""CAP-001""], ""rowKind"": ""commonContract"",
                ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                ""assignment"": ""internalFlow"", ""target"": ""connect"",
                ""slotBinding"": { ""return"": ""runArgsClone"", ""parameters"": {} } }] }";

            SchemaCorrespondenceGate.Require(
                ToolMapJsonReader.Read(map),
                ToolSchemaJsonReader.Read(@"{ ""tools"": [] }"),
                Roles(TypeRole.OperationTarget),
                Signatures());
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            ToolMap map = ToolMapJsonReader.Read(MapJson());
            ToolSchemaTable schemas = ToolSchemaJsonReader.Read(SchemaJson());
            TypeRoleTable roles = Roles(TypeRole.Dto);
            IDictionary<string, SignatureRecord> signatures = Signatures();

            Assert.Throws<ArgumentNullException>(
                () => SchemaCorrespondenceGate.Require(null, schemas, roles, signatures));
            Assert.Throws<ArgumentNullException>(
                () => SchemaCorrespondenceGate.Require(map, null, roles, signatures));
            Assert.Throws<ArgumentNullException>(
                () => SchemaCorrespondenceGate.Require(map, schemas, null, signatures));
            Assert.Throws<ArgumentNullException>(
                () => SchemaCorrespondenceGate.Require(map, schemas, roles, null));
        }
    }
}
