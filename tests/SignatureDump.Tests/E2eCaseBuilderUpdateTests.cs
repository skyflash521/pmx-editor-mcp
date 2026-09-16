using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>自分の行を持たない、項目を書き換えるツールの検査の組み立て。</summary>
    public sealed class E2eCaseBuilderUpdateTests
    {
        private const string Writing = "model_update_iks";

        private const string Reading = "model_list_iks";

        /// <summary>受け手へ至る列の1段目。</summary>
        private const string FirstStep = "model_pmx";

        /// <summary>受け手へ至る列の2段目。</summary>
        private const string SecondStep = "motion_camera_vme_object";

        private const string Angle = "angle";

        private const string LoopCount = "loopCount";

        private const string Target = "target";

        private const string AngleType = "System.Single";

        private const string LoopCountType = "System.Int32";

        [Fact(Skip = "impl pending: 項目を書き換えるツールへ、持ち続ける項目の値をどれも渡す")]
        public void AnUpdateToolIsGivenAValueForEveryMemberTheModelKeeps()
        {
            IDictionary<string, object> written = Value(Built());

            Assert.True(written.ContainsKey(Angle));
            Assert.True(written.ContainsKey(LoopCount));
        }

        [Fact(Skip = "impl pending: 書き換えるツールへ渡す値を、綴りから決まる最小の値でなく正本の値にする")]
        public void TheValuesTheUpdateWritesComeFromTheSampleTable()
        {
            IDictionary<string, object> written = Value(Built());

            Assert.Equal(Sample(AngleType), written[Angle]);
            Assert.Equal(Sample(LoopCountType), written[LoopCount]);
        }

        [Fact]
        public void AnUpdateToolIsGivenNoValueForAMemberTheModelDoesNotKeep()
        {
            Assert.False(Value(Built()).ContainsKey(Target));
        }

        [Fact(Skip = "impl pending: 書き換えたツールの検査へ、書いた値を読み返す判定を足す")]
        public void WhatTheUpdateWroteIsReadBack()
        {
            IList<E2eCase> cases = Built();
            IDictionary<string, object> written = Value(cases);
            E2eCase[] read = ReadBacks(cases);

            Assert.Equal(2, read.Length);
            Assert.Equal(
                new[] { Angle, LoopCount },
                read.Select(c => c.Expected.Member).OrderBy(
                    m => m, StringComparer.Ordinal).ToArray());
            foreach (E2eCase one in read)
            {
                Assert.Equal(written[one.Expected.Member], one.Expected.Value);
            }
        }

        [Fact(Skip = "impl pending: 読み返しは書いたその要素を相手にする")]
        public void TheReadBackAimsAtTheElementTheUpdateWrote()
        {
            IList<E2eCase> cases = Built();
            E2eCase update = Updated(cases);
            E2eCase[] read = ReadBacks(cases);

            Assert.Equal(2, read.Length);
            foreach (E2eCase one in read)
            {
                Assert.NotNull(one.Borrowed);
                Assert.Equal(update.Borrowed.Values.Single(), one.Borrowed.Values.Single());
                Assert.True(one.Arguments.ContainsKey(HandlesName));
                Assert.False(one.Arguments.ContainsKey("all"));
                Assert.False(one.Arguments.ContainsKey("parentAll"));
            }
        }

        /// <summary>書いた値を読み返す検査。</summary>
        private static E2eCase[] ReadBacks(IList<E2eCase> cases)
        {
            return cases
                .Where(c => string.Equals(c.Tool, Reading, StringComparison.Ordinal)
                    && c.Expectation == E2eExpectation.Reads)
                .ToArray();
        }

        /// <summary>項目を書き換える呼び出し。</summary>
        private static E2eCase Updated(IList<E2eCase> cases)
        {
            return cases.Single(
                c => string.Equals(c.Tool, Writing, StringComparison.Ordinal)
                    && c.Expectation == E2eExpectation.Called);
        }

        /// <summary>その検査が書き換えるツールへ渡した値の組。</summary>
        private static IDictionary<string, object> Value(IList<E2eCase> cases)
        {
            return (IDictionary<string, object>)Updated(cases).Arguments["value"];
        }

        private static IList<E2eCase> Built()
        {
            Dictionary<SchemaItem, string> sdkTypes = new Dictionary<SchemaItem, string>();

            return E2eCaseBuilder.Build(
                new ToolMap(new ToolMapRow[0]),
                new ToolSchemaTable(
                    new[] { Updating(sdkTypes), Listing(), Free(FirstStep), Held(SecondStep) }),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<SchemaItem, string>(),
                sdkTypes,
                Samples(),
                null,
                null,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { Writing, Reading },
                },
                new Dictionary<string, ISet<string>>(StringComparer.Ordinal)
                {
                    {
                        Writing,
                        new HashSet<string>(new[] { Target }, StringComparer.Ordinal)
                    },
                },
                null,
                null,
                new Dictionary<string, IList<string>>(StringComparer.Ordinal)
                {
                    { Writing, new[] { FirstStep, SecondStep } },
                });
        }

        /// <summary>項目の型ごとに書く値。綴りから決まる最小の値とは違う値を持たせる。</summary>
        private static SampleValueTable Samples()
        {
            return SampleValueJsonReader.Read(
                "{\"types\":["
                    + "{\"typeName\":\"PEPlugin.Pmx.IPXBone\",\"default\":1,\"second\":2},"
                    + "{\"typeName\":\"System.Int32\",\"default\":3,\"second\":4},"
                    + "{\"typeName\":\"System.Single\",\"default\":7,\"second\":8}"
                    + "],\"rows\":[]}");
        }

        /// <summary>その型に書く値。</summary>
        private static object Sample(string typeName)
        {
            return Samples().Types
                .Single(r => string.Equals(r.TypeName, typeName, StringComparison.Ordinal))
                .First;
        }

        /// <summary>
        /// 対象を位置でも親のハンドルでも自分のハンドルでも指せる、項目の組を書き換えるツール。
        /// 正本の書き換えるツールはどれもこの3つの呼び分けを持つ。
        /// </summary>
        private static ToolSchema Updating(IDictionary<SchemaItem, string> sdkTypes)
        {
            return new ToolSchema(
                Writing,
                new[]
                {
                    new SchemaBranch(
                        "position",
                        null,
                        null,
                        Pointing().Concat(Values(sdkTypes)).ToList(),
                        new[]
                        {
                            new SchemaChoice(
                                new[] { "parentAll", "parentIndices", "parentRange" }, true),
                            new SchemaChoice(new[] { "all", "indices", "range" }, true),
                            new SchemaChoice(new[] { "value", "values" }, true),
                        }),
                    new SchemaBranch(
                        "heldParent",
                        null,
                        null,
                        new[] { Listed("parentHandles", true) }
                            .Concat(Values(sdkTypes)).ToList(),
                        new[]
                        {
                            new SchemaChoice(new[] { "all", "indices", "range" }, true),
                            new SchemaChoice(new[] { "value", "values" }, true),
                        }),
                    new SchemaBranch(
                        "held",
                        null,
                        null,
                        new[] { Listed(HandlesName, true) }.Concat(Values(sdkTypes)).ToList(),
                        new[] { new SchemaChoice(new[] { "value", "values" }, true) }),
                },
                Counted("updated"),
                null);
        }

        /// <summary>対象を同じ3つの呼び分けで指し、項目を並べて読むツール。</summary>
        private static ToolSchema Listing()
        {
            return new ToolSchema(
                Reading,
                new[]
                {
                    new SchemaBranch(
                        "position",
                        null,
                        null,
                        Pointing().Concat(Choosing()).ToList(),
                        new[]
                        {
                            new SchemaChoice(
                                new[] { "parentAll", "parentIndices", "parentRange" }, true),
                            new SchemaChoice(new[] { "all", "indices", "range" }, true),
                        }),
                    new SchemaBranch(
                        "heldParent",
                        null,
                        null,
                        new[] { Listed("parentHandles", true) }.Concat(Choosing()).ToList(),
                        new[] { new SchemaChoice(new[] { "all", "indices", "range" }, true) }),
                    new SchemaBranch(
                        "held",
                        null,
                        null,
                        new[] { Listed(HandlesName, true) }.Concat(Choosing()).ToList(),
                        new SchemaChoice[0]),
                },
                Listed(),
                null);
        }

        /// <summary>前の段のハンドルを受け取り、別のハンドルを出すツール。</summary>
        private static ToolSchema Held(string tool)
        {
            return new ToolSchema(
                tool,
                new[]
                {
                    new SchemaBranch(
                        "held",
                        null,
                        null,
                        new[] { Listed(HandlesName, true) },
                        new SchemaChoice[0]),
                },
                new SchemaItem(
                    null, null, Numbered(), null, ItemOrigin.HostOutput, null, null, false, null,
                    null, null, false, null),
                null);
        }

        /// <summary>受け手を渡さずに呼べるツール。出たハンドルは応答の並びの中へ入る。</summary>
        private static ToolSchema Free(string tool)
        {
            return new ToolSchema(
                tool,
                new[]
                {
                    new SchemaBranch(
                        "only", null, null, new SchemaItem[0], new SchemaChoice[0]),
                },
                new SchemaItem(
                    null, null, Numbered(), null, ItemOrigin.HostOutput, null, null, false, null,
                    null, null, false, null),
                null);
        }

        private const string HandlesName = "handles";

        /// <summary>対象を位置で指す入力の並び。</summary>
        private static IList<SchemaItem> Pointing()
        {
            return new[]
            {
                Numbered("pmxHandle", false),
                Listed("parentIndices", null),
                Flagged("parentAll"),
                Listed("indices", null),
                Flagged("all"),
            };
        }

        /// <summary>書き換える項目の組と、その並び。</summary>
        private static IList<SchemaItem> Values(IDictionary<SchemaItem, string> sdkTypes)
        {
            SchemaItem angle = Member(Angle);
            SchemaItem loopCount = Member(LoopCount);
            SchemaItem target = Member(Target);
            sdkTypes[angle] = AngleType;
            sdkTypes[loopCount] = LoopCountType;
            sdkTypes[target] = "PEPlugin.Pmx.IPXBone";
            SchemaItem group = new SchemaItem(
                null, new[] { angle, loopCount, target }, null, "value", ItemOrigin.HostInput,
                null, null, false, null, null, null, false, null);

            return new[]
            {
                group,
                new SchemaItem(
                    null, null, group, "values", ItemOrigin.HostInput, null, null, false, null,
                    null, null, false, null),
            };
        }

        /// <summary>並べたものと総数を返す応答。要素は書き換える項目をそのまま載せる。</summary>
        private static SchemaItem Listed()
        {
            SchemaItem element = new SchemaItem(
                null,
                new[] { Member(Angle), Member(LoopCount), Member(Target) },
                null,
                null,
                ItemOrigin.HostOutput,
                null,
                null,
                false,
                null,
                null,
                null,
                false,
                null);

            return new SchemaItem(
                null,
                new[]
                {
                    Numbered("total", null),
                    new SchemaItem(
                        null, null, element, "items", ItemOrigin.HostOutput, null, null, false,
                        null, null, null, false, null),
                },
                null,
                null,
                ItemOrigin.HostOutput,
                null,
                null,
                false,
                null,
                null,
                null,
                false,
                null);
        }

        /// <summary>数を1つ載せる応答。</summary>
        private static SchemaItem Counted(string name)
        {
            return new SchemaItem(
                null, new[] { Numbered(name, null) }, null, null, ItemOrigin.HostOutput, null,
                null, false, null, null, null, false, null);
        }

        private static SchemaItem Numbered()
        {
            return new SchemaItem(
                "number", null, null, null, ItemOrigin.HostOutput, null, null, false, null, null,
                null, false, null);
        }

        private static SchemaItem Numbered(string name, bool? required)
        {
            return new SchemaItem(
                "number", null, null, name, ItemOrigin.HostInput, required, null, false, null,
                null, null, false, null);
        }

        private static SchemaItem Flagged(string name)
        {
            return new SchemaItem(
                "boolean", null, null, name, ItemOrigin.HostInput, null, null, false, null, null,
                null, false, null);
        }

        private static SchemaItem Listed(string name, bool? required)
        {
            return new SchemaItem(
                null,
                null,
                new SchemaItem(
                    "number", null, null, null, ItemOrigin.HostInput, null, null, false, null,
                    null, null, false, null),
                name,
                ItemOrigin.HostInput,
                required,
                null,
                false,
                null,
                null,
                null,
                false,
                null);
        }

        /// <summary>読み取るツールが返すものを選ぶ入力の並び。正本はどの呼び分けもこの3つを持つ。</summary>
        private static IList<SchemaItem> Choosing()
        {
            return new[] { Fields(), Numbered("offset", false), Numbered("limit", false) };
        }

        /// <summary>返す項目を綴りで選ぶ入力。</summary>
        private static SchemaItem Fields()
        {
            return new SchemaItem(
                null,
                null,
                new SchemaItem(
                    "text", null, null, null, ItemOrigin.HostInput, null, null, false, null,
                    null, null, false, null),
                "fields",
                ItemOrigin.HostInput,
                false,
                null,
                false,
                null,
                null,
                null,
                false,
                null);
        }

        /// <summary>SDKに由来する項目。綴りは持たず、型から決まる。</summary>
        private static SchemaItem Member(string name)
        {
            return new SchemaItem(
                null, null, null, name, ItemOrigin.HostInput, null, null, false, null, null, null,
                false, null);
        }
    }
}
