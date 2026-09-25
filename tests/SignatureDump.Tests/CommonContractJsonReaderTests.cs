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

        /// <summary>加える前に親へ揃える値を読む。</summary>
        [Fact]
        public void TheValueToAlignTheParentWithIsRead()
        {
            CommonContractTable contract = CommonContractJsonReader.Read(
                new CommonContractJsonBuilder()
                    .AddParentValue(
                        "model_add_morph_offsets", "model_update_morphs", "kind", "Bone",
                        "種別の合う親へしか加えられない。")
                    .ToString());
            ParentValues values = contract.ParentValues["model_add_morph_offsets"];

            Assert.Equal("model_update_morphs", values.ParentTool);
            Assert.Equal("kind", values.Member);
            Assert.Equal("Bone", values.Value);
        }

        [Fact]
        public void TheToolsThatDrawTheirOwnImageAreReadApartFromTheViewImages()
        {
            CommonContractTable contract = CommonContractJsonReader.Read(
                new CommonContractJsonBuilder()
                    .AddViewImage("view_capture_image", "pmx")
                    .AddDrawnImage("model_draw_uv_layout")
                    .ToString());

            Assert.Equal(new[] { "model_draw_uv_layout" }, contract.DrawnImages.ToArray());
            Assert.False(contract.ViewImages.ContainsKey("model_draw_uv_layout"));
        }

        [Fact]
        public void TheComposedToolsThatWriteFilesAreRead()
        {
            CommonContractTable contract = CommonContractJsonReader.Read(
                new CommonContractJsonBuilder()
                    .AddComposedTool("motion_save_transformed_pmx_file", false, "変形した形を保存する。")
                    .AddOverwritingTool("motion_save_transformed_pmx_file")
                    .ToString());

            Assert.Equal(new[] { "motion_save_transformed_pmx_file" }, contract.OverwritingTools.ToArray());
        }

        [Fact]
        public void AWritingToolThatIsNotComposedIsRefused()
        {
            Assert.Throws<FormatException>(() => CommonContractJsonReader.Read(
                new CommonContractJsonBuilder()
                    .AddOverwritingTool("model_to_file_pmx")
                    .ToString()));
        }

        [Fact]
        public void NeitherTableHasToNameAnything()
        {
            CommonContractTable contract = CommonContractJsonReader.Read(
                new CommonContractJsonBuilder().ToString());

            Assert.Empty(contract.UnkeptMembers);
            Assert.Empty(contract.TargetedMembers);
            Assert.Empty(contract.OverwritingTools);
        }
    }
}
