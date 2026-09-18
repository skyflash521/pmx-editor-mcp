using System;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>
    /// 共通契約の正本の読み取り。項目を名指しする表は2つあり、取り違えても名前の実在を見る門は
    /// 通ってしまうので、どちらがどちらへ入るかをここで固定する。
    /// </summary>
    public sealed class CommonContractJsonReaderTests
    {
        [Fact]
        public void TheTwoTablesOfNamedMembersDoNotMix()
        {
            CommonContractTable contract = CommonContractJsonReader.Read(
                new CommonContractJsonBuilder()
                    .AddUnkeptMember("model_update_faces", "vertex1", "書いても持ち続けない。")
                    .AddTargetedMember("model_update_bones", "parent", "加える前に埋める。")
                    .ToString());

            Assert.Equal(
                new[] { "vertex1" },
                contract.UnkeptMembers["model_update_faces"]
                    .OrderBy(m => m, StringComparer.Ordinal)
                    .ToArray());
            Assert.Equal(
                new[] { "parent" },
                contract.TargetedMembers["model_update_bones"]
                    .OrderBy(m => m, StringComparer.Ordinal)
                    .ToArray());
            Assert.False(contract.UnkeptMembers.ContainsKey("model_update_bones"));
            Assert.False(contract.TargetedMembers.ContainsKey("model_update_faces"));
        }

        [Fact]
        public void NeitherTableHasToNameAnything()
        {
            CommonContractTable contract = CommonContractJsonReader.Read(
                new CommonContractJsonBuilder().ToString());

            Assert.Empty(contract.UnkeptMembers);
            Assert.Empty(contract.TargetedMembers);
        }
    }
}
