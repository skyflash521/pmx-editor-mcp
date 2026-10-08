using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class HeldReceiverPmxSwitchTests
    {
        [Fact]
        public void EveryGeneratedCallOnAHeldReceiverAcceptsThePmxSwitchExactlyWhereItsSchemaShowsIt()
        {
            ISet<string> shown = ToolsShowingThePmxSwitch();
            List<string> disagreeing = new List<string>();
            foreach (KeyValuePair<string, IList<ToolCall>> tool in GeneratedTools.Calls(new List<string>()))
            {
                bool held = tool.Value.Any(c => c.Receiver.Kind == ToolReceiverKind.Handle);
                bool accepts = tool.Value.Any(
                    c => c.Receiver.Kind == ToolReceiverKind.Handle && ToolDispatch.Accepts(c));
                if (held && accepts != shown.Contains(tool.Key))
                {
                    disagreeing.Add(tool.Key + " accepts=" + accepts);
                }
            }

            Assert.Empty(disagreeing);
        }

        [Fact]
        public void EveryGeneratedCallAcceptsThePmxSwitchExactlyWhereItsSchemaShowsIt()
        {
            ISet<string> shown = ToolsShowingThePmxSwitch();
            List<string> disagreeing = new List<string>();
            foreach (KeyValuePair<string, IList<ToolCall>> tool in GeneratedTools.Calls(new List<string>()))
            {
                bool accepts = tool.Value.Any(ToolDispatch.Accepts);
                if (accepts != shown.Contains(tool.Key))
                {
                    disagreeing.Add(tool.Key + " accepts=" + accepts);
                }
            }

            Assert.Empty(disagreeing);
        }

        [Fact]
        public void TheToolsThatBuildAMotionOrPreviewFromAModelShowWhichModelTheyUse()
        {
            ISet<string> shown = ToolsShowingThePmxSwitch();

            Assert.Contains("motion_init_from_pmx_vme", shown);
            Assert.Contains("motion_init_vmd", shown);
            Assert.Contains("motion_preview_vme", shown);
        }

        private static ISet<string> ToolsShowingThePmxSwitch()
        {
            JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            Dictionary<string, object> table = json.Deserialize<Dictionary<string, object>>(
                File.ReadAllText(Schemas()));
            HashSet<string> shown = new HashSet<string>(StringComparer.Ordinal);
            foreach (Dictionary<string, object> tool in ((ArrayList)table["tools"]).Cast<Dictionary<string, object>>())
            {
                bool takes = ((ArrayList)tool["branches"])
                    .Cast<Dictionary<string, object>>()
                    .SelectMany(b => ((ArrayList)b["inputs"]).Cast<Dictionary<string, object>>())
                    .Any(i => string.Equals((string)i["name"], PmxSession.HandleName, StringComparison.Ordinal));
                if (takes)
                {
                    shown.Add((string)tool["tool"]);
                }
            }

            return shown;
        }

        private static string Schemas()
        {
            for (DirectoryInfo at = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                at != null;
                at = at.Parent)
            {
                string path = Path.Combine(at.FullName, "catalog", "authored", "tool-schemas.json");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new FileNotFoundException("スキーマ正本が見つからない。");
        }
    }
}
