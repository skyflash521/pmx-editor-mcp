using System;
using System.Collections.Generic;
using System.Linq;
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

        [Theory]
        [InlineData("_左腕", "_右腕")]
        [InlineData("__右腕", "__左腕")]
        [InlineData("腕左_", "腕右_")]
        [InlineData("_腕右_", "_腕左_")]
        public void UnderscoresAroundTheNameDoNotHideItsSide(string name, string expected)
        {
            Bone(name, 2f, 3f, 4f);

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Bone, 0)));

            Assert.Equal(expected, _fixture.Model.Bone[1].Name);
        }

        [Fact]
        public void ANameOfOnlyUnderscoresGetsAMark()
        {
            Bone("__", 2f, 3f, 4f);

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Bone, 0)));

            Assert.Equal("M-__", _fixture.Model.Bone[1].Name);
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
            Now(child).Parent = parent;

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
            Now(vertex).Bone1 = bone;
            Now(vertex).Weight1 = 1f;

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
            Now(vertex).Bone1 = _fixture.Model.Bone[0];
            Now(vertex).Weight1 = 1f;

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Vertex, 0)));

            Assert.Same(Now(other), _fixture.Model.Vertex[1].Bone1);
        }

        [Fact]
        public void AVertexWeighedToABoneOutsideTheCopyGoesToTheBoneOfTheOppositeName()
        {
            Bone("左肩", 1f, 2f, 0f);
            Bone("右肩", -1f, 2f, 0f);
            Bone("左腕", 2f, 2f, 0f);
            FakeVertex vertex = Vertex(2f, 2f, 0f);
            Now(vertex).Bone1 = _fixture.Model.Bone[0];
            Now(vertex).Weight1 = 1f;

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(
                    Target(ElementKinds.Bone, 2),
                    Target(ElementKinds.Vertex, 0)));

            Assert.Same(
                _fixture.Model.Bone.Single(bone => bone.Name == "右肩"),
                _fixture.Model.Vertex[1].Bone1);
        }

        [Fact]
        public void TheCentreOfASdefVertexIsTurnedOverWithTheRestOfIt()
        {
            FakeVertex vertex = Vertex(2f, 0f, 0f);
            Now(vertex).SDEF = true;
            Now(vertex).SDEF_C = new V3(2f, 1f, 0f);
            Now(vertex).SDEF_R0 = new V3(3f, 1f, 0f);
            Now(vertex).SDEF_R1 = new V3(1f, 1f, 0f);

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
            Now(vertex).Bone1 = first;
            Now(vertex).Bone2 = second;
            Now(vertex).Weight1 = 0.5f;
            Now(vertex).Weight2 = 0.5f;
            Now(vertex).SDEF = true;
            Now(vertex).SDEF_C = new V3(99f, 0f, 0f);
            Now(vertex).SDEF_R0 = new V3(10f, 0f, 0f);
            Now(vertex).SDEF_R1 = new V3(20f, 0f, 0f);

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
                new FakeFace(NowAll(corners)[0], NowAll(corners)[1], NowAll(corners)[2]));

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(
                    Target(ElementKinds.Vertex, 0, 1, 2),
                    Target(ElementKinds.Face, 0)));

            Assert.Equal(2, Now(material).Faces.Count);
            IPXFace made = Now(material).Faces[1];
            Assert.Same(_fixture.Model.Vertex[3], made.Vertex1);
            Assert.Same(_fixture.Model.Vertex[5], made.Vertex2);
            Assert.Same(_fixture.Model.Vertex[4], made.Vertex3);
        }

        [Fact]
        public void TheScreenSelectionPicksTheFacesToCopy()
        {
            IList<IPXVertex> corners = Corners();
            FakeMaterial material = Material(
                new FakeFace(NowAll(corners)[0], NowAll(corners)[1], NowAll(corners)[2]));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(
                    Target(ElementKinds.Vertex, 0, 1, 2),
                    Chosen(ElementKinds.Face)));

            Assert.Equal(2, Now(material).Faces.Count);
        }

        [Fact]
        public void AFaceWhoseCornersWereNotCopiedIsLeftAlone()
        {
            IList<IPXVertex> corners = Corners();
            FakeMaterial material = Material(
                new FakeFace(NowAll(corners)[0], NowAll(corners)[1], NowAll(corners)[2]));

            IDictionary<string, object> value = ComposedEditFixture.Value(Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Face, 0))));

            Assert.Single(Now(material).Faces);
            Assert.Equal(0, value[ModelMirrorElements.AddedName]);
        }

        [Fact]
        public void TheOffsetsThatMoveACopiedVertexAreCopiedAndTurnedOver()
        {
            IList<IPXVertex> corners = Corners();
            FakeMorph morph = new FakeMorph("笑い", MorphKind.Vertex);
            Now(morph).Offsets.Add(new FakeVertexMorphOffset(NowAll(corners)[0])
            {
                Offset = new V3(2f, 3f, 4f),
            });
            _fixture.Model.Morph.Add(morph);

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Vertex, 0)));

            Assert.Equal(2, Now(morph).Offsets.Count);
            IPXVertexMorphOffset made = (IPXVertexMorphOffset)Now(morph).Offsets[1];
            Assert.Same(_fixture.Model.Vertex[3], made.Vertex);
            Near(-2.0, made.Offset.X);
            Near(3.0, made.Offset.Y);
        }

        [Fact]
        public void AnOffsetOnAVertexThatWasNotCopiedIsLeftAlone()
        {
            IList<IPXVertex> corners = Corners();
            FakeMorph morph = new FakeMorph("笑い", MorphKind.Vertex);
            Now(morph).Offsets.Add(new FakeVertexMorphOffset(NowAll(corners)[1])
            {
                Offset = new V3(2f, 3f, 4f),
            });
            _fixture.Model.Morph.Add(morph);

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(Target(ElementKinds.Vertex, 0)));

            Assert.Single(Now(morph).Offsets);
        }

        [Fact]
        public void ACopiedJointTiesTheCopiesOfTheBodiesItTied()
        {
            FakeBody first = Body("左上", 1f, 0f, 0f);
            FakeBody second = Body("左下", 1f, -1f, 0f);
            FakeJoint joint = Joint("左ひじ", first, second);
            Now(joint).Position = new V3(1f, -0.5f, 0f);

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
            Now(joint).Limit_AngleLow = new V3(-1f, -2f, -3f);
            Now(joint).Limit_AngleHigh = new V3(4f, 5f, 6f);

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
                new FakeFace(NowAll(corners)[0], NowAll(corners)[1], NowAll(corners)[2]));
            Bone("左腕", 2f, 0f, 0f);
            Body("左上", 3f, 0f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Mirror(
                Operation(ModelMirrorElements.MirrorModel)));

            Assert.Equal(3, _fixture.Model.Vertex.Count);
            Near(-1.0, _fixture.Model.Vertex[0].Position.X);
            Assert.Same(NowAll(corners)[2], Now(material).Faces[0].Vertex2);
            Assert.Equal("右腕", _fixture.Model.Bone[0].Name);
            Near(-3.0, _fixture.Model.Body[0].Position.X);
            Assert.Equal(0, value[ModelMirrorElements.AddedName]);
        }

        [Fact]
        public void MirroringTheModelTurnsTheOffsetsOfAVertexMorphOver()
        {
            IList<IPXVertex> corners = Corners();
            FakeMorph morph = new FakeMorph("笑い", MorphKind.Vertex);
            FakeVertexMorphOffset offset = new FakeVertexMorphOffset(NowAll(corners)[0])
            {
                Offset = new V3(2f, 3f, 4f),
            };
            Now(morph).Offsets.Add(offset);
            _fixture.Model.Morph.Add(morph);

            Mirror(Operation(ModelMirrorElements.MirrorModel));

            IPXVertexMorphOffset turned =
                (IPXVertexMorphOffset)Assert.Single(_fixture.Model.Morph[0].Offsets);
            Near(-2.0, turned.Offset.X);
            Near(3.0, turned.Offset.Y);
        }

        [Fact]
        public void MirroringTheModelTurnsOverTheNormalsTheRotationsTheMoveLimitsAndTheTipOffsets()
        {
            FakeVertex vertex = Vertex(1f, 0f, 0f);
            Now(vertex).Normal = new V3(0.6f, 0.8f, 0f);
            FakeBone bone = Bone("左腕", 2f, 0f, 0f);
            Now(bone).ToOffset = new V3(1f, 2f, 3f);
            FakeBody first = Body("左上", 3f, 0f, 0f);
            Now(first).Rotation = new V3(0.1f, 0.2f, 0.3f);
            FakeJoint joint = Joint("左ひじ", first, Body("左下", 3f, -1f, 0f));
            Now(joint).Rotation = new V3(0.4f, 0.5f, 0.6f);
            Now(joint).Limit_MoveLow = new V3(-1f, -2f, -3f);
            Now(joint).Limit_MoveHigh = new V3(4f, 5f, 6f);

            Mirror(Operation(ModelMirrorElements.MirrorModel));

            Near(-0.6, Now(vertex).Normal.X);
            Near(0.8, Now(vertex).Normal.Y);
            Near(-1.0, Now(bone).ToOffset.X);
            Near(2.0, Now(bone).ToOffset.Y);
            Near(0.1, Now(first).Rotation.X);
            Near(-0.2, Now(first).Rotation.Y);
            Near(-0.3, Now(first).Rotation.Z);
            Near(0.4, Now(joint).Rotation.X);
            Near(-0.5, Now(joint).Rotation.Y);
            Near(-0.6, Now(joint).Rotation.Z);
            Near(-4.0, Now(joint).Limit_MoveLow.X);
            Near(-2.0, Now(joint).Limit_MoveLow.Y);
            Near(1.0, Now(joint).Limit_MoveHigh.X);
            Near(5.0, Now(joint).Limit_MoveHigh.Y);
        }

        /// <summary>
        /// エディタのビューのメニューの「モデルの鏡像化」は、頂点の位置と法線だけを左右へ反転し、
        /// SDEFの中心と2つの参照点はそのまま残す。
        /// </summary>
        [Fact]
        public void MirroringTheModelLeavesTheSdefCentreAndPointsAsTheEditorDoes()
        {
            FakeVertex vertex = Vertex(2f, 0f, 0f);
            Now(vertex).SDEF = true;
            Now(vertex).SDEF_C = new V3(2f, 1f, 0f);
            Now(vertex).SDEF_R0 = new V3(3f, 1f, 0f);
            Now(vertex).SDEF_R1 = new V3(1f, 1f, 0f);

            Mirror(Operation(ModelMirrorElements.MirrorModel));

            Near(-2.0, Now(vertex).Position.X);
            Near(2.0, Now(vertex).SDEF_C.X);
            Near(3.0, Now(vertex).SDEF_R0.X);
            Near(1.0, Now(vertex).SDEF_R1.X);
        }

        [Fact]
        public void CopyingTurnsOverTheNormalTheRotationsAndTheTipOffset()
        {
            FakeVertex vertex = Vertex(1f, 0f, 0f);
            Now(vertex).Normal = new V3(0.6f, 0.8f, 0f);
            FakeBone bone = Bone("左腕", 2f, 0f, 0f);
            Now(bone).ToOffset = new V3(1f, 2f, 3f);
            FakeBody first = Body("左上", 3f, 0f, 0f);
            Now(first).Rotation = new V3(0.1f, 0.2f, 0.3f);
            FakeJoint joint = Joint("左ひじ", first, Body("左下", 3f, -1f, 0f));
            Now(joint).Rotation = new V3(0.4f, 0.5f, 0.6f);
            Now(joint).Limit_MoveLow = new V3(-1f, -2f, -3f);
            Now(joint).Limit_MoveHigh = new V3(4f, 5f, 6f);

            Mirror(
                Operation(ModelMirrorElements.CopyTargets),
                Targets(
                    Target(ElementKinds.Vertex, 0),
                    Target(ElementKinds.Bone, 0),
                    Target(ElementKinds.Body, 0),
                    Target(ElementKinds.Joint, 0)));

            Near(-0.6, _fixture.Model.Vertex[1].Normal.X);
            Near(0.8, _fixture.Model.Vertex[1].Normal.Y);
            Near(-1.0, _fixture.Model.Bone[1].ToOffset.X);
            Near(2.0, _fixture.Model.Bone[1].ToOffset.Y);
            IPXBody body = _fixture.Model.Body[2];
            Near(0.1, Now(body).Rotation.X);
            Near(-0.2, Now(body).Rotation.Y);
            Near(-0.3, Now(body).Rotation.Z);
            IPXJoint made = _fixture.Model.Joint[1];
            Near(0.4, made.Rotation.X);
            Near(-0.5, made.Rotation.Y);
            Near(-0.6, made.Rotation.Z);
            Near(-4.0, made.Limit_MoveLow.X);
            Near(1.0, made.Limit_MoveHigh.X);
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

        /// <summary>画面の選択でその種類を指す組。</summary>
        private static object Chosen(string kind)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ModelMirrorElements.KindName, kind },
                { "selected", true },
            };
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

        /// <summary>握った要素が並んでいた位置に、いまのモデルで並んでいる要素。</summary>
        private T Now<T>(T held)
            where T : class
        {
            return _fixture.Now(held);
        }

        /// <summary>握った要素の並びを、それぞれいまのモデルで同じ位置に並んでいる要素へ読み直す。</summary>
        private IList<T> NowAll<T>(IList<T> held)
            where T : class
        {
            return held.Select(_fixture.Now).ToList();
        }

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }
    }
}
