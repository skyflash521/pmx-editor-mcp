using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class TemplateTargetsTests
    {
        private static readonly Func<TemplateTarget, bool> Known =
            target => target.Name == null || target.Name.StartsWith("bone", StringComparison.Ordinal);

        [Fact]
        public void TheTargetsAreTakenInTheOrderTheyWereWritten()
        {
            IList<TemplateTarget> taken = Resolve(
                new[] { Bone("bone2"), Bone("bone1") });

            Assert.Equal(new[] { "bone2", "bone1" }, new[] { taken[0].Name, taken[1].Name });
        }

        [Fact]
        public void ATargetThatIsTheOnlyOneOfItsKindNeedsNoName()
        {
            Assert.Single(Resolve(new[] { new TemplateTarget("camera") }));
        }

        [Fact]
        public void TargetsOfTheSameKindAreToldApartByTheirPath()
        {
            Assert.Equal(
                2,
                Resolve(
                    new[]
                    {
                        new TemplateTarget("camera", path: "position"),
                        new TemplateTarget("camera", path: "look_at"),
                    }).Count);
        }

        [Fact]
        public void AnEmptyListIsRefused()
        {
            Refused(new TemplateTarget[0], "空である");
            Refused(null, "空である");
        }

        [Fact]
        public void PointingAtTheSameTargetTwiceIsRefused()
        {
            Refused(new[] { Bone("bone1"), Bone("bone1") }, "同じ対象を二度");
        }

        [Fact]
        public void AGroupWithNoKindIsRefused()
        {
            Refused(new[] { new TemplateTarget(null, "bone1") }, "種別を持たない");
            Refused(new[] { new TemplateTarget(" ", "bone1") }, "種別を持たない");
        }

        /// <summary>組そのものが中身を持たない要求も、要求の誤りとして断る。</summary>
        [Fact]
        public void AGroupWithNothingInItIsRefused()
        {
            Refused(new TemplateTarget[] { null }, "種別を持たない");
        }

        /// <summary>
        /// 1件でも解けなければ何も適用しないので、解けるかは適用の前に全件を見る。指し方は正しく、
        /// いまの状態に対象が無いだけなので、指し方の誤りとは別のコードで断る。
        /// </summary>
        [Fact]
        public void ATargetThatPointsAtNothingIsRefused()
        {
            Refused(
                new[] { Bone("bone1"), Bone("missing") },
                "指す対象が無い",
                ToolEnvelope.NotApplicable);
        }

        /// <summary>指し方そのものの誤りは、対象が在るかを見る前に断る。</summary>
        [Fact]
        public void ARepeatedTargetIsRefusedBeforeTheTargetIsLookedUp()
        {
            Refused(new[] { Bone("missing"), Bone("missing") }, "同じ対象を二度");
        }

        [Fact]
        public void TheNameOfTheItemIsTheOneTheContractDefines()
        {
            Assert.Equal("targets", TemplateTargets.Name);
        }

        [Fact]
        public void TheLookupIsRequired()
        {
            Assert.Throws<ArgumentNullException>(
                () => TemplateTargets.TryResolve(new[] { Bone("bone1") }, null, out _, out _, out _));
        }

        private static TemplateTarget Bone(string name)
        {
            return new TemplateTarget("bone", name);
        }

        private static IList<TemplateTarget> Resolve(IList<TemplateTarget> targets)
        {
            Assert.True(TemplateTargets.TryResolve(
                targets, Known, out IList<TemplateTarget> taken, out string code, out string message));
            Assert.Null(code);
            Assert.Null(message);

            return taken;
        }

        private static void Refused(
            IList<TemplateTarget> targets,
            string expected,
            string expectedCode = ToolEnvelope.InvalidArgument)
        {
            Assert.False(TemplateTargets.TryResolve(
                targets, Known, out IList<TemplateTarget> taken, out string code, out string message));
            Assert.Null(taken);
            Assert.Equal(expectedCode, code);
            Assert.Contains(expected, message, StringComparison.Ordinal);
        }
    }
}
