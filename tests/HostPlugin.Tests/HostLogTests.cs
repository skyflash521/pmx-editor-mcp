using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class HostLogTests : IDisposable
    {
        private const string Pending = "impl pending: ホストのログを複数スレッドから追記し、一定量を超えたら1世代だけローテーションして書き込みの失敗を握りつぶす";

        private readonly string _directory;

        public HostLogTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-test-" + Guid.NewGuid().ToString("N"));
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
            catch (UnauthorizedAccessException)
            {
            }
        }

        private string PathIn(string name)
        {
            return Path.Combine(_directory, name);
        }

        private static void Prefill(string path, long byteCount)
        {
            File.WriteAllBytes(path, new byte[byteCount]);
        }

        [Fact(Skip = Pending)]
        public void 書いた内容が順に追記される()
        {
            HostLog log = new HostLog(PathIn("host.log"));

            log.Write("一つ目");
            log.Write("二つ目");

            string content = File.ReadAllText(log.FilePath, Encoding.UTF8);
            Assert.Contains("一つ目", content);
            Assert.Contains("二つ目", content);
            Assert.True(content.IndexOf("一つ目", StringComparison.Ordinal) < content.IndexOf("二つ目", StringComparison.Ordinal));
        }

        [Fact(Skip = Pending)]
        public void ローテーション先は書き込み先と同名に1世代ぶんの接尾辞を付けたパスになる()
        {
            HostLog log = new HostLog(PathIn("host.log"));

            Assert.Equal(log.FilePath + ".1", log.RotatedFilePath);
        }

        [Fact(Skip = Pending)]
        public void 閾値ちょうどではローテーションせず同じファイルへ追記する()
        {
            string path = PathIn("host.log");
            Prefill(path, HostLog.RotateThresholdBytes);
            HostLog log = new HostLog(path);

            log.Write("閾値ちょうどの行");

            Assert.False(File.Exists(log.RotatedFilePath));
            Assert.Contains("閾値ちょうどの行", File.ReadAllText(log.FilePath, Encoding.UTF8));
        }

        [Fact(Skip = Pending)]
        public void 閾値を超えるとローテーションして新しいファイルへ書き続ける()
        {
            string path = PathIn("host.log");
            Prefill(path, HostLog.RotateThresholdBytes + 1);
            HostLog log = new HostLog(path);

            log.Write("ローテーション後の行");

            Assert.Equal(HostLog.RotateThresholdBytes + 1, new FileInfo(log.RotatedFilePath).Length);
            Assert.Contains("ローテーション後の行", File.ReadAllText(log.FilePath, Encoding.UTF8));
            Assert.True(new FileInfo(log.FilePath).Length < HostLog.RotateThresholdBytes);
        }

        [Fact(Skip = Pending)]
        public void ローテーションは1世代だけ残し古いローテーション先を上書きする()
        {
            string path = PathIn("host.log");
            HostLog log = new HostLog(path);
            File.WriteAllText(log.RotatedFilePath, "古いローテーション先の内容", Encoding.UTF8);
            Prefill(path, HostLog.RotateThresholdBytes + 1);

            log.Write("ローテーション後の行");

            Assert.DoesNotContain("古いローテーション先の内容", File.ReadAllText(log.RotatedFilePath, Encoding.UTF8));
        }

        [Fact(Skip = Pending)]
        public void 書き込めないファイルでも例外を投げない()
        {
            string path = PathIn("host.log");
            HostLog log = new HostLog(path);

            using (File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                log.Write("失敗する書き込み");
                log.WriteException("失敗する書き込み", new InvalidOperationException("原因"));
            }
        }

        [Fact(Skip = Pending)]
        public void 例外はスタックトレース付きで記録される()
        {
            HostLog log = new HostLog(PathIn("host.log"));
            Exception captured;
            try
            {
                throw new InvalidOperationException("記録される原因");
            }
            catch (InvalidOperationException exception)
            {
                captured = exception;
            }

            log.WriteException("要求処理で例外", captured);

            string content = File.ReadAllText(log.FilePath, Encoding.UTF8);
            Assert.Contains("要求処理で例外", content);
            Assert.Contains("記録される原因", content);
            Assert.Contains(nameof(例外はスタックトレース付きで記録される), content);
        }

        [Fact(Skip = Pending)]
        public void 複数スレッドから書いても全行が失われない()
        {
            const int ThreadCount = 4;
            const int LinesPerThread = 250;
            HostLog log = new HostLog(PathIn("host.log"));
            List<Thread> threads = new List<Thread>();
            List<Exception> failures = new List<Exception>();

            for (int threadIndex = 0; threadIndex < ThreadCount; threadIndex++)
            {
                int captured = threadIndex;
                Thread thread = new Thread(() =>
                {
                    try
                    {
                        for (int line = 0; line < LinesPerThread; line++)
                        {
                            log.Write("行 " + captured + "-" + line);
                        }
                    }
                    catch (Exception exception)
                    {
                        lock (failures)
                        {
                            failures.Add(exception);
                        }
                    }
                });
                // 直列化を誤った実装で止まっても、テストホストの終了を妨げない。
                thread.IsBackground = true;
                threads.Add(thread);
                thread.Start();
            }

            foreach (Thread thread in threads)
            {
                // 直列化を誤った実装で待ち続けず、ハングを失敗として現す。
                Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
            }

            Assert.Empty(failures);
            string[] lines = File.ReadAllLines(log.FilePath, Encoding.UTF8);
            Assert.Equal(ThreadCount * LinesPerThread, lines.Length);

            HashSet<string> written = new HashSet<string>();
            foreach (string line in lines)
            {
                int index = line.IndexOf("行 ", StringComparison.Ordinal);
                if (index >= 0)
                {
                    written.Add(line.Substring(index));
                }
            }

            Assert.Equal(ThreadCount * LinesPerThread, written.Count);
        }

        [Fact(Skip = Pending)]
        public void 既定の書き込み先はプロセスごとに分かれる()
        {
            string first = HostLog.BuildDefaultFilePath(1234);
            string second = HostLog.BuildDefaultFilePath(5678);

            Assert.NotEqual(first, second);
            Assert.Equal("pmx-editor-mcp-host-1234.log", Path.GetFileName(first));
            Assert.Equal(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(first));
        }
    }
}
