using System.Linq;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class NullReferenceKeptTests
    {
        [Fact]
        public void ANodeItemForAMorphWithNoMorphSurvivesTheSweep()
        {
            FakePmx pmx = new FakePmx();
            FakeNode node = new FakeNode("枠");
            node.Items.Add(new FakeMorphNodeItem(null));
            pmx.Node.Add(node);

            ReferenceCleanup.Sweep(pmx);

            Assert.Single(node.Items);
        }

        [Fact]
        public void AnImpulseOffsetWithNoBodySurvivesTheSweep()
        {
            FakePmx pmx = new FakePmx();
            FakeMorph morph = new FakeMorph("衝撃", MorphKind.Impulse);
            morph.Offsets.Add(new FakeImpulseMorphOffset(null));
            pmx.Morph.Add(morph);

            ReferenceCleanup.Sweep(pmx);

            Assert.Single(morph.Offsets);
        }

        [Fact]
        public void AnImpulseOffsetWithNoBodyPointsAtSomethingAlive()
        {
            Assert.True(ReferenceCleanup.PointsAtLive(
                new FakeImpulseMorphOffset(null),
                ReferenceCleanup.Held(new object[0]),
                ReferenceCleanup.Held(new object[0]),
                ReferenceCleanup.Held(new object[0]),
                ReferenceCleanup.Held(new object[0]),
                ReferenceCleanup.Held(new object[0])));
        }

        [Fact]
        public void AnAnchorWithNoBodyOrNoVertexSurvivesTheSweep()
        {
            FakePmx pmx = new FakePmx();
            FakeSoftBody soft = new FakeSoftBody();
            soft.Anchors.Add(new FakeSoftBodyAnchor(null, null));
            pmx.SoftBody.Add(soft);

            ReferenceCleanup.Sweep(pmx);

            Assert.Single(soft.Anchors);
        }

        [Fact]
        public void AnAnchorThatPointsAtABodyOutsideTheListIsStillDropped()
        {
            FakePmx pmx = new FakePmx();
            FakeSoftBody soft = new FakeSoftBody();
            soft.Anchors.Add(new FakeSoftBodyAnchor(new FakeBody("居ない"), null));
            pmx.SoftBody.Add(soft);

            ReferenceCleanup.Sweep(pmx);

            Assert.Empty(soft.Anchors);
        }
    }
}
