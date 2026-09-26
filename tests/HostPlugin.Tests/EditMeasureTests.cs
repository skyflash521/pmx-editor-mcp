using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class EditMeasureTests
    {
        [Fact]
        public void VerticesWhosePositionOrNormalMovedAndBonesThatMovedAreCounted()
        {
            FakePmx before = Model();
            FakePmx after = Model();
            after.Vertex[0].Position = new V3(9f, 0f, 0f);
            after.Vertex[1].Normal = new V3(0f, 0f, 1f);
            after.Bone[1].Position = new V3(0f, 9f, 0f);

            IDictionary<string, object> changed = EditMeasure.Changed(before, after);

            Assert.Equal(2, changed[EditMeasure.ChangedVerticesName]);
            Assert.Equal(1, changed[EditMeasure.ChangedBonesName]);
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

            IDictionary<string, object> changed = EditMeasure.Changed(before, after);

            Assert.Equal(3, changed[EditMeasure.ChangedBodiesName]);
            Assert.Equal(1, changed[EditMeasure.ChangedJointsName]);
            Assert.Equal(0, changed[EditMeasure.ChangedVerticesName]);
        }

        [Fact]
        public void NothingIsCountedWhenNothingMoved()
        {
            IDictionary<string, object> changed = EditMeasure.Changed(Model(), Model());

            Assert.Equal(0, changed[EditMeasure.ChangedVerticesName]);
            Assert.Equal(0, changed[EditMeasure.ChangedBonesName]);
            Assert.Equal(0, changed[EditMeasure.ChangedBodiesName]);
            Assert.Equal(0, changed[EditMeasure.ChangedJointsName]);
        }

        [Fact]
        public void EveryEditOfTheVertexEditConnectorIsMeasured()
        {
            IDictionary<string, System.Func<object, object, object, IDictionary<string, object>>> measures =
                EditMeasure.ByRowKey();

            Assert.Equal(10, measures.Keys.Count(key => key.StartsWith("PEPlugin.View.IPEVertexEditConnector.", StringComparison.Ordinal)));
            Assert.Contains("PEPlugin.View.IPEVertexEditConnector.Move()", measures.Keys);
            Assert.Contains("PEPlugin.View.IPEVertexEditConnector.MoveNormalAxis(System.Single)", measures.Keys);
        }

        [Fact]
        public void UndoingAndRedoingAreMeasured()
        {
            IDictionary<string, System.Func<object, object, object, IDictionary<string, object>>> measures =
                EditMeasure.ByRowKey();

            Assert.Contains("PEPlugin.Form.IPEFormConnector.Undo()", measures.Keys);
            Assert.Contains("PEPlugin.Form.IPEFormConnector.Redo()", measures.Keys);
        }

        [Fact]
        public void UndoingTellsWhatMovedAndHowManyStepsAreLeftEachWay()
        {
            FakePmx before = Model();
            FakePmx after = Model();
            after.Vertex[2].Position = new V3(9f, 0f, 0f);
            FakeFormConnector form = new FakeFormConnector { UndoCount = 4, RedoCount = 1 };

            IDictionary<string, object> changed =
                EditMeasure.ByRowKey()["PEPlugin.Form.IPEFormConnector.Undo()"](form, before, after);

            Assert.Equal(1, changed[EditMeasure.ChangedVerticesName]);
            Assert.Equal(0, changed[EditMeasure.ChangedBonesName]);
            Assert.Equal(4, changed[EditMeasure.UndoCountName]);
            Assert.Equal(1, changed[EditMeasure.RedoCountName]);
        }

        [Fact]
        public void TheVertexEditsDoNotTellTheUndoSteps()
        {
            IDictionary<string, object> changed = EditMeasure.ByRowKey()[
                "PEPlugin.View.IPEVertexEditConnector.Move()"](null, Model(), Model());

            Assert.False(changed.ContainsKey(EditMeasure.UndoCountName));
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
