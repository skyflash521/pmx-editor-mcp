using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public class DangerousOperationRuleTests
    {
        private const string FormConnector = "PEPlugin.Form.IPEFormConnector";

        private const string Pmx = "PEPlugin.Pmx.IPXPmx";

        [Theory]
        [InlineData(FormConnector, "Close", DangerKind.Shutdown)]
        [InlineData(FormConnector, "InitializePMD", DangerKind.Reset)]
        [InlineData(FormConnector, "InitializePMX", DangerKind.Reset)]
        [InlineData(FormConnector, "SavePMXFile", DangerKind.Overwrite)]
        [InlineData("PEPlugin.View.IPEViewSettingConnector", "SaveViewSetting", DangerKind.Overwrite)]
        [InlineData(Pmx, "ToFile", DangerKind.Overwrite)]
        [InlineData(Pmx, "Clear", DangerKind.Reset)]
        public void TheKindFollowsTheDeclaringTypeAndTheMemberName(
            string declaringType, string memberName, DangerKind expected)
        {
            DangerKind kind;

            Assert.True(DangerousOperationRule.TryClassify(Signature(declaringType, memberName), out kind));
            Assert.Equal(expected, kind);
        }

        [Theory]
        [InlineData(Pmx, "Add")]
        [InlineData("PEPlugin.Pmx.IPXMaterialMorphOffset", "Clear")]
        [InlineData("PEPlugin.Vme.IPEVme", "Clear")]
        [InlineData("PEPlugin.Pmx.IPXBone", "Close")]
        [InlineData("PEPlugin.Pmx.IPXBone", "InitializePMX")]
        public void TheOthersAreNotDangerous(string declaringType, string memberName)
        {
            DangerKind kind;

            Assert.False(DangerousOperationRule.TryClassify(Signature(declaringType, memberName), out kind));
        }

        [Fact]
        public void SavingIsJudgedByTheNameAloneWhateverDeclaresIt()
        {
            DangerKind kind;

            Assert.True(
                DangerousOperationRule.TryClassify(Signature("PEPlugin.Pmx.IPXBone", "SaveAnything"), out kind));
            Assert.Equal(DangerKind.Overwrite, kind);
        }

        [Fact]
        public void OnlyTheDangerousOnesAreCollected()
        {
            IDictionary<string, DangerKind> found = DangerousOperationRule.Classify(new[]
            {
                Signature(FormConnector, "Close"),
                Signature(Pmx, "Add"),
                Signature(Pmx, "ToFile"),
            });

            Assert.Equal(2, found.Count);
            Assert.Equal(DangerKind.Shutdown, found[Key(FormConnector, "Close")]);
            Assert.Equal(DangerKind.Overwrite, found[Key(Pmx, "ToFile")]);
        }

        [Fact]
        public void TheInputsAreRequired()
        {
            DangerKind kind;

            Assert.Throws<ArgumentNullException>(
                () => DangerousOperationRule.TryClassify(null, out kind));
            Assert.Throws<ArgumentNullException>(() => DangerousOperationRule.Classify(null));
        }

        private static string Key(string declaringType, string memberName)
        {
            return SignatureKeyBuilder.Build(
                declaringType, memberName, 0, new List<ParameterRecord>(), "System.Void");
        }

        private static SignatureRecord Signature(string declaringType, string memberName)
        {
            List<ParameterRecord> parameters = new List<ParameterRecord>();

            return new SignatureRecord(
                Key(declaringType, memberName),
                declaringType,
                MemberKind.Method,
                memberName,
                false,
                0,
                parameters,
                "System.Void",
                false,
                false,
                OperationDirection.Write);
        }
    }
}
