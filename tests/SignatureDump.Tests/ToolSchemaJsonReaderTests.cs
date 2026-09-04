using System;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolSchemaJsonReaderTests
    {
        private const string Output = @"{ ""origin"": ""hostOutput"", ""shape"": ""number"" }";

        /// <summary>イベントの並びを返す応答。形の無い `payload` はこの中に置かれる。</summary>
        private const string PollOutput = @"{ ""origin"": ""hostOutput"", ""members"": [
            { ""name"": ""events"", ""origin"": ""hostOutput"", ""maxItems"": 1000,
              ""element"": { ""origin"": ""hostOutput"", ""members"": [
                { ""name"": ""payload"", ""origin"": ""hostOutput"" }] } }] }";

        private const string Payloads =
            @", ""payloads"": [{ ""type"": ""view.click"", ""members"": [] }]";

        /// <summary>呼び分けと応答の形だけを差し替えられるツール1件の表。</summary>
        private static string Table(string branches, string output = Output, string extra = "")
        {
            return @"{ ""tools"": [{ ""tool"": ""model_list_vertices"", ""branches"": " + branches
                + @", ""output"": " + output + extra + "}] }";
        }

        private static string Branch(string inputs, string extra = "")
        {
            return @"[{ ""branch"": ""byIndex"", ""inputs"": " + inputs + extra + "}]";
        }

        private static ToolSchema Single(string table)
        {
            return Assert.Single(ToolSchemaJsonReader.Read(table).Tools);
        }

        private static SchemaBranch OnlyBranch(string table)
        {
            return Assert.Single(Single(table).Branches);
        }

        private static SchemaItem OnlyInput(string table)
        {
            return Assert.Single(OnlyBranch(table).Inputs);
        }

        /// <summary>
        /// 落ちることに加えて、どの規則が落としたかまで固定する。別の規則が先に落ちても通って
        /// しまうと、狙った検査が働いているかを見分けられない。
        /// </summary>
        private static void Rejects(string fragment, string table)
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ToolSchemaJsonReader.Read(table));

            Assert.Contains(fragment, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ReadsAToolWithOneBranch()
        {
            ToolSchema schema = Single(Table(Branch(
                @"[{ ""name"": ""index"", ""origin"": ""hostInput"", ""shape"": ""number"",
                     ""required"": true }]")));

            Assert.Equal("model_list_vertices", schema.Tool);
            SchemaBranch branch = Assert.Single(schema.Branches);
            Assert.Equal("byIndex", branch.Branch);
            Assert.Null(branch.SelectorName);
            Assert.Empty(branch.Choices);
            SchemaItem input = Assert.Single(branch.Inputs);
            Assert.Equal("index", input.Name);
            Assert.Equal(ItemOrigin.HostInput, input.Origin);
            Assert.Equal("number", input.Shape);
            Assert.True(input.Required);
            Assert.False(input.Injected);
            Assert.Null(schema.Listing);
            Assert.Null(schema.Payloads);
            Assert.Null(schema.Output.Name);
        }

        [Fact]
        public void ReadsAValueThatSelectsABranch()
        {
            SchemaBranch branch = OnlyBranch(Table(Branch(
                @"[{ ""name"": ""kind"", ""origin"": ""hostInput"", ""shape"": ""text"",
                     ""required"": true }]",
                @", ""selector"": { ""name"": ""kind"", ""value"": ""vertex"" }")));

            Assert.Equal("kind", branch.SelectorName);
            Assert.Equal("vertex", branch.SelectorValue);
        }

        [Fact]
        public void RejectsASelectorThatNamesAnItemTheBranchDoesNotTake()
        {
            Rejects("分岐を選ぶ項目が入力に無い", Table(Branch(
                @"[{ ""name"": ""kind"", ""origin"": ""hostInput"", ""shape"": ""text"",
                     ""required"": true }]",
                @", ""selector"": { ""name"": ""absent"", ""value"": ""vertex"" }")));
        }

        [Fact]
        public void RejectsASelectorValueThatIsNotALiteral()
        {
            Rejects("値の項目の名前でない", Table(Branch(
                @"[{ ""name"": ""kind"", ""origin"": ""hostInput"", ""shape"": ""json"",
                     ""required"": true }]",
                @", ""selector"": { ""name"": ""kind"", ""value"": { ""X"": 1 } }")));
        }

        [Fact]
        public void ReadsAGroupOfItemsThatCannotBeHeldTogether()
        {
            SchemaBranch branch = OnlyBranch(Table(Branch(
                @"[{ ""name"": ""indices"", ""origin"": ""hostInput"", ""shape"": ""number"" },
                   { ""name"": ""handles"", ""origin"": ""hostInput"", ""shape"": ""number"" }]",
                @", ""choices"": [{ ""names"": [""indices"", ""handles""], ""required"": true }]")));

            SchemaChoice choice = Assert.Single(branch.Choices);
            Assert.Equal(new[] { "indices", "handles" }, choice.Names);
            Assert.True(choice.Required);
            Assert.All(branch.Inputs, i => Assert.Null(i.Required));
        }

        [Fact]
        public void RejectsAGroupThatNamesAnItemTheBranchDoesNotTake()
        {
            Rejects("まとまりが入力に無い項目を並べている", Table(Branch(
                @"[{ ""name"": ""indices"", ""origin"": ""hostInput"", ""shape"": ""number"" },
                   { ""name"": ""handles"", ""origin"": ""hostInput"", ""shape"": ""number"" }]",
                @", ""choices"": [{ ""names"": [""indices"", ""handles"", ""absent""],
                                    ""required"": true }]")));
        }

        [Fact]
        public void RejectsAnItemThatTwoGroupsShare()
        {
            Rejects("同じ項目が二つのまとまりに現れる", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""shape"": ""number"" },
                   { ""name"": ""b"", ""origin"": ""hostInput"", ""shape"": ""number"" },
                   { ""name"": ""c"", ""origin"": ""hostInput"", ""shape"": ""number"" }]",
                @", ""choices"": [{ ""names"": [""a"", ""b""], ""required"": true },
                                  { ""names"": [""b"", ""c""], ""required"": false }]")));
        }

        [Fact]
        public void RejectsAGroupOfFewerThanTwoItems()
        {
            Rejects("2件以上でなければならない", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""shape"": ""number"" }]",
                @", ""choices"": [{ ""names"": [""a""], ""required"": true }]")));
        }

        [Fact]
        public void RejectsARequiredFlagOnAnItemInAGroup()
        {
            Rejects("呼び出す側が渡す項目だけが", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""shape"": ""number"",
                     ""required"": true },
                   { ""name"": ""b"", ""origin"": ""hostInput"", ""shape"": ""number"" }]",
                @", ""choices"": [{ ""names"": [""a"", ""b""], ""required"": true }]")));
        }

        [Fact]
        public void RejectsAnInputOutsideAGroupThatOmitsTheRequiredFlag()
        {
            Rejects("呼び出す側が渡す項目だけが", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""shape"": ""number"" }]")));
        }

        [Fact]
        public void RejectsANestedInputThatSharesANameWithAChosenItem()
        {
            Rejects("呼び出す側が渡す項目だけが", Table(Branch(
                @"[{ ""name"": ""indices"", ""origin"": ""hostInput"", ""shape"": ""number"" },
                   { ""name"": ""targets"", ""origin"": ""hostInput"", ""maxItems"": 2,
                     ""element"": { ""origin"": ""hostInput"",
                       ""members"": [{ ""name"": ""indices"", ""origin"": ""hostInput"",
                         ""shape"": ""number"" }] } }]",
                @", ""choices"": [{ ""names"": [""indices"", ""targets""], ""required"": true }]")));
        }

        [Fact]
        public void RejectsANestedInputThatOmitsTheRequiredFlag()
        {
            Rejects("呼び出す側が渡す項目だけが", Table(Branch(
                @"[{ ""name"": ""targets"", ""origin"": ""hostInput"", ""required"": true,
                     ""maxItems"": 2,
                     ""element"": { ""origin"": ""hostInput"",
                       ""members"": [{ ""name"": ""index"", ""origin"": ""hostInput"",
                         ""shape"": ""number"" }] } }]")));
        }

        [Fact]
        public void RefusesTheRequiredFlagOnAnItemTheCallerDoesNotPass()
        {
            Rejects(
                "呼び出す側が渡す項目だけが",
                Table(
                    Branch(@"[]"),
                    @"{ ""origin"": ""hostOutput"", ""shape"": ""number"", ""required"": true }"));

            Rejects("呼び出す側が渡す項目だけが", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""required"": true,
                     ""maxItems"": 2,
                     ""element"": { ""origin"": ""hostInput"", ""shape"": ""number"",
                       ""required"": true } }]")));

            Rejects("呼び出す側が渡す項目だけが", Table(Branch(
                @"[{ ""name"": ""connector"", ""origin"": ""hostInput"", ""shape"": ""json"",
                     ""injected"": true, ""required"": true }]")));
        }

        [Fact]
        public void ReadsAGroupOfMembersAndAnArrayOfThem()
        {
            SchemaItem input = OnlyInput(Table(Branch(
                @"[{ ""name"": ""targets"", ""origin"": ""hostInput"", ""required"": true,
                     ""maxItems"": 100, ""minItems"": 1,
                     ""element"": { ""origin"": ""hostInput"",
                       ""members"": [{ ""name"": ""index"", ""origin"": ""hostInput"",
                         ""shape"": ""number"", ""required"": true }] } }]")));

            Assert.Equal(100, input.MaxItems);
            Assert.Equal(1, input.MinItems);
            Assert.Null(input.Element.Name);
            Assert.Equal("index", Assert.Single(input.Element.Members).Name);
        }

        [Theory]
        [InlineData(@"""shape"": ""number"", ""members"": []")]
        [InlineData(@"""shape"": ""number"", ""element"": { ""origin"": ""hostInput"",
            ""shape"": ""number"" }, ""maxItems"": 2")]
        public void RejectsAnItemThatUsesMoreThanOneFormAtOnce(string forms)
        {
            Rejects("項目の形は3つのうち1つ", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""required"": true, " + forms + "}]")));
        }

        [Fact]
        public void RejectsAnItemThatUsesNoFormAtAll()
        {
            Rejects("項目の形は3つのうち1つ", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""required"": true }]")));

            Rejects(
                "項目の形は3つのうち1つ",
                Table(Branch(@"[]"), @"{ ""origin"": ""hostOutput"" }"));
        }

        [Fact]
        public void AcceptsTheFormlessPayloadInsideEachEvent()
        {
            ToolSchema schema = Single(Table(Branch(@"[]"), PollOutput, Payloads));

            SchemaItem events = Assert.Single(schema.Output.Members);
            SchemaItem payload = Assert.Single(events.Element.Members);
            Assert.Equal("payload", payload.Name);
            Assert.Null(payload.Shape);
        }

        [Fact]
        public void RefusesAFormlessItemThatIsNotThePayload()
        {
            Rejects("項目の形は3つのうち1つ", Table(
                Branch(@"[]"),
                @"{ ""origin"": ""hostOutput"",
                    ""members"": [{ ""name"": ""dropped"", ""origin"": ""hostOutput"" }] }",
                Payloads));

            Rejects(
                "項目の形は3つのうち1つ",
                Table(Branch(@"[]"), @"{ ""origin"": ""hostOutput"" }", Payloads));
        }

        [Fact]
        public void RefusesAFormlessPayloadOnAToolWithoutEventBranches()
        {
            Rejects("項目の形は3つのうち1つ", Table(Branch(@"[]"), PollOutput));
        }

        [Fact]
        public void RefusesAPayloadThatNamesAFormOfItsOwn()
        {
            Rejects("項目の形は3つのうち1つ", Table(
                Branch(@"[]"),
                @"{ ""origin"": ""hostOutput"", ""members"": [
                    { ""name"": ""events"", ""origin"": ""hostOutput"", ""maxItems"": 1000,
                      ""element"": { ""origin"": ""hostOutput"", ""members"": [
                        { ""name"": ""payload"", ""origin"": ""hostOutput"",
                          ""shape"": ""json"" }] } }] }",
                Payloads));
        }

        [Fact]
        public void RefusesTheInjectedMarkOnAnItemThatIsNotANamedInput()
        {
            Rejects("入力の項目だけが", Table(
                Branch(@"[]"),
                @"{ ""origin"": ""hostOutput"", ""shape"": ""number"", ""injected"": true }"));

            Rejects("入力の項目だけが", Table(Branch(
                @"[{ ""name"": ""targets"", ""origin"": ""hostInput"", ""required"": true,
                     ""maxItems"": 2,
                     ""element"": { ""origin"": ""hostInput"", ""shape"": ""number"",
                       ""injected"": true } }]")));
        }

        [Fact]
        public void RejectsAToolThatHoldsBothAListingAndEventBranches()
        {
            Rejects(
                "一覧の規則の対象外",
                Table(
                    Branch(@"[]"),
                    Output,
                    @", ""listing"": { ""limitDefault"": 50, ""limitMaximum"": 200 },
                       ""payloads"": [{ ""type"": ""view.click"", ""members"": [] }]"));
        }

        [Fact]
        public void RejectsAnArrayWithoutAnItemCountLimit()
        {
            Rejects("要素を並べる項目だけが要素数の上限を持つ", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""required"": true,
                     ""element"": { ""origin"": ""hostInput"", ""shape"": ""number"" } }]")));
        }

        [Fact]
        public void RejectsAnItemCountLimitOnSomethingThatIsNotAnArray()
        {
            Rejects("要素を並べる項目だけが要素数の上限を持つ", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""shape"": ""number"",
                     ""required"": true, ""maxItems"": 2 }]")));
        }

        [Theory]
        [InlineData("0", "1以上でなければならない")]
        [InlineData("1.5", "整数でなければならない")]
        [InlineData(@"""2""", "整数でなければならない")]
        public void RejectsAnItemCountLimitThatIsNotAPositiveInteger(
            string maxItems, string fragment)
        {
            Rejects(fragment, Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""required"": true,
                     ""maxItems"": " + maxItems + @",
                     ""element"": { ""origin"": ""hostInput"", ""shape"": ""number"" } }]")));
        }

        [Fact]
        public void RejectsAMinimumItemCountThatIsNotOne()
        {
            Rejects("minItems は1でなければならない", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""required"": true,
                     ""maxItems"": 2, ""minItems"": 2,
                     ""element"": { ""origin"": ""hostInput"", ""shape"": ""number"" } }]")));
        }

        [Fact]
        public void RequiresTheSourceOfAValueTakenFromTheSdk()
        {
            Rejects("SDKに由来する既定と範囲は転記元を伴う", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""sdkIn"", ""shape"": ""number"",
                     ""required"": true, ""default"": 1 }]")));

            SchemaItem input = OnlyInput(Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""sdkIn"", ""shape"": ""number"",
                     ""required"": true, ""default"": 1,
                     ""source"": ""配布文書の該当節"" }]")));

            Assert.Equal("配布文書の該当節", input.Source);
            Assert.Equal(1, input.Default);
            Assert.True(input.HasDefault);
        }

        [Fact]
        public void RefusesASourceOnAValueThatTheCommonContractDecides()
        {
            Rejects("共通契約が定める値は転記元を持たない", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""shape"": ""number"",
                     ""required"": true, ""default"": 1, ""source"": ""配布文書の該当節"" }]")));
        }

        [Fact]
        public void RefusesASourceOnAnSdkItemThatHasNeitherADefaultNorBounds()
        {
            Rejects("SDKに由来する既定と範囲は転記元を伴う", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""sdkIn"", ""shape"": ""number"",
                     ""required"": true, ""source"": ""配布文書の該当節"" }]")));
        }

        [Theory]
        [InlineData(@"{ ""minimum"": 1 }", 1.0, null)]
        [InlineData(@"{ ""maximum"": 2.5 }", null, 2.5)]
        [InlineData(@"{ ""minimum"": 0, ""maximum"": 4294967295 }", 0.0, 4294967295.0)]
        public void ReadsBoundsOfEachShape(string bounds, double? minimum, double? maximum)
        {
            SchemaItem input = OnlyInput(Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""shape"": ""number"",
                     ""required"": true, ""bounds"": " + bounds + "}]")));

            Assert.Equal(minimum, input.Bounds.Minimum);
            Assert.Equal(maximum, input.Bounds.Maximum);
        }

        [Theory]
        [InlineData(@"{ }", "少なくとも一方を持つ")]
        [InlineData(@"{ ""minimum"": 5, ""maximum"": 1 }", "下限が上限を超えている")]
        [InlineData(@"{ ""minimum"": ""1"" }", "数値でなければならない")]
        public void RejectsBoundsThatAreNotAPairOfNumbersWithAtLeastOneEnd(
            string bounds, string fragment)
        {
            Rejects(fragment, Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""shape"": ""number"",
                     ""required"": true, ""bounds"": " + bounds + "}]")));
        }

        [Fact]
        public void ReadsTheLimitsOfAToolThatReturnsAListing()
        {
            ToolSchema schema = Single(Table(
                Branch(@"[]"), Output, @", ""listing"": { ""limitDefault"": 50, ""limitMaximum"": 200 }"));

            Assert.Equal(50, schema.Listing.LimitDefault);
            Assert.Equal(200, schema.Listing.LimitMaximum);
        }

        [Theory]
        [InlineData(@"{ ""limitDefault"": 0, ""limitMaximum"": 200 }", "1以上でなければならない")]
        [InlineData(@"{ ""limitDefault"": 300, ""limitMaximum"": 200 }", "既定が最大を超えている")]
        public void RejectsLimitsThatAreNotAPositiveDefaultWithinTheMaximum(
            string listing, string fragment)
        {
            Rejects(fragment, Table(Branch(@"[]"), Output, @", ""listing"": " + listing));
        }

        [Fact]
        public void ReadsTheBranchesOfAnEventPoll()
        {
            ToolSchema schema = Single(Table(
                Branch(@"[]"),
                Output,
                @", ""payloads"": [{ ""type"": ""view.click"",
                     ""members"": [{ ""name"": ""x"", ""origin"": ""sdkOut"",
                       ""shape"": ""number"" }] }]"));

            SchemaPayload payload = Assert.Single(schema.Payloads);
            Assert.Equal("view.click", payload.Type);
            Assert.Equal("x", Assert.Single(payload.Members).Name);
        }

        [Fact]
        public void RejectsTheSameEventBranchTwice()
        {
            Rejects(
                "同じ分岐が二度現れる",
                Table(
                    Branch(@"[]"),
                    Output,
                    @", ""payloads"": [{ ""type"": ""view.click"", ""members"": [] },
                                       { ""type"": ""view.click"", ""members"": [] }]"));
        }

        [Fact]
        public void RequiresAtLeastOneEventBranch()
        {
            Rejects(
                "payloads は1件以上でなければならない",
                Table(Branch(@"[]"), Output, @", ""payloads"": []"));
        }

        [Fact]
        public void RejectsToolsThatAreNotInAscendingOrder()
        {
            Rejects("序数の昇順で並んでいない", Tools("model_update_vertices", "model_list_vertices"));
        }

        [Fact]
        public void RejectsTheSameToolTwice()
        {
            Rejects("同じツールの名前が二度現れる", Tools("model_list_vertices", "model_list_vertices"));
        }

        [Fact]
        public void RejectsTheSameBranchNameTwice()
        {
            Rejects("同じ呼び分けが二度現れる", Table(
                @"[{ ""branch"": ""byIndex"", ""inputs"": [] },
                   { ""branch"": ""byIndex"", ""inputs"": [] }]"));
        }

        [Fact]
        public void RequiresAtLeastOneBranch()
        {
            Rejects("branches は1件以上でなければならない", Table(@"[]"));
        }

        [Fact]
        public void RejectsTheSameItemNameTwiceInOneBranch()
        {
            Rejects("同じ項目の名前が二度現れる", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""shape"": ""number"",
                     ""required"": true },
                   { ""name"": ""a"", ""origin"": ""hostInput"", ""shape"": ""number"",
                     ""required"": true }]")));
        }

        [Fact]
        public void ReadsAnInjectedItem()
        {
            SchemaItem input = OnlyInput(Table(Branch(
                @"[{ ""name"": ""connector"", ""origin"": ""hostInput"", ""shape"": ""json"",
                     ""injected"": true }]")));

            Assert.True(input.Injected);
            Assert.Null(input.Required);
        }

        [Fact]
        public void ReadsWhetherAnItemAllowsNull()
        {
            SchemaItem input = OnlyInput(Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""shape"": ""text"",
                     ""required"": true, ""nullable"": true }]")));

            Assert.True(input.Nullable);
        }

        [Theory]
        [InlineData(@"""origin"": ""sdkArgument""", "知らない origin")]
        [InlineData(@"""origin"": ""hostInput"", ""required"": ""true""", "真偽でなければならない")]
        [InlineData(
            @"""origin"": ""hostInput"", ""required"": true, ""nullable"": 1",
            "真偽でなければならない")]
        [InlineData(
            @"""origin"": ""hostInput"", ""required"": true, ""injected"": ""true""",
            "真偽でなければならない")]
        public void RejectsAValueThatIsNotOfItsDefinedForm(string member, string fragment)
        {
            Rejects(fragment, Table(Branch(
                @"[{ ""name"": ""a"", ""shape"": ""number"", " + member + "}]")));
        }

        [Theory]
        [InlineData("Index")]
        [InlineData("index_of")]
        public void RejectsAnItemNameThatIsNotOfTheDefinedForm(string name)
        {
            Rejects("英数字だけからなる語", Table(Branch(
                @"[{ ""name"": """ + name + @""", ""origin"": ""hostInput"", ""shape"": ""number"",
                     ""required"": true }]")));
        }

        [Theory]
        [InlineData("ByIndex")]
        [InlineData("by_index")]
        public void RejectsABranchNameThatIsNotOfTheDefinedForm(string branch)
        {
            Rejects("英数字だけからなる語", Table(
                @"[{ ""branch"": """ + branch + @""", ""inputs"": [] }]"));
        }

        [Theory]
        [InlineData("ModelListVertices")]
        [InlineData("model-list-vertices")]
        public void RejectsAToolNameThatIsNotOfTheDefinedForm(string tool)
        {
            Rejects("下線だけからなる語", @"{ ""tools"": [{ ""tool"": """ + tool
                + @""", ""branches"": " + Branch(@"[]") + @", ""output"": " + Output + "}] }");
        }

        [Fact]
        public void AcceptsAShapeThatIsNotOfTheToolNameForm()
        {
            Assert.Equal(
                "array_of_number",
                OnlyInput(Table(Branch(
                    @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""shape"": ""array_of_number"",
                         ""required"": true }]"))).Shape);
        }

        [Fact]
        public void RejectsAnUnknownMember()
        {
            Rejects("知らない項目がある", Table(Branch(@"[]"), Output, @", ""notes"": ""余分。"""));
        }

        [Fact]
        public void RejectsAMissingRequiredMember()
        {
            Rejects("項目が無い", @"{ ""tools"": [{ ""tool"": ""model_list_vertices"", ""branches"": "
                + Branch(@"[]") + "}] }");
        }

        [Fact]
        public void RejectsADefaultThatIsNotALiteral()
        {
            Rejects("値の項目の名前でない", Table(Branch(
                @"[{ ""name"": ""a"", ""origin"": ""hostInput"", ""shape"": ""json"",
                     ""required"": true, ""default"": { ""X"": 1 } }]")));
        }

        [Fact]
        public void ReadsAnEmptyTable()
        {
            Assert.Empty(ToolSchemaJsonReader.Read(@"{ ""tools"": [] }").Tools);
        }

        [Fact]
        public void RejectsTextThatIsNotJson()
        {
            Rejects("JSONとして読めない", "tools");
        }

        [Fact]
        public void RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => ToolSchemaJsonReader.Read(null));
        }

        private static string Tools(string first, string second)
        {
            return @"{ ""tools"": [
  { ""tool"": """ + first + @""", ""branches"": " + Branch(@"[]") + @", ""output"": " + Output + @" },
  { ""tool"": """ + second + @""", ""branches"": " + Branch(@"[]") + @", ""output"": " + Output + @" }] }";
        }
    }
}
