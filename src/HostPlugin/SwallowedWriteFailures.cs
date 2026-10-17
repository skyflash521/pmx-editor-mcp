using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;

namespace PmxEditorMcp
{
    /// <summary>
    /// 作ったスレッドで投げられた例外を、捕まえられて握りつぶされたものも含めて、<see cref="Dispose"/>
    /// までのあいだ集める。どちらの並びも、重ならない行を投げられた順で先頭から5件まで残し、1行は
    /// 300文字で切る。残らなかった数は Beyond の名の付いた方で返す。
    /// </summary>
    public sealed class SwallowedWriteFailures : IDisposable
    {
        private const int Kept = 5;

        private const int LineChars = 300;

        private readonly int _thread = Thread.CurrentThread.ManagedThreadId;

        private readonly List<string> _messages = new List<string>();

        private readonly List<string> _thrown = new List<string>();

        private int _messagesBeyond;

        private int _thrownBeyond;

        public SwallowedWriteFailures()
        {
            AppDomain.CurrentDomain.FirstChanceException += Seen;
        }

        /// <summary>ファイルの読み書きの例外の文面。</summary>
        public IList<string> Messages
        {
            get { return _messages.AsReadOnly(); }
        }

        public int MessagesBeyond
        {
            get { return _messagesBeyond; }
        }

        /// <summary>すべての例外の型の完全名と文面。</summary>
        public IList<string> Thrown
        {
            get { return _thrown.AsReadOnly(); }
        }

        public int ThrownBeyond
        {
            get { return _thrownBeyond; }
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.FirstChanceException -= Seen;
        }

        private static void Keep(List<string> lines, string line, ref int beyond)
        {
            if (line.Length > LineChars)
            {
                line = line.Substring(0, LineChars);
            }

            if (lines.Contains(line))
            {
                return;
            }

            if (lines.Count < Kept)
            {
                lines.Add(line);
            }
            else
            {
                beyond++;
            }
        }

        private void Seen(object sender, FirstChanceExceptionEventArgs e)
        {
            if (Thread.CurrentThread.ManagedThreadId != _thread)
            {
                return;
            }

            Exception thrown = e.Exception;
            Keep(_thrown, thrown.ToString().Split(new[] { '\r', '\n' }, 2)[0], ref _thrownBeyond);
            if (thrown is IOException
                || thrown is UnauthorizedAccessException
                || thrown is SecurityException
                || thrown is ExternalException)
            {
                Keep(_messages, thrown.Message, ref _messagesBeyond);
            }
        }
    }
}
