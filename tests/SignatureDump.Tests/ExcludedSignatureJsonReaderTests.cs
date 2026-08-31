using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ExcludedSignatureJsonReaderTests
    {
        private const string Pmd = "PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmd.IPEPmd)";

        private const string Alternative = "PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmx.IPXPmx)";

        private const string Frozen = "PEPlugin.IPEBuilder.CreateBone()";

        private const string Delegated = "PEPlugin.Vme.IPEVmeFrameEvent.RemoveEvent(PEPlugin.Vme.PEVmeEvent)";

        [Fact]
        public void WrittenJsonIsReadBackAsTheSameContent()
        {
            IList<ExcludedSignatureRecord> written = Records();

            IList<ExcludedSignatureRecord> read =
                ExcludedSignatureJsonReader.Read(ExcludedSignatureJson.Write(written));

            Assert.Equal(written.Count, read.Count);
            for (int i = 0; i < written.Count; i++)
            {
                Assert.Equal(written[i].Key, read[i].Key);
                Assert.Equal(written[i].Qualification, read[i].Qualification);
                Assert.Equal(written[i].CapabilityId, read[i].CapabilityId);
                Assert.Equal(written[i].Category, read[i].Category);
                Assert.Equal(written[i].Alternative, read[i].Alternative);
            }
        }

        [Fact]
        public void AllFourCategorySpellingsAreRead()
        {
            string json = "{\"signatures\":["
                + "{\"key\":\"A\",\"qualification\":\"category\",\"category\":\"cPluginArgument\"},"
                + "{\"key\":\"B\",\"qualification\":\"category\",\"category\":\"constructorDuplicate\""
                + ",\"alternative\":\"X\"},"
                + "{\"key\":\"C\",\"qualification\":\"category\",\"category\":\"delegate\"},"
                + "{\"key\":\"D\",\"qualification\":\"category\",\"category\":\"pmd\",\"alternative\":\"Y\"}"
                + "]}";

            IList<ExcludedSignatureRecord> read = ExcludedSignatureJsonReader.Read(json);

            Assert.Equal(
                new[]
                {
                    ExclusionCategory.CPluginArgument,
                    ExclusionCategory.ConstructorDuplicate,
                    ExclusionCategory.Delegate,
                    ExclusionCategory.Pmd,
                },
                read.Select(r => r.Category).ToArray());
        }

        [Fact]
        public void ReadsAnEmptyCollection()
        {
            Assert.Empty(ExcludedSignatureJsonReader.Read("{\"signatures\":[]}"));
        }

        [Fact]
        public void BodyThatIsNotJsonThrows()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read("{"));
        }

        [Fact]
        public void NullArgumentThrows()
        {
            Assert.Throws<ArgumentNullException>(() => ExcludedSignatureJsonReader.Read(null));
        }

        [Fact]
        public void MissingTopLevelMemberThrows()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read("{}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read("{\"items\":[]}"));
        }

        [Fact]
        public void UnknownTopLevelMemberThrows()
        {
            Assert.Throws<FormatException>(
                () => ExcludedSignatureJsonReader.Read("{\"signatures\":[],\"items\":[]}"));
        }

        [Fact]
        public void NonArrayValueThrows()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read("{\"signatures\":{}}"));
        }

        [Fact]
        public void ArrayItemThatIsNotAnObjectThrows()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read("{\"signatures\":[null]}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read("{\"signatures\":[\"A\"]}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read("{\"signatures\":[[]]}"));
        }

        [Fact]
        public void MissingOrUnknownQualificationThrows()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"capabilityId\":\"CAP-1\"}]}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"other\",\"capabilityId\":\"CAP-1\"}]}"));
        }

        [Fact]
        public void MemberRequiredByQualificationMissingThrows()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"baseline\"}]}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"category\"}]}"));
        }

        [Fact]
        public void MemberNotAllowedByQualificationThrows()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"baseline\",\"capabilityId\":\"CAP-1\""
                    + ",\"category\":\"pmd\"}]}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"category\",\"category\":\"delegate\""
                    + ",\"capabilityId\":\"CAP-1\"}]}"));
        }

        [Fact]
        public void UnknownCategoryThrows()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"category\",\"category\":\"stream\"}]}"));
        }

        [Fact]
        public void AlternativePresenceNotMatchingCategoryThrows()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"category\",\"category\":\"pmd\"}]}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"category\",\"category\":\"delegate\""
                    + ",\"alternative\":\"X\"}]}"));
        }

        [Fact]
        public void WrongValueTypeOrEmptyValueThrows()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":1,\"qualification\":\"baseline\",\"capabilityId\":\"CAP-1\"}]}"));
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"baseline\",\"capabilityId\":\"\"}]}"));
        }

        [Fact]
        public void OrderThatIsNotAscendingThrows()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"B\",\"qualification\":\"baseline\",\"capabilityId\":\"CAP-1\"}"
                    + ",{\"key\":\"A\",\"qualification\":\"baseline\",\"capabilityId\":\"CAP-1\"}]}"));
        }

        [Fact]
        public void DuplicateKeyThrows()
        {
            Assert.Throws<FormatException>(() => ExcludedSignatureJsonReader.Read(
                "{\"signatures\":[{\"key\":\"A\",\"qualification\":\"baseline\",\"capabilityId\":\"CAP-1\"}"
                    + ",{\"key\":\"A\",\"qualification\":\"baseline\",\"capabilityId\":\"CAP-2\"}]}"));
        }

        private static IList<ExcludedSignatureRecord> Records()
        {
            return new List<ExcludedSignatureRecord>
            {
                ExcludedSignatureRecord.FromBaseline(Frozen, "CAP-463"),
                ExcludedSignatureRecord.FromCategory(
                    Delegated, ExclusionCategory.Delegate, string.Empty),
                ExcludedSignatureRecord.FromCategory(Pmd, ExclusionCategory.Pmd, Alternative),
            }
                .OrderBy(r => r.Key, StringComparer.Ordinal)
                .ToList();
        }
    }
}
