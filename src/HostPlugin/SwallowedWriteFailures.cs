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
    /// 作ったスレッドで投げられたファイルの読み書きの例外を、捕まえられて握りつぶされたものも含めて、
    /// <see cref="Dispose"/> までのあいだ集める。
    /// </summary>
    public sealed class SwallowedWriteFailures : IDisposable
    {
        private readonly int _thread = Thread.CurrentThread.ManagedThreadId;

        private readonly List<string> _messages = new List<string>();

        public SwallowedWriteFailures()
        {
            AppDomain.CurrentDomain.FirstChanceException += Seen;
        }

        public IList<string> Messages
        {
            get { return _messages.AsReadOnly(); }
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.FirstChanceException -= Seen;
        }

        private void Seen(object sender, FirstChanceExceptionEventArgs e)
        {
            if (Thread.CurrentThread.ManagedThreadId != _thread)
            {
                return;
            }

            Exception thrown = e.Exception;
            if (thrown is IOException
                || thrown is UnauthorizedAccessException
                || thrown is SecurityException
                || thrown is ExternalException)
            {
                _messages.Add(thrown.Message);
            }
        }
    }
}
