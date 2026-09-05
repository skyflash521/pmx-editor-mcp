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

        [Fact]
        public void AMethodNoteIsReadUnderItsMemberName()
        {
            IDictionary<string, string> notes = DocumentNoteReader.ReadMethods(Document(
                "<member name=\"M:N.IThing.Draw\"><summary>描く</summary></member>"
                + "<member name=\"P:N.IThing.Size\"><summary>大きさ</summary></member>"));

            Assert.Equal(new[] { "N.IThing.Draw" }, notes.Keys);
            Assert.Equal("描く", notes["N.IThing.Draw"]);
        }

        [Fact]
        public void TheAccessorSuffixIsKeptInAMethodNote()
        {
            IDictionary<string, string> notes = DocumentNoteReader.ReadMethods(Document(
                "<member name=\"M:N.IThing.Draw\"><summary>描く set</summary></member>"));

            Assert.Equal("描く set", notes["N.IThing.Draw"]);
        }

        [Fact]
        public void TheVerticalBarIsStillCutFromAMethodNote()
        {
            IDictionary<string, string> notes = DocumentNoteReader.ReadMethods(Document(
                "<member name=\"M:N.IThing.Draw\"><summary>描く | 補足</summary></member>"));

            Assert.Equal("描く", notes["N.IThing.Draw"]);
        }

        [Fact]
        public void AMethodWithoutArgumentsCarriesNoParentheses()
        {
            Assert.Equal("N.IThing.Draw", DocumentNoteReader.MemberName(Method("Draw")));
        }

        [Fact]
        public void TheArgumentTypesArePlacedInParentheses()
        {
            Assert.Equal(
                "N.IThing.Draw(System.String,System.Int32)",
                DocumentNoteReader.MemberName(Method(
                    "Draw", Parameter("System.String"), Parameter("System.Int32"))));
        }

        [Fact]
        public void APassedBackArgumentCarriesTheAtSign()
        {
            Assert.Equal(
                "N.IThing.Draw(N.V3@,N.V3@)",
                DocumentNoteReader.MemberName(Method(
                    "Draw",
                    Parameter("N.V3", ParameterDirection.Out),
                    Parameter("N.V3", ParameterDirection.Ref))));
        }

        [Fact]
        public void AClosedGenericArgumentIsWrittenWithBraces()
        {
            Assert.Equal(
                "N.IThing.Draw(System.Func{System.Int32,System.Double})",
                DocumentNoteReader.MemberName(Method(
                    "Draw", Parameter("System.Func<System.Int32,System.Double>"))));
        }

        [Fact]
        public void ANestedGenericArgumentKeepsItsInnerBraces()
        {
            Assert.Equal(
                "N.IThing.Draw(System.Func{System.Action{System.Int32}})",
                DocumentNoteReader.MemberName(Method(
                    "Draw", Parameter("System.Func<System.Action<System.Int32>>"))));
        }

        [Fact]
        public void AnArrayArgumentKeepsItsBrackets()
        {
            Assert.Equal(
                "N.IThing.Draw(System.Int32[][])",
                DocumentNoteReader.MemberName(Method("Draw", Parameter("System.Int32[][]"))));
        }

        [Fact]
        public void ANestedArgumentTypeIsSeparatedByADot()
        {
            Assert.Equal(
                "N.IThing.Draw(N.Helper.Para[])",
                DocumentNoteReader.MemberName(Method("Draw", Parameter("N.Helper+Para[]"))));
        }

        [Fact]
        public void AMethodTypeParameterBecomesItsPositionWithTwoBackticks()
        {
            SignatureRecord signature = new SignatureRecord(
                "N.IThing.ForEach<1>(System.Action<T>)",
                "N.IThing",
                MemberKind.Method,
                "ForEach",
                false,
                1,
                new[] { Parameter("System.Action<T>") },
                "System.Void",
                false,
                false,
                OperationDirection.Read,
                false,
                new[] { "T" });

            Assert.Equal(
                "N.IThing.ForEach``1(System.Action{``0})",
                DocumentNoteReader.MemberName(signature));
        }

        [Fact]
        public void ADeclaringTypeParameterBecomesItsPositionWithOneBacktick()
        {
            SignatureRecord signature = new SignatureRecord(
                "N.Proc<T,TState>.Invoke(TState)",
                "N.Proc<T,TState>",
                MemberKind.Method,
                "Invoke",
                false,
                0,
                new[] { Parameter("TState") },
                "System.Void",
                false,
                false,
                OperationDirection.Read);

            Assert.Equal("N.Proc`2.Invoke(`1)", DocumentNoteReader.MemberName(signature));
        }

        [Fact]
        public void MemberNameTakesMethodsAlone()
        {
            Assert.Throws<ArgumentNullException>(
                () => DocumentNoteReader.MemberName((SignatureRecord)null));
            Assert.Throws<ArgumentException>(() => DocumentNoteReader.MemberName(
                new SignatureRecord(
                    "N.IThing.Size()",
                    "N.IThing",
                    MemberKind.Property,
                    "Size",
                    false,
                    0,
                    new ParameterRecord[0],
                    "System.Int32",
                    true,
                    false,
                    OperationDirection.Read)));
        }

        private static SignatureRecord Method(string memberName, params ParameterRecord[] parameters)
        {
            return new SignatureRecord(
                SignatureKeyBuilder.Build("N.IThing", memberName, 0, parameters, "System.Void"),
                "N.IThing",
                MemberKind.Method,
                memberName,
                false,
                0,
                parameters,
                "System.Void",
                false,
                false,
                OperationDirection.Read);
        }

        private static ParameterRecord Parameter(
            string typeName, ParameterDirection direction = ParameterDirection.In)
        {
            return new ParameterRecord("value", typeName, direction, false, false);
        }

        private static string Document(string members)
        {
            return "<?xml version=\"1.0\"?><doc><assembly><name>PEPlugin</name></assembly>"
                + "<members>" + members + "</members></doc>";
        }
    }
}
