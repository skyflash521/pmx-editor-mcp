using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class ScreenTargetsTests
    {
        private readonly FakePmxView _view = new FakePmxView();

        private readonly FakeFormConnector _form = new FakeFormConnector();

        [Theory]
        [InlineData(typeof(IPXVertex))]
        [InlineData(typeof(IPXBone))]
        [InlineData(typeof(IPXBody))]
        [InlineData(typeof(IPXJoint))]
        [InlineData(typeof(IPXMaterial))]
        [InlineData(typeof(IPXFace))]
        public void TheScreenCanSelectTheKindsItsWindowsList(Type element)
        {
            Assert.True(ScreenTargets.Selectable(element));
        }

        [Theory]
        [InlineData(typeof(IPXMorph))]
        [InlineData(typeof(IPXNode))]
        [InlineData(typeof(IPXSoftBody))]
        public void TheScreenCannotSelectTheOtherKinds(Type element)
        {
            Assert.False(ScreenTargets.Selectable(element));
        }

        [Fact]
        public void TheVertexSelectionComesFromTheViewInTheOrderTheScreenHolds()
        {
            _view.Selected[ElementKinds.Vertex] = new[] { 4, 1 };

            Assert.Equal(new[] { 4, 1 }, Targets().Taken(typeof(IPXVertex), 6));
        }

        [Fact]
        public void TheBoneSelectionComesFromTheView()
        {
            _view.Selected[ElementKinds.Bone] = new[] { 2 };

            Assert.Equal(new[] { 2 }, Targets().Taken(typeof(IPXBone), 3));
        }

        [Fact]
        public void TheMaterialSelectionComesFromTheListWindow()
        {
            _form.SelectedMaterials = new[] { 3, 0 };

            Assert.Equal(new[] { 3, 0 }, Targets().Taken(typeof(IPXMaterial), 4));
        }

        [Fact]
        public void APositionThatTheListNoLongerHoldsIsLeftOut()
        {
            _view.Selected[ElementKinds.Body] = new[] { 0, 7 };

            Assert.Equal(new[] { 0 }, Targets().Taken(typeof(IPXBody), 2));
        }

        [Fact]
        public void AKindTheScreenCannotSelectHoldsNothing()
        {
            Assert.Empty(Targets().Taken(typeof(IPXMorph), 5));
        }

        [Fact]
        public void TheSelectionIsEmptyWhereTheScreenCannotBeReached()
        {
            Assert.Empty(ScreenTargets.None.Taken(typeof(IPXVertex), 5));
            Assert.Empty(ScreenTargets.None.Taken(typeof(IPXMaterial), 5));
        }

        [Fact]
        public void TheFaceSelectionComesBackAsTheSerialTheWholeModelCounts()
        {
            _view.Selected[ElementKinds.Face] = new[] { 3, 4, 5 };

            Assert.Equal(new[] { 1 }, Targets().Taken(ElementKinds.Face, 4));
        }

        [Theory]
        [InlineData(ElementKinds.Vertex, "view_set_selected_vertex_indices_pmd_view_connector")]
        [InlineData(ElementKinds.Face, "view_set_selected_face_indices_pmd_view_connector")]
        [InlineData(ElementKinds.Bone, "view_set_selected_bone_indices_pmd_view_connector")]
        [InlineData(ElementKinds.Body, "view_set_selected_body_indices_pmd_view_connector")]
        [InlineData(ElementKinds.Joint, "view_set_selected_joint_indices_pmd_view_connector")]
        [InlineData(ElementKinds.Material, "session_set_selected_material_indices")]
        public void EachKindNamesTheToolThatWritesItsSelection(string kind, string tool)
        {
            Assert.Equal(tool, ScreenTargets.Picking(kind));
            Assert.Contains(tool, Registered());
        }

        [Fact]
        public void ANegativeListCountStops()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Targets().Taken(typeof(IPXVertex), -1));
        }


        [Fact]
        public void TheToolsThatTakeTheScreenSelectionAreTheOnesTheScreenLists()
        {
            string[] expected =
            {
                "model_clone_body",
                "model_clone_bone",
                "model_clone_joint",
                "model_clone_material",
                "model_clone_vertex",
                "model_get_local_axis_bone",
                "model_list_bodies",
                "model_list_bones",
                "model_list_joints",
                "model_list_materials",
                "model_list_vertices",
                "model_remove_bodies",
                "model_remove_bones",
                "model_remove_joints",
                "model_remove_materials",
                "model_remove_vertices",
                "model_set_local_axis_bone",
                "model_set_pmd_bone_kind_bone",
                "model_update_bodies",
                "model_update_bones",
                "model_update_joints",
                "model_update_materials",
                "model_update_vertices",
            };

            Assert.Equal(expected, Pointing().ToArray());
        }

        /// <summary>結び付きの表のうち、画面の選択で対象を指せるツールの名前。</summary>
        private static IEnumerable<string> Pointing()
        {
            List<string> named = new List<string>();
            foreach (KeyValuePair<string, ToolFields> one in GeneratedTools.Aggregations())
            {
                if (Takes(one.Value.Access, one.Value.Receiver))
                {
                    named.Add(one.Key);
                }
            }

            foreach (KeyValuePair<string, ToolElements> one in GeneratedTools.Elements())
            {
                if (one.Value.Kind != ToolElementKind.Add
                    && Takes(one.Value.Access, one.Value.Receiver))
                {
                    named.Add(one.Key);
                }
            }

            foreach (KeyValuePair<string, IList<ToolCall>> one in GeneratedTools.Calls())
            {
                if (one.Value.Any(call => Takes(call.Access, call.Receiver)))
                {
                    named.Add(one.Key);
                }
            }

            return named.OrderBy(name => name, StringComparer.Ordinal);
        }

        /// <summary>結び付きの表が持つツールの名前。</summary>
        private static IEnumerable<string> Registered()
        {
            return GeneratedTools.Aggregations().Keys
                .Concat(GeneratedTools.Elements().Keys)
                .Concat(GeneratedTools.Calls().Keys);
        }

        private static bool Takes(ToolAccess access, ToolReceiver receiver)
        {
            return receiver != null
                && receiver.Kind != ToolReceiverKind.Handle
                && ScreenTargets.Selects(access);
        }

        private ScreenTargets Targets()
        {
            return new ScreenTargets(() => _view, () => _form);
        }
    }
}
