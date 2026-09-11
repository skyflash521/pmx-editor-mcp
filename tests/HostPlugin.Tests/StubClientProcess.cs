using System;
using System.IO;
using System.Threading;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 接続元のプロセスを、実際のプロセスを立てずに表す。合図の有無をテストが決められるので、
    /// 生きているプロセスも、開いた直後から終わっているプロセスも作れる。
    /// </summary>
    internal static class StubClientProcess
    {
        /// <summary>生きているプロセスとして開く。</summary>
        public static ClientProcessOpener Opener(int id)
        {
            return (Stream stream, out ClientProcess process) =>
            {
                process = Living(id);
                return true;
            };
        }

        /// <summary>開いた直後からすでに終わっているプロセスとして開く。</summary>
        public static ClientProcessOpener ExitedOpener(int id)
        {
            return (Stream stream, out ClientProcess process) =>
            {
                process = Exited(id);
                return true;
            };
        }

        /// <summary>
        /// 接続ごとに別のプロセスから繋いだものとして開く。IDは渡した順に配り、尽きたら最後の
        /// ものを使い続ける。
        /// </summary>
        public static ClientProcessOpener Opener(params int[] ids)
        {
            int index = 0;

            return (Stream stream, out ClientProcess process) =>
            {
                process = Living(ids[Math.Min(index, ids.Length - 1)]);
                index++;
                return true;
            };
        }

        /// <summary>開けないものとして扱う。</summary>
        public static ClientProcessOpener FailingOpener()
        {
            return (Stream stream, out ClientProcess process) =>
            {
                process = null;
                return false;
            };
        }

        /// <summary>まだ終わっていないプロセス。</summary>
        public static ClientProcess Living(int id)
        {
            return new ClientProcess(id, new ManualResetEvent(false));
        }

        /// <summary>すでに終わったプロセス。</summary>
        public static ClientProcess Exited(int id)
        {
            return new ClientProcess(id, new ManualResetEvent(true));
        }
    }
}
