using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class SurfaceGeometryTests
    {
        private readonly FakePmx _model = new FakePmx();

        [Fact]
        public void APointInsideAClosedSurfaceIsInsideAndOneOutsideIsNot()
        {
            Cube(0, -1f, 1f);

            SurfaceGeometry.SurfaceTree tree = SurfaceGeometry.SurfaceTree.Of(_model, new[] { 0 });

            Assert.True(tree.IsInside(new SurfaceGeometry.Vec(0.1, 0.2, -0.3)));
            Assert.True(tree.IsInside(new SurfaceGeometry.Vec(0.9, -0.9, 0.9)));
            Assert.False(tree.IsInside(new SurfaceGeometry.Vec(2, 0, 0)));
            Assert.False(tree.IsInside(new SurfaceGeometry.Vec(0.1, 5, -0.3)));
            Assert.False(tree.IsInside(new SurfaceGeometry.Vec(-3, -3, -3)));
        }

        [Fact]
        public void TheInsideDoesNotDependOnTheWindingOrTheNormalsOfTheFaces()
        {
            Cube(0, -1f, 1f);
            foreach (IPXFace face in _model.Material[0].Faces)
            {
                IPXVertex swap = face.Vertex2;
                face.Vertex2 = face.Vertex3;
                face.Vertex3 = swap;
            }

            foreach (IPXVertex vertex in _model.Vertex)
            {
                vertex.Normal = new V3(0f, 0f, 0f);
            }

            SurfaceGeometry.SurfaceTree tree = SurfaceGeometry.SurfaceTree.Of(_model, new[] { 0 });

            Assert.True(tree.IsInside(new SurfaceGeometry.Vec(0, 0, 0)));
            Assert.False(tree.IsInside(new SurfaceGeometry.Vec(0, 0, 1.5)));
        }

        [Fact]
        public void ThePointsInsideAnInnerShellAreOutsideWhenTheOuterShellEnclosesIt()
        {
            Cube(0, -2f, 2f);
            Cube(0, -1f, 1f);

            SurfaceGeometry.SurfaceTree tree = SurfaceGeometry.SurfaceTree.Of(_model, new[] { 0 });

            Assert.True(tree.IsInside(new SurfaceGeometry.Vec(1.5, 0.2, 0.1)));
            Assert.False(tree.IsInside(new SurfaceGeometry.Vec(0, 0, 0)));
            Assert.False(tree.IsInside(new SurfaceGeometry.Vec(3, 0, 0)));
        }

        [Fact]
        public void OneRayEscapingThroughAHoleDoesNotOutvoteTheOthers()
        {
            Cube(0, -1f, 1f);
            _model.Material[0].Faces.RemoveAt(0);
            _model.Material[0].Faces.RemoveAt(0);

            SurfaceGeometry.SurfaceTree tree = SurfaceGeometry.SurfaceTree.Of(_model, new[] { 0 });

            Assert.True(tree.IsInside(new SurfaceGeometry.Vec(0, 0, 0)));
            Assert.False(tree.IsInside(new SurfaceGeometry.Vec(3, 3, 3)));
        }

        [Fact]
        public void TheInsideIsFoundInASurfaceOfManyFaces()
        {
            Cube(0, -1f, 1f);
            for (int at = 0; at < 40; at++)
            {
                Cube(0, 10f + (at * 3f), 11f + (at * 3f));
            }

            SurfaceGeometry.SurfaceTree tree = SurfaceGeometry.SurfaceTree.Of(_model, new[] { 0 });

            Assert.True(tree.IsInside(new SurfaceGeometry.Vec(0, 0, 0)));
            Assert.True(tree.IsInside(new SurfaceGeometry.Vec(10.5 + 60, 10.5 + 60, 10.5 + 60)));
            Assert.False(tree.IsInside(new SurfaceGeometry.Vec(5, 5, 5)));
        }

        [Fact]
        public void TheNeighboursOfEachVertexAreTheOnesSharingAFaceInAscendingOrder()
        {
            IPXVertex[] corners = { Vertex(0, 0, 0), Vertex(1, 0, 0), Vertex(1, 0, 1), Vertex(0, 0, 1) };
            Vertex(5, 5, 5);
            FakeMaterial quad = new FakeMaterial("板");
            quad.Faces.Add(new FakeFace(corners[0], corners[1], corners[2]));
            quad.Faces.Add(new FakeFace(corners[0], corners[2], corners[3]));
            _model.Material.Add(quad);

            IList<int>[] around = SurfaceGeometry.Neighbours(_model, new[] { 0 });

            Assert.Equal(5, around.Length);
            Assert.Equal(new[] { 1, 2, 3 }, around[0]);
            Assert.Equal(new[] { 0, 2 }, around[1]);
            Assert.Equal(new[] { 0, 1, 3 }, around[2]);
            Assert.Equal(new[] { 0, 2 }, around[3]);
            Assert.Empty(around[4]);
        }

        [Fact]
        public void OnlyTheFacesOfTheGivenMaterialsMakeNeighbours()
        {
            IPXVertex[] corners = { Vertex(0, 0, 0), Vertex(1, 0, 0), Vertex(0, 0, 1), Vertex(1, 0, 1) };
            FakeMaterial first = new FakeMaterial("一");
            first.Faces.Add(new FakeFace(corners[0], corners[1], corners[2]));
            FakeMaterial second = new FakeMaterial("二");
            second.Faces.Add(new FakeFace(corners[1], corners[2], corners[3]));
            _model.Material.Add(first);
            _model.Material.Add(second);

            IList<int>[] around = SurfaceGeometry.Neighbours(_model, new[] { 1, 1 });

            Assert.Empty(around[0]);
            Assert.Equal(new[] { 2, 3 }, around[1]);
            Assert.Equal(new[] { 1, 3 }, around[2]);
            Assert.Equal(new[] { 1, 2 }, around[3]);
        }

        [Fact]
        public void VerticesWithinTheThresholdFormGroupsAndLoneVerticesAreLeftOut()
        {
            List<SurfaceGeometry.Vec> spots = Spots(
                new[] { 0.0, 0.0, 0.0 },
                new[] { 0.0004, 0.0, 0.0 },
                new[] { 1.0, 1.0, 1.0 },
                new[] { 1.0, 1.0, 1.0004 },
                new[] { 1.0, 1.0004, 1.0 },
                new[] { 9.0, 9.0, 9.0 });

            IList<int[]> groups = SurfaceGeometry.Coincident(spots, Enumerable.Range(0, 6), 0.001);

            Assert.Equal(2, groups.Count);
            Assert.Equal(new[] { 0, 1 }, groups[0]);
            Assert.Equal(new[] { 2, 3, 4 }, groups[1]);
        }

        [Fact]
        public void AChainOfVerticesEachWithinTheThresholdOfTheNextIsOneGroup()
        {
            List<SurfaceGeometry.Vec> spots = Spots(
                new[] { 0.0, 0.0, 0.0 },
                new[] { 0.0009, 0.0, 0.0 },
                new[] { 0.0018, 0.0, 0.0 },
                new[] { 0.0027, 0.0, 0.0 });

            IList<int[]> groups = SurfaceGeometry.Coincident(spots, new[] { 3, 2, 1, 0 }, 0.001);

            Assert.Single(groups);
            Assert.Equal(new[] { 0, 1, 2, 3 }, groups[0]);
        }

        [Fact]
        public void AGapExactlyAtTheThresholdJoinsAndOneBeyondItDoesNot()
        {
            List<SurfaceGeometry.Vec> spots = Spots(
                new[] { 0.0, 0.0, 0.0 },
                new[] { 0.5, 0.0, 0.0 },
                new[] { 2.0, 0.0, 0.0 },
                new[] { 2.5001, 0.0, 0.0 });

            IList<int[]> groups = SurfaceGeometry.Coincident(spots, Enumerable.Range(0, 4), 0.5);

            Assert.Single(groups);
            Assert.Equal(new[] { 0, 1 }, groups[0]);
        }

        [Fact]
        public void VerticesOnEitherSideOfACellBorderAreStillGrouped()
        {
            List<SurfaceGeometry.Vec> spots = Spots(
                new[] { -0.0001, 0.0, 0.0 },
                new[] { 0.0001, 0.0, 0.0 },
                new[] { 0.0, -0.0001, 0.0 });

            IList<int[]> groups = SurfaceGeometry.Coincident(spots, Enumerable.Range(0, 3), 0.001);

            Assert.Single(groups);
            Assert.Equal(new[] { 0, 1, 2 }, groups[0]);
        }

        [Fact]
        public void AThresholdOfZeroGroupsOnlyVerticesAtTheSameSpot()
        {
            List<SurfaceGeometry.Vec> spots = Spots(
                new[] { 1.0, 2.0, 3.0 },
                new[] { 1.0, 2.0, 3.0 },
                new[] { 1.0, 2.0, 3.0000001 });

            IList<int[]> groups = SurfaceGeometry.Coincident(spots, Enumerable.Range(0, 3), 0.0);

            Assert.Single(groups);
            Assert.Equal(new[] { 0, 1 }, groups[0]);
        }

        [Fact]
        public void OnlyTheGivenIndicesAreGrouped()
        {
            List<SurfaceGeometry.Vec> spots = Spots(
                new[] { 0.0, 0.0, 0.0 },
                new[] { 0.0, 0.0, 0.0 },
                new[] { 0.0, 0.0, 0.0 });

            IList<int[]> groups = SurfaceGeometry.Coincident(spots, new[] { 2, 0, 2 }, 0.001);

            Assert.Single(groups);
            Assert.Equal(new[] { 0, 2 }, groups[0]);
        }

        [Fact]
        public void DistantVerticesFormNoGroup()
        {
            List<SurfaceGeometry.Vec> spots = new List<SurfaceGeometry.Vec>();
            for (int at = 0; at < 20000; at++)
            {
                spots.Add(new SurfaceGeometry.Vec(at % 100, (at / 100) % 100, at / 10000 * 50.0));
            }

            IList<int[]> groups = SurfaceGeometry.Coincident(spots, Enumerable.Range(0, spots.Count), 0.1);

            Assert.Empty(groups);
        }

        private static List<SurfaceGeometry.Vec> Spots(params double[][] spots)
        {
            return spots.Select(spot => new SurfaceGeometry.Vec(spot[0], spot[1], spot[2])).ToList();
        }

        private IPXVertex Vertex(float x, float y, float z)
        {
            FakeVertex made = new FakeVertex(x, y, z);
            _model.Vertex.Add(made);

            return made;
        }

        private void Cube(int material, float low, float high)
        {
            while (_model.Material.Count <= material)
            {
                _model.Material.Add(new FakeMaterial("箱"));
            }

            IPXVertex[] corners = new IPXVertex[8];
            for (int at = 0; at < 8; at++)
            {
                corners[at] = Vertex(
                    (at & 1) == 0 ? low : high, (at & 2) == 0 ? low : high, (at & 4) == 0 ? low : high);
            }

            int[][] quads =
            {
                new[] { 0, 1, 3, 2 }, new[] { 4, 6, 7, 5 }, new[] { 0, 4, 5, 1 },
                new[] { 2, 3, 7, 6 }, new[] { 0, 2, 6, 4 }, new[] { 1, 5, 7, 3 },
            };
            foreach (int[] quad in quads)
            {
                _model.Material[material].Faces.Add(new FakeFace(corners[quad[0]], corners[quad[1]], corners[quad[2]]));
                _model.Material[material].Faces.Add(new FakeFace(corners[quad[0]], corners[quad[2]], corners[quad[3]]));
            }
        }
    }
}
