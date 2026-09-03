using System;
using System.IO;
using System.Linq;
using PEPlugin;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ResidentConnectionTests : IDisposable
    {
        private const string ModulePath = @"C:\plugins\PmxEditorMcp.HostPlugin.dll";

        private readonly string _root;

        private readonly HostLog _log;

        private readonly StubSystemConnector _system;

        private readonly StubCPluginConnector _cPluginConnector;

        public ResidentConnectionTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-resident-" + Guid.NewGuid().ToString("N"));
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
        public void TheConnectionRootIsHeldWithoutTakingTheConnector()
        {
            IPERunArgs runArgs = RunArgs(ModulePath);

            using (ResidentConnection connection = ResidentConnection.Hold(runArgs, _log))
            {
                Assert.Same(runArgs, connection.RunArgs);
                Assert.Null(connection.CPluginConnector);
            }

            Assert.Equal(0, _system.CloneCount);
            Assert.Empty(Lines());
        }

        [Fact]
        public void TheCPluginConnectorIsTakenOnceWithTheModulePath()
        {
            using (ResidentConnection connection = Taken(ModulePath))
            {
                Assert.Same(_cPluginConnector, connection.CPluginConnector);
            }

            Assert.Equal(1, _system.CloneCount);
            Assert.Equal(ModulePath, _system.LastModulePath);
        }

        [Fact]
        public void TakingTheCPluginConnectorTwiceStops()
        {
            using (ResidentConnection connection = Taken(ModulePath))
            {
                InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                    () => connection.TakeCPluginConnector());

                Assert.Contains("二度", error.Message, StringComparison.Ordinal);
                Assert.Equal(1, _system.CloneCount);
            }
        }

        [Fact]
        public void TakingTheCPluginConnectorIsRecorded()
        {
            using (Taken(ModulePath))
            {
            }

            Assert.Single(Lines(), l => l.Contains("Cプラグインコネクタの取得"));
        }

        [Fact]
        public void DiscardingTheCPluginConnectorIsRecorded()
        {
            ResidentConnection connection = Taken(ModulePath);

            connection.Dispose();

            Assert.Single(Lines(), l => l.Contains("Cプラグインコネクタの破棄"));
            Assert.Null(connection.CPluginConnector);
        }

        [Fact]
        public void DiscardingTwiceIsRecordedOnce()
        {
            ResidentConnection connection = Taken(ModulePath);

            connection.Dispose();
            connection.Dispose();

            Assert.Single(Lines(), l => l.Contains("Cプラグインコネクタの破棄"));
        }

        [Fact]
        public void DiscardingWithoutTakingIsNotRecorded()
        {
            ResidentConnection.Hold(RunArgs(ModulePath), _log).Dispose();

            Assert.Empty(Lines());
        }

        [Fact]
        public void AConnectionRootWithoutAModulePathStops()
        {
            foreach (string modulePath in new[] { null, string.Empty, "  " })
            {
                ResidentConnection connection = ResidentConnection.Hold(RunArgs(modulePath), _log);

                InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                    () => connection.TakeCPluginConnector());

                Assert.Contains("モジュールパス", error.Message, StringComparison.Ordinal);
                Assert.NotNull(connection.RunArgs);
            }

            Assert.Equal(0, _system.CloneCount);
            Assert.Empty(Lines());
        }

        [Fact]
        public void AMissingCPluginRunArgsStopsWithoutRecording()
        {
            ResidentConnection connection = ResidentConnection.Hold(
                new StubRunArgs(
                    new StubPluginHost(new StubConnector(new StubSystemConnector(null))),
                    ModulePath),
                _log);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => connection.TakeCPluginConnector());

            Assert.Contains("実行引数", error.Message, StringComparison.Ordinal);
            Assert.Empty(Lines());
        }

        [Fact]
        public void AMissingCPluginConnectorStopsWithoutRecording()
        {
            ResidentConnection connection = ResidentConnection.Hold(
                new StubRunArgs(
                    new StubPluginHost(
                        new StubConnector(
                            new StubSystemConnector(new StubCPluginRunArgs(null)))),
                    ModulePath),
                _log);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => connection.TakeCPluginConnector());

            Assert.Contains("コネクタ", error.Message, StringComparison.Ordinal);
            Assert.Empty(Lines());
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => ResidentConnection.Hold(null, _log));
            Assert.Throws<ArgumentNullException>(
                () => ResidentConnection.Hold(RunArgs(ModulePath), null));
        }

        /// <summary>接続の根を保持し、Cプラグイン連携のコネクタまで得た状態。</summary>
        private ResidentConnection Taken(string modulePath)
        {
            ResidentConnection connection = ResidentConnection.Hold(RunArgs(modulePath), _log);
            connection.TakeCPluginConnector();

            return connection;
        }

        private IPERunArgs RunArgs(string modulePath)
        {
            return new StubRunArgs(new StubPluginHost(new StubConnector(_system)), modulePath);
        }

        private string[] Lines()
        {
            return File.Exists(_log.FilePath)
                ? File.ReadAllLines(_log.FilePath).Where(l => l.Length != 0).ToArray()
                : new string[0];
        }
    }
}
