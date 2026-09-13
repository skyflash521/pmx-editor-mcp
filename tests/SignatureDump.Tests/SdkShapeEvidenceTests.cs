using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>SDKに由来する項目の表現の綴り。正本は綴りを書かないので、行の側から導く。</summary>
    public sealed class SdkShapeEvidenceTests
    {
        private const string Form = "Sdk.Form";

        private const string GetTool = "session_get_form";

        private const string UpdateTool = "session_update_form";

        private static readonly IDictionary<string, string> ShapesByType =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "System.Boolean", "boolean" },
                { "System.Int32", "number" },
                { "System.String", "text" },
                { "System.Byte", "number" },
                { "Sdk.Handle", null },
            };

        [Theory]
        [InlineData("UndoCount", "undoCount")]
        [InlineData("PmxFormActivate", "pmxFormActivate")]
        [InlineData("IKItemsCount", "ikItemsCount")]
        [InlineData("SavePMXFile", "savePmxFile")]
        public void TheMemberNameBreaksWhereTheActionWordBreaks(string member, string expected)
        {
            Assert.Equal(expected, SdkShapeEvidence.MemberNameOf(member));
        }

        [Fact]
        public void AnArgumentTakesTheSpellingOfItsType()
        {
            IDictionary<SchemaItem, string> shapes = Resolve(
                Schemas(Dispatched("session_save", "{\"name\":\"path\",\"required\":true}", "{}")),
                Map(Row("Save", "System.Boolean", "path")),
                Signatures(Method("Save", "System.Boolean", "path")),
                Named("Save", "session_save"));

            Assert.Equal("text", Shape(shapes, "path"));
        }

        [Fact]
        public void TheOutputOfARowThatReturnsAValueTakesTheSpellingOfThatValue()
        {
            IDictionary<SchemaItem, string> shapes = Resolve(
                Schemas(Dispatched("session_save", "{\"name\":\"path\",\"required\":true}", "{}")),
                Map(Row("Save", "System.Boolean", "path")),
                Signatures(Method("Save", "System.Boolean", "path")),
                Named("Save", "session_save"));

            Assert.Contains("boolean", shapes.Values);
        }

        [Fact]
        public void AnOutputTheHostDecidesIsLeftAlone()
        {
            IDictionary<SchemaItem, string> shapes = Resolve(
                Schemas(Dispatched(
                    "session_save",
                    "{\"name\":\"path\",\"required\":true}",
                    "{\"origin\":\"hostOutput\",\"shape\":\"null_value\"}")),
                Map(Row("Save", "System.Void", "path")),
                Signatures(Method("Save", "System.Void", "path")),
                Named("Save", "session_save"));

            Assert.Equal(new[] { "text" }, shapes.Values.ToArray());
        }

        [Fact]
        public void AnEmbeddedItemTakesTheSpellingOnBothSides()
        {
            IDictionary<SchemaItem, string> shapes = Resolve(
                Aggregations(),
                Map(Property("Flag", "System.Boolean", GetTool, UpdateTool)),
                Signatures(PropertyRecordOf("Flag", "System.Boolean")),
                Named());

            Assert.Equal(new[] { "boolean", "boolean" }, shapes.Values.ToArray());
        }

        [Fact]
        public void AnEmbeddedItemThatTheToolDoesNotCarryStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Resolve(
                    Aggregations(),
                    Map(Property("Depth", "System.Boolean", GetTool)),
                    Signatures(PropertyRecordOf("Depth", "System.Boolean")),
                    Named()));

            Assert.StartsWith("埋め込み先に持ち込む項目が無い:", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnArrayOfBytesIsPackedAsOneString()
        {
            IDictionary<SchemaItem, string> shapes = Resolve(
                Schemas(Dispatched("session_save", "{\"name\":\"data\",\"required\":true}", "{}")),
                Map(Row("Save", "System.Boolean", "data", "System.Byte[]")),
                Signatures(Method("Save", "System.Boolean", "data", "System.Byte[]")),
                Named("Save", "session_save"));

            Assert.Equal("base64", Shape(shapes, "data"));
        }

        [Fact]
        public void AnArrayWrittenAsAnElementTakesTheSpellingOfItsElement()
        {
            IDictionary<SchemaItem, string> shapes = Resolve(
                Schemas(Dispatched(
                    "session_save",
                    "{\"name\":\"indices\",\"element\":{},\"required\":true}",
                    "{}")),
                Map(Row("Save", "System.Boolean", "indices", "System.Int32[]")),
                Signatures(Method("Save", "System.Boolean", "indices", "System.Int32[]")),
                Named("Save", "session_save"));

            Assert.Contains("number", shapes.Values);
        }

        [Fact]
        public void AValueThatCanBeAbsentTakesTheSpellingOfTheValueItHolds()
        {
            IDictionary<SchemaItem, string> shapes = Resolve(
                Schemas(Dispatched("session_save", "{\"name\":\"depth\",\"required\":true}", "{}")),
                Map(Row("Save", "System.Boolean", "depth", "System.Nullable<System.Int32>")),
                Signatures(Method("Save", "System.Boolean", "depth", "System.Nullable<System.Int32>")),
                Named("Save", "session_save"));

            Assert.Equal("number", Shape(shapes, "depth"));
        }

        [Fact]
        public void ATypeThatCannotBeWrittenAsAValueStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Resolve(
                    Schemas(Dispatched("session_save", "{\"name\":\"handle\",\"required\":true}", "{}")),
                    Map(Row("Save", "System.Boolean", "handle", "Sdk.Handle")),
                    Signatures(Method("Save", "System.Boolean", "handle", "Sdk.Handle")),
                    Named("Save", "session_save")));

            Assert.StartsWith("値として写せない型の項目がある:", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheBranchOfARowIsFoundFromTheSpellingTable()
        {
            IDictionary<string, SignatureRecord> signatures = Shares();
            IDictionary<SchemaItem, string> shapes = SdkShapeEvidence.Resolve(
                Schemas(SharingSchema()),
                new ToolMap(signatures.Values.Select(SharingRow).ToList()),
                signatures,
                signatures.Keys.ToDictionary(
                    k => k, k => "session_share", StringComparer.Ordinal),
                ShapesByType.Keys.ToDictionary(k => k, k => k, StringComparer.Ordinal),
                ShapesByType);

            Assert.Equal(
                new[] { "System.Int32", "System.String" },
                shapes
                    .Where(s => string.Equals(s.Key.Name, "data", StringComparison.Ordinal))
                    .Select(s => s.Value)
                    .OrderBy(v => v, StringComparer.Ordinal)
                    .ToArray());
        }

        /// <summary>引数の名前が同じで型だけが違う2つの呼び分け。</summary>
        private static string SharingSchema()
        {
            return "{\"tool\":\"session_share\",\"branches\":["
                + SharingBranch("text") + "," + SharingBranch("number")
                + "],\"output\":{}}";
        }

        private static string SharingBranch(string shape)
        {
            return "{\"branch\":\"" + shape + "\""
                + ",\"selector\":{\"name\":\"dataShape\",\"value\":\"" + shape + "\"}"
                + ",\"inputs\":[{\"name\":\"dataShape\",\"origin\":\"hostInput\""
                + ",\"shape\":\"text\",\"required\":true}"
                + ",{\"name\":\"key\",\"required\":true}"
                + ",{\"name\":\"data\",\"required\":true}]}";
        }

        private static IDictionary<string, SignatureRecord> Shares()
        {
            return new[] { "System.String", "System.Int32" }
                .Select(Sharing)
                .ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);
        }

        private static SignatureRecord Sharing(string dataType)
        {
            return new SignatureRecord(
                Form + ".Share(System.String," + dataType + ")",
                Form,
                MemberKind.Method,
                "Share",
                false,
                0,
                new[]
                {
                    new ParameterRecord("key", "System.String", ParameterDirection.In, false),
                    new ParameterRecord("data", dataType, ParameterDirection.In, false),
                },
                "System.Boolean",
                false,
                false,
                OperationDirection.Write);
        }

        private static ToolMapRow SharingRow(SignatureRecord signature)
        {
            return new ToolMapRow(
                signature.Key,
                ToolMapEditKind.DirectChange,
                null,
                "題材の根拠。",
                new[]
                {
                    new Postcondition(
                        EffectType.None,
                        string.Empty,
                        EffectCheckKind.CallLogOnly,
                        null,
                        null,
                        null,
                        EffectComparison.Exists,
                        null,
                        false,
                        null),
                },
                null,
                null);
        }

        private static string Shape(IDictionary<SchemaItem, string> shapes, string name)
        {
            return shapes
                .Where(s => string.Equals(s.Key.Name, name, StringComparison.Ordinal))
                .Select(s => s.Value)
                .Single();
        }

        private static IDictionary<SchemaItem, string> Resolve(
            ToolSchemaTable schemas,
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolNames)
        {
            return SdkShapeEvidence.Resolve(
                schemas, map, signatures, toolNames, ShapesByType, ShapesByType);
        }

        private static ToolSchemaTable Schemas(params string[] tools)
        {
            return ToolSchemaJsonReader.Read(
                "{\"tools\":[" + string.Join(",", tools) + "]}");
        }

        private static string Dispatched(string tool, string input, string output)
        {
            return "{\"tool\":\"" + tool + "\",\"branches\":[{\"branch\":\"only\",\"inputs\":["
                + input + "]}],\"output\":" + output + "}";
        }

        private static ToolSchemaTable Aggregations()
        {
            return Schemas(
                "{\"tool\":\"" + GetTool + "\",\"branches\":[{\"branch\":\"only\",\"inputs\":[]}]"
                    + ",\"output\":{\"origin\":\"hostOutput\",\"members\":[{\"name\":\"flag\"}]}}",
                "{\"tool\":\"" + UpdateTool + "\",\"branches\":[{\"branch\":\"only\",\"inputs\":["
                    + "{\"name\":\"flag\",\"required\":false}]}]"
                    + ",\"output\":{\"origin\":\"hostOutput\",\"shape\":\"number\"}}");
        }

        private static ToolMap Map(params ToolMapRow[] rows)
        {
            return new ToolMap(rows);
        }

        private static ToolMapRow Row(
            string member,
            string valueType,
            string parameter,
            string parameterType = "System.String")
        {
            return new ToolMapRow(
                Method(member, valueType, parameter, parameterType).Key,
                ToolMapEditKind.DirectChange,
                null,
                "題材の根拠。",
                new[]
                {
                    new Postcondition(
                        EffectType.None,
                        string.Empty,
                        EffectCheckKind.CallLogOnly,
                        null,
                        null,
                        null,
                        EffectComparison.Exists,
                        null,
                        false,
                        null),
                },
                null,
                null);
        }

        private static ToolMapRow Property(string member, string valueType, params string[] tools)
        {
            return new ToolMapRow(
                PropertyRecordOf(member, valueType).Key,
                ToolMapEditKind.ViewSession,
                null,
                "題材の根拠。",
                null,
                null,
                tools);
        }

        private static IDictionary<string, SignatureRecord> Signatures(
            params SignatureRecord[] signatures)
        {
            return signatures.ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);
        }

        private static IDictionary<string, string> Named(params string[] pairs)
        {
            Dictionary<string, string> named = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int at = 0; at < pairs.Length; at += 2)
            {
                named.Add(Form + "." + pairs[at] + "(System.String)", pairs[at + 1]);
            }

            return named;
        }

        private static SignatureRecord Method(
            string member,
            string valueType,
            string parameter,
            string parameterType = "System.String")
        {
            return new SignatureRecord(
                Form + "." + member + "(System.String)",
                Form,
                MemberKind.Method,
                member,
                false,
                0,
                new[] { new ParameterRecord(parameter, parameterType, ParameterDirection.In, false) },
                valueType,
                false,
                false,
                OperationDirection.Write);
        }

        private static SignatureRecord PropertyRecordOf(string member, string valueType)
        {
            return new SignatureRecord(
                Form + "." + member + "()",
                Form,
                MemberKind.Property,
                member,
                false,
                0,
                new ParameterRecord[0],
                valueType,
                true,
                true,
                OperationDirection.Write);
        }
    }
}
