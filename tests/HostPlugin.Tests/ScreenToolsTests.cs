using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmd;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 画面の選択と表示、リストの作り直し。どれも1回の呼び出しで済み、モデルの中身は変えない。
    /// </summary>
    public sealed class ScreenToolsTests : IDisposable
    {
        /// <summary>小数の突き合わせで見る桁。</summary>
        private const int Digits = 4;

        private readonly ComposedScreenFixture _fixture = new ComposedScreenFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact(Skip = "impl pending: その種類の要素を全部選ぶ")]
        public void SelectingEverythingPutsEveryIndexOfThatKindIntoTheSelection()
        {
            Vertices(3);

            IDictionary<string, object> value = ComposedScreenFixture.Value(Select(
                Operation(ViewSelectElements.All),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex)));

            Assert.Equal(new[] { 0, 1, 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
            Assert.Equal(3, value[ViewSelectElements.SelectedName]);
        }

        [Fact(Skip = "impl pending: 選んでいるものと選んでいないものを入れ替える")]
        public void InvertingSwapsTheOnesThatWerePickedForTheOnesThatWereNot()
        {
            Vertices(3);
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 1 };

            Select(
                Operation(ViewSelectElements.Invert),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex));

            Assert.Equal(new[] { 0, 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact(Skip = "impl pending: 選択に、面で隣り合う頂点を足す")]
        public void ExpandingAddsTheVerticesThatShareAFaceWithTheSelection()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0 };

            Select(
                Operation(ViewSelectElements.Expand),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex));

            Assert.Equal(new[] { 0, 1, 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact(Skip = "impl pending: 選択から、選んでいない頂点と隣り合うものを外す")]
        public void ReducingDropsTheVerticesThatTouchOnesOutsideTheSelection()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 2, 3, 0));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0, 1, 2 };

            Select(
                Operation(ViewSelectElements.Reduce),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex));

            Assert.Equal(new[] { 1 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact(Skip = "impl pending: 選んだボーンの子孫を足す")]
        public void TheChildChainAddsEveryBoneBelowTheOnesThatWerePicked()
        {
            IList<IPXBone> bones = Bones("根", "子", "孫");
            bones[1].Parent = bones[0];
            bones[2].Parent = bones[1];
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 0 };

            Select(
                Operation(ViewSelectElements.ChildChain),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Bone));

            Assert.Equal(new[] { 0, 1, 2 }, _fixture.View.Selected[ElementKinds.Bone]);
        }

        [Fact(Skip = "impl pending: 指した軸の片側にある要素だけを選ぶ")]
        public void TakingHalfTheModelKeepsOnlyOneSideOfTheAxis()
        {
            Vertex(-1f, 0f, 0f);
            Vertex(1f, 0f, 0f);
            Vertex(2f, 0f, 0f);

            Select(
                Operation(ViewSelectElements.HalfModel),
                ComposedScreenFixture.Given(ViewSelectElements.KindName, ElementKinds.Vertex),
                ComposedScreenFixture.Given(
                    ViewSelectElements.AxisName, ModelEditVertices.AxisX));

            Assert.Equal(new[] { 1, 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact(Skip = "impl pending: 種類を渡さない選択を断る")]
        public void SelectingWithoutSayingTheKindIsRefused()
        {
            Vertices(1);

            IDictionary<string, object> envelope = Select(Operation(ViewSelectElements.All));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact(Skip = "impl pending: 選んだ頂点だけで作られている面を選ぶ")]
        public void TheFacesMadeOnlyOfTheSelectedVerticesAreSelected()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0, 1, 2 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(Related(
                Operation(ViewSelectRelated.VerticesToFaces)));

            Assert.Equal(new[] { 0 }, _fixture.View.Selected[ElementKinds.Face]);
            Assert.Equal(1, value[ViewSelectRelated.SelectedName]);
        }

        [Fact(Skip = "impl pending: 選んだ面が使う頂点を選ぶ")]
        public void TheVerticesThatTheSelectedFacesUseAreSelected()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 1 };

            Related(Operation(ViewSelectRelated.FacesToVertices));

            Assert.Equal(new[] { 1, 2, 3 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact(Skip = "impl pending: 選んだ面と辺を共有する面を足す")]
        public void TheFacesThatShareAnEdgeWithTheSelectedOnesAreAdded()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0 };

            Related(Operation(ViewSelectRelated.ExpandAdjacentFaces));

            Assert.Equal(new[] { 0, 1 }, _fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact(Skip = "impl pending: 指した材質の面を選ぶ")]
        public void TheFacesOfThePickedMaterialAreSelected()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));

            Related(
                Operation(ViewSelectRelated.MaterialToFaces),
                ComposedScreenFixture.Given(
                    ViewSelectRelated.MaterialIndicesName, new object[] { 1 }));

            Assert.Equal(new[] { 1 }, _fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact(Skip = "impl pending: 選んだ頂点を使う材質の面を選ぶ")]
        public void TheFacesOfEveryMaterialThatUsesTheSelectedVerticesAreSelected()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 3 };

            Related(Operation(ViewSelectRelated.VerticesToMaterials));

            Assert.Equal(new[] { 1 }, _fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact(Skip = "impl pending: 選んだ面を持つ材質の面を全部選ぶ")]
        public void EveryFaceOfTheMaterialsThatHoldTheSelectedFacesIsSelected()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2), Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0 };

            Related(Operation(ViewSelectRelated.FacesToMaterials));

            Assert.Equal(new[] { 0, 1 }, _fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact(Skip = "impl pending: 選んだ面を持つ材質の面を選択から外す")]
        public void TheFacesOfTheMaterialsThatHoldTheSelectedFacesAreTakenOut()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1 };

            Related(Operation(ViewSelectRelated.ExcludeFacesMaterials));

            Assert.Empty(_fixture.View.Selected[ElementKinds.Face]);
        }

        [Fact(Skip = "impl pending: どの面にも使われていない頂点を選ぶ")]
        public void TheVerticesThatNoFaceUsesAreSelected()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));

            Related(Operation(ViewSelectRelated.UnusedVertices));

            Assert.Equal(new[] { 3 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact(Skip = "impl pending: エッジの倍率が1でない頂点を選ぶ")]
        public void TheVerticesWhoseEdgeScaleIsNotOneAreSelected()
        {
            IList<IPXVertex> vertices = Vertices(3);
            ((FakeVertex)vertices[2]).EdgeScale = 0.5f;

            Related(Operation(ViewSelectRelated.EdgeScaleChangedVertices));

            Assert.Equal(new[] { 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact(Skip = "impl pending: 視点の回転の中心を、選んだ頂点の重心にする")]
        public void TheRotateCentreGoesToTheMiddleOfTheSelectedVertices()
        {
            Vertex(0f, 0f, 0f);
            Vertex(2f, 4f, 0f);
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 0, 1 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(Centre(
                Operation(ViewSetCameraRotateCenter.Vertices)));

            Assert.Equal(
                new object[] { 1f, 2f, 0f }, (object[])value[ViewSetCameraRotateCenter.CentreName]);
            Near(1.0, _fixture.View.CameraRotateCenter.X);
        }

        [Fact(Skip = "impl pending: 視点の回転の中心を、選んだボーンの重心にする")]
        public void TheRotateCentreGoesToTheMiddleOfTheSelectedBones()
        {
            IList<IPXBone> bones = Bones("一", "二");
            ((FakeBone)bones[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)bones[1]).Position = new V3(0f, 6f, 0f);
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 0, 1 };

            Centre(Operation(ViewSetCameraRotateCenter.Bones));

            Near(3.0, _fixture.View.CameraRotateCenter.Y);
        }

        [Fact(Skip = "impl pending: 視点の回転の中心を、選んだ面の重心にする")]
        public void TheRotateCentreGoesToTheMiddleOfTheSelectedFace()
        {
            IList<IPXVertex> vertices = Vertices(3);
            ((FakeVertex)vertices[1]).Position = new V3(3f, 0f, 0f);
            ((FakeVertex)vertices[2]).Position = new V3(0f, 3f, 0f);
            Faces(Face(vertices, 0, 1, 2));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0 };

            Centre(Operation(ViewSetCameraRotateCenter.Face));

            Near(1.0, _fixture.View.CameraRotateCenter.X);
            Near(1.0, _fixture.View.CameraRotateCenter.Y);
        }

        [Fact(Skip = "impl pending: 何も選んでいないときの回転の中心の指定を断る")]
        public void TakingTheCentreWithNothingSelectedIsRefused()
        {
            Vertices(2);

            IDictionary<string, object> envelope = Centre(
                Operation(ViewSetCameraRotateCenter.Vertices));

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(envelope));
        }

        [Fact(Skip = "impl pending: テクスチャを読み直して描画を作り直す")]
        public void ReloadingBuildsTheDrawingAgain()
        {
            Vertices(1);

            _fixture.Call(ViewReloadModel.ToolName, ComposedScreenFixture.Arguments());

            Assert.Equal(1, _fixture.View.Redraws);
            Assert.Equal(1, _fixture.View.Repaints);
        }

        [Fact(Skip = "impl pending: VMDViewへモデルだけを読み込む")]
        public void LoadingTheModelOnlyLeavesTheMotionAlone()
        {
            Vertices(1);

            _fixture.Call(
                ViewLoadVmdView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    ViewLoadVmdView.PartsName, ViewLoadVmdView.ModelOnly)));

            Assert.NotNull(_fixture.View.Loaded);
            Assert.Null(_fixture.View.Motion);
            Assert.Equal(0, _fixture.View.Plays);
        }

        [Fact(Skip = "impl pending: VMDViewへモデルとモーションを読み込んで再生を始める")]
        public void LoadingTheMotionAsWellStartsPlayingIt()
        {
            Vertices(1);

            _fixture.Call(
                ViewLoadVmdView.ToolName,
                ComposedScreenFixture.Arguments(
                    ComposedScreenFixture.Given(
                        ViewLoadVmdView.PartsName, ViewLoadVmdView.ModelAndMotion),
                    ComposedScreenFixture.Given(
                        ViewLoadVmdView.MotionPathName, @"C:\motions\walk.vmd")));

            Assert.NotNull(_fixture.View.Motion);
            Assert.Equal(1, _fixture.View.Plays);
        }

        [Fact(Skip = "impl pending: モーションのファイルを渡さない読み込みを断る")]
        public void LoadingTheMotionWithoutTheFileIsRefused()
        {
            Vertices(1);

            IDictionary<string, object> envelope = _fixture.Call(
                ViewLoadVmdView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    ViewLoadVmdView.PartsName, ViewLoadVmdView.ModelAndMotion)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact(Skip = "impl pending: VMDViewの再生だけを止める")]
        public void ClearingTheMotionOnlyStopsThePlayingAndKeepsTheModel()
        {
            _fixture.View.Booted = true;

            _fixture.Call(
                ViewClearVmdView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    ViewClearVmdView.PartsName, ViewClearVmdView.MotionOnly)));

            Assert.Equal(1, _fixture.View.Stops);
            Assert.True(_fixture.View.Booted);
        }

        [Fact(Skip = "impl pending: VMDViewの再生を止め、読み込んだモデルも外す")]
        public void ClearingTheModelAsWellTakesTheViewBackDown()
        {
            _fixture.View.Booted = true;

            _fixture.Call(
                ViewClearVmdView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    ViewClearVmdView.PartsName, ViewClearVmdView.ModelAndMotion)));

            Assert.False(_fixture.View.Booted);
        }

        [Fact(Skip = "impl pending: 全部のリストの表示を1回で作り直す")]
        public void EveryListIsBuiltAgainInOneCall()
        {
            IDictionary<string, object> value = ComposedScreenFixture.Value(
                _fixture.Call(
                    SessionUpdateAllLists.ToolName, ComposedScreenFixture.Arguments()));

            Assert.Contains(UpdateObject.All, _fixture.Form.Updated);
            Assert.Equal(
                _fixture.Form.Updated.Count, value[SessionUpdateAllLists.UpdatedName]);
        }

        [Fact(Skip = "impl pending: 選んだ面の材質を材質のリストの選択へ写す")]
        public void TheMaterialsBehindTheSelectedFacesAreCheckedInTheList()
        {
            IList<IPXVertex> vertices = Vertices(4);
            Faces(Face(vertices, 0, 1, 2));
            Faces(Face(vertices, 1, 2, 3));
            _fixture.View.Selected[ElementKinds.Face] = new[] { 1 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(_fixture.Call(
                SessionSelectListsFromView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    SessionSelectListsFromView.KindsName,
                    new object[] { SessionSelectListsFromView.Material }))));

            Assert.Equal(new[] { 1 }, _fixture.Form.SelectedMaterials);
            Assert.Equal(1, value[SessionSelectListsFromView.SelectedName]);
        }

        [Fact(Skip = "impl pending: 画面で選んだボーンのうち先頭をボーンのリストの選択へ写す")]
        public void TheFirstBoneSelectedInTheViewIsPickedInTheBoneList()
        {
            Bones("一", "二", "三");
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 2, 0 };

            IDictionary<string, object> value = ComposedScreenFixture.Value(_fixture.Call(
                SessionSelectListsFromView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    SessionSelectListsFromView.KindsName,
                    new object[] { SessionSelectListsFromView.Bone }))));

            Assert.Equal(2, _fixture.Form.SelectedBoneIndex);
            Assert.Equal(1, value[SessionSelectListsFromView.SelectedName]);
        }

        [Fact(Skip = "impl pending: 知らない種類のリストへの写し取りを断る")]
        public void AKindTheListToolDoesNotKnowIsRefused()
        {
            Vertices(1);

            IDictionary<string, object> envelope = _fixture.Call(
                SessionSelectListsFromView.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given(
                    SessionSelectListsFromView.KindsName, new object[] { "いない種類" })));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void TheGroundNoticesWhenTheModelIsTouched()
        {
            ComposedScreenFixture ground = new ComposedScreenFixture();
            ground.Model.Bone.Add(new FakeBone("一"));
            ground.Watch();
            ((FakeBone)ground.Model.Bone[0]).Name = "二";

            Assert.Throws<InvalidOperationException>(() => ground.Dispose());
        }

        private IDictionary<string, object> Select(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ViewSelectElements.ToolName, ComposedScreenFixture.Arguments(given));
        }

        private IDictionary<string, object> Related(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ViewSelectRelated.ToolName, ComposedScreenFixture.Arguments(given));
        }

        private IDictionary<string, object> Centre(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ViewSetCameraRotateCenter.ToolName, ComposedScreenFixture.Arguments(given));
        }

        private static KeyValuePair<string, object> Operation(string operation)
        {
            return ComposedScreenFixture.Given(ComposedOperation.OperationName, operation);
        }

        private IList<IPXVertex> Vertices(int count)
        {
            List<IPXVertex> made = new List<IPXVertex>();
            for (int at = 0; at < count; at++)
            {
                made.Add(Vertex(at, 0f, 0f));
            }

            return made;
        }

        private IPXVertex Vertex(float x, float y, float z)
        {
            FakeVertex vertex = new FakeVertex(x, y, z);
            _fixture.Model.Vertex.Add(vertex);

            return vertex;
        }

        private IList<IPXBone> Bones(params string[] names)
        {
            List<IPXBone> made = new List<IPXBone>();
            foreach (string name in names)
            {
                FakeBone bone = new FakeBone(name);
                _fixture.Model.Bone.Add(bone);
                made.Add(bone);
            }

            return made;
        }

        private static IPXFace Face(IList<IPXVertex> vertices, int first, int second, int third)
        {
            return new FakeFace(vertices[first], vertices[second], vertices[third]);
        }

        /// <summary>その面を持つ材質を1つ足す。面の位置はモデル全体の通し番号になる。</summary>
        private void Faces(params IPXFace[] faces)
        {
            FakeMaterial material = new FakeMaterial("材質" + _fixture.Model.Material.Count);
            foreach (IPXFace face in faces)
            {
                material.Faces.Add(face);
            }

            _fixture.Model.Material.Add(material);
        }

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }
    }
}
