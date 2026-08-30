using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class InventoryJsonTests
    {
        // 題材は、分類を表す全項目の取りうる値と、真偽で表す全項目の両方の状態を1度ずつ含む。
        // 一部の値しか通らない題材では、通らなかった値を誤った文字列で書き出す実装が残る。
        private static readonly string[] ExpectedLines =
        {
            "{",
            "\"assemblyName\":\"Sample\",",
            "\"assemblyVersion\":\"1.2.3.4\",",
            "\"types\":[",
            "{\"name\":\"N.Holder\",\"kind\":\"class\",\"isNested\":false,\"isAbstract\":false,"
                + "\"isGenericTypeDefinition\":true,\"baseTypes\":[],\"enumMembers\":[]},",
            "{\"name\":\"N.Holder+Inner\",\"kind\":\"struct\",\"isNested\":true,\"isAbstract\":false,"
                + "\"isGenericTypeDefinition\":false,\"baseTypes\":[],\"enumMembers\":[]},",
            "{\"name\":\"N.IBase\",\"kind\":\"interface\",\"isNested\":false,\"isAbstract\":true,"
                + "\"isGenericTypeDefinition\":false,\"baseTypes\":[],\"enumMembers\":[]},",
            "{\"name\":\"N.IThing\",\"kind\":\"interface\",\"isNested\":false,\"isAbstract\":true,"
                + "\"isGenericTypeDefinition\":false,\"baseTypes\":[\"N.IBase\"],\"enumMembers\":[]},",
            "{\"name\":\"N.Kind\",\"kind\":\"enum\",\"isNested\":false,\"isAbstract\":false,"
                + "\"isGenericTypeDefinition\":false,\"baseTypes\":[],"
                + "\"enumMembers\":[\"Second\",\"First\"]},",
            "{\"name\":\"N.Proc\",\"kind\":\"delegate\",\"isNested\":false,\"isAbstract\":false,"
                + "\"isGenericTypeDefinition\":false,\"baseTypes\":[],\"enumMembers\":[]}",
            "],",
            "\"referencedTypes\":[",
            "{\"name\":\"System.EventHandler\",\"kind\":\"delegate\",\"isNested\":false,"
                + "\"isAbstract\":false,\"isGenericTypeDefinition\":false,\"baseTypes\":[],"
                + "\"enumMembers\":[]}",
            "],",
            "\"signatures\":[",
            "{\"key\":\"N.Holder..ctor(System.Int32)\",\"declaringType\":\"N.Holder\","
                + "\"memberKind\":\"constructor\",\"memberName\":\".ctor\",\"isStatic\":false,"
                + "\"genericArity\":0,\"parameters\":["
                + "{\"name\":\"seed\",\"typeName\":\"System.Int32\",\"direction\":\"in\",\"isOptional\":false,\"isTypeArgument\":false}"
                + "],\"valueType\":\"N.Holder\",\"canRead\":false,\"canWrite\":false,"
                + "\"operationDirection\":\"write\",\"valueTypeIsTypeArgument\":false},",
            "{\"key\":\"N.Holder.Count()\",\"declaringType\":\"N.Holder\",\"memberKind\":\"field\","
                + "\"memberName\":\"Count\",\"isStatic\":true,\"genericArity\":0,\"parameters\":[],"
                + "\"valueType\":\"System.Int32\",\"canRead\":true,\"canWrite\":true,"
                + "\"operationDirection\":\"write\",\"valueTypeIsTypeArgument\":false},",
            "{\"key\":\"N.IThing.Changed()\",\"declaringType\":\"N.IThing\",\"memberKind\":\"event\","
                + "\"memberName\":\"Changed\",\"isStatic\":false,\"genericArity\":0,\"parameters\":[],"
                + "\"valueType\":\"System.EventHandler\",\"canRead\":false,\"canWrite\":false,"
                + "\"operationDirection\":\"write\",\"valueTypeIsTypeArgument\":false},",
            "{\"key\":\"N.IThing.Level()\",\"declaringType\":\"N.IThing\",\"memberKind\":\"property\","
                + "\"memberName\":\"Level\",\"isStatic\":false,\"genericArity\":0,\"parameters\":[],"
                + "\"valueType\":\"System.Int32\",\"canRead\":false,\"canWrite\":true,"
                + "\"operationDirection\":\"write\",\"valueTypeIsTypeArgument\":false},",
            "{\"key\":\"N.IThing.Name()\",\"declaringType\":\"N.IThing\",\"memberKind\":\"property\","
                + "\"memberName\":\"Name\",\"isStatic\":false,\"genericArity\":0,\"parameters\":[],"
                + "\"valueType\":\"System.String\",\"canRead\":true,\"canWrite\":false,"
                + "\"operationDirection\":\"read\",\"valueTypeIsTypeArgument\":false},",
            "{\"key\":\"N.IThing.Swap<1>(ref T)\",\"declaringType\":\"N.IThing\",\"memberKind\":\"method\","
                + "\"memberName\":\"Swap\",\"isStatic\":false,\"genericArity\":1,\"parameters\":["
                + "{\"name\":\"value\",\"typeName\":\"T\",\"direction\":\"ref\",\"isOptional\":false,\"isTypeArgument\":true}"
                + "],\"valueType\":\"T\",\"canRead\":false,\"canWrite\":false,"
                + "\"operationDirection\":\"write\",\"valueTypeIsTypeArgument\":true},",
            "{\"key\":\"N.IThing.TryGet(System.Int32,out System.String)\",\"declaringType\":\"N.IThing\","
                + "\"memberKind\":\"method\",\"memberName\":\"TryGet\",\"isStatic\":false,"
                + "\"genericArity\":0,\"parameters\":["
                + "{\"name\":\"index\",\"typeName\":\"System.Int32\",\"direction\":\"in\",\"isOptional\":false,\"isTypeArgument\":false},"
                + "{\"name\":\"text\",\"typeName\":\"System.String\",\"direction\":\"out\",\"isOptional\":true,\"isTypeArgument\":false}"
                + "],\"valueType\":\"System.Boolean\",\"canRead\":false,\"canWrite\":false,"
                + "\"operationDirection\":\"write\",\"valueTypeIsTypeArgument\":false}",
            "]",
            "}",
            string.Empty,
        };

        private static TypeRecord Type(
            string name,
            TypeKind kind,
            bool isNested,
            bool isAbstract,
            bool isGenericTypeDefinition,
            IList<string> baseTypes,
            IList<string> enumMembers)
        {
            return new TypeRecord(name, kind, isNested, isAbstract, isGenericTypeDefinition, baseTypes, enumMembers);
        }

        private static InventoryRecord Sample()
        {
            List<string> none = new List<string>();

            List<TypeRecord> types = new List<TypeRecord>
            {
                Type("N.Holder", TypeKind.Class, false, false, true, none, none),
                Type("N.Holder+Inner", TypeKind.Struct, true, false, false, none, none),
                Type("N.IBase", TypeKind.Interface, false, true, false, none, none),
                Type("N.IThing", TypeKind.Interface, false, true, false, new List<string> { "N.IBase" }, none),
                Type("N.Kind", TypeKind.Enum, false, false, false, none, new List<string> { "Second", "First" }),
                Type("N.Proc", TypeKind.Delegate, false, false, false, none, none),
            };

            List<SignatureRecord> signatures = new List<SignatureRecord>
            {
                new SignatureRecord(
                    "N.Holder..ctor(System.Int32)",
                    "N.Holder",
                    MemberKind.Constructor,
                    SignatureKeyBuilder.ConstructorName,
                    false,
                    0,
                    new List<ParameterRecord>
                    {
                        new ParameterRecord("seed", "System.Int32", ParameterDirection.In, false),
                    },
                    "N.Holder",
                    false,
                    false,
                    OperationDirection.Write),
                new SignatureRecord(
                    "N.Holder.Count()",
                    "N.Holder",
                    MemberKind.Field,
                    "Count",
                    true,
                    0,
                    new List<ParameterRecord>(),
                    "System.Int32",
                    true,
                    true,
                    OperationDirection.Write),
                new SignatureRecord(
                    "N.IThing.Changed()",
                    "N.IThing",
                    MemberKind.Event,
                    "Changed",
                    false,
                    0,
                    new List<ParameterRecord>(),
                    "System.EventHandler",
                    false,
                    false,
                    OperationDirection.Write),
                new SignatureRecord(
                    "N.IThing.Level()",
                    "N.IThing",
                    MemberKind.Property,
                    "Level",
                    false,
                    0,
                    new List<ParameterRecord>(),
                    "System.Int32",
                    false,
                    true,
                    OperationDirection.Write),
                new SignatureRecord(
                    "N.IThing.Name()",
                    "N.IThing",
                    MemberKind.Property,
                    "Name",
                    false,
                    0,
                    new List<ParameterRecord>(),
                    "System.String",
                    true,
                    false,
                    OperationDirection.Read),
                new SignatureRecord(
                    "N.IThing.Swap<1>(ref T)",
                    "N.IThing",
                    MemberKind.Method,
                    "Swap",
                    false,
                    1,
                    new List<ParameterRecord>
                    {
                        new ParameterRecord("value", "T", ParameterDirection.Ref, false, true),
                    },
                    "T",
                    false,
                    false,
                    OperationDirection.Write,
                    true),
                new SignatureRecord(
                    "N.IThing.TryGet(System.Int32,out System.String)",
                    "N.IThing",
                    MemberKind.Method,
                    "TryGet",
                    false,
                    0,
                    new List<ParameterRecord>
                    {
                        new ParameterRecord("index", "System.Int32", ParameterDirection.In, false),
                        new ParameterRecord("text", "System.String", ParameterDirection.Out, true),
                    },
                    "System.Boolean",
                    false,
                    false,
                    OperationDirection.Write),
            };

            List<TypeRecord> referencedTypes = new List<TypeRecord>
            {
                new TypeRecord(
                    "System.EventHandler",
                    TypeKind.Delegate,
                    false,
                    false,
                    false,
                    new List<string>(),
                    new List<string>()),
            };

            return new InventoryRecord("Sample", "1.2.3.4", types, referencedTypes, signatures);
        }

        [Fact]
        public void 期待どおりのJSONになる()
        {
            Assert.Equal(string.Join("\n", ExpectedLines), InventoryJson.Write(Sample()));
        }

        [Fact]
        public void 書き出したJSONは解析できる()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> root =
                (Dictionary<string, object>)serializer.DeserializeObject(InventoryJson.Write(Sample()));

            Assert.Equal("Sample", root["assemblyName"]);
            Assert.Equal(6, ((object[])root["types"]).Length);
            Assert.Single((object[])root["referencedTypes"]);
            Assert.Equal(7, ((object[])root["signatures"]).Length);
        }

        [Fact]
        public void 要素のない配列は空の配列になる()
        {
            InventoryRecord empty = new InventoryRecord(
                "Sample", "1.0", new List<TypeRecord>(), new List<TypeRecord>(), new List<SignatureRecord>());

            Assert.Equal(
                string.Join(
                    "\n",
                    "{",
                    "\"assemblyName\":\"Sample\",",
                    "\"assemblyVersion\":\"1.0\",",
                    "\"types\":[],",
                    "\"referencedTypes\":[],",
                    "\"signatures\":[]",
                    "}",
                    string.Empty),
                InventoryJson.Write(empty));
        }

        [Fact]
        public void 特殊な文字は逃がされる()
        {
            InventoryRecord inventory = new InventoryRecord(
                "a\"b\\c\nd\te\u0001f",
                "1.0",
                new List<TypeRecord>(),
                new List<TypeRecord>(),
                new List<SignatureRecord>());

            Assert.Contains(
                "\"assemblyName\":\"a\\\"b\\\\c\\nd\\te\\u0001f\"", InventoryJson.Write(inventory));
        }

        [Fact]
        public void 列挙結果を渡さないと例外になる()
        {
            Assert.Throws<ArgumentNullException>(() => InventoryJson.Write(null));
        }
    }
}
