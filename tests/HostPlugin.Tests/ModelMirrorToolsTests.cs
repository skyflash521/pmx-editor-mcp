using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelMirrorToolsTests : IDisposable
    {
        private const int Digits = 4;

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void CopyingTurnsThePositionOverAndSwapsTheSidesInTheName()
        {
            Bone("左腕", 2f, 3f, 4f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Bone, 0))));

            Assert.Equal(2, _fixture.Model.Bone.Count);
            IPXBone made = _fixture.Model.Bone[1];
            Assert.Equal("右腕", made.Name);
            Near(-2.0, made.Position.X);
            Near(3.0, made.Position.Y);
            Assert.Equal(1, value[ModelMirrorElements.AddedName]);
        }

        [Fact]
        public void ACopiedNameWithNoSideGetsAMarkSoThatItStaysApart()
        {
            Bone("センター", 1f, 0f, 0f);

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Bone, 0)));

            Assert.Equal("M-センター", _fixture.Model.Bone[1].Name);
        }

        [Fact]
        public void TheCopiedBonesPointAtEachOtherRatherThanAtTheOnesTheyCameFrom()
        {
            FakeBone parent = Bone("左肩", 1f, 0f, 0f);
            FakeBone child = Bone("左腕", 2f, 0f, 0f);
            child.Parent = parent;

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Bone, 0, 1)));

            Assert.Same(_fixture.Model.Bone[2], _fixture.Model.Bone[3].Parent);
        }

        [Fact]
        public void ACopiedVertexIsWeighedToTheCopyOfTheBoneItWasWeighedTo()
        {
            FakeBone bone = Bone("左腕", 1f, 0f, 0f);
            FakeVertex vertex = Vertex(1f, 2f, 3f);
            vertex.Bone1 = bone;
            vertex.Weight1 = 1f;

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(
                    Target(ElementKinds.Bone, 0),
                    Target(ElementKinds.Vertex, 0)));

            IPXVertex made = _fixture.Model.Vertex[1];
            Near(-1.0, made.Position.X);
            Assert.Same(_fixture.Model.Bone[1], made.Bone1);
        }

        [Fact]
        public void AVertexCopiedOnItsOwnIsWeighedToTheBoneOfTheOppositeName()
        {
            Bone("左腕", 1f, 0f, 0f);
            FakeBone other = Bone("右腕", -1f, 0f, 0f);
            FakeVertex vertex = Vertex(1f, 0f, 0f);
            vertex.Bone1 = _fixture.Model.Bone[0];
            vertex.Weight1 = 1f;

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Vertex, 0)));

            Assert.Same(other, _fixture.Model.Vertex[1].Bone1);
        }

        [Fact]
        public void AVertexWeighedToABoneOutsideTheCopyGoesToTheBoneOfTheOppositeName()
        {
            Bone("左肩", 1f, 2f, 0f);
            FakeBone other = Bone("右肩", -1f, 2f, 0f);
            Bone("左腕", 2f, 2f, 0f);
            FakeVertex vertex = Vertex(2f, 2f, 0f);
            vertex.Bone1 = _fixture.Model.Bone[0];
            vertex.Weight1 = 1f;

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(
                    Target(ElementKinds.Bone, 2),
                    Target(ElementKinds.Vertex, 0)));

            Assert.Same(other, _fixture.Model.Vertex[1].Bone1);
        }

        [Fact]
        public void TheCentreOfASdefVertexIsTurnedOverWithTheRestOfIt()
        {
            FakeVertex vertex = Vertex(2f, 0f, 0f);
            vertex.SDEF = true;
            vertex.SDEF_C = new V3(2f, 1f, 0f);
            vertex.SDEF_R0 = new V3(3f, 1f, 0f);
            vertex.SDEF_R1 = new V3(1f, 1f, 0f);

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Vertex, 0)));

            IPXVertex made = _fixture.Model.Vertex[1];
            Near(-2.0, made.SDEF_C.X);
            Near(1.0, made.SDEF_C.Y);
            Near(-3.0, made.SDEF_R0.X);
            Near(-1.0, made.SDEF_R1.X);
        }

        [Fact]
        public void TheCentreOfASdefVertexIsTakenAgainFromTheBonesTheCopyIsWeighedTo()
        {
            FakeBone first = Bone("左肩", 1f, 0f, 0f);
            FakeBone second = Bone("左腕", 3f, 0f, 0f);
            FakeVertex vertex = Vertex(2f, 0f, 0f);
            vertex.Bone1 = first;
            vertex.Bone2 = second;
            vertex.Weight1 = 0.5f;
            vertex.Weight2 = 0.5f;
            vertex.SDEF = true;
            vertex.SDEF_C = new V3(99f, 0f, 0f);
            vertex.SDEF_R0 = new V3(10f, 0f, 0f);
            vertex.SDEF_R1 = new V3(20f, 0f, 0f);

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(
                    Target(ElementKinds.Bone, 0, 1),
                    Target(ElementKinds.Vertex, 0)));

            IPXVertex made = _fixture.Model.Vertex[1];
            Near(-2.0, made.SDEF_C.X);
            Near(-10.0, made.SDEF_R0.X);
            Near(-20.0, made.SDEF_R1.X);
        }

        [Fact]
        public void ACopiedFaceWindsTheOtherWayAndUsesTheCopiedVertices()
        {
            IList<IPXVertex> corners = Corners();
            FakeMaterial material = Material(
                new FakeFace(corners[0], corners[1], corners[2]));

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(
                    Target(ElementKinds.Vertex, 0, 1, 2),
                    Target(ElementKinds.Face, 0)));

            Assert.Equal(2, material.Faces.Count);
            IPXFace made = material.Faces[1];
            Assert.Same(_fixture.Model.Vertex[3], made.Vertex1);
            Assert.Same(_fixture.Model.Vertex[5], made.Vertex2);
            Assert.Same(_fixture.Model.Vertex[4], made.Vertex3);
        }

        [Fact]
        public void AFaceWhoseCornersWereNotCopiedIsLeftAlone()
        {
            IList<IPXVertex> corners = Corners();
            FakeMaterial material = Material(
                new FakeFace(corners[0], corners[1], corners[2]));

            IDictionary<string, object> value = ComposedEditFixture.Value(Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Face, 0))));

            Assert.Single(material.Faces);
            Assert.Equal(0, value[ModelMirrorElements.AddedName]);
        }

        [Fact]
        public void TheOffsetsThatMoveACopiedVertexAreCopiedAndTurnedOver()
        {
            IList<IPXVertex> corners = Corners();
            FakeMorph morph = new FakeMorph("笑い", MorphKind.Vertex);
            morph.Offsets.Add(new FakeVertexMorphOffset(corners[0])
            {
                Offset = new V3(2f, 3f, 4f),
            });
            _fixture.Model.Morph.Add(morph);

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Vertex, 0)));

            Assert.Equal(2, morph.Offsets.Count);
            IPXVertexMorphOffset made = (IPXVertexMorphOffset)morph.Offsets[1];
            Assert.Same(_fixture.Model.Vertex[3], made.Vertex);
            Near(-2.0, made.Offset.X);
            Near(3.0, made.Offset.Y);
        }

        [Fact]
        public void AnOffsetOnAVertexThatWasNotCopiedIsLeftAlone()
        {
            IList<IPXVertex> corners = Corners();
            FakeMorph morph = new FakeMorph("笑い", MorphKind.Vertex);
            morph.Offsets.Add(new FakeVertexMorphOffset(corners[1])
            {
                Offset = new V3(2f, 3f, 4f),
            });
            _fixture.Model.Morph.Add(morph);

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Vertex, 0)));

            Assert.Single(morph.Offsets);
        }

        [Fact]
        public void ACopiedJointTiesTheCopiesOfTheBodiesItTied()
        {
            FakeBody first = Body("左上", 1f, 0f, 0f);
            FakeBody second = Body("左下", 1f, -1f, 0f);
            FakeJoint joint = Joint("左ひじ", first, second);
            joint.Position = new V3(1f, -0.5f, 0f);

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(
                    Target(ElementKinds.Body, 0, 1),
                    Target(ElementKinds.Joint, 0)));

            IPXJoint made = _fixture.Model.Joint[1];
            Assert.Equal("右ひじ", made.Name);
            Near(-1.0, made.Position.X);
            Assert.Same(_fixture.Model.Body[2], made.BodyA);
            Assert.Same(_fixture.Model.Body[3], made.BodyB);
        }

        [Fact]
        public void TheAngleLimitsOfACopiedJointAreTurnedOverAndSwapped()
        {
            FakeJoint joint = Joint("左ひじ", Body("左上", 0f, 0f, 0f), Body("左下", 0f, 0f, 0f));
            joint.Limit_AngleLow = new V3(-1f, -2f, -3f);
            joint.Limit_AngleHigh = new V3(4f, 5f, 6f);

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Joint, 0)));

            IPXJoint made = _fixture.Model.Joint[1];
            Near(-5.0, made.Limit_AngleLow.Y);
            Near(-6.0, made.Limit_AngleLow.Z);
            Near(2.0, made.Limit_AngleHigh.Y);
            Near(3.0, made.Limit_AngleHigh.Z);
        }

        [Fact]
        public void MirroringTheModelTurnsEveryKindOverWithoutAddingAnything()
        {
            IList<IPXVertex> corners = Corners();
            FakeMaterial material = Material(
                new FakeFace(corners[0], corners[1], corners[2]));
            Bone("左腕", 2f, 0f, 0f);
            Body("左上", 3f, 0f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Mirror(
                Operation(ModelMirrorElements.MirrorModel)));

            Assert.Equal(3, _fixture.Model.Vertex.Count);
            Near(-1.0, _fixture.Model.Vertex[0].Position.X);
            Assert.Same(corners[2], material.Faces[0].Vertex2);
            Assert.Equal("右腕", _fixture.Model.Bone[0].Name);
            Near(-3.0, _fixture.Model.Body[0].Position.X);
            Assert.Equal(0, value[ModelMirrorElements.AddedName]);
        }

        [Fact]
        public void MirroringTheModelTurnsTheOffsetsOfAVertexMorphOver()
        {
            IList<IPXVertex> corners = Corners();
            FakeMorph morph = new FakeMorph("笑い", MorphKind.Vertex);
            FakeVertexMorphOffset offset = new FakeVertexMorphOffset(corners[0])
            {
                Offset = new V3(2f, 3f, 4f),
            };
            morph.Offsets.Add(offset);
            _fixture.Model.Morph.Add(morph);

            Mirror(Operation(ModelMirrorElements.MirrorModel));

            Near(-2.0, offset.Offset.X);
            Near(3.0, offset.Offset.Y);
        }

        [Fact]
        public void PointingAtWhatToCopyWhileMirroringTheWholeModelIsRefused()
        {
            Bone("左腕", 1f, 0f, 0f);

            IDictionary<string, object> envelope = Mirror(
                Operation(ModelMirrorElements.MirrorModel),
                Targets(Target(ElementKinds.Bone, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void LeavingOutWhatToCopyIsRefused()
        {
            Bone("左腕", 1f, 0f, 0f);

            IDictionary<string, object> envelope = Mirror(
                Operation(ModelMirrorElements.CopyTargets));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AKindThatCannotBeMirroredIsRefused()
        {
            Bone("左腕", 1f, 0f, 0f);

            IDictionary<string, object> envelope = Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Material, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        private IDictionary<string, object> Mirror(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelMirrorElements.ToolName, ComposedEditFixture.Arguments(given));
        }

        private static KeyValuePair<string, object> Operation(string operation)
        {
            return ComposedEditFixture.Given(ComposedOperation.OperationName, operation);
        }

        private static KeyValuePair<string, object> Targets(params object[] targets)
        {
            return ComposedEditFixture.Given(ModelMirrorElements.TargetsName, targets);
        }

        private static object Target(string kind, params int[] at)
        {
            List<object> made = new List<object>();
            foreach (int one in at)
            {
                made.Add(one);
            }

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ModelMirrorElements.KindName, kind },
                { "indices", made.ToArray() },
            };
        }

        private FakeBone Bone(string name, float x, float y, float z)
        {
            FakeBone made = new FakeBone(name) { Position = new V3(x, y, z) };
            _fixture.Model.Bone.Add(made);

            return made;
        }

        private FakeVertex Vertex(float x, float y, float z)
        {
            FakeVertex made = new FakeVertex(x, y, z);
            _fixture.Model.Vertex.Add(made);

            return made;
        }

        private IList<IPXVertex> Corners()
        {
            return new List<IPXVertex>
            {
                Vertex(1f, 0f, 0f),
                Vertex(2f, 0f, 0f),
                Vertex(1f, 1f, 0f),
            };
        }

        private FakeMaterial Material(params IPXFace[] faces)
        {
            FakeMaterial made = new FakeMaterial("材質");
            foreach (IPXFace face in faces)
            {
                made.Faces.Add(face);
            }

            _fixture.Model.Material.Add(made);

            return made;
        }

        private FakeBody Body(string name, float x, float y, float z)
        {
            FakeBody made = new FakeBody(name) { Position = new V3(x, y, z) };
            _fixture.Model.Body.Add(made);

            return made;
        }

        private FakeJoint Joint(string name, IPXBody first, IPXBody second)
        {
            FakeJoint made = new FakeJoint(name) { BodyA = first, BodyB = second };
            _fixture.Model.Joint.Add(made);

            return made;
        }

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }
    }
}
