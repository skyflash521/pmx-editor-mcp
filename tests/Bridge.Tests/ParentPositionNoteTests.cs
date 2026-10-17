using System;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public sealed class ParentPositionNoteTests
    {
        private const string Note = "それぞれの中で数える";

        [Theory]
        [InlineData("model_list_faces", true)]
        [InlineData("model_list_morph_offsets", true)]
        [InlineData("motion_remove_vmd_bone_keys", true)]
        [InlineData("model_list_iks", false)]
        [InlineData("model_update_iks", false)]
        public void OnlyAToolWhoseParentsHoldAListSaysPositionsAreCountedInsideEachParent(
            string tool, bool says)
        {
            GeneratedToolDefinition definition = GeneratedToolDefinitions.Create()
                .Single(d => string.Equals(d.Name, tool, StringComparison.Ordinal));

            Assert.Equal(says, definition.Description.Contains(Note));
        }
    }
}
