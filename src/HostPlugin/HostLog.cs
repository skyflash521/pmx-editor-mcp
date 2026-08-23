using System;

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

        /// <summary>書き込み先のファイルパスを指定して生成する。</summary>
        public HostLog(string filePath)
        {
            throw new NotImplementedException();
        }

        /// <summary>現在の書き込み先。</summary>
        public string FilePath => throw new NotImplementedException();

        /// <summary>ローテーション先。<see cref="FilePath"/> と同名に接尾辞を付けた1世代ぶんのファイル。</summary>
        public string RotatedFilePath => throw new NotImplementedException();

        /// <summary>エディタのプロセスIDから既定の書き込み先を組み立てる。</summary>
        public static string BuildDefaultFilePath(int editorProcessId)
        {
            throw new NotImplementedException();
        }

        /// <summary>1行を追記する。</summary>
        public void Write(string message)
        {
            throw new NotImplementedException();
        }

        /// <summary>例外をスタックトレース付きで追記する。</summary>
        public void WriteException(string message, Exception exception)
        {
            throw new NotImplementedException();
        }
    }
}
