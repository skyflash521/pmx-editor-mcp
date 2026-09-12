using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class DiscoveryRuleTests
    {
        [Fact]
        public void AToolWhoseNameCarriesEveryTermIsMatched()
        {
            Assert.Equal(
                new[] { "model_list_bones" },
                DiscoveryRule.Matched(Descriptions(), new[] { "list", "bone" }).ToArray());
        }

        [Fact]
        public void ATermThatOnlyTheDescriptionCarriesStillMatches()
        {
            Assert.Equal(
                new[] { "model_list_bones" },
                DiscoveryRule.Matched(Descriptions(), new[] { "ボーンの一覧" }).ToArray());
        }

        [Fact]
        public void MatchingIgnoresTheCaseOfTheTermAndTheTool()
        {
            Assert.Equal(
                new[] { "model_list_bones" },
                DiscoveryRule.Matched(Descriptions(), new[] { "LIST", "BONE" }).ToArray());
        }

        [Fact]
        public void AToolThatMissesOneTermIsNotMatched()
        {
            Assert.Empty(DiscoveryRule.Matched(Descriptions(), new[] { "bone", "頂点" }));
        }

        [Fact]
        public void MatchedToolsComeBackInTheOrderOfTheirNames()
        {
            Assert.Equal(
                new[] { "model_list_bones", "model_list_vertices" },
                DiscoveryRule.Matched(Descriptions(), new[] { "list" }).ToArray());
        }

        [Fact]
        public void TheSearchesOfOneTaskAreGatheredWithoutRepeating()
        {
            DiscoveryTask task = new DiscoveryTask(
                "題材",
                new DiscoverySearch[]
                {
                    new DiscoverySearch(new[] { "list" }),
                    new DiscoverySearch(new[] { "bone" }),
                },
                new[] { "model_list_bones", "model_list_vertices" });

            Assert.Equal(
                new[] { "model_list_bones", "model_list_vertices" },
                DiscoveryRule.Found(Descriptions(), task).ToArray());
        }

        [Fact]
        public void ATaskWhoseSearchMissesAToolIsRefused()
        {
            DiscoveryTaskTable table = Table(
                new[] { "頂点" }, new[] { "model_list_bones", "model_list_vertices" });

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
                () => DiscoveryGate.Require(table, Descriptions()));

            Assert.Contains("model_list_bones", refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ATaskWhoseSearchBringsAnUnneededToolIsRefused()
        {
            DiscoveryTaskTable table = Table(new[] { "list" }, new[] { "model_list_bones" });

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
                () => DiscoveryGate.Require(table, Descriptions()));

            Assert.Contains("model_list_vertices", refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ATaskThatNamesAToolNobodyPublishesIsRefused()
        {
            DiscoveryTaskTable table = Table(new[] { "list" }, new[] { "model_list_faces" });

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
                () => DiscoveryGate.Require(table, Descriptions()));

            Assert.Contains("model_list_faces", refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ATaskWhoseSearchesBringExactlyItsToolsPasses()
        {
            DiscoveryGate.Require(
                Table(new[] { "list" }, new[] { "model_list_bones", "model_list_vertices" }),
                Descriptions());
        }

        private static DiscoveryTaskTable Table(IList<string> terms, IList<string> tools)
        {
            return new DiscoveryTaskTable(new[]
            {
                new DiscoveryTask(
                    "題材", new[] { new DiscoverySearch(terms) }, tools),
            });
        }

        private static IDictionary<string, string> Descriptions()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "model_list_bones", "対象 bone / 動作 list\n一次資料: ボーンの一覧" },
                { "model_list_vertices", "対象 vertex / 動作 list\n一次資料: 頂点の一覧" },
            };
        }
    }
}
