using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>
    /// 自動E2E検査の実行器が持つ規則は、scripts の *.test.mjs が確かめる。ここでは、その確認を node で
    /// 走らせて、外れないことを見る。
    /// </summary>
    public sealed class E2eScriptTests
    {
        [Theory]
        [InlineData("e2e-setups.test.mjs")]
        [InlineData("e2e-rows.test.mjs")]
        public void TheRuleOfTheRunnerHoldsForItsCases(string name)
        {
            string script = Path.Combine(RepositoryRoot(), "scripts", name);
            using (Process node = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                Arguments = "\"" + script + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }))
            {
                string said = node.StandardOutput.ReadToEnd() + node.StandardError.ReadToEnd();
                Assert.True(node.WaitForExit(30000), "確認が終わらない。");
                Assert.True(node.ExitCode == 0, "確認が外れた:\n" + said);
            }
        }

        private static string RepositoryRoot()
        {
            for (DirectoryInfo at = new DirectoryInfo(AppContext.BaseDirectory); at != null; at = at.Parent)
            {
                if (File.Exists(Path.Combine(at.FullName, "scripts", "e2e-setups.test.mjs")))
                {
                    return at.FullName;
                }
            }

            throw new FileNotFoundException("scripts/e2e-setups.test.mjs が見つからない。");
        }
    }
}
