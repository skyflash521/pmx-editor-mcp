using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ViewPickScreenPointTests : IDisposable
    {
        private const int Digits = 3;

        private const int Side = 200;

        private readonly ComposedScreenFixture _fixture = new ComposedScreenFixture();

        public ViewPickScreenPointTests()
        {
            _fixture.View.ViewMatrix = new M { M43 = 10f };
            _fixture.View.ProjectionMatrix = new M
            {
                M11 = 1f,
                M22 = 1f,
                M33 = 100f / 99f,
                M34 = 1f,
                M43 = -100f / 99f,
                M44 = 0f,
            };
        }

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void ThePixelAtTheMiddleOfTheImageHitsTheFaceInFrontOfTheViewpoint()
        {
            Layer(0f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            IDictionary<string, object> value = Pick(100, 100);

            Assert.Equal(1, value[ViewPickScreenPoint.CountName]);
            IDictionary<string, object> hit = Hits(value).Single();
            Assert.Equal(0, hit[ViewPickScreenPoint.MaterialName]);
            Assert.Equal(0, hit[ViewPickScreenPoint.FaceName]);
            Assert.Equal(true, hit[ViewPickScreenPoint.FrontFacingName]);
            Near(9.0, hit[ViewPickScreenPoint.DistanceName]);
            AssertNear(new[] { 0.0, 0.0, 0.0 }, hit[ViewPickScreenPoint.PointName]);
        }

        [Fact]
        public void TheNearestCornerOfTheHitFaceComesBackWithItsPlaceOnTheImage()
        {
            Layer(0f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            IDictionary<string, object> hit = Hits(Pick(100, 100)).Single();

            Assert.Equal(0, hit[ViewPickScreenPoint.VertexName]);
            Near(2.0, hit[ViewPickScreenPoint.VertexDistanceName]);
            AssertNear(new[] { 100.0, 80.0 }, hit[ViewPickScreenPoint.VertexScreenName]);
            Assert.Equal(new object[] { 0, 1, 2 }, (object[])hit[ViewPickScreenPoint.VerticesName]);
        }

        [Fact]
        public void ANearestCornerBehindTheViewpointComesBackWithoutAPlaceOnTheImage()
        {
            Solid(-1f, -1f, -15f, 1f, -1f, -15f, 0f, 3f, 60f);

            IDictionary<string, object> hit = Hits(Pick(100, 100)).Single();

            Assert.Equal(0, hit[ViewPickScreenPoint.VertexName]);
            Assert.False(hit.ContainsKey(ViewPickScreenPoint.VertexScreenName));
        }

        [Fact]
        public void ANearestCornerNearerThanTheNearPlaneOfAnOrthographicViewComesBackWithoutAPlaceOnTheImage()
        {
            _fixture.View.ProjectionMatrix = new M
            {
                M11 = 0.5f,
                M22 = 0.5f,
                M33 = 1f / 99f,
                M43 = -1f / 99f,
                M44 = 1f,
            };
            Solid(-1f, -1f, -15f, 1f, -1f, -15f, 0f, 3f, 60f);

            IDictionary<string, object> hit = Hits(Pick(100, 100)).Single();

            Assert.Equal(0, hit[ViewPickScreenPoint.VertexName]);
            Assert.False(hit.ContainsKey(ViewPickScreenPoint.VertexScreenName));
        }

        [Fact]
        public void ANearestCornerInFrontOfTheNearPlaneOfAnOrthographicViewComesBackWithItsPlaceOnTheImage()
        {
            _fixture.View.ProjectionMatrix = new M
            {
                M11 = 0.5f,
                M22 = 0.5f,
                M33 = 1f / 99f,
                M43 = -1f / 99f,
                M44 = 1f,
            };
            Layer(0f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            IDictionary<string, object> hit = Hits(Pick(100, 100)).Single();

            Assert.True(hit.ContainsKey(ViewPickScreenPoint.VertexScreenName));
        }

        [Fact]
        public void ThePixelIsMeasuredFromTheTopLeftCornerOfTheImageWithYGoingDown()
        {
            Layer(0f, 0, 1, 2, false, Triangle(0f, 100f, 100f, -100f, -100f, -100f));

            IDictionary<string, object> hit = Hits(Pick(150, 50)).Single();

            AssertNear(new[] { 5.0, 5.0, 0.0 }, hit[ViewPickScreenPoint.PointName]);
        }

        [Fact]
        public void TheImageMayBeAnySizeAsLongAsThePixelIsMeasuredInIt()
        {
            Layer(0f, 0, 1, 2, false, Triangle(0f, 100f, 100f, -100f, -100f, -100f));

            IDictionary<string, object> hit = Hits(Pick(75, 25, 100, 100)).Single();

            AssertNear(new[] { 5.0, 5.0, 0.0 }, hit[ViewPickScreenPoint.PointName]);
        }

        [Fact]
        public void APixelThatNoFaceCoversReturnsNoHit()
        {
            Layer(0f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            IDictionary<string, object> value = Pick(10, 10);

            Assert.Equal(0, value[ViewPickScreenPoint.CountName]);
            Assert.Empty(Hits(value));
        }

        [Fact]
        public void WhenFacesOverlapTheNearestOneToTheViewpointComesFirst()
        {
            Layer(0f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));
            Layer(-1f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            IDictionary<string, object> hit = Hits(Pick(100, 100)).Single();

            Assert.Equal(1, hit[ViewPickScreenPoint.MaterialName]);
            Near(8.0, hit[ViewPickScreenPoint.DistanceName]);
        }

        [Fact]
        public void ALimitAboveOneListsTheFacesBehindInTheOrderTheViewpointMeetsThem()
        {
            Layer(0f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));
            Layer(-1f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            IList<IDictionary<string, object>> hits = Hits(Pick(
                100, 100, Given(ViewPickScreenPoint.LimitName, 5)));

            Assert.Equal(new object[] { 1, 0 }, hits.Select(h => h[ViewPickScreenPoint.MaterialName]));
        }

        [Fact]
        public void TheFaceNumberCountsAcrossMaterials()
        {
            Layer(0f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));
            Layer(-1f, 0, 1, 2, false, Triangle(20f, 22f, 22f, 18f, 18f, 18f));
            Layer(-2f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            IDictionary<string, object> hit = Hits(Pick(
                100, 100, Given(ViewPickScreenPoint.MaterialIndicesName, new object[] { 2 }))).Single();

            Assert.Equal(2, hit[ViewPickScreenPoint.MaterialName]);
            Assert.Equal(2, hit[ViewPickScreenPoint.FaceName]);
        }

        [Fact]
        public void MaterialIndicesLeaveTheOtherMaterialsOutOfTheSearch()
        {
            Layer(0f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));
            Layer(-1f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            IDictionary<string, object> hit = Hits(Pick(
                100, 100, Given(ViewPickScreenPoint.MaterialIndicesName, new object[] { 0 }))).Single();

            Assert.Equal(0, hit[ViewPickScreenPoint.MaterialName]);
        }

        [Fact]
        public void AFaceSeenFromItsBackIsSkippedWhenTheMaterialIsNotDrawnOnBothSides()
        {
            Layer(0f, 0, 2, 1, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            Assert.Equal(0, Pick(100, 100)[ViewPickScreenPoint.CountName]);
        }

        [Fact]
        public void AFaceSeenFromItsBackIsHitWhenTheMaterialIsDrawnOnBothSides()
        {
            Layer(0f, 0, 2, 1, true, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            IDictionary<string, object> hit = Hits(Pick(100, 100)).Single();

            Assert.Equal(false, hit[ViewPickScreenPoint.FrontFacingName]);
        }

        [Fact]
        public void AFaceBetweenTheViewpointAndTheNearPlaneIsNotHit()
        {
            Layer(-9.5f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            Assert.Equal(0, Pick(100, 100)[ViewPickScreenPoint.CountName]);
        }

        [Fact]
        public void AFaceBeyondTheFarPlaneIsNotHit()
        {
            Layer(95f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            Assert.Equal(0, Pick(100, 100)[ViewPickScreenPoint.CountName]);
        }

        [Fact]
        public void TheMatricesAreReadFromTheScreenThatWasAskedFor()
        {
            Layer(0f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            Pick(100, 100, Given(ViewPickScreenPoint.ScreenName, 2));

            Assert.Equal(new[] { 2, 2 }, _fixture.View.MatrixScreens);
        }

        [Fact]
        public void WithoutAScreenTheFirstOneIsRead()
        {
            Layer(0f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            Pick(100, 100);

            Assert.Equal(new[] { 0, 0 }, _fixture.View.MatrixScreens);
        }

        [Fact]
        public void APixelOutsideTheImageIsRefused()
        {
            Layer(0f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(Call(
                Given(ViewPickScreenPoint.XName, 201),
                Given(ViewPickScreenPoint.YName, 100),
                Given(ViewPickScreenPoint.ImageWidthName, Side),
                Given(ViewPickScreenPoint.ImageHeightName, Side))));
        }

        [Fact]
        public void AMissingImageSizeIsRefused()
        {
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(Call(
                Given(ViewPickScreenPoint.XName, 100),
                Given(ViewPickScreenPoint.YName, 100),
                Given(ViewPickScreenPoint.ImageWidthName, Side))));
        }

        [Fact]
        public void AnImageWithoutAreaIsRefused()
        {
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(Call(
                Given(ViewPickScreenPoint.XName, 0),
                Given(ViewPickScreenPoint.YName, 0),
                Given(ViewPickScreenPoint.ImageWidthName, 0),
                Given(ViewPickScreenPoint.ImageHeightName, Side))));
        }

        [Fact]
        public void AScreenOutsideTheFourIsRefused()
        {
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(Call(
                Given(ViewPickScreenPoint.XName, 100),
                Given(ViewPickScreenPoint.YName, 100),
                Given(ViewPickScreenPoint.ImageWidthName, Side),
                Given(ViewPickScreenPoint.ImageHeightName, Side),
                Given(ViewPickScreenPoint.ScreenName, 4))));
        }

        [Fact]
        public void AMaterialOutsideTheModelIsRefused()
        {
            Layer(0f, 0, 1, 2, false, Triangle(0f, 2f, 2f, -2f, -2f, -2f));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedScreenFixture.Code(Ask(
                100, 100, Side, Side, Given(ViewPickScreenPoint.MaterialIndicesName, new object[] { 1 }))));
        }

        [Fact]
        public void ALimitBelowOneIsRefused()
        {
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(Ask(
                100, 100, Side, Side, Given(ViewPickScreenPoint.LimitName, 0))));
        }

        private static KeyValuePair<string, object> Given(string name, object value)
        {
            return ComposedScreenFixture.Given(name, value);
        }

        private IDictionary<string, object> Call(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ViewPickScreenPoint.ToolName, ComposedScreenFixture.Arguments(given));
        }

        private IDictionary<string, object> Pick(
            int x, int y, params KeyValuePair<string, object>[] more)
        {
            return ComposedScreenFixture.Value(Ask(x, y, Side, Side, more));
        }

        private IDictionary<string, object> Pick(
            int x, int y, int width, int height, params KeyValuePair<string, object>[] more)
        {
            return ComposedScreenFixture.Value(Ask(x, y, width, height, more));
        }

        private IDictionary<string, object> Ask(
            int x, int y, int width, int height, params KeyValuePair<string, object>[] more)
        {
            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>
            {
                Given(ViewPickScreenPoint.XName, x),
                Given(ViewPickScreenPoint.YName, y),
                Given(ViewPickScreenPoint.ImageWidthName, width),
                Given(ViewPickScreenPoint.ImageHeightName, height),
            };
            given.AddRange(more);

            return Call(given.ToArray());
        }

        private static IList<IDictionary<string, object>> Hits(IDictionary<string, object> value)
        {
            return ((object[])value[ViewPickScreenPoint.HitsName])
                .Cast<IDictionary<string, object>>()
                .ToList();
        }

        private static float[] Triangle(float x0, float y0, float x1, float y1, float x2, float y2)
        {
            return new[] { x0, y0, x1, y1, x2, y2 };
        }

        private void Layer(
            float z, int first, int second, int third, bool bothDraw, float[] corners)
        {
            List<IPXVertex> made = new List<IPXVertex>();
            for (int at = 0; at < 3; at++)
            {
                FakeVertex vertex = new FakeVertex(corners[at * 2], corners[(at * 2) + 1], z);
                _fixture.Model.Vertex.Add(vertex);
                made.Add(vertex);
            }

            FakeMaterial material = new FakeMaterial("材質" + _fixture.Model.Material.Count)
            {
                BothDraw = bothDraw,
            };
            material.Faces.Add(new FakeFace(made[first], made[second], made[third]));
            _fixture.Model.Material.Add(material);
        }

        private void Solid(
            float x0, float y0, float z0, float x1, float y1, float z1, float x2, float y2, float z2)
        {
            FakeVertex first = new FakeVertex(x0, y0, z0);
            FakeVertex second = new FakeVertex(x1, y1, z1);
            FakeVertex third = new FakeVertex(x2, y2, z2);
            _fixture.Model.Vertex.Add(first);
            _fixture.Model.Vertex.Add(second);
            _fixture.Model.Vertex.Add(third);
            FakeMaterial material = new FakeMaterial("材質" + _fixture.Model.Material.Count)
            {
                BothDraw = true,
            };
            material.Faces.Add(new FakeFace(first, second, third));
            _fixture.Model.Material.Add(material);
        }

        private static void Near(double wanted, object found)
        {
            Assert.Equal(wanted, Convert.ToDouble(found), Digits);
        }

        private static void AssertNear(double[] wanted, object found)
        {
            object[] items = (object[])found;
            Assert.Equal(wanted.Length, items.Length);
            for (int at = 0; at < wanted.Length; at++)
            {
                Near(wanted[at], items[at]);
            }
        }
    }
}
