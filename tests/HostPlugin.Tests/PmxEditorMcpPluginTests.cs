using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PEPlugin;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class PmxEditorMcpPluginTests : IDisposable
    {
        private const string Failure = "題材の失敗";

        private readonly string _directory;

        public PmxEditorMcpPluginTests()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-plugin-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
        }

        private HostLog Log()
        {
            return new HostLog(Path.Combine(_directory, "host.log"));
        }

        private static Action<IPERunArgs> Noting(IList<string> ran, string name)
        {
            return args => ran.Add(name);
        }

        private static Action Noting(IList<string> ran)
        {
            return () => ran.Add("status");
        }

        private static Action Throwing()
        {
            return () => { throw new InvalidOperationException(Failure); };
        }

        [Fact]
        public void BootupGoesToTheResidentSide()
        {
            List<string> ran = new List<string>();

            PmxEditorMcpPlugin.Run(
                new StubRunArgs(true), Noting(ran, "bootup"), Noting(ran), () => null);

            Assert.Equal(new[] { "bootup" }, ran);
        }

        [Fact]
        public void TheResidentSideReceivesTheArgumentsItWasGiven()
        {
            IPERunArgs given = new StubRunArgs(true);
            IPERunArgs seen = null;

            PmxEditorMcpPlugin.Run(given, args => seen = args, () => { }, () => null);

            Assert.Same(given, seen);
        }

        [Fact]
        public void ARerunFromTheMenuGoesToTheStatusSide()
        {
            List<string> ran = new List<string>();

            PmxEditorMcpPlugin.Run(
                new StubRunArgs(false), Noting(ran, "bootup"), Noting(ran), () => null);

            Assert.Equal(new[] { "status" }, ran);
        }

        [Fact]
        public void MissingArgumentsGoToTheStatusSide()
        {
            List<string> ran = new List<string>();

            PmxEditorMcpPlugin.Run(null, Noting(ran, "bootup"), Noting(ran), () => null);

            Assert.Equal(new[] { "status" }, ran);
        }

        [Fact]
        public void AFailureOnTheResidentSideIsWrittenToTheLog()
        {
            HostLog log = Log();

            PmxEditorMcpPlugin.Run(
                new StubRunArgs(true), args => { throw new InvalidOperationException(Failure); },
                () => { },
                () => log);

            string written = File.ReadAllText(log.FilePath, Encoding.UTF8);
            Assert.Contains("プラグインの実行で例外が起きた。", written, StringComparison.Ordinal);
            Assert.Contains(Failure, written, StringComparison.Ordinal);
        }

        [Fact]
        public void AFailureOnTheStatusSideIsWrittenToTheLog()
        {
            HostLog log = Log();

            PmxEditorMcpPlugin.Run(new StubRunArgs(false), args => { }, Throwing(), () => log);

            string written = File.ReadAllText(log.FilePath, Encoding.UTF8);
            Assert.Contains("プラグインの実行で例外が起きた。", written, StringComparison.Ordinal);
            Assert.Contains(Failure, written, StringComparison.Ordinal);
        }

        [Fact]
        public void AFailureIsWrittenToTheLogThatTheRunItselfMade()
        {
            HostLog made = null;

            PmxEditorMcpPlugin.Run(
                new StubRunArgs(true),
                args =>
                {
                    made = Log();
                    throw new InvalidOperationException(Failure);
                },
                () => { },
                () => made);

            string written = File.ReadAllText(made.FilePath, Encoding.UTF8);
            Assert.Contains(Failure, written, StringComparison.Ordinal);
        }

        [Fact]
        public void AFailureWithoutALogDoesNotLeaveTheCall()
        {
            List<string> ran = new List<string>();

            PmxEditorMcpPlugin.Run(
                new StubRunArgs(false),
                Noting(ran, "bootup"),
                () =>
                {
                    ran.Add("status");
                    throw new InvalidOperationException(Failure);
                },
                () => null);

            Assert.Equal(new[] { "status" }, ran);
        }
    }
}
