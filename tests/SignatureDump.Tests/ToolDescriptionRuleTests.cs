using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolDescriptionRuleTests
    {
        [Fact]
        public void TheHeadCarriesTheTargetTheActionAndTheSourceInThatOrder()
        {
            string head = ToolDescriptionRule.Compose(Material(null, null, null))
                .Text.Split('\n')[0];

            Assert.True(
                head.IndexOf("vertex", StringComparison.Ordinal)
                    < head.IndexOf("list", StringComparison.Ordinal));
            Assert.True(
                head.IndexOf("list", StringComparison.Ordinal)
                    < head.IndexOf("PEPlugin.Pmx.IPXVertex", StringComparison.Ordinal));
        }

        [Fact]
        public void TheNotesFollowTheHeadInTheOrderTheyAreGiven()
        {
            string[] lines = ToolDescriptionRule
                .Compose(Material("使うな。", "頂点リスト", null)).Text.Split('\n');

            Assert.Equal(3, lines.Length);
            Assert.StartsWith("契約注記: ", lines[1], StringComparison.Ordinal);
            Assert.Contains("使うな。", lines[1], StringComparison.Ordinal);
            Assert.StartsWith("一次資料: ", lines[2], StringComparison.Ordinal);
            Assert.Contains("頂点リスト", lines[2], StringComparison.Ordinal);
        }

        [Fact]
        public void ANoteThatIsNotThereLeavesNoLine()
        {
            Assert.Single(ToolDescriptionRule.Compose(Material(null, "  ", null)).Text.Split('\n'));
        }

        [Fact]
        public void TheIndexTermsCarryBothTheNameAndTheJapaneseName()
        {
            ToolDescription description = ToolDescriptionRule
                .Compose(Material(null, null, Terms(2, "名")));

            Assert.Contains("索引語: 項目0(名0)・項目1(名1)", description.Text, StringComparison.Ordinal);
            Assert.Empty(description.Dropped);
        }

        [Fact]
        public void TermsThatDoNotFitWithTheirNamesFallBackToTheJapaneseNamesAlone()
        {
            ToolDescription description = ToolDescriptionRule
                .Compose(Material(null, null, Terms(90, new string('名', 5))));

            Assert.DoesNotContain("(", description.Text, StringComparison.Ordinal);
            Assert.Contains(new string('名', 5) + "0", description.Text, StringComparison.Ordinal);
            Assert.Empty(description.Dropped);
            Assert.True(Bytes(description.Text) <= ToolDescriptionRule.LimitBytes);
        }

        [Fact]
        public void TermsThatDoNotFitEvenAsJapaneseNamesAreKeptFromTheFrontAndTheRestReported()
        {
            IList<IndexTerm> terms = Terms(200, new string('名', 5));

            ToolDescription description = ToolDescriptionRule.Compose(Material(null, null, terms));

            Assert.True(Bytes(description.Text) <= ToolDescriptionRule.LimitBytes);
            Assert.NotEmpty(description.Dropped);
            Assert.Equal(
                terms.Skip(terms.Count - description.Dropped.Count).Select(t => t.Name).ToArray(),
                description.Dropped.ToArray());
            Assert.Contains(terms[0].JapaneseName, description.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(
                terms[terms.Count - 1].JapaneseName, description.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void ATermThatDoesNotFitStopsTheListEvenIfALaterOneWould()
        {
            List<IndexTerm> terms = new List<IndexTerm>(Terms(30, new string('名', 5)));
            terms.Add(new IndexTerm("長い項目", new string('名', 500)));
            terms.Add(new IndexTerm("短い項目", "名"));

            ToolDescription description = ToolDescriptionRule.Compose(Material(null, null, terms));

            Assert.Equal(new[] { "長い項目", "短い項目" }, description.Dropped.ToArray());
            Assert.DoesNotContain("索引語: 名・", description.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void AHeadThatIsAlreadyOverTheLimitKeepsNoTermAndReportsThemAll()
        {
            IList<IndexTerm> terms = Terms(3, "名");

            ToolDescription description = ToolDescriptionRule
                .Compose(Material(new string('注', 1000), null, terms));

            Assert.DoesNotContain("索引語", description.Text, StringComparison.Ordinal);
            Assert.Equal(terms.Select(t => t.Name).ToArray(), description.Dropped.ToArray());
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => ToolDescriptionRule.Compose(null));
            Assert.Throws<ArgumentNullException>(() => new IndexTerm(null, "名"));
            Assert.Throws<ArgumentException>(() => new IndexTerm("項目", " "));
            Assert.Throws<ArgumentNullException>(
                () => new ToolDescriptionMaterial(
                    null, "model", "list", null, "vertex", "PEPlugin.Pmx.IPXVertex", null, null, null));
        }

        private static ToolDescriptionMaterial Material(
            string contractNote, string sourceNote, IList<IndexTerm> terms)
        {
            return new ToolDescriptionMaterial(
                "model_list_vertices",
                "model",
                "list",
                "vertices",
                "vertex",
                "PEPlugin.Pmx.IPXVertex",
                contractNote,
                sourceNote,
                terms);
        }

        private static IList<IndexTerm> Terms(int count, string japanesePrefix)
        {
            List<IndexTerm> terms = new List<IndexTerm>();
            for (int index = 0; index < count; index++)
            {
                terms.Add(new IndexTerm("項目" + index, japanesePrefix + index));
            }

            return terms;
        }

        private static int Bytes(string text)
        {
            return Encoding.UTF8.GetByteCount(text);
        }
    }
}
