using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class E2eCaseJsonTests
    {
        private static readonly Regex Spelled = new Regex(
            "\"expect\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant);

        [Fact]
        public void EveryOutcomeIsCheckedAgainstTheRunner()
        {
            ISet<string> given = new HashSet<string>(
                Spelled.Matches(File.ReadAllText(Fixture(), Encoding.UTF8))
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value),
                StringComparer.Ordinal);

            foreach (E2eExpectation expectation in
                Enum.GetValues(typeof(E2eExpectation)).Cast<E2eExpectation>())
            {
                Assert.Contains(Written(expectation), given);
            }
        }

        [Fact]
        public void OnlyACaseRunAfterTheViewsCarriesTheMark()
        {
            E2eCase plain = new E2eCase(
                string.Empty,
                string.Empty,
                string.Empty,
                "motion_apply_current_pose",
                "確かめること",
                new Dictionary<string, object>(StringComparer.Ordinal),
                E2eExpectation.Called,
                null);

            Assert.DoesNotContain("afterViews", E2eCaseJson.Compose(new[] { plain }));
            Assert.Matches(
                "\"afterViews\"\\s*:\\s*true",
                E2eCaseJson.Compose(new[] { plain.RunAfterViews() }));
        }

        /// <summary>その結末を綴った文字列。綴りを決めるのは書き手なので、書かせて読み取る。</summary>
        private static string Written(E2eExpectation expectation)
        {
            string composed = E2eCaseJson.Compose(new[]
            {
                new E2eCase(
                    "Sdk.Type.Wipe()",
                    "read",
                    "Host.Connector.Pmx",
                    "model_wipe",
                    "確かめること",
                    new Dictionary<string, object>(StringComparer.Ordinal),
                    expectation,
                    null),
            });

            return Spelled.Match(composed).Groups[1].Value;
        }

        /// <summary>
        /// 実行器を確かめる検査が読む検査の定義の置き場。この綴りの一覧と、書き手が綴る結末の
        /// 一覧が揃っていないと、綴りを足しても実行器はその結末を突き合わせないまま通る。
        /// </summary>
        private static string Fixture([CallerFilePath] string here = null)
        {
            return Path.Combine(
                Path.GetDirectoryName(here), "..", "..", "scripts", "e2e-stub-cases.json");
        }
    }
}
