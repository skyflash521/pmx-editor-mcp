using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class JsonFormTests
    {
        private static readonly JsonForm Rows = JsonForm.Object(
            JsonForm.Member("rows", JsonForm.Array(
                JsonForm.Object(
                    JsonForm.Member("name", JsonForm.Text()),
                    JsonForm.Member("count", JsonForm.Count()),
                    JsonForm.Member("on", JsonForm.Flag()),
                    JsonForm.Member("note", JsonForm.OrNull(JsonForm.Text()))),
                "name")));

        private const string OneRow =
            "{\"rows\":[{\"name\":\"a\",\"count\":1,\"on\":true,\"note\":null}]}";

        [Fact]
        public void ADeclaredFormReadsEveryValueOfItsRow()
        {
            IDictionary<string, object> row = Row(OneRow);

            Assert.Equal("a", row["name"]);
            Assert.Equal(1, row["count"]);
            Assert.Equal(true, row["on"]);
            Assert.Null(row["note"]);
        }

        [Fact]
        public void AValueStandingWhereNullIsAllowedIsStillRead()
        {
            Assert.Equal(
                "書き置き",
                Row("{\"rows\":[{\"name\":\"a\",\"count\":1,\"on\":true,\"note\":\"書き置き\"}]}")[
                    "note"]);
        }

        [Theory]
        [InlineData("{\"rows\":[{\"count\":1,\"on\":true,\"note\":null}]}", "項目が無い")]
        [InlineData(
            "{\"rows\":[{\"name\":\"a\",\"count\":1,\"on\":true,\"note\":null,\"他\":1}]}",
            "知らない項目がある")]
        [InlineData("{\"rows\":[{\"name\":\" \",\"count\":1,\"on\":true,\"note\":null}]}", "空でない文字列")]
        [InlineData("{\"rows\":[{\"name\":\"a\",\"count\":0,\"on\":true,\"note\":null}]}", "1以上の整数")]
        [InlineData("{\"rows\":[{\"name\":\"a\",\"count\":1,\"on\":\"真\",\"note\":null}]}", "真偽")]
        [InlineData("{\"rows\":[]}", "1件以上")]
        [InlineData("{\"rows\":{}}", "項目の並び")]
        [InlineData("[]", "項目の組")]
        public void AValueThatDoesNotStandInTheDeclaredFormIsRefused(string json, string reason)
        {
            FormatException refused = Assert.Throws<FormatException>(() => Rows.Read(json));

            Assert.Contains(reason, refused.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(
            "{\"rows\":[{\"name\":\"a\",\"name\":\"b\",\"count\":1,\"on\":true,\"note\":null}]}",
            "rows.0.name")]
        [InlineData("{\"rows\":[],\"rows\":[]}", "rows")]
        public void AMemberWrittenTwiceInOneObjectIsRefusedWithItsPlace(string json, string place)
        {
            FormatException refused = Assert.Throws<FormatException>(() => Rows.Read(json));

            Assert.Contains("同じ項目が二度現れる: " + place, refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnOptionalMemberMayBeAbsentAndIsLeftOutOfWhatIsRead()
        {
            JsonForm form = JsonForm.Object(
                JsonForm.Member("name", JsonForm.Text()),
                JsonForm.Optional("note", JsonForm.Text()));

            IDictionary<string, object> absent =
                (IDictionary<string, object>)form.Read("{\"name\":\"a\"}");
            IDictionary<string, object> present =
                (IDictionary<string, object>)form.Read("{\"name\":\"a\",\"note\":\"b\"}");

            Assert.False(absent.ContainsKey("note"));
            Assert.Equal("b", present["note"]);
            Assert.Contains(
                "空でない文字列",
                Assert.Throws<FormatException>(() => form.Read("{\"name\":\"a\",\"note\":\"\"}")).Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void TheKeyOfAnArrayRefusesTheSameValueTwiceAndNamesTheLaterRow()
        {
            FormatException refused = Assert.Throws<FormatException>(() => Rows.Read(
                "{\"rows\":[{\"name\":\"a\",\"count\":1,\"on\":true,\"note\":null}"
                    + ",{\"name\":\"a\",\"count\":1,\"on\":true,\"note\":null}]}"));

            Assert.Contains("rows.1.name", refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheKeyOfAnArrayRefusesADescendingOrderAndNamesTheLaterRow()
        {
            FormatException refused = Assert.Throws<FormatException>(() => Rows.Read(
                "{\"rows\":[{\"name\":\"b\",\"count\":1,\"on\":true,\"note\":null}"
                    + ",{\"name\":\"a\",\"count\":1,\"on\":true,\"note\":null}]}"));

            Assert.Contains("rows.1.name", refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheRefusalNamesWhereTheValueStands()
        {
            FormatException refused = Assert.Throws<FormatException>(() => Rows.Read(
                "{\"rows\":[{\"name\":\"a\",\"count\":\"1\",\"on\":true,\"note\":null}]}"));

            Assert.Contains("rows.0.count", refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TextThatIsNotJsonIsRefused()
        {
            Assert.Throws<FormatException>(() => Rows.Read("rows"));
        }

        [Fact]
        public void ANullTextIsRefusedAsAMissingArgument()
        {
            Assert.Throws<ArgumentNullException>(() => Rows.Read(null));
        }

        private static IDictionary<string, object> Row(string json)
        {
            IDictionary<string, object> root = (IDictionary<string, object>)Rows.Read(json);

            return (IDictionary<string, object>)((object[])root["rows"])[0];
        }
    }
}
