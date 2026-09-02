using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class DocumentNoteReaderTests
    {
        [Fact]
        public void APropertyNoteIsReadUnderItsMemberName()
        {
            IDictionary<string, string> notes = DocumentNoteReader.Read(
                Document("<member name=\"P:N.IThing.Size\"><summary>大きさ</summary></member>"));

            Assert.Equal("大きさ", notes["N.IThing.Size"]);
        }

        [Fact]
        public void TheAccessorSuffixIsDropped()
        {
            IDictionary<string, string> notes = DocumentNoteReader.Read(Document(
                "<member name=\"P:N.IThing.A\"><summary>大きさ get/set</summary></member>"
                + "<member name=\"P:N.IThing.B\"><summary>位置 get</summary></member>"
                + "<member name=\"P:N.IThing.C\"><summary>色 set</summary></member>"
                + "<member name=\"P:N.IThing.D\"><summary>数  get/set</summary></member>"));

            Assert.Equal("大きさ", notes["N.IThing.A"]);
            Assert.Equal("位置", notes["N.IThing.B"]);
            Assert.Equal("色", notes["N.IThing.C"]);
            Assert.Equal("数", notes["N.IThing.D"]);
        }

        [Fact]
        public void OnlyAWholeTrailingTokenCountsAsTheAccessorSuffix()
        {
            IDictionary<string, string> notes = DocumentNoteReader.Read(Document(
                "<member name=\"P:N.IThing.A\"><summary>offset</summary></member>"
                + "<member name=\"P:N.IThing.B\"><summary>大きさ get/setget</summary></member>"));

            Assert.Equal("offset", notes["N.IThing.A"]);
            Assert.Equal("大きさ get/setget", notes["N.IThing.B"]);
        }

        [Fact]
        public void OnlyTheFirstNonEmptyLineIsTaken()
        {
            IDictionary<string, string> notes = DocumentNoteReader.Read(Document(
                "<member name=\"P:N.IThing.Size\"><summary>\n"
                + "            Boxサイズ get/set\n"
                + "            X -> サイズ1\n"
                + "            </summary></member>"));

            Assert.Equal("Boxサイズ", notes["N.IThing.Size"]);
        }

        [Fact]
        public void WhatFollowsAVerticalBarIsDropped()
        {
            IDictionary<string, string> notes = DocumentNoteReader.Read(Document(
                "<member name=\"P:N.IThing.A\"><summary>描画モード | 0:通常 1:Wire</summary></member>"
                + "<member name=\"P:N.IThing.B\"><summary>風圧計算モデル get/set | 0:V_Point</summary>"
                + "</member>"
                + "<member name=\"P:N.IThing.C\"><summary>大きさ | 補足 | さらに補足</summary></member>"));

            Assert.Equal("描画モード", notes["N.IThing.A"]);
            Assert.Equal("風圧計算モデル", notes["N.IThing.B"]);
            Assert.Equal("大きさ", notes["N.IThing.C"]);
        }

        [Fact]
        public void MembersThatAreNotPropertiesAreSkipped()
        {
            IDictionary<string, string> notes = DocumentNoteReader.Read(Document(
                "<member name=\"T:N.IThing\"><summary>もの</summary></member>"
                + "<member name=\"M:N.IThing.Do\"><summary>する</summary></member>"));

            Assert.Empty(notes);
        }

        [Fact]
        public void AMemberWithoutAUsableNoteIsSkipped()
        {
            IDictionary<string, string> notes = DocumentNoteReader.Read(Document(
                "<member name=\"P:N.IThing.A\"><remarks>大きさ</remarks></member>"
                + "<member name=\"P:N.IThing.B\"><summary>  </summary></member>"
                + "<member name=\"P:N.IThing.C\"><summary>get/set</summary></member>"
                + "<member name=\"P:N.IThing.D\"><summary>| 補足だけ</summary></member>"));

            Assert.Empty(notes);
        }

        [Fact]
        public void TheSameMemberWrittenTwiceStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => DocumentNoteReader.Read(Document(
                    "<member name=\"P:N.IThing.A\"><summary>大きさ</summary></member>"
                    + "<member name=\"P:N.IThing.A\"><summary>位置</summary></member>")));

            Assert.Contains("N.IThing.A", error.Message);
        }

        [Fact]
        public void AMemberWithTwoSummariesStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => DocumentNoteReader.Read(Document(
                    "<member name=\"P:N.IThing.A\"><summary>大きさ</summary>"
                    + "<summary>位置</summary></member>")));

            Assert.Contains("N.IThing.A", error.Message);
        }

        [Fact]
        public void ASummaryWithAChildElementStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => DocumentNoteReader.Read(Document(
                    "<member name=\"P:N.IThing.A\"><summary>大きさ<see cref=\"T:N.IThing\"/>"
                    + "</summary></member>")));

            Assert.Contains("N.IThing.A", error.Message);
        }

        [Fact]
        public void AMemberWithoutANameStops()
        {
            Assert.Throws<FormatException>(
                () => DocumentNoteReader.Read(Document("<member><summary>大きさ</summary></member>")));
            Assert.Throws<FormatException>(
                () => DocumentNoteReader.Read(
                    Document("<member name=\"\"><summary>大きさ</summary></member>")));
            Assert.Throws<FormatException>(
                () => DocumentNoteReader.Read(
                    Document("<member name=\"P:\"><summary>大きさ</summary></member>")));
        }

        [Fact]
        public void TextThatIsNotXmlStops()
        {
            Assert.Throws<FormatException>(() => DocumentNoteReader.Read("大きさ"));
        }

        [Fact]
        public void NullArgumentThrows()
        {
            Assert.Throws<ArgumentNullException>(() => DocumentNoteReader.Read(null));
        }

        [Fact]
        public void MemberNameWritesNestedTypesWithDots()
        {
            Assert.Equal(
                "N.Outer.Inner.Size",
                DocumentNoteReader.MemberName("N.Outer+Inner", "Size"));
        }

        [Fact]
        public void MemberNameWritesGenericArgumentsAsTheirCount()
        {
            Assert.Equal(
                "N.Box`1.Value",
                DocumentNoteReader.MemberName("N.Box<System.Int32>", "Value"));
            Assert.Equal(
                "N.Pair`2.Value",
                DocumentNoteReader.MemberName("N.Pair<System.Int32,System.String>", "Value"));
        }

        [Fact]
        public void MemberNameCountsNestedGenericArgumentsAsOne()
        {
            Assert.Equal(
                "N.Box`1.Value",
                DocumentNoteReader.MemberName("N.Box<N.Pair<System.Int32,System.String>>", "Value"));
        }

        [Fact]
        public void MemberNameCountsTheArgumentsOfEachLevelOnItsOwn()
        {
            Assert.Equal(
                "N.Outer`1.Inner`2.Value",
                DocumentNoteReader.MemberName("N.Outer<T>+Inner<U,V>", "Value"));
            Assert.Equal(
                "N.Outer`1.Inner.Value",
                DocumentNoteReader.MemberName("N.Outer<T>+Inner", "Value"));
        }

        [Fact]
        public void MemberNameDoesNotCountCommasInsideAnArrayRank()
        {
            Assert.Equal(
                "N.Box`1.Value",
                DocumentNoteReader.MemberName("N.Box<System.Int32[,]>", "Value"));
        }

        [Fact]
        public void MemberNameSplitsLevelsOutsideGenericArguments()
        {
            Assert.Equal(
                "N.Box`1.Value",
                DocumentNoteReader.MemberName("N.Box<N.Outer+Inner>", "Value"));
        }

        [Fact]
        public void MemberNameStopsOnAnUnclosedAngleBracket()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => DocumentNoteReader.MemberName("N.Box<System.Int32", "Value"));

            Assert.Contains("N.Box<System.Int32", error.Message);
        }

        [Fact]
        public void MemberNameRequiresBothParts()
        {
            Assert.Throws<ArgumentNullException>(() => DocumentNoteReader.MemberName(null, "Value"));
            Assert.Throws<ArgumentException>(() => DocumentNoteReader.MemberName("N.Box", " "));
        }

        private static string Document(string members)
        {
            return "<?xml version=\"1.0\"?><doc><assembly><name>PEPlugin</name></assembly>"
                + "<members>" + members + "</members></doc>";
        }
    }
}
