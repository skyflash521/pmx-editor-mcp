using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolNameEvidenceTests
    {
        private const string Vertex = "PEPlugin.Pmx.IPXVertex";

        private const string Connector = "PEPlugin.Pmx.IPXPmxConnector";

        private const string Other = "PEPlugin.Pmx.IPXOtherConnector";

        private const string Model = "PEPlugin.Form.IPXUIModel";

        private const string Key = Vertex + ".NormalizePmx()";

        [Fact]
        public void AMethodTakesTheGroupAndTheActionWordAndTheElementNoun()
        {
            IDictionary<string, string> names = Resolve(
                Map(Key), Roles(), Signatures(Method(Key, Vertex, "NormalizePmx")));

            Assert.Equal("model_normalize_pmx_vertex", names[Key]);
        }

        [Fact]
        public void AConnectorMethodTakesNoQualifierWithoutACollision()
        {
            string key = Connector + ".Save()";
            IDictionary<string, string> names = Resolve(
                Map(key),
                Roles(TypeRole.Connector, Connector, "pmx_connector"),
                Signatures(Method(key, Connector, "Save")));

            Assert.Equal("model_save", names[key]);
        }

        [Fact]
        public void CollidingConnectorMethodsTakeTheQualifier()
        {
            string first = Other + ".Save()";
            string second = Connector + ".Save()";
            IDictionary<string, string> names = Resolve(
                Map(first, second),
                Roles(
                    TypeRole.Connector,
                    Connector,
                    "pmx_connector",
                    more: new[] { Type(Other, TypeRole.Connector, "other_connector") }),
                Signatures(Method(first, Other, "Save"), Method(second, Connector, "Save")));

            Assert.Equal("model_save_other_connector", names[first]);
            Assert.Equal("model_save_pmx_connector", names[second]);
        }

        [Fact]
        public void OverloadsOfOneConnectorMethodDoNotCollide()
        {
            string first = Connector + ".Save()";
            string second = Connector + ".Save(System.String)";
            IDictionary<string, string> names = Resolve(
                Map(first, second),
                Roles(TypeRole.Connector, Connector, "pmx_connector"),
                Signatures(Method(first, Connector, "Save"), Method(second, Connector, "Save")));

            Assert.Equal("model_save", names[first]);
            Assert.Equal("model_save", names[second]);
        }

        [Fact]
        public void AConstructorTakesTheCreateNameOfItsDeclaringType()
        {
            string key = Model + "..ctor()";
            IDictionary<string, string> names = Resolve(
                Map(key),
                Roles(TypeRole.HandleTarget, Model, "ui_model"),
                Signatures(Method(key, Model, ".ctor", Model, MemberKind.Constructor)));

            Assert.Equal("model_create_ui_model", names[key]);
        }

        [Fact]
        public void AMethodThatMakesAHandleTargetKeepsItsMemberName()
        {
            const string Builder = "PEPlugin.IPEBuilder";
            string key = Builder + ".CreateModel()";
            IDictionary<string, string> names = Resolve(
                Map(key),
                Roles(
                    TypeRole.HandleTarget,
                    Model,
                    "ui_model",
                    more: new[] { Type(Builder, TypeRole.Connector, "builder") }),
                Signatures(Method(key, Builder, "CreateModel", Model)));

            Assert.Equal("model_create_model", names[key]);
        }

        [Fact]
        public void AnArrayReturnDoesNotChangeTheDeclaringTypeLookup()
        {
            string key = Vertex + ".ToKeyArray()";
            IDictionary<string, string> names = Resolve(
                Map(key), Roles(), Signatures(Method(key, Vertex, "ToKeyArray", Vertex + "[]")));

            Assert.Equal("model_to_key_array_vertex", names[key]);
        }

        [Fact]
        public void ARowTheAssignmentCanonHasIsNotInTheTable()
        {
            IDictionary<string, string> names = ToolNameEvidence.Resolve(
                ToolMapJsonReader.Read(Map(Key)),
                Roles(),
                CommonAssignmentJsonReader.Read(Assignments(Key)),
                Signatures(Method(Key, Vertex, "NormalizePmx")));

            Assert.Empty(names);
        }

        [Fact]
        public void APropertyIsNotInTheTable()
        {
            string key = Vertex + ".Index";
            IDictionary<string, string> names = Resolve(
                Map(key),
                Roles(),
                Signatures(Method(key, Vertex, "Index", "System.Int32", MemberKind.Property)));

            Assert.Empty(names);
        }

        [Fact]
        public void AConstructorOfATypeWithoutItsOwnToolIsNotInTheTable()
        {
            const string Args = "PEPlugin.View.PXViewClickEventArgs";
            string key = Args + "..ctor()";
            IDictionary<string, string> names = Resolve(
                Map(key),
                Roles(more: new[] { Embedded(Args, TypeRole.EventArgs) }),
                Signatures(Method(key, Args, ".ctor", Args, MemberKind.Constructor)));

            Assert.Empty(names);
        }

        [Fact]
        public void ARowKeyOutsideTheEnumerationIsNotInTheTable()
        {
            Assert.Empty(Resolve(Map(Key), Roles(), Signatures()));
        }

        [Fact]
        public void ATypeTheRoleTableDoesNotCarryStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Resolve(
                    Map(Key),
                    Roles(typeName: "PEPlugin.Pmx.IPXOther"),
                    Signatures(Method(Key, Vertex, "NormalizePmx"))));

            Assert.Contains("型役割表に無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ATypeWithoutAGroupStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Resolve(
                    Map(Key),
                    new TypeRoleTable(
                        new[] { Embedded(Vertex, TypeRole.Dto) },
                        new HandleIssuanceRecord[0],
                        new ElementCollectionRecord[0]),
                    Signatures(Method(Key, Vertex, "NormalizePmx"))));

            Assert.Contains("担当群を持たない型のツール", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            ToolMap map = ToolMapJsonReader.Read(Map(Key));
            TypeRoleTable roles = Roles();
            CommonAssignmentTable assignments =
                CommonAssignmentJsonReader.Read(@"{ ""assignments"": [] }");
            IDictionary<string, SignatureRecord> signatures =
                Signatures(Method(Key, Vertex, "NormalizePmx"));

            Assert.Throws<ArgumentNullException>(
                () => ToolNameEvidence.Resolve(null, roles, assignments, signatures));
            Assert.Throws<ArgumentNullException>(
                () => ToolNameEvidence.Resolve(map, null, assignments, signatures));
            Assert.Throws<ArgumentNullException>(
                () => ToolNameEvidence.Resolve(map, roles, null, signatures));
            Assert.Throws<ArgumentNullException>(
                () => ToolNameEvidence.Resolve(map, roles, assignments, null));
        }

        [Fact]
        public void APropertyThatListsAHandledTypeTakesNoNameWhileOneThatHoldsItDoes()
        {
            const string owner = "PEPlugin.Vme.IPEVmeOwner";
            const string held = "PEPlugin.Vme.IPEVmeHeld";
            string one = owner + ".Held()";
            string many = owner + ".Items()";
            IDictionary<string, string> names = Resolve(
                Map(one, many),
                Roles(
                    TypeRole.HandleTarget,
                    owner,
                    "vme_owner",
                    new[] { Type(held, TypeRole.HandleTarget, "vme_held") },
                    new[] { new ElementCollectionRecord(many, true, "根拠。", new[] { many }) }),
                Signatures(
                    Method(one, owner, "Held", held, MemberKind.Property),
                    Method(
                        many,
                        owner,
                        "Items",
                        "System.Collections.Generic.IList<" + held + ">",
                        MemberKind.Property)));

            Assert.Equal("model_held_vme_owner", names[one]);
            Assert.False(names.ContainsKey(many));
        }

        private static IDictionary<string, string> Resolve(
            string map, TypeRoleTable roles, IDictionary<string, SignatureRecord> signatures)
        {
            return ToolNameEvidence.Resolve(
                ToolMapJsonReader.Read(map),
                roles,
                CommonAssignmentJsonReader.Read(@"{ ""assignments"": [] }"),
                signatures);
        }

        /// <summary>行キーだけを差し替える能力対応表。種別ごとの項目は照合が見るので持たせない。</summary>
        private static string Map(params string[] keys)
        {
            List<string> rows = new List<string>();
            foreach (string key in keys)
            {
                rows.Add(@"{ ""signatureKey"": """ + key + @""",
                    ""editKind"": ""read"", ""basis"": ""根拠。"" }");
            }

            return @"{ ""rows"": [" + string.Join(",", rows.ToArray()) + "] }";
        }

        /// <summary>その行キーを共通契約へ割り当てた正本。</summary>
        private static string Assignments(string key)
        {
            return @"{ ""assignments"": [{ ""signatureKey"": """ + key + @""",
                ""assignment"": ""internalFlow"", ""target"": ""connect"",
                ""basis"": ""根拠。"" }] }";
        }

        private static TypeRoleTable Roles(
            TypeRole role = TypeRole.OperationTarget,
            string typeName = Vertex,
            string elementNoun = "vertex",
            IList<TypeRoleRecord> more = null,
            IList<ElementCollectionRecord> collections = null)
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
                new HandleIssuanceRecord[0],
                collections ?? new ElementCollectionRecord[0]);
        }

        private static TypeRoleRecord Type(string typeName, TypeRole role, string elementNoun)
        {
            return new TypeRoleRecord(
                typeName, role, "根拠。", elementNoun, elementNoun + "es", CapabilityOwner.Model);
        }

        /// <summary>独立したツールを持たない役割の型。担当群を持たない。</summary>
        private static TypeRoleRecord Embedded(string typeName, TypeRole role)
        {
            return new TypeRoleRecord(
                typeName, role, "根拠。", "embedded", "embeddeds", CapabilityOwner.None);
        }

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
    }
}
