using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PmxEditorMcp
{
    /// <summary>
    /// ホストのログ。エディタのプロセスごとに別ファイルへ追記し、一定量を超えたら1世代だけ
    /// ローテーションする。複数のスレッドから同時に呼んでよい。書き込みの失敗は握りつぶす。
    /// </summary>
    public sealed class HostLog
    {
        public const long RotateThresholdBytes = 1024 * 1024;

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private readonly object _gate = new object();

        public HostLog(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            FilePath = filePath;
            RotatedFilePath = filePath + ".1";
        }

        public string FilePath { get; }

        /// <summary>ローテーション先。<see cref="FilePath"/> と同名に接尾辞を付けた1世代ぶんのファイル。</summary>
        public string RotatedFilePath { get; }

        public static string BuildDefaultFilePath(int editorProcessId)
        {
            return Path.Combine(
                Path.GetTempPath(),
                "pmx-editor-mcp-host-" + editorProcessId.ToString(CultureInfo.InvariantCulture) + ".log");
        }

        public void Write(string message)
        {
            Append(message);
        }

        /// <summary>例外をスタックトレース付きで追記する。</summary>
        public void WriteException(string message, Exception exception)
        {
            Append(exception == null ? message : message + Environment.NewLine + exception);
        }

        /// <summary>並べた行を、1度ファイルを開いて順に追記する。</summary>
        public void WriteAll(IEnumerable<string> messages)
        {
            if (messages == null)
            {
                throw new ArgumentNullException(nameof(messages));
            }

            Append(messages.ToArray());
        }

        private void Append(params string[] bodies)
        {
            if (bodies.Length == 0)
            {
                return;
            }

            try
            {
                string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                    + " [" + Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture) + "] ";

                lock (_gate)
                {
                    TryRotate();
                    using (FileStream stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (StreamWriter writer = new StreamWriter(stream, Utf8WithoutBom))
                    {
                        foreach (string body in bodies)
                        {
                            writer.WriteLine(stamp + body);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>必要ならローテーションする。失敗は握りつぶし、追記は続行させる。</summary>
        private void TryRotate()
        {
            try
            {
                FileInfo current = new FileInfo(FilePath);
                if (!current.Exists || current.Length <= RotateThresholdBytes)
                {
                    return;
                }

                if (File.Exists(RotatedFilePath))
                {
                    File.Delete(RotatedFilePath);
                }

                File.Move(FilePath, RotatedFilePath);
            }
            catch (Exception)
            {
            }
        }
    }
}
