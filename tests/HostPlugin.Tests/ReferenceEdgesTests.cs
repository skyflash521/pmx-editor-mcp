using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ReferenceEdgesTests
    {
        /// <summary>組んだ題材で、指されている要素の位置。</summary>
        private const int Held = 0;

        private readonly FakePmx _model = new FakePmx();

        [Fact]
        public void EveryEdgeOfTheTableIsNamedOnce()
        {
            IList<string> named = ReferenceEdges.All
                .Select(edge => edge.ReferrerKind + "->" + edge.TargetKind)
                .ToList();

            Assert.Equal(named.Count, named.Distinct().Count());
            Assert.Equal(
                new[]
                {
                    "body->bone",
                    "bone->bone",
                    "face->vertex",
                    "joint->body",
                    "material->vertex",
                    "morph->body",
                    "morph->bone",
                    "morph->material",
                    "morph->morph",
                    "morph->vertex",
                    "node->bone",
                    "node->morph",
                    "softBody->body",
                    "softBody->material",
                    "softBody->vertex",
                    "vertex->bone",
                },
                named.OrderBy(name => name, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void TheTableGroupsItsEdgesByWhatTheyPointAt()
        {
            Assert.Equal(
                new[]
                {
                    ElementKinds.Vertex,
                    ElementKinds.Material,
                    ElementKinds.Bone,
                    ElementKinds.Morph,
                    ElementKinds.Body,
                },
                ReferenceEdges.TargetKinds.ToArray());
            Assert.All(
                ReferenceEdges.Into(ElementKinds.Bone),
                edge => Assert.Equal(ElementKinds.Bone, edge.TargetKind));
            Assert.Equal(5, ReferenceEdges.Into(ElementKinds.Bone).Count);
        }

        [Fact]
        public void AFaceIsTakenAsPointingAtItsThreeVertices()
        {
            IList<IPXVertex> corners = Vertices(4);
            IPXFace face = new FakeFace(corners[0], corners[1], corners[2]);
            Material(face);

            Assert.Equal(
                new object[] { corners[0], corners[1], corners[2] },
                Pointed(ElementKinds.Face, ElementKinds.Vertex, face).ToArray());
        }

        [Fact]
        public void AMaterialIsTakenAsPointingAtTheVerticesOfItsFaces()
        {
            IList<IPXVertex> corners = Vertices(4);
            FakeMaterial held = Material(new FakeFace(corners[0], corners[1], corners[2]));
            Material(new FakeFace(corners[3], corners[3], corners[3]));

            Assert.Equal(
                new object[] { corners[0], corners[1], corners[2] },
                Pointed(ElementKinds.Material, ElementKinds.Vertex, held).ToArray());
        }

        [Fact]
        public void AMorphIsTakenAsPointingAtTheVerticesOfItsVertexAndUvOffsets()
        {
            IList<IPXVertex> corners = Vertices(3);
            FakeMorph morph = Morph(MorphKind.Vertex, new FakeVertexMorphOffset(corners[0]));
            morph.Offsets.Add(new FakeUVMorphOffset(corners[1]));

            Assert.Equal(
                new object[] { corners[0], corners[1] },
                Pointed(ElementKinds.Morph, ElementKinds.Vertex, morph).ToArray());
        }

        [Fact]
        public void ASoftBodyIsTakenAsPointingAtItsPinnedAndAnchoredVertices()
        {
            IList<IPXVertex> corners = Vertices(3);
            FakeSoftBody soft = SoftBody();
            soft.Pins.Add(corners[0]);
            soft.Anchors.Add(new FakeSoftBodyAnchor(null, corners[1]));

            Assert.Equal(
                new object[] { corners[0], corners[1] },
                Pointed(ElementKinds.SoftBody, ElementKinds.Vertex, soft).ToArray());
        }

        [Fact]
        public void AMaterialMorphOffsetWithoutAMaterialIsTakenAsPointingAtEveryMaterial()
        {
            FakeMaterial first = Material();
            FakeMaterial second = Material();
            FakeMorph named = Morph(MorphKind.Material, new FakeMaterialMorphOffset(second));
            FakeMorph whole = Morph(MorphKind.Material, new FakeMaterialMorphOffset());

            Assert.Equal(
                new object[] { second },
                Pointed(ElementKinds.Morph, ElementKinds.Material, named).ToArray());
            Assert.Equal(
                new object[] { first, second },
                Pointed(ElementKinds.Morph, ElementKinds.Material, whole).ToArray());
        }

        [Fact]
        public void ASoftBodyIsTakenAsPointingAtItsMaterial()
        {
            FakeMaterial held = Material();
            Material();
            FakeSoftBody soft = SoftBody();
            soft.Material = held;

            Assert.Equal(
                new object[] { held },
                Pointed(ElementKinds.SoftBody, ElementKinds.Material, soft).ToArray());
        }

        [Fact]
        public void AVertexIsTakenAsPointingAtTheBonesOfItsWeightedSlots()
        {
            IList<IPXBone> bones = Bones(3);
            IPXVertex vertex = Weighted(bones[0], 0.75f);
            vertex.Bone2 = bones[1];
            vertex.Weight2 = 0.25f;

            Assert.Equal(
                new object[] { bones[0], bones[1] },
                Pointed(ElementKinds.Vertex, ElementKinds.Bone, vertex).ToArray());
        }

        [Fact]
        public void TheWeightOfASlotIsCarriedWithTheVertexThatHoldsIt()
        {
            IList<IPXBone> bones = Bones(2);
            Weighted(bones[0], 0.25f);

            ReferenceEdge edge = Edge(ElementKinds.Vertex, ElementKinds.Bone);
            List<float> weights = new List<float>();
            edge.Walk(_model, (referrer, target, weight) => weights.Add(weight));

            Assert.True(edge.IsWeighted);
            Assert.Equal(new[] { 0.25f }, weights.ToArray());
        }

        [Fact]
        public void ABoneIsTakenAsPointingAtEveryBoneItsPortsHold()
        {
            IList<IPXBone> bones = Bones(5);
            IPXBone held = bones[0];
            held.Parent = bones[1];
            held.ToBone = bones[2];
            held.AppendParent = bones[3];
            held.IK.Target = bones[4];
            held.IK.Links.Add(new FakeIkLink(bones[1]));

            Assert.Equal(
                new object[] { bones[1], bones[2], bones[3], bones[4], bones[1] },
                Pointed(ElementKinds.Bone, ElementKinds.Bone, held).ToArray());
        }

        [Fact]
        public void AMorphIsTakenAsPointingAtTheBoneOfItsBoneOffset()
        {
            IList<IPXBone> bones = Bones(2);
            FakeMorph morph = Morph(MorphKind.Bone, new FakeBoneMorphOffset(bones[1]));

            Assert.Equal(
                new object[] { bones[1] },
                Pointed(ElementKinds.Morph, ElementKinds.Bone, morph).ToArray());
        }

        [Fact]
        public void ANodeIsTakenAsPointingAtTheBoneOfItsBoneItem()
        {
            IList<IPXBone> bones = Bones(2);
            FakeNode node = Node(new FakeBoneNodeItem(bones[1]));

            Assert.Equal(
                new object[] { bones[1] },
                Pointed(ElementKinds.Node, ElementKinds.Bone, node).ToArray());
        }

        [Fact]
        public void ABodyIsTakenAsPointingAtItsBone()
        {
            IList<IPXBone> bones = Bones(2);
            FakeBody body = Body();
            body.Bone = bones[1];

            Assert.Equal(
                new object[] { bones[1] },
                Pointed(ElementKinds.Body, ElementKinds.Bone, body).ToArray());
        }

        [Fact]
        public void AGroupMorphIsTakenAsPointingAtTheMorphOfItsOffset()
        {
            FakeMorph held = Morph(MorphKind.Vertex);
            FakeMorph group = Morph(MorphKind.Group, new FakeGroupMorphOffset(held));

            Assert.Equal(
                new object[] { held },
                Pointed(ElementKinds.Morph, ElementKinds.Morph, group).ToArray());
        }

        [Fact]
        public void ANodeIsTakenAsPointingAtTheMorphOfItsMorphItem()
        {
            FakeMorph held = Morph(MorphKind.Vertex);
            FakeNode node = Node(new FakeMorphNodeItem(held));

            Assert.Equal(
                new object[] { held },
                Pointed(ElementKinds.Node, ElementKinds.Morph, node).ToArray());
        }

        [Fact]
        public void AJointIsTakenAsPointingAtBothOfItsBodies()
        {
            FakeBody first = Body();
            FakeBody second = Body();
            FakeJoint joint = Joint();
            joint.BodyA = first;
            joint.BodyB = second;

            Assert.Equal(
                new object[] { first, second },
                Pointed(ElementKinds.Joint, ElementKinds.Body, joint).ToArray());
        }

        [Fact]
        public void AMorphIsTakenAsPointingAtTheBodyOfItsImpulseOffset()
        {
            Body();
            FakeBody held = Body();
            FakeMorph morph = Morph(MorphKind.Impulse, new FakeImpulseMorphOffset(held));

            Assert.Equal(
                new object[] { held },
                Pointed(ElementKinds.Morph, ElementKinds.Body, morph).ToArray());
        }

        [Fact]
        public void ASoftBodyIsTakenAsPointingAtTheBodyOfItsAnchor()
        {
            Body();
            FakeBody held = Body();
            FakeSoftBody soft = SoftBody();
            soft.Anchors.Add(new FakeSoftBodyAnchor(held));

            Assert.Equal(
                new object[] { held },
                Pointed(ElementKinds.SoftBody, ElementKinds.Body, soft).ToArray());
        }

        [Fact]
        public void AnEmptyPortIsNotTakenAsPointingAtAnything()
        {
            Vertices(1);
            Bones(1);
            Material(new FakeFace());
            Morph(MorphKind.Vertex, new FakeVertexMorphOffset());
            Node(new FakeBoneNodeItem());
            Body();
            Joint();
            SoftBody().Anchors.Add(new FakeSoftBodyAnchor());

            Assert.All(
                ReferenceEdges.All,
                edge =>
                {
                    List<object> met = new List<object>();
                    edge.Walk(_model, (referrer, target, weight) => met.Add(target));
                    Assert.Empty(met);
                });
        }

        [Fact]
        public void EveryEdgeOfTheTableTakesAwayThePortThatPointsAtWhatLeft()
        {
            Strayed();
            Assert.All(ReferenceEdges.All, edge => Assert.True(
                Outside(edge).Count > 0, edge.ReferrerKind + "->" + edge.TargetKind));

            foreach (ReferenceEdge edge in ReferenceEdges.All)
            {
                edge.Mend(
                    _model,
                    ReferenceCleanup.Held(ReferenceEdges.Listed(_model, edge.TargetKind)));
            }

            Assert.All(ReferenceEdges.All, edge => Assert.True(
                Outside(edge).Count == 0, edge.ReferrerKind + "->" + edge.TargetKind));
        }

        private IList<object> Outside(ReferenceEdge edge)
        {
            IList<object> listed = ReferenceEdges.Listed(_model, edge.TargetKind);
            List<object> met = new List<object>();
            edge.Walk(_model, (referrer, target, weight) =>
            {
                if (!listed.Contains(target, ReferenceComparer<object>.Instance))
                {
                    met.Add(target);
                }
            });

            return met;
        }

        private void Strayed()
        {
            IPXVertex vertex = new FakeVertex(9f, 9f, 9f);
            IPXBone bone = new FakeBone("居ないボーン");
            FakeMaterial material = new FakeMaterial("居ない材質");
            FakeMorph morph = new FakeMorph("居ないモーフ", MorphKind.Vertex);
            FakeBody body = new FakeBody("居ない剛体");

            Material(new FakeFace(vertex, vertex, vertex));
            Morph(MorphKind.Vertex, new FakeVertexMorphOffset(vertex));
            Morph(MorphKind.UV, new FakeUVMorphOffset(vertex));
            Morph(MorphKind.Material, new FakeMaterialMorphOffset(material));
            Morph(MorphKind.Bone, new FakeBoneMorphOffset(bone));
            Morph(MorphKind.Group, new FakeGroupMorphOffset(morph));
            Morph(MorphKind.Impulse, new FakeImpulseMorphOffset(body));
            Node(new FakeBoneNodeItem(bone), new FakeMorphNodeItem(morph));
            IPXVertex listed = Weighted(bone, 1f);
            IPXBone rigged = Bones(1)[0];
            Rigged(rigged, bone);
            rigged.IsIK = true;
            FakeBody held = Body();
            held.Bone = bone;
            FakeJoint joint = Joint();
            joint.BodyA = body;
            joint.BodyB = body;
            FakeSoftBody soft = SoftBody();
            soft.Material = material;
            soft.Pins.Add(vertex);
            soft.Anchors.Add(new FakeSoftBodyAnchor(held, vertex));
            soft.Anchors.Add(new FakeSoftBodyAnchor(body, listed));
        }

        [Fact]
        public void AnIkTargetThatLeftIsEmptiedEvenWhenTheBoneIsNotAnIk()
        {
            IPXBone bone = Bones(1)[0];
            bone.IK.Target = new FakeBone("居ないボーン");

            int repaired = Edge(ElementKinds.Bone, ElementKinds.Bone)
                .Mend(_model, ReferenceCleanup.Held(_model.Bone.Cast<object>()));

            Assert.False(bone.IsIK);
            Assert.Null(bone.IK.Target);
            Assert.Equal(1, repaired);
        }

        [Fact]
        public void AnEdgeThatReachesThroughAnotherElementCannotBeRetargeted()
        {
            Assert.False(Edge(ElementKinds.Material, ElementKinds.Vertex).CanRetarget);
            Assert.True(Edge(ElementKinds.Face, ElementKinds.Vertex).CanRetarget);
        }

        [Fact]
        public void FacesAreListedByTheRunningNumberAcrossMaterials()
        {
            IList<IPXVertex> corners = Vertices(3);
            IPXFace first = new FakeFace(corners[0], corners[1], corners[2]);
            IPXFace second = new FakeFace(corners[2], corners[1], corners[0]);
            Material(first);
            Material(second);

            Assert.Equal(
                new object[] { first, second },
                ReferenceEdges.Listed(_model, ElementKinds.Face).ToArray());
        }

        [Fact]
        public void OnlyWhatPointsAtTheGivenVertexIsGathered()
        {
            Wired();

            Gathered(
                ElementKinds.Vertex,
                1,
                Expected(ElementKinds.Face, 0),
                Expected(ElementKinds.Material, 0),
                Expected(ElementKinds.Morph, 0),
                Expected(ElementKinds.SoftBody, 0));
        }

        [Fact]
        public void OnlyWhatPointsAtTheGivenMaterialIsGathered()
        {
            Wired();

            Gathered(
                ElementKinds.Material,
                1,
                Expected(ElementKinds.Morph, 1),
                Expected(ElementKinds.SoftBody, 0));
        }

        [Fact]
        public void OnlyWhatPointsAtTheGivenBoneIsGathered()
        {
            Wired();

            Gathered(
                ElementKinds.Bone,
                1,
                Expected(ElementKinds.Vertex, 0),
                Expected(ElementKinds.Bone, 2),
                Expected(ElementKinds.Morph, 2),
                Expected(ElementKinds.Node, 0),
                Expected(ElementKinds.Body, 0));
        }

        [Fact]
        public void OnlyWhatPointsAtTheGivenMorphIsGathered()
        {
            Wired();

            Gathered(
                ElementKinds.Morph,
                5,
                Expected(ElementKinds.Morph, 3),
                Expected(ElementKinds.Node, 0));
        }

        [Fact]
        public void OnlyWhatPointsAtTheGivenBodyIsGathered()
        {
            Wired();

            Gathered(
                ElementKinds.Body,
                1,
                Expected(ElementKinds.Joint, 0),
                Expected(ElementKinds.Morph, 4),
                Expected(ElementKinds.SoftBody, 0));
        }

        [Fact]
        public void PointingAtAnyOneOfTheGivenTargetsIsFoldedIntoOneSet()
        {
            IList<IPXBone> bones = Bones(2);
            Weighted(bones[0], 1f);
            Weighted(bones[1], 1f);

            ReferrerSets folded = ReferenceEdges.Union(
                _model, ElementKinds.Bone, new[] { 0, 1 }, 0f);

            Assert.Equal(new[] { 0, 1 }, folded.Of(ElementKinds.Vertex).ToArray());
            Assert.Empty(folded.Of(ElementKinds.Node));
        }

        [Fact]
        public void EachGivenTargetKeepsItsOwnSetInTheGivenOrder()
        {
            IList<IPXBone> bones = Bones(2);
            Weighted(bones[0], 1f);
            Weighted(bones[1], 1f);

            IList<ReferrerSets> each = ReferenceEdges.PerTarget(
                _model, ElementKinds.Bone, new[] { 1, 0 }, 0f);

            Assert.Equal(new[] { 1 }, each[0].Of(ElementKinds.Vertex).ToArray());
            Assert.Equal(new[] { 0 }, each[1].Of(ElementKinds.Vertex).ToArray());
        }

        [Fact]
        public void ASlotWhoseWeightDoesNotPassTheThresholdIsNotCounted()
        {
            IList<IPXBone> bones = Bones(2);
            IPXVertex vertex = Weighted(bones[0], 0.9f);
            vertex.Bone2 = bones[1];
            vertex.Weight2 = 0.1f;

            Assert.Equal(
                new[] { 0 },
                ReferenceEdges.Union(_model, ElementKinds.Bone, new[] { 0 }, 0.5f)
                    .Of(ElementKinds.Vertex)
                    .ToArray());
            Assert.Empty(
                ReferenceEdges.Union(_model, ElementKinds.Bone, new[] { 1 }, 0.5f)
                    .Of(ElementKinds.Vertex));
        }

        [Fact]
        public void ASlotHoldingABoneWithoutWeightIsNotCountedByDefault()
        {
            IList<IPXBone> bones = Bones(1);
            Weighted(bones[0], 0f);

            Assert.Empty(
                ReferenceEdges.Union(_model, ElementKinds.Bone, new[] { 0 }, 0f)
                    .Of(ElementKinds.Vertex));
        }

        [Fact]
        public void AFrameOutsideTheListedFramesIsNotCounted()
        {
            IList<IPXBone> bones = Bones(1);
            _model.ExpressionNode.Items.Add(new FakeBoneNodeItem(bones[0]));

            Assert.Empty(
                ReferenceEdges.Union(_model, ElementKinds.Bone, new[] { 0 }, 0f)
                    .Of(ElementKinds.Node));
        }

        [Fact]
        public void RetargetingAVertexMovesEveryPortThatPointedAtIt()
        {
            Wired();
            _model.Vertex.Add(new FakeVertex(9f, 9f, 9f));
            IPXVertex made = _model.Vertex[2];

            Moved(ElementKinds.Vertex, _model.Vertex[Held], made);

            IPXFace face = _model.Material[0].Faces[0];
            Assert.Same(made, face.Vertex1);
            Assert.Same(made, face.Vertex2);
            Assert.Same(made, face.Vertex3);
            Assert.Same(made, ((IPXVertexMorphOffset)_model.Morph[0].Offsets[0]).Vertex);
            Assert.Same(made, ((IPXUVMorphOffset)_model.Morph[0].Offsets[1]).Vertex);
            Assert.Same(made, _model.SoftBody[0].Pins[0]);
            Assert.Same(made, _model.SoftBody[0].Anchors[0].Vertex);
            Gathered(
                ElementKinds.Vertex,
                Held,
                2,
                Expected(ElementKinds.Face, 0),
                Expected(ElementKinds.Material, 0),
                Expected(ElementKinds.Morph, 0),
                Expected(ElementKinds.SoftBody, 0));
        }

        [Fact]
        public void RetargetingAMaterialMovesEveryPortThatPointedAtIt()
        {
            Wired();
            FakeMaterial made = Material();

            Moved(ElementKinds.Material, _model.Material[Held], made);

            Assert.Same(made, ((IPXMaterialMorphOffset)_model.Morph[1].Offsets[0]).Material);
            Assert.Same(made, _model.SoftBody[0].Material);
            Gathered(
                ElementKinds.Material,
                Held,
                2,
                Expected(ElementKinds.Morph, 1),
                Expected(ElementKinds.SoftBody, 0));
        }

        [Fact]
        public void RetargetingABoneMovesEveryPortThatPointedAtIt()
        {
            Wired();
            IPXBone made = new FakeBone("移した先");
            _model.Bone.Add(made);

            Moved(ElementKinds.Bone, _model.Bone[Held], made);

            IPXVertex vertex = _model.Vertex[0];
            Assert.Same(made, vertex.Bone1);
            Assert.Same(made, vertex.Bone2);
            Assert.Same(made, vertex.Bone3);
            Assert.Same(made, vertex.Bone4);
            IPXBone bone = _model.Bone[2];
            Assert.Same(made, bone.Parent);
            Assert.Same(made, bone.ToBone);
            Assert.Same(made, bone.AppendParent);
            Assert.Same(made, bone.IK.Target);
            Assert.Same(made, bone.IK.Links[0].Bone);
            Assert.Same(made, ((IPXBoneMorphOffset)_model.Morph[2].Offsets[0]).Bone);
            Assert.Same(made, _model.Node[0].Items[0].BoneItem.Bone);
            Assert.Same(made, _model.Body[0].Bone);
            Gathered(
                ElementKinds.Bone,
                Held,
                3,
                Expected(ElementKinds.Vertex, 0),
                Expected(ElementKinds.Bone, 2),
                Expected(ElementKinds.Morph, 2),
                Expected(ElementKinds.Node, 0),
                Expected(ElementKinds.Body, 0));
        }

        [Fact]
        public void RetargetingAMorphMovesEveryPortThatPointedAtIt()
        {
            Wired();
            FakeMorph made = Morph(MorphKind.Vertex);

            Moved(ElementKinds.Morph, _model.Morph[Held], made);

            Assert.Same(made, ((IPXGroupMorphOffset)_model.Morph[3].Offsets[0]).Morph);
            Assert.Same(made, _model.Node[0].Items[1].MorphItem.Morph);
            Gathered(
                ElementKinds.Morph,
                Held,
                6,
                Expected(ElementKinds.Morph, 3),
                Expected(ElementKinds.Node, 0));
        }

        [Fact]
        public void RetargetingABodyMovesEveryPortThatPointedAtIt()
        {
            Wired();
            FakeBody made = Body();

            Moved(ElementKinds.Body, _model.Body[Held], made);

            Assert.Same(made, _model.Joint[0].BodyA);
            Assert.Same(made, _model.Joint[0].BodyB);
            Assert.Same(made, ((IPXImpulseMorphOffset)_model.Morph[4].Offsets[0]).Body);
            Assert.Same(made, _model.SoftBody[0].Anchors[0].Body);
            Gathered(
                ElementKinds.Body,
                Held,
                2,
                Expected(ElementKinds.Joint, 0),
                Expected(ElementKinds.Morph, 4),
                Expected(ElementKinds.SoftBody, 0));
        }

        [Fact]
        public void RetargetingLeavesAnEmptyPortEmpty()
        {
            Wired();
            FakeJoint empty = Joint();

            Moved(ElementKinds.Body, _model.Body[Held], Body());

            Assert.Null(empty.BodyA);
            Assert.Null(empty.BodyB);
        }

        [Fact]
        public void RetargetingLeavesAMaterialMorphOffsetWithoutAMaterialEmpty()
        {
            Wired();
            FakeMorph whole = Morph(MorphKind.Material, new FakeMaterialMorphOffset());
            Material();

            Moved(ElementKinds.Material, _model.Material[Held], _model.Material[2]);

            Assert.Equal(
                _model.Material.Cast<object>().ToArray(),
                Pointed(ElementKinds.Morph, ElementKinds.Material, whole).ToArray());
        }

        private static KeyValuePair<string, int> Expected(string referrerKind, int at)
        {
            return new KeyValuePair<string, int>(referrerKind, at);
        }

        /// <summary>
        /// 指されている位置で挙がるのが <paramref name="expected"/> だけで、
        /// <paramref name="loose"/> の位置では何も挙がらないことを、まとめて引く向きと1つずつ引く
        /// 向きの両方で見る。
        /// </summary>
        private void Gathered(
            string targetKind, int loose, params KeyValuePair<string, int>[] expected)
        {
            Gathered(targetKind, loose, Held, expected);
        }

        private void Gathered(
            string targetKind,
            int loose,
            int held,
            params KeyValuePair<string, int>[] expected)
        {
            Fitted(ReferenceEdges.Union(_model, targetKind, new[] { held }, 0f), expected);
            Empty(ReferenceEdges.Union(_model, targetKind, new[] { loose }, 0f));
            IList<ReferrerSets> each = ReferenceEdges.PerTarget(
                _model, targetKind, new[] { loose, held }, 0f);
            Assert.Equal(2, each.Count);
            Empty(each[0]);
            Fitted(each[1], expected);
        }

        private static void Fitted(ReferrerSets found, KeyValuePair<string, int>[] expected)
        {
            Assert.All(expected, pair => Assert.Contains(pair.Key, found.Kinds));
            foreach (string kind in found.Kinds)
            {
                Assert.Equal(
                    expected.Where(pair => pair.Key == kind).Select(pair => pair.Value).ToArray(),
                    found.Of(kind).ToArray());
            }
        }

        private static void Empty(ReferrerSets found)
        {
            Assert.All(found.Kinds, kind => Assert.Empty(found.Of(kind)));
        }

        /// <summary>その種類を指している口を、すべて別の相手へ付け替える。</summary>
        private void Moved(string targetKind, object from, object to)
        {
            foreach (ReferenceEdge edge in ReferenceEdges.Into(targetKind))
            {
                edge.Retarget(_model, held => ReferenceEquals(held, from) ? to : held);
            }
        }

        /// <summary>
        /// 16本の辺の口がすべて埋まった題材を組む。指される側はどの種類も位置0の1つだけで、同じ
        /// 種類のほかの要素はどの口からも指されない。
        /// </summary>
        private void Wired()
        {
            IList<IPXVertex> vertices = Vertices(2);
            IList<IPXBone> bones = Bones(3);
            FakeMaterial material = Material(
                new FakeFace(vertices[0], vertices[0], vertices[0]));
            Material();
            FakeMorph morph = Morph(
                MorphKind.Vertex,
                new FakeVertexMorphOffset(vertices[0]),
                new FakeUVMorphOffset(vertices[0]));
            Morph(MorphKind.Material, new FakeMaterialMorphOffset(material));
            Morph(MorphKind.Bone, new FakeBoneMorphOffset(bones[0]));
            Morph(MorphKind.Group, new FakeGroupMorphOffset(morph));
            FakeBody body = Body();
            Body();
            Morph(MorphKind.Impulse, new FakeImpulseMorphOffset(body));
            Morph(MorphKind.Vertex);
            Node(new FakeBoneNodeItem(bones[0]), new FakeMorphNodeItem(morph));
            Node();
            body.Bone = bones[0];
            Rigged(bones[2], bones[0]);
            Slotted(vertices[0], bones[0]);
            FakeJoint joint = Joint();
            joint.BodyA = body;
            joint.BodyB = body;
            FakeSoftBody soft = SoftBody();
            soft.Material = material;
            soft.Pins.Add(vertices[0]);
            soft.Anchors.Add(new FakeSoftBodyAnchor(body, vertices[0]));
        }

        /// <summary>そのボーンが持つボーンの口を、すべて同じ相手で埋める。</summary>
        private static void Rigged(IPXBone bone, IPXBone held)
        {
            bone.Parent = held;
            bone.ToBone = held;
            bone.AppendParent = held;
            bone.IK.Target = held;
            bone.IK.Links.Add(new FakeIkLink(held));
        }

        /// <summary>その頂点が持つ4つの枠を、すべて同じボーンで埋める。</summary>
        private static void Slotted(IPXVertex vertex, IPXBone held)
        {
            vertex.Bone1 = held;
            vertex.Bone2 = held;
            vertex.Bone3 = held;
            vertex.Bone4 = held;
            vertex.Weight1 = 0.25f;
            vertex.Weight2 = 0.25f;
            vertex.Weight3 = 0.25f;
            vertex.Weight4 = 0.25f;
        }

        private ReferenceEdge Edge(string referrerKind, string targetKind)
        {
            return ReferenceEdges.All.Single(
                edge => edge.ReferrerKind == referrerKind && edge.TargetKind == targetKind);
        }

        private IList<object> Pointed(string referrerKind, string targetKind, object referrer)
        {
            List<object> met = new List<object>();
            Edge(referrerKind, targetKind).Walk(
                _model,
                (held, target, weight) =>
                {
                    if (ReferenceEquals(held, referrer))
                    {
                        met.Add(target);
                    }
                });

            return met;
        }

        private IList<IPXVertex> Vertices(int count)
        {
            List<IPXVertex> made = new List<IPXVertex>();
            for (int at = 0; at < count; at++)
            {
                IPXVertex vertex = new FakeVertex(at, 0f, 0f);
                _model.Vertex.Add(vertex);
                made.Add(vertex);
            }

            return made;
        }

        private IList<IPXBone> Bones(int count)
        {
            List<IPXBone> made = new List<IPXBone>();
            for (int at = 0; at < count; at++)
            {
                IPXBone bone = new FakeBone("ボーン" + at);
                _model.Bone.Add(bone);
                made.Add(bone);
            }

            return made;
        }

        private IPXVertex Weighted(IPXBone bone, float weight)
        {
            IPXVertex vertex = new FakeVertex(0f, 0f, 0f);
            vertex.Bone1 = bone;
            vertex.Weight1 = weight;
            _model.Vertex.Add(vertex);

            return vertex;
        }

        private FakeMaterial Material(params IPXFace[] faces)
        {
            FakeMaterial made = new FakeMaterial("材質" + _model.Material.Count);
            foreach (IPXFace face in faces)
            {
                made.Faces.Add(face);
            }

            _model.Material.Add(made);

            return made;
        }

        private FakeMorph Morph(MorphKind kind, params IPXMorphOffset[] offsets)
        {
            FakeMorph made = new FakeMorph("モーフ" + _model.Morph.Count, kind);
            foreach (IPXMorphOffset offset in offsets)
            {
                made.Offsets.Add(offset);
            }

            _model.Morph.Add(made);

            return made;
        }

        private FakeNode Node(params IPXNodeItem[] items)
        {
            FakeNode made = new FakeNode("枠" + _model.Node.Count);
            foreach (IPXNodeItem item in items)
            {
                made.Items.Add(item);
            }

            _model.Node.Add(made);

            return made;
        }

        private FakeBody Body()
        {
            FakeBody made = new FakeBody("剛体" + _model.Body.Count);
            _model.Body.Add(made);

            return made;
        }

        private FakeJoint Joint()
        {
            FakeJoint made = new FakeJoint("Joint" + _model.Joint.Count);
            _model.Joint.Add(made);

            return made;
        }

        private FakeSoftBody SoftBody()
        {
            FakeSoftBody made = new FakeSoftBody("SoftBody" + _model.SoftBody.Count);
            _model.SoftBody.Add(made);

            return made;
        }
    }
}
