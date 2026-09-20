using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 並びから消えた要素を指したままの口を片付けるところ。要素はIndexではなくオブジェクトで
    /// 繋がるので、並びから外しただけでは指したままになる。
    /// </summary>
    public class ReferenceCleanupTests
    {

        [Fact]
        public void AFaceThatPointsAtAVertexNoLongerInTheListIsDropped()
        {
            FakePmx pmx = new FakePmx();
            FakeVertex kept = new FakeVertex();
            FakeVertex gone = new FakeVertex();
            pmx.Vertex.Add(kept);
            FakeMaterial material = new FakeMaterial();
            material.Faces.Add(new FakeFace(kept, kept, kept));
            material.Faces.Add(new FakeFace(kept, gone, kept));
            pmx.Material.Add(material);

            int repaired = ReferenceCleanup.Sweep(pmx);

            Assert.Single(material.Faces);
            Assert.Equal(1, repaired);
        }

        [Fact]
        public void AWeightOnABoneNoLongerInTheListMovesToTheNearestAncestorThatStayed()
        {
            FakePmx pmx = new FakePmx();
            FakeBone root = new FakeBone("親");
            FakeBone gone = new FakeBone("消えた");
            gone.Parent = root;
            pmx.Bone.Add(root);
            FakeVertex vertex = new FakeVertex();
            vertex.Bone1 = gone;
            vertex.Weight1 = 1f;
            pmx.Vertex.Add(vertex);

            ReferenceCleanup.Sweep(pmx);

            Assert.Same(root, vertex.Bone1);
            Assert.Equal(1f, vertex.Weight1);
        }

        [Fact]
        public void WeightsThatLandOnTheSameBoneAreAddedTogetherIntoOneSlot()
        {
            FakePmx pmx = new FakePmx();
            FakeBone root = new FakeBone("親");
            FakeBone gone = new FakeBone("消えた");
            gone.Parent = root;
            pmx.Bone.Add(root);
            FakeVertex vertex = new FakeVertex();
            vertex.Bone1 = root;
            vertex.Weight1 = 0.25f;
            vertex.Bone2 = gone;
            vertex.Weight2 = 0.75f;
            pmx.Vertex.Add(vertex);

            ReferenceCleanup.Sweep(pmx);

            Assert.Same(root, vertex.Bone1);
            Assert.Equal(1f, vertex.Weight1);
            Assert.Null(vertex.Bone2);
            Assert.Equal(0f, vertex.Weight2);
        }

        [Fact]
        public void AWeightWithNoAncestorLeftFallsOnTheFirstBoneInTheList()
        {
            FakePmx pmx = new FakePmx();
            FakeBone first = new FakeBone("先頭");
            pmx.Bone.Add(first);
            FakeVertex vertex = new FakeVertex();
            vertex.Bone1 = new FakeBone("消えた");
            vertex.Weight1 = 1f;
            pmx.Vertex.Add(vertex);

            ReferenceCleanup.Sweep(pmx);

            Assert.Same(first, vertex.Bone1);
            Assert.Equal(1f, vertex.Weight1);
        }

        [Fact]
        public void AWeightWithNoBoneLeftAtAllIsEmptied()
        {
            FakePmx pmx = new FakePmx();
            FakeVertex vertex = new FakeVertex();
            vertex.Bone1 = new FakeBone("消えた");
            vertex.Weight1 = 1f;
            pmx.Vertex.Add(vertex);

            ReferenceCleanup.Sweep(pmx);

            Assert.Null(vertex.Bone1);
            Assert.Equal(0f, vertex.Weight1);
        }

        [Fact]
        public void ABoneWhoseParentLeftTakesTheNearestAncestorThatStayed()
        {
            FakePmx pmx = new FakePmx();
            FakeBone root = new FakeBone("祖父");
            FakeBone gone = new FakeBone("消えた") { Parent = root };
            FakeBone child = new FakeBone("子") { Parent = gone };
            pmx.Bone.Add(root);
            pmx.Bone.Add(child);

            ReferenceCleanup.Sweep(pmx);

            Assert.Same(root, child.Parent);
        }

        [Fact]
        public void ABoneWhoseTipBoneLeftKeepsItsOffsetAndLosesTheTip()
        {
            FakePmx pmx = new FakePmx();
            FakeBone bone = new FakeBone("元") { ToBone = new FakeBone("消えた") };
            pmx.Bone.Add(bone);

            ReferenceCleanup.Sweep(pmx);

            Assert.Null(bone.ToBone);
        }

        [Fact]
        public void ABoneWhoseAppendParentLeftStopsFollowingIt()
        {
            FakePmx pmx = new FakePmx();
            FakeBone bone = new FakeBone("付与先")
            {
                AppendParent = new FakeBone("消えた"),
                IsAppendRotation = true,
                IsAppendTranslation = true,
            };
            pmx.Bone.Add(bone);

            ReferenceCleanup.Sweep(pmx);

            Assert.Null(bone.AppendParent);
            Assert.False(bone.IsAppendRotation);
            Assert.False(bone.IsAppendTranslation);
        }

        [Fact]
        public void AnIkWhoseTargetLeftStopsBeingAnIk()
        {
            FakePmx pmx = new FakePmx();
            FakeBone bone = new FakeBone("IK") { IsIK = true };
            bone.IK.Target = new FakeBone("消えた");
            pmx.Bone.Add(bone);

            ReferenceCleanup.Sweep(pmx);

            Assert.False(bone.IsIK);
        }

        [Fact]
        public void AnIkLinkThatPointsAtABoneThatLeftIsDropped()
        {
            FakePmx pmx = new FakePmx();
            FakeBone kept = new FakeBone("残った");
            FakeBone bone = new FakeBone("IK") { IsIK = true };
            bone.IK.Target = kept;
            bone.IK.Links.Add(new FakeIkLink(kept));
            bone.IK.Links.Add(new FakeIkLink(new FakeBone("消えた")));
            pmx.Bone.Add(kept);
            pmx.Bone.Add(bone);

            ReferenceCleanup.Sweep(pmx);

            Assert.True(bone.IsIK);
            Assert.Single(bone.IK.Links);
        }

        [Fact]
        public void AMorphOffsetThatPointsAtSomethingThatLeftIsDroppedAndTheMorphStays()
        {
            FakePmx pmx = new FakePmx();
            FakeMorph morph = new FakeMorph("頂点モーフ", MorphKind.Vertex);
            morph.Offsets.Add(new FakeVertexMorphOffset(new FakeVertex()));
            pmx.Morph.Add(morph);

            ReferenceCleanup.Sweep(pmx);

            Assert.Empty(morph.Offsets);
            Assert.Single(pmx.Morph);
        }

        [Fact]
        public void AMaterialMorphOffsetWhoseMaterialLeftIsDroppedRatherThanEmptied()
        {
            FakePmx pmx = new FakePmx();
            FakeMorph morph = new FakeMorph("材質モーフ", MorphKind.Material);
            morph.Offsets.Add(new FakeMaterialMorphOffset(new FakeMaterial("消えた")));
            pmx.Morph.Add(morph);

            ReferenceCleanup.Sweep(pmx);

            Assert.Empty(morph.Offsets);
        }

        [Fact]
        public void ANodeItemThatPointsAtSomethingThatLeftIsDropped()
        {
            FakePmx pmx = new FakePmx();
            FakeBone kept = new FakeBone("残った");
            pmx.Bone.Add(kept);
            FakeNode node = new FakeNode("枠");
            node.Items.Add(new FakeBoneNodeItem(kept));
            node.Items.Add(new FakeBoneNodeItem(new FakeBone("消えた")));
            node.Items.Add(new FakeMorphNodeItem(new FakeMorph("消えた")));
            pmx.Node.Add(node);

            ReferenceCleanup.Sweep(pmx);

            Assert.Single(node.Items);
        }

        [Fact]
        public void TheRootAndExpressionNodesArePartOfTheSweepToo()
        {
            FakePmx pmx = new FakePmx();
            pmx.RootNode.Items.Add(new FakeBoneNodeItem(new FakeBone("消えた")));
            pmx.ExpressionNode.Items.Add(new FakeMorphNodeItem(new FakeMorph("消えた")));

            ReferenceCleanup.Sweep(pmx);

            Assert.Empty(pmx.RootNode.Items);
            Assert.Empty(pmx.ExpressionNode.Items);
        }

        [Fact]
        public void ABodyWhoseBoneLeftLosesTheBoneAndStays()
        {
            FakePmx pmx = new FakePmx();
            FakeBody body = new FakeBody("剛体") { Bone = new FakeBone("消えた") };
            pmx.Body.Add(body);

            ReferenceCleanup.Sweep(pmx);

            Assert.Null(body.Bone);
            Assert.Single(pmx.Body);
        }

        [Fact]
        public void AJointWhoseBodyLeftLosesThatSideAndStays()
        {
            FakePmx pmx = new FakePmx();
            FakeBody kept = new FakeBody("残った");
            pmx.Body.Add(kept);
            FakeJoint joint = new FakeJoint("Joint")
            {
                BodyA = kept,
                BodyB = new FakeBody("消えた"),
            };
            pmx.Joint.Add(joint);

            ReferenceCleanup.Sweep(pmx);

            Assert.Same(kept, joint.BodyA);
            Assert.Null(joint.BodyB);
            Assert.Single(pmx.Joint);
        }

        [Fact]
        public void AnImpulseMorphOffsetWhoseBodyLeftIsDropped()
        {
            FakePmx pmx = new FakePmx();
            FakeMorph morph = new FakeMorph("インパルス", MorphKind.Impulse);
            morph.Offsets.Add(new FakeImpulseMorphOffset(new FakeBody("消えた")));
            pmx.Morph.Add(morph);

            ReferenceCleanup.Sweep(pmx);

            Assert.Empty(morph.Offsets);
        }

        [Fact]
        public void ASoftBodyLosesPinsAndAnchorsThatPointAtWhatLeft()
        {
            FakePmx pmx = new FakePmx();
            FakeVertex kept = new FakeVertex();
            pmx.Vertex.Add(kept);
            FakeSoftBody soft = new FakeSoftBody("布")
            {
                Material = new FakeMaterial("消えた"),
            };
            soft.Pins.Add(kept);
            soft.Pins.Add(new FakeVertex());
            soft.Anchors.Add(new FakeSoftBodyAnchor(new FakeBody("消えた"), kept));
            pmx.SoftBody.Add(soft);

            ReferenceCleanup.Sweep(pmx);

            Assert.Same(kept, Assert.Single(soft.Pins));
            Assert.Empty(soft.Anchors);
            Assert.Null(soft.Material);
        }

        [Fact]
        public void AModelWithNothingDanglingIsLeftAloneAndCountsZero()
        {
            FakePmx pmx = new FakePmx();
            FakeVertex vertex = new FakeVertex();
            pmx.Vertex.Add(vertex);
            FakeMaterial material = new FakeMaterial();
            material.Faces.Add(new FakeFace(vertex, vertex, vertex));
            pmx.Material.Add(material);

            Assert.Equal(0, ReferenceCleanup.Sweep(pmx));
            Assert.Single(material.Faces);
        }

        [Fact]
        public void RemovingAMaterialDragsTheVerticesOnlyItUsedAndTheMorphsThatWouldEmpty()
        {
            FakePmx pmx = new FakePmx();
            FakeVertex shared = new FakeVertex();
            FakeVertex alone = new FakeVertex();
            pmx.Vertex.Add(shared);
            pmx.Vertex.Add(alone);
            FakeMaterial other = new FakeMaterial("残る");
            other.Faces.Add(new FakeFace(shared, shared, shared));
            FakeMaterial going = new FakeMaterial("消す");
            going.Faces.Add(new FakeFace(shared, alone, alone));
            pmx.Material.Add(other);
            pmx.Material.Add(going);
            FakeMorph morph = new FakeMorph("そのモーフ", MorphKind.Vertex);
            morph.Offsets.Add(new FakeVertexMorphOffset(alone));
            pmx.Morph.Add(morph);

            IDictionary<string, IList<object>> following = ReferenceCleanup.Following(
                pmx, Resolved(ElementKinds.Material), new object[] { going });

            Assert.Same(alone, Assert.Single(following[ElementKinds.Vertex]));
            Assert.Same(morph, Assert.Single(following[ElementKinds.Morph]));
        }

        [Fact]
        public void RemovingAVertexDragsTheMaterialsThatWouldLoseEveryFace()
        {
            FakePmx pmx = new FakePmx();
            FakeVertex going = new FakeVertex();
            pmx.Vertex.Add(going);
            FakeMaterial material = new FakeMaterial("消える材質");
            material.Faces.Add(new FakeFace(going, going, going));
            pmx.Material.Add(material);

            IDictionary<string, IList<object>> following = ReferenceCleanup.Following(
                pmx, Resolved(ElementKinds.Vertex), new object[] { going });

            Assert.Same(material, Assert.Single(following[ElementKinds.Material]));
        }

        [Fact]
        public void RemovingAVertexDragsTheOtherVerticesThatOnlyTheLostMaterialUsed()
        {
            FakePmx pmx = new FakePmx();
            FakeVertex going = new FakeVertex();
            FakeVertex beside = new FakeVertex(1f, 0f, 0f);
            pmx.Vertex.Add(going);
            pmx.Vertex.Add(beside);
            FakeMaterial material = new FakeMaterial("消える材質");
            material.Faces.Add(new FakeFace(going, beside, beside));
            pmx.Material.Add(material);

            IDictionary<string, IList<object>> following = ReferenceCleanup.Following(
                pmx, Resolved(ElementKinds.Vertex), new object[] { going });

            Assert.Same(material, Assert.Single(following[ElementKinds.Material]));
            Assert.Same(beside, Assert.Single(following[ElementKinds.Vertex]));
        }

        [Fact]
        public void RemovingAMorphDragsTheGroupMorphThatOnlyPointedAtIt()
        {
            FakePmx pmx = new FakePmx();
            FakeVertex vertex = new FakeVertex();
            pmx.Vertex.Add(vertex);
            FakeMorph going = new FakeMorph("消す", MorphKind.Vertex);
            going.Offsets.Add(new FakeVertexMorphOffset(vertex));
            pmx.Morph.Add(going);
            FakeMorph grouped = new FakeMorph("まとめ", MorphKind.Group);
            grouped.Offsets.Add(new FakeGroupMorphOffset(going));
            pmx.Morph.Add(grouped);

            IDictionary<string, IList<object>> following = ReferenceCleanup.Following(
                pmx, Resolved(ElementKinds.Morph), new object[] { going });

            Assert.Same(grouped, Assert.Single(following[ElementKinds.Morph]));
        }

        [Fact]
        public void RemovingABodyDragsTheJointsThatPointedAtIt()
        {
            FakePmx pmx = new FakePmx();
            FakeBody going = new FakeBody("消す");
            pmx.Body.Add(going);
            FakeJoint joint = new FakeJoint("Joint") { BodyA = going };
            pmx.Joint.Add(joint);

            IDictionary<string, IList<object>> following = ReferenceCleanup.Following(
                pmx, Resolved(ElementKinds.Body), new object[] { going });

            Assert.Same(joint, Assert.Single(following[ElementKinds.Joint]));
        }

        [Fact]
        public void RemovingABoneDragsNothingBecauseTheWeightsAndBodiesAreRepairedInPlace()
        {
            FakePmx pmx = new FakePmx();
            FakeBone going = new FakeBone("消す");
            pmx.Bone.Add(going);

            IDictionary<string, IList<object>> following = ReferenceCleanup.Following(
                pmx, Resolved(ElementKinds.Bone), new object[] { going });

            Assert.Empty(following.SelectMany(pair => pair.Value));
        }

        [Theory]
        [InlineData(ReferenceCleanup.Keep, RelatedHandling.Keep)]
        [InlineData(ReferenceCleanup.Repair, RelatedHandling.Repair)]
        [InlineData(ReferenceCleanup.Cascade, RelatedHandling.Cascade)]
        public void EveryValueTheSchemaOffersResolves(string given, RelatedHandling expected)
        {
            RelatedHandling handling;
            string message;

            Assert.True(ReferenceCleanup.TryResolve(given, out handling, out message));
            Assert.Equal(expected, handling);
            Assert.Contains(given, ReferenceCleanup.Names);
        }

        [Fact]
        public void LeavingItOutMeansRepairing()
        {
            RelatedHandling handling;
            string message;

            Assert.True(ReferenceCleanup.TryResolve(null, out handling, out message));
            Assert.Equal(RelatedHandling.Repair, handling);
        }

        [Fact]
        public void AValueTheSchemaDoesNotOfferIsRefusedWithTheOnesItDoes()
        {
            RelatedHandling handling;
            string message;

            Assert.False(ReferenceCleanup.TryResolve("知らない値", out handling, out message));
            Assert.Contains(ReferenceCleanup.Repair, message);
        }

        [Fact]
        public void AUvMorphOffsetWhoseVertexLeftIsDropped()
        {
            FakePmx pmx = new FakePmx();
            FakeMorph morph = new FakeMorph("UVモーフ", MorphKind.UV);
            morph.Offsets.Add(new FakeUVMorphOffset(new FakeVertex()));
            pmx.Morph.Add(morph);

            ReferenceCleanup.Sweep(pmx);

            Assert.Empty(morph.Offsets);
        }

        [Fact]
        public void ABoneMorphOffsetWhoseBoneLeftIsDropped()
        {
            FakePmx pmx = new FakePmx();
            FakeMorph morph = new FakeMorph("ボーンモーフ", MorphKind.Bone);
            morph.Offsets.Add(new FakeBoneMorphOffset(new FakeBone("消えた")));
            pmx.Morph.Add(morph);

            ReferenceCleanup.Sweep(pmx);

            Assert.Empty(morph.Offsets);
        }

        [Theory]
        [InlineData(MorphKind.Group)]
        [InlineData(MorphKind.Flip)]
        public void AnOffsetThatPointsAtAMorphThatLeftIsDropped(MorphKind kind)
        {
            FakePmx pmx = new FakePmx();
            FakeMorph kept = new FakeMorph("残った", MorphKind.Vertex);
            pmx.Morph.Add(kept);
            FakeMorph morph = new FakeMorph("まとめ", kind);
            morph.Offsets.Add(new FakeGroupMorphOffset(kept));
            morph.Offsets.Add(new FakeGroupMorphOffset(new FakeMorph("消えた")));
            pmx.Morph.Add(morph);

            ReferenceCleanup.Sweep(pmx);

            Assert.Single(morph.Offsets);
        }

        [Fact]
        public void ASoftBodyAnchorWhoseVertexLeftIsDropped()
        {
            FakePmx pmx = new FakePmx();
            FakeBody body = new FakeBody("剛体");
            pmx.Body.Add(body);
            FakeSoftBody soft = new FakeSoftBody("布");
            soft.Anchors.Add(new FakeSoftBodyAnchor(body, new FakeVertex()));
            pmx.SoftBody.Add(soft);

            ReferenceCleanup.Sweep(pmx);

            Assert.Empty(soft.Anchors);
        }

        [Fact]
        public void AnSdefVertexLeftWithOneBoneStopsBeingSdef()
        {
            FakePmx pmx = new FakePmx();
            FakeBone root = new FakeBone("親");
            FakeBone gone = new FakeBone("消えた") { Parent = root };
            pmx.Bone.Add(root);
            FakeVertex vertex = new FakeVertex
            {
                SDEF = true,
                Bone1 = root,
                Weight1 = 0.5f,
                Bone2 = gone,
                Weight2 = 0.5f,
            };
            pmx.Vertex.Add(vertex);

            ReferenceCleanup.Sweep(pmx);

            Assert.Same(root, vertex.Bone1);
            Assert.Equal(1f, vertex.Weight1);
            Assert.False(vertex.SDEF);
        }

        [Fact]
        public void AnSdefVertexThatKeepsTwoBonesStaysSdef()
        {
            FakePmx pmx = new FakePmx();
            FakeBone root = new FakeBone("親");
            FakeBone other = new FakeBone("もう一つ");
            FakeBone gone = new FakeBone("消えた") { Parent = other };
            pmx.Bone.Add(root);
            pmx.Bone.Add(other);
            FakeVertex vertex = new FakeVertex
            {
                SDEF = true,
                Bone1 = root,
                Weight1 = 0.5f,
                Bone2 = gone,
                Weight2 = 0.5f,
            };
            pmx.Vertex.Add(vertex);

            ReferenceCleanup.Sweep(pmx);

            Assert.True(vertex.SDEF);
            Assert.Same(other, vertex.Bone2);
        }

        [Fact]
        public void AQdefVertexLeftWithOneBoneStaysQdef()
        {
            FakePmx pmx = new FakePmx();
            FakeBone root = new FakeBone("親");
            FakeBone gone = new FakeBone("消えた") { Parent = root };
            pmx.Bone.Add(root);
            FakeVertex vertex = new FakeVertex
            {
                QDEF = true,
                Bone1 = root,
                Weight1 = 0.5f,
                Bone2 = gone,
                Weight2 = 0.5f,
            };
            pmx.Vertex.Add(vertex);

            ReferenceCleanup.Sweep(pmx);

            Assert.True(vertex.QDEF);
            Assert.Equal(1f, vertex.Weight1);
        }

        [Fact]
        public void AVertexWithNoBoneLeftAtAllLosesBothDeformFlags()
        {
            FakePmx pmx = new FakePmx();
            FakeVertex vertex = new FakeVertex
            {
                SDEF = true,
                QDEF = true,
                Bone1 = new FakeBone("消えた"),
                Weight1 = 1f,
            };
            pmx.Vertex.Add(vertex);

            ReferenceCleanup.Sweep(pmx);

            Assert.False(vertex.SDEF);
            Assert.False(vertex.QDEF);
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
