using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmd;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelSetPassGroupsTests : IDisposable
    {
        private const string ToolName = "model_set_pass_groups";

        private const string PassGroup = "passGroup";

        private const string PassGroups = "passGroups";

        private const string Kind = "kind";

        private const string Changed = "changed";

        private const int Groups = 16;

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void OnePassGroupGoesIntoEveryPointedBody()
        {
            IList<FakeBody> bodies = Bodies(3);

            IDictionary<string, object> value = ComposedEditFixture.Value(Set(
                Indices(0, 2),
                ComposedEditFixture.Given(PassGroup, Flags(1, 15))));

            Assert.Equal(Expected(1, 15), Now(bodies[0]).PassGroup);
            Assert.Equal(Expected(), Now(bodies[1]).PassGroup);
            Assert.Equal(Expected(1, 15), Now(bodies[2]).PassGroup);
            Assert.Equal(2, value[Changed]);
        }

        [Fact]
        public void PassGroupsGoIntoThePointedBodiesOneByOneInOrder()
        {
            IList<FakeBody> bodies = Bodies(3);

            ComposedEditFixture.Value(Set(
                Indices(1, 2),
                ComposedEditFixture.Given(PassGroups, new object[] { Flags(3), Flags(4, 5) })));

            Assert.Equal(Expected(), Now(bodies[0]).PassGroup);
            Assert.Equal(Expected(3), Now(bodies[1]).PassGroup);
            Assert.Equal(Expected(4, 5), Now(bodies[2]).PassGroup);
        }

        [Fact]
        public void APassGroupThatClearsFlagsTurnsThemOff()
        {
            IList<FakeBody> bodies = Bodies(1);
            bodies[0].PassGroup[7] = true;

            IDictionary<string, object> value = ComposedEditFixture.Value(Set(
                Indices(0),
                ComposedEditFixture.Given(PassGroup, Flags())));

            Assert.Equal(Expected(), Now(bodies[0]).PassGroup);
            Assert.Equal(1, value[Changed]);
        }

        [Fact]
        public void TheKindSoftBodyWritesIntoTheSoftBodies()
        {
            IList<FakeBody> bodies = Bodies(1);
            FakeSoftBody soft = new FakeSoftBody("布");
            _fixture.Model.SoftBody.Add(soft);

            IDictionary<string, object> value = ComposedEditFixture.Value(Set(
                ComposedEditFixture.Given(Kind, "softBody"),
                Indices(0),
                ComposedEditFixture.Given(PassGroup, Flags(0, 2))));

            Assert.Equal(Expected(0, 2), Now(soft).PassGroup);
            Assert.Equal(Expected(), Now(bodies[0]).PassGroup);
            Assert.Equal(1, value[Changed]);
        }

        [Fact]
        public void TheKindBodyWritesIntoTheBodies()
        {
            IList<FakeBody> bodies = Bodies(1);
            FakeSoftBody soft = new FakeSoftBody("布");
            _fixture.Model.SoftBody.Add(soft);

            ComposedEditFixture.Value(Set(
                ComposedEditFixture.Given(Kind, "body"),
                Indices(0),
                ComposedEditFixture.Given(PassGroup, Flags(6))));

            Assert.Equal(Expected(6), Now(bodies[0]).PassGroup);
            Assert.Equal(Expected(), Now(soft).PassGroup);
        }

        [Fact]
        public void EverythingButThePassGroupIsKept()
        {
            IList<FakeBody> bodies = Bodies(1);
            FakeBody body = bodies[0];
            body.Group = 4;
            body.Mass = 3f;
            body.Mode = BodyMode.Dynamic;
            body.BoxKind = BodyBoxKind.Box;
            body.BoxSize = new V3(1f, 2f, 3f);
            body.Position = new V3(4f, 5f, 6f);
            body.Rotation = new V3(0.1f, 0.2f, 0.3f);

            ComposedEditFixture.Value(Set(Indices(0), ComposedEditFixture.Given(PassGroup, Flags(9))));

            IPXBody now = Now(body);
            Assert.Equal("剛体0", now.Name);
            Assert.Equal(4, now.Group);
            Assert.Equal(3f, now.Mass);
            Assert.Equal(BodyMode.Dynamic, now.Mode);
            Assert.Equal(BodyBoxKind.Box, now.BoxKind);
            Assert.Equal(new[] { 1f, 2f, 3f }, new[] { now.BoxSize.X, now.BoxSize.Y, now.BoxSize.Z });
            Assert.Equal(new[] { 4f, 5f, 6f }, new[] { now.Position.X, now.Position.Y, now.Position.Z });
            Assert.Equal(new[] { 0.1f, 0.2f, 0.3f }, new[] { now.Rotation.X, now.Rotation.Y, now.Rotation.Z });
        }

        [Fact]
        public void ABodyAlreadyHoldingThePassGroupIsNotCounted()
        {
            IList<FakeBody> bodies = Bodies(2);
            bodies[0].PassGroup[1] = true;

            IDictionary<string, object> value = ComposedEditFixture.Value(Set(
                Indices(0, 1),
                ComposedEditFixture.Given(PassGroup, Flags(1))));

            Assert.Equal(1, value[Changed]);
        }

        [Fact]
        public void AllTheBodiesCanBePointedAtOnce()
        {
            IList<FakeBody> bodies = Bodies(2);

            ComposedEditFixture.Value(Set(
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(PassGroup, Flags(8))));

            Assert.All(bodies, body => Assert.Equal(Expected(8), Now(body).PassGroup));
        }

        [Fact]
        public void ARangeOfBodiesCanBePointedAt()
        {
            IList<FakeBody> bodies = Bodies(3);

            ComposedEditFixture.Value(Set(
                ComposedEditFixture.Given("range", Range(1, 2)),
                ComposedEditFixture.Given(PassGroup, Flags(8))));

            Assert.Equal(Expected(), Now(bodies[0]).PassGroup);
            Assert.Equal(Expected(8), Now(bodies[1]).PassGroup);
            Assert.Equal(Expected(8), Now(bodies[2]).PassGroup);
        }

        [Fact]
        public void SeveralRangesOfBodiesCanBePointedAt()
        {
            IList<FakeBody> bodies = Bodies(3);

            ComposedEditFixture.Value(Set(
                ComposedEditFixture.Given("ranges", new object[] { Range(0, 1), Range(2, 1) }),
                ComposedEditFixture.Given(PassGroup, Flags(8))));

            Assert.Equal(Expected(8), Now(bodies[0]).PassGroup);
            Assert.Equal(Expected(), Now(bodies[1]).PassGroup);
            Assert.Equal(Expected(8), Now(bodies[2]).PassGroup);
        }

        [Fact]
        public void TheBodiesSelectedOnTheScreenCanBePointedAt()
        {
            IList<FakeBody> bodies = Bodies(3);
            _fixture.View.Selected[ElementKinds.Body] = new[] { 0, 2 };

            ComposedEditFixture.Value(Set(
                ComposedEditFixture.Given("selected", true),
                ComposedEditFixture.Given(PassGroup, Flags(8))));

            Assert.Equal(Expected(8), Now(bodies[0]).PassGroup);
            Assert.Equal(Expected(), Now(bodies[1]).PassGroup);
            Assert.Equal(Expected(8), Now(bodies[2]).PassGroup);
        }

        [Fact]
        public void PointingAtTheTargetsInTwoWaysIsRefused()
        {
            Bodies(2);

            IDictionary<string, object> envelope = Set(
                Indices(0),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(PassGroup, Flags(8)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void GivingBothPassGroupAndPassGroupsIsRefused()
        {
            Bodies(1);

            IDictionary<string, object> envelope = Set(
                Indices(0),
                ComposedEditFixture.Given(PassGroup, Flags(1)),
                ComposedEditFixture.Given(PassGroups, new object[] { Flags(1) }));

            Refused(envelope, PassGroup);
        }

        [Fact]
        public void GivingNeitherPassGroupNorPassGroupsIsRefused()
        {
            Bodies(1);

            Refused(Set(Indices(0)), PassGroup);
        }

        [Fact]
        public void PassGroupsWhoseCountDiffersFromTheTargetsAreRefused()
        {
            Bodies(2);

            IDictionary<string, object> envelope = Set(
                Indices(0, 1),
                ComposedEditFixture.Given(PassGroups, new object[] { Flags(1) }));

            Refused(envelope, PassGroups);
        }

        [Fact]
        public void APassGroupThatIsNotSixteenTrueOrFalseIsRefused()
        {
            Bodies(1);
            object[] wrong =
            {
                "a",
                new object[15].Select(item => (object)false).ToArray(),
                new object[17].Select(item => (object)false).ToArray(),
                Enumerable.Range(0, Groups).Select(at => at == 3 ? (object)1 : false).ToArray(),
            };
            foreach (object given in wrong)
            {
                Refused(Set(Indices(0), ComposedEditFixture.Given(PassGroup, given)), PassGroup);
                Refused(Set(Indices(0), ComposedEditFixture.Given(PassGroups, new[] { given })), PassGroups);
            }
        }

        [Fact]
        public void AKindThatIsNotKnownIsRefused()
        {
            Bodies(1);

            Refused(
                Set(ComposedEditFixture.Given(Kind, "joint"), Indices(0), ComposedEditFixture.Given(PassGroup, Flags())),
                Kind);
        }

        [Fact]
        public void TheTargetsAreRequired()
        {
            Bodies(1);

            IDictionary<string, object> envelope = Set(ComposedEditFixture.Given(PassGroup, Flags()));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void ATargetOutsideTheListIsRefused()
        {
            Bodies(1);

            IDictionary<string, object> envelope = Set(Indices(1), ComposedEditFixture.Given(PassGroup, Flags()));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
        }

        private IList<FakeBody> Bodies(int count)
        {
            List<FakeBody> made = new List<FakeBody>();
            for (int at = 0; at < count; at++)
            {
                FakeBody body = new FakeBody("剛体" + at);
                _fixture.Model.Body.Add(body);
                made.Add(body);
            }

            return made;
        }

        private static object[] Flags(params int[] on)
        {
            return Enumerable.Range(0, Groups).Select(at => (object)on.Contains(at)).ToArray();
        }

        private static bool[] Expected(params int[] on)
        {
            return Enumerable.Range(0, Groups).Select(at => on.Contains(at)).ToArray();
        }

        private static IDictionary<string, object> Range(int start, int count)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "start", start },
                { "count", count },
            };
        }

        private static KeyValuePair<string, object> Indices(params int[] at)
        {
            return ComposedEditFixture.Given("indices", at.Select(one => (object)one).ToArray());
        }

        private IDictionary<string, object> Set(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ToolName, ComposedEditFixture.Arguments(given));
        }

        private T Now<T>(T held)
            where T : class
        {
            return _fixture.Now(held);
        }

        private static void Refused(IDictionary<string, object> envelope, string name)
        {
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
        }
    }
}
