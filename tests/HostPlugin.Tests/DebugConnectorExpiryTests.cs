using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PEPlugin;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>常駐コネクタを人為的に失効させる入口。</summary>
    public sealed class DebugConnectorExpiryTests : IDisposable
    {
        private const string ModulePath = @"C:\plugins\PmxEditorMcp.HostPlugin.dll";

        private readonly string _root;

        private readonly HostLog _log;

        private readonly StubSystemConnector _system;

        private readonly StubCPluginConnector _cPluginConnector;

        public DebugConnectorExpiryTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-expiry-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _log = new HostLog(Path.Combine(_root, "host.log"));
            _cPluginConnector = new StubCPluginConnector();
            _system = new StubSystemConnector(new StubCPluginRunArgs(_cPluginConnector));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void ExpiringRecordsTheRetakeAfterTheExpiry()
        {
            using (ResidentConnection resident = Held())
            {
                DebugConnectorExpiry.Expire(resident);

                Assert.Equal(
                    new[] { "取得", "失効", "取得" },
                    Lines().Select(Kind).Where(k => k != null).ToArray());
                Assert.Equal(2, _system.CloneCount);
            }
        }

        [Fact]
        public void ExpiringGivesBackAConnectorAgain()
        {
            using (ResidentConnection resident = Held())
            {
                DebugConnectorExpiry.Expire(resident);

                Assert.True(resident.IsHolding);
                Assert.Same(_cPluginConnector, resident.Use());
            }
        }

        [Fact]
        public void TheEntryAnswersThatItRenewed()
        {
            using (ResidentConnection resident = Held())
            {
                IDictionary<string, object> result =
                    Assert.IsAssignableFrom<IDictionary<string, object>>(
                        DebugConnectorExpiry.Expire(resident));

                Assert.Equal(true, result["renewed"]);
            }
        }

        [Fact]
        public void TheEntryIsInTheTableOnlyWhileItIsOpen()
        {
            using (ResidentConnection resident = Held())
            {
                McpMethodTable opened = new McpMethodTable();
                DebugConnectorExpiry.AddTo(opened, true, resident);
                McpMethod found;
                Assert.True(opened.TryGet(DebugConnectorExpiry.MethodName, out found));

                McpMethodTable closed = new McpMethodTable();
                DebugConnectorExpiry.AddTo(closed, false, resident);
                Assert.False(closed.TryGet(DebugConnectorExpiry.MethodName, out found));
            }
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            using (ResidentConnection resident = Held())
            {
                Assert.Throws<ArgumentNullException>(
                    () => DebugConnectorExpiry.AddTo(null, true, resident));
                Assert.Throws<ArgumentNullException>(
                    () => DebugConnectorExpiry.AddTo(new McpMethodTable(), true, null));
                Assert.Throws<ArgumentNullException>(() => DebugConnectorExpiry.Expire(null));
            }
        }

        private ResidentConnection Held()
        {
            ResidentConnection resident = ResidentConnection.Hold(
                new StubRunArgs(new StubPluginHost(new StubConnector(_system)), ModulePath), _log);
            resident.Use();

            return resident;
        }

        private static string Kind(string line)
        {
            foreach (string kind in new[] { "取得", "失効", "破棄" })
            {
                if (line.Contains("Cプラグインコネクタの" + kind))
                {
                    return kind;
                }
            }

            return null;
        }

        private string[] Lines()
        {
            return File.Exists(_log.FilePath)
                ? File.ReadAllLines(_log.FilePath)
                : new string[0];
        }
    }
}
