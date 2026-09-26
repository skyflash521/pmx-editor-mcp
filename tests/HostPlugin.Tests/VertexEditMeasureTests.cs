using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class VertexEditMeasureTests
    {
        [Fact]
        public void VerticesWhosePositionOrNormalMovedAndBonesThatMovedAreCounted()
        {
            FakePmx before = Model();
            FakePmx after = Model();
            after.Vertex[0].Position = new V3(9f, 0f, 0f);
            after.Vertex[1].Normal = new V3(0f, 0f, 1f);
            after.Bone[1].Position = new V3(0f, 9f, 0f);

            IDictionary<string, object> changed = VertexEditMeasure.Changed(before, after);

            Assert.Equal(2, changed[VertexEditMeasure.ChangedVerticesName]);
            Assert.Equal(1, changed[VertexEditMeasure.ChangedBonesName]);
        }

        [Fact]
        public void BodiesAndJointsThatMovedTurnedOrWereResizedAreCounted()
        {
            FakePmx before = Model();
            FakePmx after = Model();
            after.Body[0].Position = new V3(9f, 0f, 0f);
            after.Body[1].Rotation = new V3(0f, 1f, 0f);
            after.Body[2].BoxSize = new V3(2f, 2f, 2f);
            after.Joint[0].Rotation = new V3(0f, 0f, 1f);

            IDictionary<string, object> changed = VertexEditMeasure.Changed(before, after);

            Assert.Equal(3, changed[VertexEditMeasure.ChangedBodiesName]);
            Assert.Equal(1, changed[VertexEditMeasure.ChangedJointsName]);
            Assert.Equal(0, changed[VertexEditMeasure.ChangedVerticesName]);
        }

        [Fact]
        public void NothingIsCountedWhenNothingMoved()
        {
            IDictionary<string, object> changed = VertexEditMeasure.Changed(Model(), Model());

            Assert.Equal(0, changed[VertexEditMeasure.ChangedVerticesName]);
            Assert.Equal(0, changed[VertexEditMeasure.ChangedBonesName]);
            Assert.Equal(0, changed[VertexEditMeasure.ChangedBodiesName]);
            Assert.Equal(0, changed[VertexEditMeasure.ChangedJointsName]);
        }

        [Fact]
        public void EveryEditOfTheVertexEditConnectorIsMeasured()
        {
            IDictionary<string, System.Func<object, object, IDictionary<string, object>>> measures =
                VertexEditMeasure.ByRowKey();

            Assert.Equal(10, measures.Count);
            Assert.Contains("PEPlugin.View.IPEVertexEditConnector.Move()", measures.Keys);
            Assert.Contains("PEPlugin.View.IPEVertexEditConnector.MoveNormalAxis(System.Single)", measures.Keys);
        }

        private static FakePmx Model()
        {
            FakePmx made = new FakePmx();
            for (int at = 0; at < 3; at++)
            {
                FakeVertex vertex = new FakeVertex(at, 0f, 0f);
                vertex.Normal = new V3(0f, 1f, 0f);
                made.Vertex.Add(vertex);
                FakeBone bone = new FakeBone("ボーン" + at);
                bone.Position = new V3(at, 0f, 0f);
                made.Bone.Add(bone);
                FakeBody body = new FakeBody("剛体" + at);
                body.Position = new V3(at, 0f, 0f);
                body.BoxSize = new V3(1f, 1f, 1f);
                made.Body.Add(body);
                FakeJoint joint = new FakeJoint("Joint" + at);
                joint.Position = new V3(at, 0f, 0f);
                made.Joint.Add(joint);
            }

            return made;
        }
    }
}
