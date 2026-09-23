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
    /// ローテーションする。UIスレッドとIPCサーバースレッドの双方から呼ばれるためスレッドセーフで、
    /// 書き込みの失敗はエディタを巻き込まないよう握りつぶす。
    /// </summary>
    public sealed class HostLog
    {
        /// <summary>この量を超えたらローテーションするファイルサイズ。</summary>
        public const long RotateThresholdBytes = 1024 * 1024;

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private readonly object _gate = new object();

        /// <summary>書き込み先のファイルパスを指定して生成する。</summary>
        public HostLog(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            FilePath = filePath;
            RotatedFilePath = filePath + ".1";
        }

        /// <summary>現在の書き込み先。</summary>
        public string FilePath { get; }

        /// <summary>ローテーション先。<see cref="FilePath"/> と同名に接尾辞を付けた1世代ぶんのファイル。</summary>
        public string RotatedFilePath { get; }

        /// <summary>エディタのプロセスIDから既定の書き込み先を組み立てる。</summary>
        public static string BuildDefaultFilePath(int editorProcessId)
        {
            return Path.Combine(
                Path.GetTempPath(),
                "pmx-editor-mcp-host-" + editorProcessId.ToString(CultureInfo.InvariantCulture) + ".log");
        }

        /// <summary>1行を追記する。</summary>
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
                // ログの失敗でエディタを巻き込まない。
            }
        }

        /// <summary>
        /// 必要ならローテーションする。失敗はここで握りつぶし、追記そのものは続行させる
        /// (ローテーション先が開かれ続けている間にログ全体が沈黙しないようにする)。
        /// </summary>
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
                // ローテーションできなくても追記は続ける。
            }
        }
    }
}
