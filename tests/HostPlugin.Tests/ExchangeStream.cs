using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.Tests
{
    /// <summary>UIスレッドを持たないため、委譲された処理はその場で実行する。</summary>
    internal sealed class InlineInvoker : IUiInvoker
    {
        public bool TryInvokeOnUi(Action action)
        {
            action();
            return true;
        }
    }

    /// <summary>読み取り用の入力と書き出し先を別に持ち、書き終えたメッセージの件数を数えるストリーム。</summary>
    internal sealed class ExchangeStream : Stream
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private readonly object _gate = new object();
        private readonly MemoryStream _input;
        private readonly MemoryStream _output = new MemoryStream();

        private int _messageCount;
        private int _writesBeforeFailure = int.MaxValue;

        public ExchangeStream(byte[] input)
        {
            _input = new MemoryStream(input);
        }

        /// <summary>区切りまで書き終えたメッセージの件数。書き出しの粒度には依らない。</summary>
        public int MessageCount
        {
            get
            {
                lock (_gate)
                {
                    return _messageCount;
                }
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        /// <summary>指定の回数だけ書き出したあと、以後の書き出しを失敗させる。</summary>
        public void FailWritesAfter(int writes)
        {
            lock (_gate)
            {
                _writesBeforeFailure = writes;
            }
        }

        /// <summary>書き終えたメッセージが指定の件数に達するまで待つ。</summary>
        public bool WaitForMessages(int count, TimeSpan limit)
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            lock (_gate)
            {
                while (_messageCount < count)
                {
                    TimeSpan remaining = limit - elapsed.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                    {
                        return false;
                    }

                    Monitor.Wait(_gate, remaining);
                }

                return true;
            }
        }

        /// <summary>書き出された応答を1件ずつ解いて返す。</summary>
        public IList<IDictionary<string, object>> ReadResponses()
        {
            List<IDictionary<string, object>> responses = new List<IDictionary<string, object>>();
            byte[] written;
            lock (_gate)
            {
                written = _output.ToArray();
            }

            foreach (string line in Utf8WithoutBom.GetString(written).Split('\n'))
            {
                if (line.Length > 0)
                {
                    responses.Add((IDictionary<string, object>)Serializer.DeserializeObject(line));
                }
            }

            return responses;
        }

        /// <summary>
        /// 書き終えたメッセージの件数を渡して呼ぶ処理。どの応答まで返ったかを見て、要求と要求の
        /// 合間に外から状態を変えるのに使う。
        /// </summary>
        public Action<int> AfterWrite { get; set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _input.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            int written;
            lock (_gate)
            {
                if (_writesBeforeFailure <= 0)
                {
                    throw new IOException("書き出しに失敗した。");
                }

                _writesBeforeFailure--;
                _output.Write(buffer, offset, count);
                for (int index = offset; index < offset + count; index++)
                {
                    if (buffer[index] == (byte)'\n')
                    {
                        _messageCount++;
                    }
                }

                Monitor.PulseAll(_gate);
                written = _messageCount;
            }

            Action<int> after = AfterWrite;
            if (after != null)
            {
                after(written);
            }
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _input.Dispose();
                _output.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
