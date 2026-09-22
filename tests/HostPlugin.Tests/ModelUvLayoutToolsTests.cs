using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>指した材質の面をUVの上へ線で描いた画像を返すツール。</summary>
    public sealed class ModelUvLayoutToolsTests : IDisposable
    {
        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheEdgesOfTheNamedMaterialAreDrawnWhereTheirUvFalls()
        {
            Material(Uv(0.1f, 0.1f), Uv(0.9f, 0.1f), Uv(0.1f, 0.9f));

            using (Bitmap image = Drawn(ComposedEditFixture.Given("indices", new object[] { 0 })))
            {
                Assert.True(Inked(image, 0.5f, 0.1f, 1f));
                Assert.True(Inked(image, 0.1f, 0.5f, 1f));
                Assert.True(Inked(image, 0.5f, 0.5f, 1f));
                Assert.False(Inked(image, 0.7f, 0.7f, 1f));
            }
        }

        [Fact]
        public void TheFacesOfMaterialsThatWereNotNamedAreLeftOut()
        {
            Material(Uv(0.1f, 0.1f), Uv(0.9f, 0.1f), Uv(0.1f, 0.9f));
            Material(Uv(0.2f, 0.95f), Uv(0.9f, 0.95f), Uv(0.9f, 0.3f));

            using (Bitmap image = Drawn(ComposedEditFixture.Given("indices", new object[] { 0 })))
            {
                Assert.False(Inked(image, 0.55f, 0.95f, 1f));
            }
        }

        [Fact]
        public void TheMaterialsPickedOnTheScreenAreDrawnWhenSelectedIsGiven()
        {
            Material(Uv(0.1f, 0.1f), Uv(0.9f, 0.1f), Uv(0.1f, 0.9f));
            Material(Uv(0.2f, 0.95f), Uv(0.9f, 0.95f), Uv(0.9f, 0.3f));
            _fixture.Form.SelectedMaterials = new[] { 1 };

            using (Bitmap image = Drawn(ComposedEditFixture.Given("selected", true)))
            {
                Assert.True(Inked(image, 0.55f, 0.95f, 1f));
                Assert.False(Inked(image, 0.5f, 0.1f, 1f));
            }
        }

        [Fact]
        public void AUvBeyondOneWidensThePictureInsteadOfBeingCutOff()
        {
            Material(Uv(0.2f, 0.2f), Uv(1.8f, 0.2f), Uv(0.2f, 1.8f));

            using (Bitmap image = Drawn(ComposedEditFixture.Given("all", true)))
            {
                Assert.True(Inked(image, 1.6f, 0.2f, 1.8f));
                Assert.True(Inked(image, 0.2f, 1.6f, 1.8f));
            }
        }

        [Fact]
        public void UvsAtTheFarEndsOfTheFloatRangeAreStillDrawn()
        {
            Material(Uv(-3e38f, 0f), Uv(3e38f, 0f), Uv(0f, 1f));

            using (Bitmap image = Drawn(ComposedEditFixture.Given("all", true)))
            {
                Assert.True(Inked(image, 0.5f, 0f, 1f));
            }
        }

        [Fact]
        public void TheModelIsLeftAsItWas()
        {
            Material(Uv(0.1f, 0.1f), Uv(0.9f, 0.1f), Uv(0.1f, 0.9f));

            Drawn(ComposedEditFixture.Given("all", true)).Dispose();

            Assert.Equal(0, _fixture.Commits);
        }

        [Fact]
        public void AMaterialOutsideTheListIsRefused()
        {
            Material(Uv(0.1f, 0.1f), Uv(0.9f, 0.1f), Uv(0.1f, 0.9f));

            IDictionary<string, object> envelope = _fixture.Call(
                ModelDrawUvLayout.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("indices", new object[] { 1 })));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
        }

        /// <summary>
        /// UVの点が描かれた画像の中のどこへ落ちるか。描く範囲は0から <paramref name="span"/> までの
        /// 正方形で、Vは下へ向かって増える。その点の周りの数画素のどれかに辺の色が載っていれば真。
        /// 地と、0から1の範囲を示す枠は無彩色なので数えない。
        /// </summary>
        private static bool Inked(Bitmap image, float u, float v, float span)
        {
            int x = (int)Math.Round(u / span * (image.Width - 1));
            int y = (int)Math.Round(v / span * (image.Height - 1));
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    int px = Math.Max(0, Math.Min(image.Width - 1, x + dx));
                    int py = Math.Max(0, Math.Min(image.Height - 1, y + dy));
                    Color seen = image.GetPixel(px, py);
                    int high = Math.Max(seen.R, Math.Max(seen.G, seen.B));
                    int low = Math.Min(seen.R, Math.Min(seen.G, seen.B));
                    if (high - low > 64)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Bitmap Drawn(params KeyValuePair<string, object>[] given)
        {
            IDictionary<string, object> envelope = _fixture.Call(
                ModelDrawUvLayout.ToolName, ComposedEditFixture.Arguments(given));
            if (!Equals(envelope["ok"], true))
            {
                throw new InvalidOperationException(
                    "成功でない包み: " + ComposedEditFixture.Message(envelope));
            }

            byte[] png = Convert.FromBase64String((string)envelope["value"]);
            using (MemoryStream stream = new MemoryStream(png))
            using (Image loaded = Image.FromStream(stream))
            {
                return new Bitmap(loaded);
            }
        }

        private static V2 Uv(float u, float v)
        {
            return new V2(u, v);
        }

        private void Material(V2 first, V2 second, V2 third)
        {
            IPXVertex[] corners = new IPXVertex[3];
            V2[] uvs = { first, second, third };
            for (int at = 0; at < corners.Length; at++)
            {
                FakeVertex vertex = new FakeVertex();
                vertex.UV = uvs[at];
                _fixture.Model.Vertex.Add(vertex);
                corners[at] = vertex;
            }

            FakeMaterial material = new FakeMaterial("材質" + _fixture.Model.Material.Count);
            material.Faces.Add(new FakeFace(corners[0], corners[1], corners[2]));
            _fixture.Model.Material.Add(material);
        }
    }
}
