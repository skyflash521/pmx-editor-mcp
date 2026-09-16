using System;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>覆えないツールの正本の読み取り。形が崩れた正本を黙って通さないことを固定する。</summary>
    public sealed class UncoveredToolJsonReaderTests
    {
        private const string Json = @"{ ""tools"": [
  { ""tool"": ""model_clone_ik"", ""reason"": ""noCase"" },
  { ""tool"": ""model_ik_link"", ""reason"": ""noEffectCheck"" }
] }";

        [Fact]
        public void ReadsEveryToolWithItsReason()
        {
            UncoveredToolTable table = UncoveredToolJsonReader.Read(Json);

            Assert.Equal(
                new[] { "model_clone_ik", "model_ik_link" },
                table.Tools.Select(t => t.Tool).ToArray());
            Assert.Equal(
                new[] { UncoveredReason.NoCase, UncoveredReason.NoEffectCheck },
                table.Tools.Select(t => t.Reason).ToArray());
        }

        [Fact]
        public void AnEmptyListIsRead()
        {
            Assert.Empty(UncoveredToolJsonReader.Read(@"{ ""tools"": [] }").Tools);
        }

        [Fact]
        public void AReasonOutsideTheKnownOnesStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => UncoveredToolJsonReader.Read(
                    @"{ ""tools"": [ { ""tool"": ""model_clone_ik"", ""reason"": ""どれでもない"" } ] }"));

            Assert.Contains("reason", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AMissingItemStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => UncoveredToolJsonReader.Read(
                    @"{ ""tools"": [ { ""tool"": ""model_clone_ik"" } ] }"));

            Assert.Contains("reason", error.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// 知らない項目を黙って捨てると、正本の形が崩れても気づけない。言葉で述べた事情を足す形へ
        /// 戻す変更も、ここで止まる。
        /// </summary>
        [Fact]
        public void AnUnknownItemStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => UncoveredToolJsonReader.Read(
                    @"{ ""tools"": [ { ""tool"": ""model_clone_ik"", ""reason"": ""noCase"","
                        + @" ""basis"": ""事情。"" } ] }"));

            Assert.Contains("知らない項目がある", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnEmptyToolNameStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => UncoveredToolJsonReader.Read(
                    @"{ ""tools"": [ { ""tool"": """", ""reason"": ""noCase"" } ] }"));

            Assert.Contains("tool", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AListThatIsNotAnArrayStops()
        {
            Assert.Throws<FormatException>(
                () => UncoveredToolJsonReader.Read(@"{ ""tools"": 1 }"));
        }

        [Fact]
        public void TextThatIsNotJsonStops()
        {
            Assert.Throws<FormatException>(() => UncoveredToolJsonReader.Read("{"));
        }

        [Fact]
        public void TheTextIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => UncoveredToolJsonReader.Read(null));
        }
    }
}
