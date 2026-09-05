using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolNameGateTests
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
                "接続の経路。",
                CapabilityOwner.Model,
                new Dictionary<ToolVerb, string> { { ToolVerb.List, "model_list_" + elementNoun } });
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
                "接続の経路。",
                CapabilityOwner.None,
                new Dictionary<ToolVerb, string>());
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
            ToolNameGate.Require(
                ToolMapJsonReader.Read(MapJson("model_normalize_pmx_vertex")),
                Roles(),
                Signatures(Method(Key, Vertex, "NormalizePmx")));
        }

        [Fact]
        public void RejectsAMethodNameWithoutTheSourceQualifier()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ToolNameGate.Require(
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
            ToolNameGate.Require(
                ToolMapJsonReader.Read(MapJson("model_save", Connector + ".Save()")),
                Roles(TypeRole.Connector, Connector, "pmx_connector"),
                Signatures(Method(Connector + ".Save()", Connector, "Save")));
        }

        [Fact]
        public void CollidingConnectorMethodsTakeTheQualifier()
        {
            const string Other = "PEPlugin.Pmx.IPXOtherConnector";

            ToolNameGate.Require(
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
                () => ToolNameGate.Require(
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

            ToolNameGate.Require(
                ToolMapJsonReader.Read(MapJson("model_create_ui_model", key)),
                Roles(
                    TypeRole.HandleTarget,
                    Model,
                    "ui_model",
                    issuances: new[]
                    {
                        new HandleIssuanceRecord(key, true, HandleIssuanceKind.Constructor, "根拠。"),
                    }),
                Signatures(Method(key, Model, ".ctor", Model, MemberKind.Constructor)));
        }

        [Fact]
        public void AMethodThatMakesAHandleTargetKeepsItsMemberName()
        {
            const string Model = "PEPlugin.Form.IPXUIModel";
            const string Builder = "PEPlugin.IPEBuilder";
            string factory = Builder + ".CreateModel()";

            ToolNameGate.Require(
                ToolMapJsonReader.Read(MapJson("model_create_model", factory)),
                Roles(
                    TypeRole.HandleTarget,
                    Model,
                    "ui_model",
                    issuances: new[]
                    {
                        new HandleIssuanceRecord(
                            factory, true, HandleIssuanceKind.Factory, "根拠。"),
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
                () => ToolNameGate.Require(
                    ToolMapJsonReader.Read(MapJson("view_create_ui_model", key)),
                    Roles(TypeRole.HandleTarget, Model, "ui_model"),
                    Signatures(Method(key, Model, ".ctor", Model, MemberKind.Constructor))));

            Assert.Contains("model_create_ui_model", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnArrayReturnDoesNotChangeTheDeclaringTypeLookup()
        {
            string key = Vertex + ".ToKeyArray()";

            ToolNameGate.Require(
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

            ToolNameGate.Require(
                ToolMapJsonReader.Read(map),
                Roles(TypeRole.Connector, Connector, "pmx_connector"),
                Signatures(
                    Method(first, Connector, "Save"), Method(second, Connector, "Save")));
        }

        [Fact]
        public void AnEmbeddedNameMustBeAToolOfTheDeclaringType()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ToolNameGate.Require(
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
                "接続の経路。",
                CapabilityOwner.Model,
                new Dictionary<ToolVerb, string>
                {
                    { ToolVerb.List, "model_list_vertices" },
                    { ToolVerb.Add, "model_add_vertices" },
                });

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ToolNameGate.Require(
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
            ToolNameGate.Require(
                ToolMapJsonReader.Read(EmbeddedMapJson("model_list_vertex")),
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

            ToolNameGate.Require(
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
                ""embeddedIn"": [""model_list_vertex""] }] }";

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ToolNameGate.Require(
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

            ToolNameGate.Require(
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
                ""embeddedIn"": [""model_list_vertex""] }] }";

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ToolNameGate.Require(
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
                () => ToolNameGate.Require(
                    ToolMapJsonReader.Read(MapJson("model_normalize_pmx_vertex")),
                    Roles(),
                    Signatures()));

            Assert.Contains("公開APIの列挙に無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsAToolOnATypeWithoutARole()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ToolNameGate.Require(
                    ToolMapJsonReader.Read(MapJson("model_normalize_pmx_vertex")),
                    Roles(typeName: "PEPlugin.Pmx.IPXOther"),
                    Signatures(Method(Key, Vertex, "NormalizePmx"))));

            Assert.Contains("型役割表に無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsAToolOnATypeWithoutAGroup()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ToolNameGate.Require(
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
                                "接続の経路。",
                                CapabilityOwner.None,
                                new Dictionary<ToolVerb, string>()),
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

            ToolNameGate.Require(ToolMapJsonReader.Read(map), Roles(), Signatures());
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            ToolMap map = ToolMapJsonReader.Read(MapJson("model_normalize_pmx_vertex"));
            TypeRoleTable roles = Roles();
            IDictionary<string, SignatureRecord> signatures =
                Signatures(Method(Key, Vertex, "NormalizePmx"));

            Assert.Throws<ArgumentNullException>(
                () => ToolNameGate.Require(null, roles, signatures));
            Assert.Throws<ArgumentNullException>(
                () => ToolNameGate.Require(map, null, signatures));
            Assert.Throws<ArgumentNullException>(
                () => ToolNameGate.Require(map, roles, null));
        }
    }
}
