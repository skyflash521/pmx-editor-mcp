using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// kind と親の指定を読み、その種類の並びを持つ相手を解くところ。親を持たない種類ではPMX自身
    /// ひとつが相手になる。
    /// </summary>
    public sealed class ElementScopeTests : IDisposable
    {

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheNamesAreTheKindAndTheParentSet()
        {
            Assert.Equal(
                new[]
                {
                    ElementKinds.KindName,
                    TargetNames.Parent.Indices,
                    TargetNames.Parent.Range,
                    TargetNames.Parent.All,
                },
                ElementScope.Names);
        }

        [Fact]
        public void LeavingOutTheKindIsRefused()
        {
            Assert.Equal(ToolEnvelope.InvalidArgument, Refused(Arguments()));
        }

        [Fact]
        public void AKindTheTableDoesNotKnowIsRefused()
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Refused(Arguments(Given(ElementKinds.KindName, "知らない種類"))));
        }

        [Fact]
        public void AKindPmxLinesUpTakesPmxItselfAsTheOnlyOwner()
        {
            FakePmx pmx = Filled();

            IList<object> owners = Taken(pmx, Arguments(Kind(ElementKinds.Bone)));

            Assert.Same(pmx, Assert.Single(owners));
        }

        [Fact]
        public void AKindPmxLinesUpDoesNotTakeAParentSet()
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Refused(Arguments(
                    Kind(ElementKinds.Bone),
                    Given(TargetNames.Parent.All, true))));
        }

        [Theory]
        [InlineData(ElementKinds.Face)]
        [InlineData(ElementKinds.IkLink)]
        [InlineData(ElementKinds.MorphOffset)]
        [InlineData(ElementKinds.NodeItem)]
        [InlineData(ElementKinds.SoftBodyAnchor)]
        public void AKindAnElementLinesUpNeedsTheParentSet(string name)
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument, Refused(Arguments(Kind(name))));
        }

        [Theory]
        [InlineData(ElementKinds.Face)]
        [InlineData(ElementKinds.IkLink)]
        [InlineData(ElementKinds.MorphOffset)]
        [InlineData(ElementKinds.NodeItem)]
        [InlineData(ElementKinds.SoftBodyAnchor)]
        public void EveryParentedKindResolvesTheOwnerItsElementsHangFrom(string name)
        {
            FakePmx pmx = Filled();
            ElementKind kind = Resolved(name);

            IList<object> owners = Taken(
                pmx, Arguments(Kind(name), Given(TargetNames.Parent.Indices, new object[] { 1 })));

            object owner = Assert.Single(owners);
            Assert.Same(ElementKinds.Owners(pmx, kind)[1], owner);
            Assert.Single(kind.Items(owner));
        }

        [Fact]
        public void TheParentRangeTakesTheOwnersItCovers()
        {
            FakePmx pmx = Filled();
            pmx.Material.Add(new FakeMaterial("三つめ"));

            IList<object> owners = Taken(
                pmx,
                Arguments(
                    Kind(ElementKinds.Face),
                    Given(
                        TargetNames.Parent.Range,
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            { "start", 1 },
                            { "count", 2 },
                        })));

            Assert.Equal(new object[] { pmx.Material[1], pmx.Material[2] }, owners);
        }

        [Fact]
        public void TheParentAllTakesEveryOwner()
        {
            FakePmx pmx = Filled();

            IList<object> owners = Taken(
                pmx, Arguments(Kind(ElementKinds.Face), Given(TargetNames.Parent.All, true)));

            Assert.Equal(pmx.Material.Count, owners.Count);
        }

        [Fact]
        public void TwoWaysOfPointingAtTheParentAtOnceAreRefused()
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Refused(Arguments(
                    Kind(ElementKinds.Face),
                    Given(TargetNames.Parent.Indices, new object[] { 0 }),
                    Given(TargetNames.Parent.All, true))));
        }

        [Fact]
        public void AParentOutsideTheListIsRefused()
        {
            Assert.Equal(
                ToolEnvelope.IndexOutOfRange,
                Refused(Arguments(
                    Kind(ElementKinds.Face),
                    Given(TargetNames.Parent.Indices, new object[] { 3 }))));
        }

        [Fact]
        public void PointingAtTheParentByHandleIsNotOfferedHere()
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Refused(Arguments(
                    Kind(ElementKinds.Face),
                    Given(TargetNames.Parent.Handles, new object[] { 1 }))));
        }

        private IList<object> Taken(FakePmx pmx, IDictionary<string, object> arguments)
        {
            ElementKind kind;
            IList<object> owners;
            string code;
            string message;
            Assert.True(
                ElementScope.TryTake(
                    _fixture.Context(arguments), pmx, out kind, out owners, out code, out message),
                "解けなかった: " + message);
            Assert.NotNull(kind);

            return owners;
        }

        private string Refused(IDictionary<string, object> arguments)
        {
            ElementKind kind;
            IList<object> owners;
            string code;
            string message;
            Assert.False(ElementScope.TryTake(
                _fixture.Context(arguments), Filled(), out kind, out owners, out code, out message));
            Assert.NotNull(message);

            return code;
        }

        private static ElementKind Resolved(string name)
        {
            ElementKind kind;
            string message;
            Assert.True(ElementKinds.TryResolve(name, out kind, out message), message);

            return kind;
        }

        private static KeyValuePair<string, object> Kind(string name)
        {
            return Given(ElementKinds.KindName, name);
        }

        private static KeyValuePair<string, object> Given(string name, object value)
        {
            return ComposedEditFixture.Given(name, value);
        }

        private static IDictionary<string, object> Arguments(
            params KeyValuePair<string, object>[] given)
        {
            return ComposedEditFixture.Arguments(given);
        }

        /// <summary>親を持つ5種類がそれぞれ相手を2つ持つ題材。相手の取り違えを見分けられる数にする。</summary>
        private static FakePmx Filled()
        {
            FakePmx pmx = new FakePmx();
            FakeVertex vertex = new FakeVertex();
            pmx.Vertex.Add(vertex);
            FakeBody body = new FakeBody("剛体");
            pmx.Body.Add(body);
            FakeBone rooted = new FakeBone("全ての親");
            pmx.Bone.Add(rooted);
            pmx.RootNode.Items.Add(new FakeBoneNodeItem(rooted));
            pmx.ExpressionNode.Items.Add(new FakeMorphNodeItem(new FakeMorph("表情", PEPlugin.Pmx.MorphKind.Vertex)));
            for (int at = 0; at < 2; at++)
            {
                FakeMaterial material = new FakeMaterial("材質" + at);
                material.Faces.Add(new FakeFace(vertex, vertex, vertex));
                pmx.Material.Add(material);
                FakeBone bone = new FakeBone("ボーン" + at) { IsIK = true };
                bone.IK.Links.Add(new FakeIkLink(bone));
                pmx.Bone.Add(bone);
                FakeMorph morph = new FakeMorph("モーフ" + at, PEPlugin.Pmx.MorphKind.Vertex);
                morph.Offsets.Add(new FakeVertexMorphOffset(vertex));
                pmx.Morph.Add(morph);
                FakeNode node = new FakeNode("表示枠" + at);
                node.Items.Add(new FakeBoneNodeItem(bone));
                pmx.Node.Add(node);
                FakeSoftBody soft = new FakeSoftBody("SoftBody" + at) { Material = material };
                soft.Anchors.Add(new FakeSoftBodyAnchor(body, vertex));
                pmx.SoftBody.Add(soft);
            }

            return pmx;
        }
    }
}
