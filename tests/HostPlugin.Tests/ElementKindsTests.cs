using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>組み立てたツールが kind で受け取る要素の種類の表。</summary>
    public class ElementKindsTests
    {
        private const string Pending = "impl pending: 要素の種類ごとに並びの在りかと要素の作り方を引く";

        [Theory(Skip = Pending)]
        [InlineData(ElementKinds.Vertex)]
        [InlineData(ElementKinds.Face)]
        [InlineData(ElementKinds.Material)]
        [InlineData(ElementKinds.Bone)]
        [InlineData(ElementKinds.IkLink)]
        [InlineData(ElementKinds.Morph)]
        [InlineData(ElementKinds.MorphOffset)]
        [InlineData(ElementKinds.Node)]
        [InlineData(ElementKinds.NodeItem)]
        [InlineData(ElementKinds.Body)]
        [InlineData(ElementKinds.Joint)]
        [InlineData(ElementKinds.SoftBody)]
        [InlineData(ElementKinds.SoftBodyAnchor)]
        public void EveryNameTheSchemaOffersResolves(string name)
        {
            Assert.Contains(name, ElementKinds.Names);
            Assert.Equal(name, Resolved(name).Name);
        }

        [Fact(Skip = Pending)]
        public void TheNamesAreTheOnesTheSchemaOffersAndNoOther()
        {
            Assert.Equal(13, ElementKinds.Names.Count);
        }

        [Fact(Skip = Pending)]
        public void ANameTheTableDoesNotKnowIsRefusedWithTheNamesItDoes()
        {
            ElementKind kind;
            string message;

            Assert.False(ElementKinds.TryResolve("知らない種類", out kind, out message));
            Assert.Null(kind);
            Assert.Contains(ElementKinds.Vertex, message);
        }

        [Fact(Skip = Pending)]
        public void AKindThatIsNotTextIsRefused()
        {
            ElementKind kind;
            string message;

            Assert.False(ElementKinds.TryResolve(1, out kind, out message));
            Assert.NotNull(message);
        }

        [Theory(Skip = Pending)]
        [InlineData(ElementKinds.Vertex)]
        [InlineData(ElementKinds.Material)]
        [InlineData(ElementKinds.Bone)]
        [InlineData(ElementKinds.Morph)]
        [InlineData(ElementKinds.Node)]
        [InlineData(ElementKinds.Body)]
        [InlineData(ElementKinds.Joint)]
        [InlineData(ElementKinds.SoftBody)]
        public void TheKindsPmxItselfLinesUpHaveNoOwner(string name)
        {
            Assert.Null(Resolved(name).Owner);
        }

        [Theory(Skip = Pending)]
        [InlineData(ElementKinds.Face, ElementKinds.Material)]
        [InlineData(ElementKinds.IkLink, ElementKinds.Bone)]
        [InlineData(ElementKinds.MorphOffset, ElementKinds.Morph)]
        [InlineData(ElementKinds.NodeItem, ElementKinds.Node)]
        [InlineData(ElementKinds.SoftBodyAnchor, ElementKinds.SoftBody)]
        public void TheKindsAnElementLinesUpNameTheirOwner(string name, string owner)
        {
            Assert.Equal(owner, Resolved(name).Owner.Name);
        }

        [Fact(Skip = Pending)]
        public void TheOwnerOfAKindPmxLinesUpIsPmxItself()
        {
            FakePmx pmx = new FakePmx();

            IList<object> owners = ElementKinds.Owners(pmx, Resolved(ElementKinds.Vertex));

            Assert.Same(pmx, Assert.Single(owners));
        }

        [Fact(Skip = Pending)]
        public void TheOwnersOfFacesAreTheMaterialsInTheirOrder()
        {
            FakePmx pmx = new FakePmx();
            FakeMaterial first = new FakeMaterial("一");
            FakeMaterial second = new FakeMaterial("二");
            pmx.Material.Add(first);
            pmx.Material.Add(second);

            IList<object> owners = ElementKinds.Owners(pmx, Resolved(ElementKinds.Face));

            Assert.Equal(new object[] { first, second }, owners.ToArray());
        }

        [Fact(Skip = Pending)]
        public void TheItemsOfAKindPmxLinesUpAreThatList()
        {
            FakePmx pmx = new FakePmx();
            FakeBone bone = new FakeBone("センター");
            pmx.Bone.Add(bone);

            Assert.Same(bone, Assert.Single(Resolved(ElementKinds.Bone).Items(pmx)));
        }

        [Fact(Skip = Pending)]
        public void TheItemsOfFacesComeFromTheMaterialThatOwnsThem()
        {
            FakeMaterial material = new FakeMaterial();
            FakeFace face = new FakeFace();
            material.Faces.Add(face);

            Assert.Same(face, Assert.Single(Resolved(ElementKinds.Face).Items(material)));
        }

        [Fact(Skip = Pending)]
        public void TheItemsOfIkLinksComeFromTheIkOfTheBoneThatOwnsThem()
        {
            FakeBone bone = new FakeBone();
            FakeIkLink link = new FakeIkLink();
            bone.IK.Links.Add(link);

            Assert.Same(link, Assert.Single(Resolved(ElementKinds.IkLink).Items(bone)));
        }

        [Fact(Skip = Pending)]
        public void ReplacingPutsTheItemsBackInTheOrderTheyWereGiven()
        {
            FakePmx pmx = new FakePmx();
            FakeBone first = new FakeBone("一");
            FakeBone second = new FakeBone("二");
            pmx.Bone.Add(first);
            pmx.Bone.Add(second);

            Resolved(ElementKinds.Bone).Replace(pmx, new object[] { second, first });

            Assert.Equal(new IPXBone[] { second, first }, pmx.Bone.ToArray());
        }

        [Fact(Skip = Pending)]
        public void ReplacingLeavesTheSameObjectsInPlaceRatherThanCopies()
        {
            FakeMaterial material = new FakeMaterial();
            FakeFace face = new FakeFace();
            material.Faces.Add(face);

            Resolved(ElementKinds.Face).Replace(material, new object[] { face });

            Assert.Same(face, material.Faces[0]);
        }

        [Fact(Skip = Pending)]
        public void CreatingMakesAnElementOfThatKind()
        {
            FakePmx pmx = new FakePmx();

            object made = Resolved(ElementKinds.Bone).Create(new FakeBuilder(), pmx, null);

            Assert.IsAssignableFrom<IPXBone>(made);
        }

        [Theory(Skip = Pending)]
        [InlineData(MorphKind.Group, typeof(IPXGroupMorphOffset))]
        [InlineData(MorphKind.Flip, typeof(IPXGroupMorphOffset))]
        [InlineData(MorphKind.Vertex, typeof(IPXVertexMorphOffset))]
        [InlineData(MorphKind.Bone, typeof(IPXBoneMorphOffset))]
        [InlineData(MorphKind.UV, typeof(IPXUVMorphOffset))]
        [InlineData(MorphKind.UVA1, typeof(IPXUVMorphOffset))]
        [InlineData(MorphKind.UVA2, typeof(IPXUVMorphOffset))]
        [InlineData(MorphKind.UVA3, typeof(IPXUVMorphOffset))]
        [InlineData(MorphKind.UVA4, typeof(IPXUVMorphOffset))]
        [InlineData(MorphKind.Material, typeof(IPXMaterialMorphOffset))]
        [InlineData(MorphKind.Impulse, typeof(IPXImpulseMorphOffset))]
        public void AMorphOffsetTakesTheShapeTheMorphThatOwnsItCallsFor(MorphKind kind, Type shape)
        {
            FakeMorph morph = new FakeMorph("モーフ", kind);

            Assert.IsAssignableFrom(
                shape, Resolved(ElementKinds.MorphOffset).Create(new FakeBuilder(), morph, null));
        }

        [Fact(Skip = Pending)]
        public void CreatingANodeItemNeedsTheVariantBecauseTheKindDoesNotDecideIt()
        {
            FakeNode node = new FakeNode();

            Assert.Contains(ElementKinds.BoneVariant, Resolved(ElementKinds.NodeItem).Variants);
            Assert.IsAssignableFrom<IPXMorphNodeItem>(
                Resolved(ElementKinds.NodeItem)
                    .Create(new FakeBuilder(), node, ElementKinds.MorphVariant));
        }

        [Fact(Skip = Pending)]
        public void TheKindsThatDecideTheirOwnShapeOfferNoVariant()
        {
            Assert.Empty(Resolved(ElementKinds.Bone).Variants);
        }

        [Fact(Skip = Pending)]
        public void CloningMakesAnotherObjectThatCarriesTheSameValues()
        {
            FakeBone bone = new FakeBone("センター");

            object made = Resolved(ElementKinds.Bone).CloneOf(bone);

            Assert.NotSame(bone, made);
            Assert.Equal("センター", ((IPXBone)made).Name);
        }

        [Theory(Skip = Pending)]
        [InlineData(ElementKinds.Vertex)]
        [InlineData(ElementKinds.Face)]
        [InlineData(ElementKinds.Material)]
        [InlineData(ElementKinds.Bone)]
        [InlineData(ElementKinds.IkLink)]
        [InlineData(ElementKinds.Morph)]
        [InlineData(ElementKinds.MorphOffset)]
        [InlineData(ElementKinds.Node)]
        [InlineData(ElementKinds.NodeItem)]
        [InlineData(ElementKinds.Body)]
        [InlineData(ElementKinds.Joint)]
        [InlineData(ElementKinds.SoftBody)]
        [InlineData(ElementKinds.SoftBodyAnchor)]
        public void EveryKindReadsItsOwnListAndPutsItBackInTheGivenOrder(string name)
        {
            FakePmx pmx = Filled();
            ElementKind kind = Resolved(name);
            object owner = ElementKinds.Owners(pmx, kind)[0];

            IList<object> items = kind.Items(owner);
            Assert.Equal(2, items.Count);
            kind.Replace(owner, new[] { items[1], items[0] });

            IList<object> after = kind.Items(owner);
            Assert.Same(items[1], after[0]);
            Assert.Same(items[0], after[1]);
        }

        [Theory(Skip = Pending)]
        [InlineData(ElementKinds.Vertex, typeof(IPXVertex))]
        [InlineData(ElementKinds.Face, typeof(IPXFace))]
        [InlineData(ElementKinds.Material, typeof(IPXMaterial))]
        [InlineData(ElementKinds.Bone, typeof(IPXBone))]
        [InlineData(ElementKinds.IkLink, typeof(IPXIKLink))]
        [InlineData(ElementKinds.Morph, typeof(IPXMorph))]
        [InlineData(ElementKinds.MorphOffset, typeof(IPXVertexMorphOffset))]
        [InlineData(ElementKinds.Node, typeof(IPXNode))]
        [InlineData(ElementKinds.NodeItem, typeof(IPXBoneNodeItem))]
        [InlineData(ElementKinds.Body, typeof(IPXBody))]
        [InlineData(ElementKinds.Joint, typeof(IPXJoint))]
        [InlineData(ElementKinds.SoftBody, typeof(IPXSoftBody))]
        [InlineData(ElementKinds.SoftBodyAnchor, typeof(IPXSoftBodyAnchor))]
        public void EveryKindMakesAndClonesTheShapeItsListHolds(string name, Type shape)
        {
            FakePmx pmx = Filled();
            ElementKind kind = Resolved(name);
            object owner = ElementKinds.Owners(pmx, kind)[0];
            string variant = kind.Variants.Count == 0 ? null : kind.Variants[0];

            Assert.IsAssignableFrom(shape, kind.Create(new FakeBuilder(), owner, variant));
            Assert.IsAssignableFrom(shape, kind.CloneOf(kind.Items(owner)[0]));
        }

        [Fact(Skip = Pending)]
        public void CloningABoneCarriesItsIkAndKeepsPointingAtTheSameBones()
        {
            FakeBone target = new FakeBone("先");
            FakeBone bone = new FakeBone("IK") { IsIK = true, Parent = target };
            bone.IK.Target = target;
            bone.IK.LoopCount = 8;
            bone.IK.Links.Add(new FakeIkLink(target));

            IPXBone made = (IPXBone)Resolved(ElementKinds.Bone).CloneOf(bone);

            Assert.NotSame(bone, made);
            Assert.True(made.IsIK);
            Assert.Same(target, made.IK.Target);
            Assert.Equal(8, made.IK.LoopCount);
            Assert.Same(target, Assert.Single(made.IK.Links).Bone);
            Assert.Same(target, made.Parent);
        }

        /// <summary>13種類の要素をそれぞれ2つずつ持つ題材。並びの入れ替えを見分けられる数にする。</summary>
        private static FakePmx Filled()
        {
            FakePmx pmx = new FakePmx();
            FakeVertex firstVertex = new FakeVertex();
            FakeVertex secondVertex = new FakeVertex(1f, 0f, 0f);
            pmx.Vertex.Add(firstVertex);
            pmx.Vertex.Add(secondVertex);
            FakeBone firstBone = new FakeBone("ボーン一") { IsIK = true };
            FakeBone secondBone = new FakeBone("ボーン二");
            firstBone.IK.Links.Add(new FakeIkLink(firstBone));
            firstBone.IK.Links.Add(new FakeIkLink(secondBone));
            pmx.Bone.Add(firstBone);
            pmx.Bone.Add(secondBone);
            for (int at = 0; at < 2; at++)
            {
                FakeMaterial material = new FakeMaterial("材質" + at);
                material.Faces.Add(new FakeFace(firstVertex, secondVertex, firstVertex));
                material.Faces.Add(new FakeFace(secondVertex, firstVertex, secondVertex));
                pmx.Material.Add(material);
                FakeMorph morph = new FakeMorph("モーフ" + at, MorphKind.Vertex);
                morph.Offsets.Add(new FakeVertexMorphOffset(firstVertex));
                morph.Offsets.Add(new FakeVertexMorphOffset(secondVertex));
                pmx.Morph.Add(morph);
                FakeNode node = new FakeNode("表示枠" + at);
                node.Items.Add(new FakeBoneNodeItem(firstBone));
                node.Items.Add(new FakeBoneNodeItem(secondBone));
                pmx.Node.Add(node);
                FakeBody body = new FakeBody("剛体" + at) { Bone = firstBone };
                pmx.Body.Add(body);
                pmx.Joint.Add(new FakeJoint("Joint" + at) { BodyA = body, BodyB = body });
                FakeSoftBody soft = new FakeSoftBody("SoftBody" + at) { Material = material };
                soft.Anchors.Add(new FakeSoftBodyAnchor(body, firstVertex));
                soft.Anchors.Add(new FakeSoftBodyAnchor(body, secondVertex));
                pmx.SoftBody.Add(soft);
            }

            return pmx;
        }

        private static ElementKind Resolved(string name)
        {
            ElementKind kind;
            string message;
            Assert.True(ElementKinds.TryResolve(name, out kind, out message), message);

            return kind;
        }
    }
}
