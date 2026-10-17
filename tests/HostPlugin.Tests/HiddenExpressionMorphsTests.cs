using System.Collections.Generic;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class HiddenExpressionMorphsTests
    {
        [Fact]
        public void AHiddenMorphOnTheExpressionNodeIsFound()
        {
            FakePmx model = new FakePmx();
            FakeMorph hidden = Morph(model, "隠し", 0);
            model.ExpressionNode.Items.Add(new FakeMorphNodeItem(hidden));

            Assert.Equal(new IPXMorph[] { hidden }, HiddenExpressionMorphs.Of(model));
        }

        [Fact]
        public void AHiddenMorphOnlyOnTheOtherNodesIsNotFound()
        {
            FakePmx model = new FakePmx();
            FakeMorph onRoot = Morph(model, "根", 0);
            FakeMorph onOther = Morph(model, "ほかの枠", 0);
            model.RootNode.Items.Add(new FakeMorphNodeItem(onRoot));
            FakeNode node = new FakeNode("枠");
            node.Items.Add(new FakeMorphNodeItem(onOther));
            model.Node.Add(node);

            Assert.Empty(HiddenExpressionMorphs.Of(model));
        }

        [Fact]
        public void AShownMorphOnTheExpressionNodeAndAHiddenMorphOnNoNodeAreNotFound()
        {
            FakePmx model = new FakePmx();
            FakeMorph shown = Morph(model, "表示", 4);
            Morph(model, "枠なし", 0);
            model.ExpressionNode.Items.Add(new FakeMorphNodeItem(shown));

            Assert.Empty(HiddenExpressionMorphs.Of(model));
        }

        [Fact]
        public void AMorphListedTwiceIsFoundOnce()
        {
            FakePmx model = new FakePmx();
            FakeMorph hidden = Morph(model, "隠し", 0);
            model.ExpressionNode.Items.Add(new FakeMorphNodeItem(hidden));
            model.ExpressionNode.Items.Add(new FakeMorphNodeItem(hidden));

            Assert.Single(HiddenExpressionMorphs.Of(model));
        }

        [Fact]
        public void OnlyTheHiddenMorphsOnTheExpressionNodeThatAroseAreWarnedAbout()
        {
            FakePmx model = new FakePmx();
            FakeMorph kept = Morph(model, "前から", 0);
            FakeMorph arisen = Morph(model, "新しく", 0);
            model.ExpressionNode.Items.Add(new FakeMorphNodeItem(kept));
            model.ExpressionNode.Items.Add(new FakeMorphNodeItem(arisen));

            string warning = Assert.Single(HiddenExpressionMorphs.Arisen(Before(kept), model));

            Assert.Contains("「新しく」", warning);
            Assert.DoesNotContain("「前から」", warning);
        }

        [Fact]
        public void RenamingAHiddenMorphThatWasOnTheExpressionNodeIsNotWarnedAbout()
        {
            FakePmx model = new FakePmx();
            FakeMorph kept = Morph(model, "前の名前", 0);
            model.ExpressionNode.Items.Add(new FakeMorphNodeItem(kept));
            ISet<object> before = Before(kept);

            kept.Name = "後の名前";

            Assert.Empty(HiddenExpressionMorphs.Arisen(before, model));
        }

        [Fact]
        public void AnObjectThatIsNotAPmxHasNoHiddenMorphOnTheExpressionNode()
        {
            Assert.Empty(HiddenExpressionMorphs.Of(new object()));
            Assert.Empty(HiddenExpressionMorphs.Arisen(Before(), new object()));
        }

        private static ISet<object> Before(params object[] morphs)
        {
            return ReferenceCleanup.Held(morphs);
        }

        private static FakeMorph Morph(FakePmx model, string name, int panel)
        {
            FakeMorph morph = new FakeMorph(name) { Panel = panel };
            model.Morph.Add(morph);

            return morph;
        }
    }
}
