using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace PmxEditorMcp
{
    /// <summary>
    /// 接続元のプロセス。終わったかどうかは、開いて保持している待機ハンドルの合図だけで判じる。
    /// プロセスIDで後から引き直すと、IDが再利用されたときに別のプロセスの寿命へ繋がる。
    /// </summary>
    public sealed class ClientProcess : IDisposable
    {
        private readonly WaitHandle _exited;

        public ClientProcess(int id, WaitHandle exited)
        {
            if (exited == null)
            {
                throw new ArgumentNullException(nameof(exited));
            }

            Id = id;
            _exited = exited;
        }

        /// <summary>接続元のプロセスID。</summary>
        public int Id { get; }

        /// <summary>そのプロセスが終わっていれば真。</summary>
        public bool HasExited
        {
            get { return _exited.WaitOne(0); }
        }

        public void Dispose()
        {
            _exited.Close();
        }
    }

    /// <summary>
    /// 接続から接続元のプロセスを開く。開けなければ偽で、<paramref name="process"/> は null。
    /// テストから差し替えるための形で、通常は <see cref="PipeClientProcess.TryOpen"/> を用いる。
    /// </summary>
    public delegate bool ClientProcessOpener(Stream stream, out ClientProcess process);

    /// <summary>名前付きパイプの接続元のプロセスを開く。</summary>
    public static class PipeClientProcess
    {
        /// <summary>終了を待てるだけの権限。中身を覗く権限は要らない。</summary>
        private const int Synchronize = 0x00100000;

        /// <summary>接続元のプロセスを開く。パイプでない・IDを取れない・開けないときは偽。</summary>
        public static bool TryOpen(Stream stream, out ClientProcess process)
        {
            process = null;

            PipeStream pipe = stream as PipeStream;
            if (pipe == null)
            {
                return false;
            }

            uint id;
            if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out id))
            {
                return false;
            }

            SafeWaitHandle handle = OpenProcess(Synchronize, false, id);
            if (handle.IsInvalid)
            {
                handle.Close();
                return false;
            }

            ManualResetEvent exited = new ManualResetEvent(false);
            exited.SafeWaitHandle = handle;
            process = new ClientProcess((int)id, exited);

            return true;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeWaitHandle OpenProcess(
            int access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    }
}
