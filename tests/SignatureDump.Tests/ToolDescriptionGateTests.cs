using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolDescriptionGateTests
    {
        [Fact]
        public void ComposedDescriptionsPass()
        {
            Accepts(Material("model_list_vertices", "model", "list", "vertices"));
        }

        [Fact]
        public void AToolWithoutADescriptionStops()
        {
            ToolDescriptionMaterial material = Material("model_list_vertices", "model", "list", "vertices");

            Rejects(
                "説明文が無い",
                new[] { material },
                new Dictionary<string, ToolDescription>(StringComparer.Ordinal));
        }

        [Fact]
        public void ADescriptionWithoutAMaterialStops()
        {
            ToolDescriptionMaterial material = Material("model_list_vertices", "model", "list", "vertices");
            Dictionary<string, ToolDescription> descriptions = Composed(material);
            descriptions.Add("model_list_bones", new ToolDescription("対象 bone", null));

            Rejects("材料の無い説明文", new[] { material }, descriptions);
        }

        [Fact]
        public void ADescriptionOverTheLimitStops()
        {
            ToolDescriptionMaterial material = Material("model_list_vertices", "model", "list", "vertices");
            Dictionary<string, ToolDescription> descriptions = Composed(material);
            descriptions[material.Tool] = new ToolDescription(new string('注', 1000), null);

            Rejects("上限のバイト数を超える", new[] { material }, descriptions);
        }

        [Fact]
        public void ACollidingActionWordWithoutAQualifierStops()
        {
            ToolDescriptionMaterial[] materials =
            {
                Material("model_update", "model", "update", null),
                Material("model_update_bones", "model", "update", "bones"),
            };

            Rejects("出所修飾が無い", materials, Composed(materials));
        }

        [Fact]
        public void AQualifierThatIsNotAtTheEndStops()
        {
            ToolDescriptionMaterial[] materials =
            {
                Material("model_vertices_update", "model", "update", "vertices"),
                Material("model_update_bones", "model", "update", "bones"),
            };

            Rejects("後置されていない", materials, Composed(materials));
        }

        [Fact]
        public void ADescriptionWhoseHeadLostTheTargetStops()
        {
            ToolDescriptionMaterial[] materials =
            {
                Material("model_update_vertices", "model", "update", "vertices"),
                Material("model_update_bones", "model", "update", "bones"),
            };
            Dictionary<string, ToolDescription> descriptions = Composed(materials);
            descriptions[materials[0].Tool] = new ToolDescription(
                "動作 update / 出所 " + materials[0].TypeName, null);

            Rejects("先頭に対象の語が無い", materials, descriptions);
        }

        [Fact]
        public void ADescriptionWhoseHeadLostTheSourceStops()
        {
            ToolDescriptionMaterial[] materials =
            {
                Material("model_update_vertices", "model", "update", "vertices"),
                Material("model_update_bones", "model", "update", "bones"),
            };
            Dictionary<string, ToolDescription> descriptions = Composed(materials);
            descriptions[materials[0].Tool] = new ToolDescription(
                "対象 " + materials[0].ElementNoun + " / 動作 update", null);

            Rejects("先頭に出所の語が無い", materials, descriptions);
        }

        [Fact]
        public void AnActionWordThatDoesNotCollideNeedsNoQualifier()
        {
            Accepts(
                Material("model_update_vertices", "model", "update", "vertices"),
                Material("model_clear", "model", "clear", null));
        }

        [Fact]
        public void TheSourceWordIsLookedForInTheHeadAlone()
        {
            ToolDescriptionMaterial[] materials =
            {
                Material("model_update_vertices", "model", "update", "vertices"),
                Material("model_update_bones", "model", "update", "bones"),
            };
            Dictionary<string, ToolDescription> descriptions = Composed(materials);
            descriptions[materials[0].Tool] = new ToolDescription(
                "対象 " + materials[0].ElementNoun + " / 動作 update\n一次資料: "
                    + materials[0].TypeName,
                null);

            Rejects("先頭に出所の語が無い", materials, descriptions);
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionGate.Require(
                    null, new Dictionary<string, ToolDescription>(StringComparer.Ordinal)));
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionGate.Require(new ToolDescriptionMaterial[0], null));
        }

        private static void Accepts(params ToolDescriptionMaterial[] materials)
        {
            ToolDescriptionGate.Require(materials, Composed(materials));
        }

        private static void Rejects(
            string fragment,
            IList<ToolDescriptionMaterial> materials,
            IDictionary<string, ToolDescription> descriptions)
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ToolDescriptionGate.Require(materials, descriptions));

            Assert.Contains(fragment, error.Message, StringComparison.Ordinal);
        }

        private static Dictionary<string, ToolDescription> Composed(
            params ToolDescriptionMaterial[] materials)
        {
            Dictionary<string, ToolDescription> descriptions =
                new Dictionary<string, ToolDescription>(StringComparer.Ordinal);
            foreach (ToolDescriptionMaterial material in materials)
            {
                descriptions.Add(material.Tool, ToolDescriptionRule.Compose(material));
            }

            return descriptions;
        }

        private static ToolDescriptionMaterial Material(
            string tool, string group, string actionWord, string qualifier)
        {
            return new ToolDescriptionMaterial(
                tool,
                group,
                actionWord,
                qualifier,
                "vertex",
                "PEPlugin.Pmx.IPXVertex",
                null,
                null,
                null);
        }
    }
}
