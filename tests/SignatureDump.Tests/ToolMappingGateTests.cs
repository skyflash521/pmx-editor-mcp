using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolMappingGateTests
    {
        private const string Vertex = "PEPlugin.Pmx.IPXVertex";

        private const string Connector = "PEPlugin.Pmx.IPXPmxConnector";

        private const string Key = Vertex + ".NormalizePmx()";

        /// <summary>ツールを1件割り当てた行を持つ能力対応表。</summary>
        private static string MapJson(string tool, string signatureKey = Key)
        {
            return @"{ ""rows"": [{ ""signatureKey"": """ + signatureKey + @""",
                ""capabilityIds"": [""CAP-001""], ""rowKind"": ""directDispatch"",
                ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                ""tool"": """ + tool + @""",
                ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
                  ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] }] }";
        }

        /// <summary>行を2件持つ能力対応表。コネクタ型のメソッドの衝突を作る。</summary>
        private static string TwoRowMapJson(string first, string second)
        {
            return @"{ ""rows"": [
                { ""signatureKey"": ""PEPlugin.Pmx.IPXOtherConnector.Save()"",
                  ""capabilityIds"": [""CAP-002""], ""rowKind"": ""directDispatch"",
                  ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                  ""tool"": """ + second + @""",
                  ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
                    ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] },
                { ""signatureKey"": """ + Connector + @".Save()"",
                  ""capabilityIds"": [""CAP-001""], ""rowKind"": ""directDispatch"",
                  ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                  ""tool"": """ + first + @""",
                  ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
                    ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] }] }";
        }

        /// <summary>型を1件だけ持つ型役割表。</summary>
        private static TypeRoleTable Roles(
            TypeRole role = TypeRole.OperationTarget,
            string typeName = Vertex,
            string elementNoun = "vertex",
            IList<HandleIssuanceRecord> issuances = null,
            IList<TypeRoleRecord> more = null)
        {
            List<TypeRoleRecord> types = new List<TypeRoleRecord>
            {
                Type(typeName, role, elementNoun),
            };
            if (more != null)
            {
                types.AddRange(more);
            }

            return new TypeRoleTable(
                types,
                issuances ?? new HandleIssuanceRecord[0],
                new ElementCollectionRecord[0]);
        }

        private static TypeRoleRecord Type(string typeName, TypeRole role, string elementNoun)
        {
            return new TypeRoleRecord(
                typeName,
                role,
                "根拠。",
                elementNoun,
                elementNoun + "es",
                CapabilityOwner.Model);
        }

        /// <summary>独立したツールを持たない役割の型。担当群もツールの名前も持たない。</summary>
        private static TypeRoleRecord Embedded(string typeName, TypeRole role)
        {
            return new TypeRoleRecord(
                typeName,
                role,
                "根拠。",
                "embedded",
                "embeddeds",
                CapabilityOwner.None);
        }

        /// <summary>スキーマ埋め込み行を1件持つ能力対応表。</summary>
        private static string EmbeddedMapJson(string embeddedIn)
        {
            return @"{ ""rows"": [{ ""signatureKey"": """ + Vertex + @".Index"",
                ""capabilityIds"": [""CAP-001""], ""rowKind"": ""schemaEmbedded"",
                ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                ""embeddedIn"": [""" + embeddedIn + @"""] }] }";
        }

        /// <summary>シグネチャを差し替えられる列挙の結果。</summary>
        private static IDictionary<string, SignatureRecord> Signatures(
            params SignatureRecord[] records)
        {
            Dictionary<string, SignatureRecord> byKey =
                new Dictionary<string, SignatureRecord>(StringComparer.Ordinal);
            foreach (SignatureRecord record in records)
            {
                byKey.Add(record.Key, record);
            }

            return byKey;
        }

        private static SignatureRecord Method(
            string key,
            string declaringType,
            string memberName,
            string valueType = "System.Void",
            MemberKind memberKind = MemberKind.Method)
        {
            return new SignatureRecord(
                key,
                declaringType,
                memberKind,
                memberName,
                false,
                0,
                new ParameterRecord[0],
                valueType,
                false,
                false,
                OperationDirection.Read);
        }

        /// <summary>読み書きできるプロパティのシグネチャ。</summary>
        private static SignatureRecord Property(string key, string declaringType = Vertex)
        {
            return new SignatureRecord(
                key,
                declaringType,
                MemberKind.Property,
                "Index",
                false,
                0,
                new ParameterRecord[0],
                "System.Int32",
                true,
                true,
                OperationDirection.Read);
        }

        [Fact]
        public void AcceptsAMethodNameWithTheSourceQualifier()
        {
            Require(
                ToolMapJsonReader.Read(MapJson("model_normalize_pmx_vertex")),
                Roles(),
                Signatures(Method(Key, Vertex, "NormalizePmx")));
        }

        [Fact]
        public void RejectsAMethodNameWithoutTheSourceQualifier()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    ToolMapJsonReader.Read(MapJson("model_normalize_pmx")),
                    Roles(),
                    Signatures(Method(Key, Vertex, "NormalizePmx"))));

            Assert.Contains(
                "規則から導いた名前と合わない", error.Message, StringComparison.Ordinal);
            Assert.Contains("model_normalize_pmx_vertex", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AConnectorMethodTakesNoQualifierWithoutACollision()
        {
            Require(
                ToolMapJsonReader.Read(MapJson("model_save", Connector + ".Save()")),
                Roles(TypeRole.Connector, Connector, "pmx_connector"),
                Signatures(Method(Connector + ".Save()", Connector, "Save")));
        }

        [Fact]
        public void CollidingConnectorMethodsTakeTheQualifier()
        {
            const string Other = "PEPlugin.Pmx.IPXOtherConnector";

            Require(
                ToolMapJsonReader.Read(
                    TwoRowMapJson("model_save_pmx_connector", "model_save_other_connector")),
                Roles(
                    TypeRole.Connector,
                    Connector,
                    "pmx_connector",
                    more: new[] { Type(Other, TypeRole.Connector, "other_connector") }),
                Signatures(
                    Method(Connector + ".Save()", Connector, "Save"),
                    Method(Other + ".Save()", Other, "Save")));
        }

        [Fact]
        public void RejectsACollidingConnectorMethodWithoutTheQualifier()
        {
            const string Other = "PEPlugin.Pmx.IPXOtherConnector";

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    ToolMapJsonReader.Read(
                        TwoRowMapJson("model_save", "model_save_other_connector")),
                    Roles(
                        TypeRole.Connector,
                        Connector,
                        "pmx_connector",
                        more: new[] { Type(Other, TypeRole.Connector, "other_connector") }),
                    Signatures(
                        Method(Connector + ".Save()", Connector, "Save"),
                        Method(Other + ".Save()", Other, "Save"))));

            Assert.Contains(
                "model_save_pmx_connector", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AConstructorOfAHandleTargetTakesTheCreateName()
        {
            const string Model = "PEPlugin.Form.IPXUIModel";
            string key = Model + "..ctor()";

            Require(
                ToolMapJsonReader.Read(MapJson("model_create_ui_model", key)),
                Roles(
                    TypeRole.HandleTarget,
                    Model,
                    "ui_model",
                    issuances: new[]
                    {
                        new HandleIssuanceRecord(key, true, "根拠。"),
                    }),
                Signatures(Method(key, Model, ".ctor", Model, MemberKind.Constructor)));
        }

        [Fact]
        public void AMethodThatMakesAHandleTargetKeepsItsMemberName()
        {
            const string Model = "PEPlugin.Form.IPXUIModel";
            const string Builder = "PEPlugin.IPEBuilder";
            string factory = Builder + ".CreateModel()";

            Require(
                ToolMapJsonReader.Read(MapJson("model_create_model", factory)),
                Roles(
                    TypeRole.HandleTarget,
                    Model,
                    "ui_model",
                    issuances: new[]
                    {
                        new HandleIssuanceRecord(
                            factory, true, "根拠。"),
                    },
                    more: new[] { Type(Builder, TypeRole.Connector, "builder") }),
                Signatures(Method(factory, Builder, "CreateModel", Model)));
        }

        [Fact]
        public void AConstructorTakesTheGroupOfItsDeclaringType()
        {
            const string Model = "PEPlugin.Form.IPXUIModel";
            string key = Model + "..ctor()";

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    ToolMapJsonReader.Read(MapJson("view_create_ui_model", key)),
                    Roles(TypeRole.HandleTarget, Model, "ui_model"),
                    Signatures(Method(key, Model, ".ctor", Model, MemberKind.Constructor))));

            Assert.Contains("model_create_ui_model", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnArrayReturnDoesNotChangeTheDeclaringTypeLookup()
        {
            string key = Vertex + ".ToKeyArray()";

            Require(
                ToolMapJsonReader.Read(MapJson("model_to_key_array_vertex", key)),
                Roles(),
                Signatures(Method(key, Vertex, "ToKeyArray", Vertex + "[]")));
        }

        [Fact]
        public void OverloadsOfOneConnectorMethodDoNotCollide()
        {
            string first = Connector + ".Save()";
            string second = Connector + ".Save(System.String)";
            string map = @"{ ""rows"": [
                { ""signatureKey"": """ + first + @""",
                  ""capabilityIds"": [""CAP-001""], ""rowKind"": ""directDispatch"",
                  ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                  ""tool"": ""model_save"",
                  ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
                    ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] },
                { ""signatureKey"": """ + second + @""",
                  ""capabilityIds"": [""CAP-002""], ""rowKind"": ""directDispatch"",
                  ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                  ""tool"": ""model_save"",
                  ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
                    ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] }] }";

            Require(
                ToolMapJsonReader.Read(map),
                Roles(TypeRole.Connector, Connector, "pmx_connector"),
                Signatures(
                    Method(first, Connector, "Save"), Method(second, Connector, "Save")));
        }

        [Fact]
        public void AnEmbeddedNameMustBeAToolOfTheDeclaringType()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    ToolMapJsonReader.Read(EmbeddedMapJson("model_list_bones")),
                    Roles(),
                    Signatures(Property(Vertex + ".Index"))));

            Assert.Contains(
                "埋め込み先が宣言型の取得と更新のツールに無い",
                error.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsAnEmbeddedNameThatIsTheAddToolOfTheDeclaringType()
        {
            TypeRoleRecord vertex = new TypeRoleRecord(
                Vertex,
                TypeRole.OperationTarget,
                "根拠。",
                "vertex",
                "vertices",
                CapabilityOwner.Model);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    ToolMapJsonReader.Read(EmbeddedMapJson("model_add_vertices")),
                    new TypeRoleTable(
                        new[] { vertex },
                        new HandleIssuanceRecord[0],
                        new ElementCollectionRecord[0]),
                    Signatures(Property(Vertex + ".Index"))));

            Assert.Contains(
                "埋め込み先が宣言型の取得と更新のツールに無い",
                error.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void AnEmbeddedNameOfTheDeclaringTypePasses()
        {
            Require(
                ToolMapJsonReader.Read(EmbeddedMapJson("model_list_vertexes")),
                Roles(),
                Signatures(Property(Vertex + ".Index")));
        }

        [Fact]
        public void AnEventArgsPropertyIsEmbeddedInTheBranchOfAnEventRow()
        {
            const string Args = "PEPlugin.View.PXViewClickEventArgs";
            string map = @"{ ""rows"": [
                { ""signatureKey"": """ + Vertex + @".Changed"",
                  ""capabilityIds"": [""CAP-002""], ""rowKind"": ""eventBranch"",
                  ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                  ""eventType"": ""view.click"" },
                { ""signatureKey"": """ + Args + @".Index"",
                  ""capabilityIds"": [""CAP-001""], ""rowKind"": ""schemaEmbedded"",
                  ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                  ""embeddedIn"": [""view.click""] }] }";

            Require(
                ToolMapJsonReader.Read(map),
                Roles(more: new[] { Embedded(Args, TypeRole.EventArgs) }),
                Signatures(
                    Property(Args + ".Index", Args),
                    Method(Vertex + ".Changed", Vertex, "Changed", "System.EventHandler")));
        }

        [Fact]
        public void RejectsAnEventArgsPropertyEmbeddedInATool()
        {
            const string Args = "PEPlugin.View.PXViewClickEventArgs";
            string map = @"{ ""rows"": [{ ""signatureKey"": """ + Args + @".Index"",
                ""capabilityIds"": [""CAP-001""], ""rowKind"": ""schemaEmbedded"",
                ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                ""embeddedIn"": [""model_list_vertexes""] }] }";

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    ToolMapJsonReader.Read(map),
                    Roles(more: new[] { Embedded(Args, TypeRole.EventArgs) }),
                    Signatures(Property(Args + ".Index", Args))));

            Assert.Contains(
                "イベント引数型の埋め込み先がイベントの分岐に無い",
                error.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void ADtoPropertyIsEmbeddedInAToolOfTheTable()
        {
            const string Dto = "PEPlugin.PEVmePreviewOption";
            string map = @"{ ""rows"": [
                { ""signatureKey"": """ + Dto + @".Index"",
                  ""capabilityIds"": [""CAP-001""], ""rowKind"": ""schemaEmbedded"",
                  ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                  ""embeddedIn"": [""model_normalize_pmx_vertex""] },
                { ""signatureKey"": """ + Key + @""",
                  ""capabilityIds"": [""CAP-002""], ""rowKind"": ""directDispatch"",
                  ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                  ""tool"": ""model_normalize_pmx_vertex"",
                  ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
                    ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] }] }";

            Require(
                ToolMapJsonReader.Read(map),
                Roles(more: new[] { Embedded(Dto, TypeRole.Dto) }),
                Signatures(
                    Property(Dto + ".Index", Dto), Method(Key, Vertex, "NormalizePmx")));
        }

        [Fact]
        public void RejectsADtoPropertyEmbeddedInAToolTheTableDoesNotHave()
        {
            const string Dto = "PEPlugin.PEVmePreviewOption";
            string map = @"{ ""rows"": [{ ""signatureKey"": """ + Dto + @".Index"",
                ""capabilityIds"": [""CAP-001""], ""rowKind"": ""schemaEmbedded"",
                ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                ""embeddedIn"": [""model_list_vertexes""] }] }";

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    ToolMapJsonReader.Read(map),
                    Roles(more: new[] { Embedded(Dto, TypeRole.Dto) }),
                    Signatures(Property(Dto + ".Index", Dto))));

            Assert.Contains(
                "DTO型の埋め込み先が表のツールにもイベントの分岐にも無い",
                error.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsARowWhoseSignatureIsNotEnumerated()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    ToolMapJsonReader.Read(MapJson("model_normalize_pmx_vertex")),
                    Roles(),
                    Signatures()));

            Assert.Contains("公開APIの列挙に無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsAToolOnATypeWithoutARole()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    ToolMapJsonReader.Read(MapJson("model_normalize_pmx_vertex")),
                    Roles(typeName: "PEPlugin.Pmx.IPXOther"),
                    Signatures(Method(Key, Vertex, "NormalizePmx"))));

            Assert.Contains("型役割表に無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsAToolOnATypeWithoutAGroup()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    ToolMapJsonReader.Read(MapJson("model_normalize_pmx_vertex")),
                    new TypeRoleTable(
                        new[]
                        {
                            new TypeRoleRecord(
                                Vertex,
                                TypeRole.Dto,
                                "根拠。",
                                "vertex",
                                "vertexes",
                                CapabilityOwner.None),
                        },
                        new HandleIssuanceRecord[0],
                        new ElementCollectionRecord[0]),
                    Signatures(Method(Key, Vertex, "NormalizePmx"))));

            Assert.Contains("担当群を持たない型のツール", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ARowWithoutAToolIsNotChecked()
        {
            string map = @"{ ""rows"": [{ ""signatureKey"": """ + Key + @""",
                ""capabilityIds"": [""CAP-001""], ""rowKind"": ""commonContract"",
                ""editKind"": ""read"", ""direction"": ""read"", ""basis"": ""根拠。"",
                ""assignment"": ""internalFlow"", ""target"": ""connect"",
                ""slotBinding"": { ""return"": ""runArgsClone"", ""parameters"": {} } }] }";

            Require(ToolMapJsonReader.Read(map), Roles(), Signatures());
        }

        [Fact]
        public void RejectsTwoBranchesThatTakeTheSameRequiredInputs()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ToolMappingGate.Require(
                    ToolMapJsonReader.Read(@"{ ""rows"": [] }"),
                    Roles(),
                    Signatures(),
                    ToolSchemaJsonReader.Read(TwoBranchSchemaJson("count"))));

            Assert.Contains(
                "入力で判別できない呼び分けがある", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void BranchesThatTakeDifferentRequiredInputsPass()
        {
            ToolMappingGate.Require(
                ToolMapJsonReader.Read(@"{ ""rows"": [] }"),
                Roles(),
                Signatures(),
                ToolSchemaJsonReader.Read(TwoBranchSchemaJson("total")));
        }

        /// <summary>
        /// 呼び分けを2つ持ち、どちらも必ず1つを渡すまとまりを持つ入出力の形。2つ目のまとまりの
        /// 名前を差し替えられる。
        /// </summary>
        private static string ChoiceSchemaJson(string first, string second)
        {
            return @"{ ""tools"": [{ ""tool"": ""model_list_vertices"",
                ""branches"": [
                  { ""branch"": ""count"", ""inputs"": [
                    { ""name"": ""count"", ""origin"": ""hostInput"", ""shape"": ""number"" },
                    { ""name"": ""all"", ""origin"": ""hostInput"", ""shape"": ""boolean"" }],
                    ""choices"": [{ ""names"": [""count"", ""all""], ""required"": true }] },
                  { ""branch"": ""other"", ""inputs"": [
                    { ""name"": """ + first + @""", ""origin"": ""hostInput"",
                      ""shape"": ""number"" },
                    { ""name"": """ + second + @""", ""origin"": ""hostInput"",
                      ""shape"": ""boolean"" }],
                    ""choices"": [{ ""names"": [""" + first + @""", """ + second + @"""],
                      ""required"": true }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>
        /// 呼び分けを2つ持ち、無くてもよいまとまりの中だけが違う入出力の形。渡らないことがあるので、
        /// このまとまりでは呼び分けを見分けられない。
        /// </summary>
        private static string OptionalChoiceSchemaJson()
        {
            return @"{ ""tools"": [{ ""tool"": ""model_list_vertices"",
                ""branches"": [
                  { ""branch"": ""first"", ""inputs"": [
                    { ""name"": ""count"", ""origin"": ""hostInput"", ""shape"": ""number"",
                      ""required"": true },
                    { ""name"": ""offset"", ""origin"": ""hostInput"", ""shape"": ""number"" },
                    { ""name"": ""all"", ""origin"": ""hostInput"", ""shape"": ""boolean"" }],
                    ""choices"": [{ ""names"": [""offset"", ""all""], ""required"": false }] },
                  { ""branch"": ""second"", ""inputs"": [
                    { ""name"": ""count"", ""origin"": ""hostInput"", ""shape"": ""number"",
                      ""required"": true },
                    { ""name"": ""total"", ""origin"": ""hostInput"", ""shape"": ""number"" },
                    { ""name"": ""all"", ""origin"": ""hostInput"", ""shape"": ""boolean"" }],
                    ""choices"": [{ ""names"": [""total"", ""all""], ""required"": false }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>
        /// 呼び分けを2つ持ち、必ず渡す項目が同じで、無くてもよいまとまりだけが共通の名前を持たない
        /// 入出力の形。渡らないことがあるので、このまとまりでは呼び分けを見分けられない。
        /// </summary>
        private static string OptionalApartSchemaJson()
        {
            return @"{ ""tools"": [{ ""tool"": ""model_list_vertices"",
                ""branches"": [
                  { ""branch"": ""first"", ""inputs"": [
                    { ""name"": ""count"", ""origin"": ""hostInput"", ""shape"": ""number"",
                      ""required"": true },
                    { ""name"": ""offset"", ""origin"": ""hostInput"", ""shape"": ""number"" },
                    { ""name"": ""span"", ""origin"": ""hostInput"", ""shape"": ""number"" }],
                    ""choices"": [{ ""names"": [""offset"", ""span""], ""required"": false }] },
                  { ""branch"": ""second"", ""inputs"": [
                    { ""name"": ""count"", ""origin"": ""hostInput"", ""shape"": ""number"",
                      ""required"": true },
                    { ""name"": ""total"", ""origin"": ""hostInput"", ""shape"": ""number"" },
                    { ""name"": ""all"", ""origin"": ""hostInput"", ""shape"": ""boolean"" }],
                    ""choices"": [{ ""names"": [""total"", ""all""], ""required"": false }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>
        /// 呼び分けを2つ持ち、必ず渡す項目が組の配列である入出力の形。要素の組の中と、配列を空に
        /// できるかどうかを差し替えられる。
        /// </summary>
        private static string ElementMembersSchemaJson(string second, bool empty = false)
        {
            string least = empty ? string.Empty : @", ""minItems"": 1";
            return @"{ ""tools"": [{ ""tool"": ""model_list_vertices"",
                ""branches"": [
                  { ""branch"": ""first"", ""inputs"": [
                    { ""name"": ""argsList"", ""origin"": ""hostInput"", ""required"": true" + least + @",
                      ""element"": { ""origin"": ""hostInput"", ""members"": [
                        { ""name"": ""count"", ""origin"": ""sdkIn"", ""shape"": ""number"",
                          ""required"": true }] } }] },
                  { ""branch"": ""second"", ""inputs"": [
                    { ""name"": ""argsList"", ""origin"": ""hostInput"", ""required"": true" + least + @",
                      ""element"": { ""origin"": ""hostInput"", ""members"": [
                        { ""name"": """ + second + @""", ""origin"": ""sdkIn"",
                          ""shape"": ""number"", ""required"": true }] } }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>呼び分けを2つ持ち、必須の項目が組である入出力の形。組の中だけが違う。</summary>
        private static string MembersSchemaJson(string second)
        {
            return @"{ ""tools"": [{ ""tool"": ""model_list_vertices"",
                ""branches"": [
                  { ""branch"": ""first"", ""inputs"": [
                    { ""name"": ""args"", ""origin"": ""hostInput"", ""required"": true,
                      ""members"": [{ ""name"": ""count"", ""origin"": ""sdkIn"",
                        ""shape"": ""number"", ""required"": true }] }] },
                  { ""branch"": ""second"", ""inputs"": [
                    { ""name"": ""args"", ""origin"": ""hostInput"", ""required"": true,
                      ""members"": [{ ""name"": """ + second + @""", ""origin"": ""sdkIn"",
                        ""shape"": ""number"", ""required"": true }] }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>呼び分けを2つ持ち、値で分かれる入出力の形。選ぶ項目の値だけが違う。</summary>
        private static string SelectorSchemaJson(string second)
        {
            return @"{ ""tools"": [{ ""tool"": ""model_list_vertices"",
                ""branches"": [
                  { ""branch"": ""first"", ""selector"": { ""name"": ""kind"", ""value"": ""a"" },
                    ""inputs"": [
                      { ""name"": ""kind"", ""origin"": ""hostInput"", ""shape"": ""text"",
                        ""required"": true }] },
                  { ""branch"": ""second"",
                    ""selector"": { ""name"": ""kind"", ""value"": """ + second + @""" },
                    ""inputs"": [
                      { ""name"": ""kind"", ""origin"": ""hostInput"", ""shape"": ""text"",
                        ""required"": true }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        /// <summary>呼び分けを2つ持つ入出力の形。2つ目の必須の入力の名前だけを差し替える。</summary>
        private static string TwoBranchSchemaJson(string second)
        {
            return @"{ ""tools"": [{ ""tool"": ""model_list_vertices"",
                ""branches"": [
                  { ""branch"": ""count"", ""inputs"": [
                    { ""name"": ""count"", ""origin"": ""hostInput"", ""shape"": ""number"",
                      ""required"": true }] },
                  { ""branch"": ""other"", ""inputs"": [
                    { ""name"": """ + second + @""", ""origin"": ""hostInput"",
                      ""shape"": ""number"", ""required"": true }] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }";
        }

        [Fact]
        public void RequiredChoicesWithoutACommonNameTellTwoBranchesApart()
        {
            RequireBranches(ChoiceSchemaJson("total", "span"));
        }

        [Fact]
        public void RejectsRequiredChoicesThatShareAName()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => RequireBranches(ChoiceSchemaJson("total", "all")));

            Assert.Contains(
                "入力で判別できない呼び分けがある", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsTwoBranchesWhoseRequiredChoicesAreTheSame()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => RequireBranches(ChoiceSchemaJson("count", "all")));

            Assert.Contains(
                "入力で判別できない呼び分けがある", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AChoiceThatMayBeAbsentDoesNotTellTwoBranchesApart()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => RequireBranches(OptionalChoiceSchemaJson()));

            Assert.Contains(
                "入力で判別できない呼び分けがある", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ChoicesThatMayBeAbsentDoNotTellTwoBranchesApartEvenWithoutACommonName()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => RequireBranches(OptionalApartSchemaJson()));

            Assert.Contains(
                "入力で判別できない呼び分けがある", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheMembersOfTheElementOfARequiredArrayTellTwoBranchesApart()
        {
            RequireBranches(ElementMembersSchemaJson("total"));
        }

        [Fact]
        public void TheMembersOfTheElementOfAnEmptiableArrayDoNotTellTwoBranchesApart()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => RequireBranches(ElementMembersSchemaJson("total", empty: true)));

            Assert.Contains(
                "入力で判別できない呼び分けがある", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsTwoBranchesWhoseArrayElementsHaveTheSameMembers()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => RequireBranches(ElementMembersSchemaJson("count")));

            Assert.Contains(
                "入力で判別できない呼び分けがある", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheMembersOfARequiredGroupTellTwoBranchesApart()
        {
            RequireBranches(MembersSchemaJson("total"));
        }

        [Fact]
        public void RejectsTwoBranchesWhoseGroupsHaveTheSameMembers()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => RequireBranches(MembersSchemaJson("count")));

            Assert.Contains(
                "入力で判別できない呼び分けがある", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheValueOfTheSelectorTellsTwoBranchesApart()
        {
            RequireBranches(SelectorSchemaJson("b"));
        }

        [Fact]
        public void RejectsTwoBranchesThatSelectOnTheSameValue()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => RequireBranches(SelectorSchemaJson("a")));

            Assert.Contains(
                "入力で判別できない呼び分けがある", error.Message, StringComparison.Ordinal);
        }

        /// <summary>呼び分けの見分けだけを見る呼び出し。行を持たない能力対応表を渡す。</summary>
        private static void RequireBranches(string schemas)
        {
            ToolMappingGate.Require(
                ToolMapJsonReader.Read(@"{ ""rows"": [] }"),
                Roles(),
                Signatures(),
                ToolSchemaJsonReader.Read(schemas));
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            ToolMap map = ToolMapJsonReader.Read(MapJson("model_normalize_pmx_vertex"));
            TypeRoleTable roles = Roles();
            IDictionary<string, SignatureRecord> signatures =
                Signatures(Method(Key, Vertex, "NormalizePmx"));
            ToolSchemaTable schemas = ToolSchemaJsonReader.Read(NoTools);

            Assert.Throws<ArgumentNullException>(
                () => ToolMappingGate.Require(null, roles, signatures, schemas));
            Assert.Throws<ArgumentNullException>(
                () => ToolMappingGate.Require(map, null, signatures, schemas));
            Assert.Throws<ArgumentNullException>(
                () => ToolMappingGate.Require(map, roles, null, schemas));
            Assert.Throws<ArgumentNullException>(
                () => ToolMappingGate.Require(map, roles, signatures, null));
        }

        /// <summary>入出力の形を持たないスキーマ正本。名前の照合だけを見る試験が使う。</summary>
        private const string NoTools = @"{ ""tools"": [] }";

        /// <summary>名前の照合だけを見る呼び出し。呼び分けを持たないスキーマ正本を渡す。</summary>
        private static void Require(
            ToolMap map, TypeRoleTable roles, IDictionary<string, SignatureRecord> signatures)
        {
            ToolMappingGate.Require(map, roles, signatures, ToolSchemaJsonReader.Read(NoTools));
        }
    }
}
