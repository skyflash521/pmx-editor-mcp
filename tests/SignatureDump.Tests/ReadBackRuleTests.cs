using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ReadBackRuleTests
    {
        private const string SetKey = "Sdk.View.SetSelectedBoneIndices(System.Int32[])";

        private const string GetKey = "Sdk.View.GetSelectedBoneIndices()";

        [Fact]
        public void ASetterOfAnArrayReadsBackThroughTheGetterOfTheSameName()
        {
            Assert.Equal(GetKey, ReadBackRule.Of(Setter(), Table(Getter("Sdk.View", "System.Int32[]"))));
        }

        [Fact]
        public void AGetterOnAnotherTypeOrOfAnotherShapeIsNotTaken()
        {
            Assert.Null(ReadBackRule.Of(Setter(), Table(Getter("Sdk.Other", "System.Int32[]"))));
            Assert.Null(ReadBackRule.Of(Setter(), Table(Getter("Sdk.View", "System.Int32"))));
            Assert.Null(ReadBackRule.Of(Setter(), Table()));
        }

        [Fact]
        public void AnArrayOtherThanPositionsIsNotReadBack()
        {
            SignatureRecord set = new SignatureRecord(
                "Sdk.View.SetBodyVisibles(System.Boolean[])",
                "Sdk.View",
                MemberKind.Method,
                "SetBodyVisibles",
                false,
                0,
                new[] { new ParameterRecord("visibles", "System.Boolean[]", ParameterDirection.In, false) },
                "System.Void",
                true,
                false,
                OperationDirection.Write);
            SignatureRecord get = new SignatureRecord(
                "Sdk.View.GetBodyVisibles()",
                "Sdk.View",
                MemberKind.Method,
                "GetBodyVisibles",
                false,
                0,
                new ParameterRecord[0],
                "System.Boolean[]",
                true,
                false,
                OperationDirection.Read);

            Assert.Null(ReadBackRule.Of(
                set,
                new Dictionary<string, SignatureRecord> { { set.Key, set }, { get.Key, get } }));
        }

        private static IDictionary<string, SignatureRecord> Table(params SignatureRecord[] records)
        {
            Dictionary<string, SignatureRecord> table = new Dictionary<string, SignatureRecord>();
            SignatureRecord set = Setter();
            table.Add(set.Key, set);
            foreach (SignatureRecord record in records)
            {
                table.Add(record.Key, record);
            }

            return table;
        }

        private static SignatureRecord Setter()
        {
            return new SignatureRecord(
                SetKey,
                "Sdk.View",
                MemberKind.Method,
                "SetSelectedBoneIndices",
                false,
                0,
                new[] { new ParameterRecord("indices", "System.Int32[]", ParameterDirection.In, false) },
                "System.Void",
                true,
                false,
                OperationDirection.Write);
        }

        private static SignatureRecord Getter(string declaring, string valueType)
        {
            return new SignatureRecord(
                declaring == "Sdk.View" ? GetKey : declaring + ".GetSelectedBoneIndices()",
                declaring,
                MemberKind.Method,
                "GetSelectedBoneIndices",
                false,
                0,
                new ParameterRecord[0],
                valueType,
                true,
                false,
                OperationDirection.Read);
        }
    }
}
