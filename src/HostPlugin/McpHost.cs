using System;
using System.Globalization;
using System.Linq;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace PmxEditorMcp
{
    /// <summary>
    /// 名前付きパイプの待受と稼働世代を持つホスト本体。開始・停止は排他ロックで直列化し、
    /// 呼び出しスレッド(UIスレッドでありうる)をIPCサーバースレッドの終了待ちでブロックしない。
    /// </summary>
    public sealed class McpHost
    {
        private const int PipeBufferSizeBytes = 65536;
        private const int PrepareRetryDelayMs = 500;

        private readonly object _gate = new object();
        private readonly string _pipeName;
        private readonly HostLog _log;
        private readonly ResponseBudget _budget;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly ConnectionHandler _connectionHandler;

        private HostGeneration _current;
        private HostGeneration _stopped;

        /// <summary>
        /// 待受に使うパイプ名・ログ・応答サイズ予算・UIディスパッチ・接続処理を与えて生成する。
        /// </summary>
        public McpHost(
            string pipeName,
            HostLog log,
            ResponseBudget budget,
            IUiDispatcher uiDispatcher,
            ConnectionHandler connectionHandler)
        {
            if (pipeName == null)
            {
                throw new ArgumentNullException(nameof(pipeName));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (budget == null)
            {
                throw new ArgumentNullException(nameof(budget));
            }

            if (uiDispatcher == null)
            {
                throw new ArgumentNullException(nameof(uiDispatcher));
            }

            if (connectionHandler == null)
            {
                throw new ArgumentNullException(nameof(connectionHandler));
            }

            _pipeName = pipeName;
            _log = log;
            _budget = budget;
            _uiDispatcher = uiDispatcher;
            _connectionHandler = connectionHandler;
        }

        /// <summary>待受に使うパイプ名。</summary>
        public string PipeName => _pipeName;

        /// <summary>ログの書き込み先。</summary>
        public string LogFilePath => _log.FilePath;

        /// <summary>応答サイズ予算の設定。</summary>
        public ResponseBudget Budget => _budget;

        /// <summary>現在の稼働状態の区分。呼ばれるたびに判定する。</summary>
        public HostStatus Status
        {
            get
            {
                lock (_gate)
                {
                    return StatusUnderLock();
                }
            }
        }

        /// <summary>クライアントと接続中かどうか。</summary>
        public bool IsClientConnected
        {
            get
            {
                lock (_gate)
                {
                    return _current != null && _current.IsClientConnected;
                }
            }
        }

        /// <summary>エディタのプロセスIDから待受に使うパイプ名を組み立てる。</summary>
        public static string BuildPipeName(int editorProcessId)
        {
            return "pmx-editor-mcp-" + editorProcessId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 新しい稼働世代を作って待受を始める。開始できないときは偽を返し、
        /// <paramref name="reason"/> に理由を入れる。
        /// </summary>
        public bool TryStart(out string reason)
        {
            lock (_gate)
            {
                switch (StatusUnderLock())
                {
                    case HostStatus.Running:
                        reason = "すでに稼働している。";
                        return false;

                    case HostStatus.Stopping:
                        // 旧世代と新世代の要求が並行すると、要求の直列処理とUIディスパッチの直列性が崩れる。
                        reason = "停止処理がまだ終わっていない。しばらく待ってからやり直す。";
                        return false;

                    case HostStatus.NotStartedInvalidBudget:
                        // 環境変数を読み直しても同じ結果になるため、開始は受け付けない。
                        reason = _budget.InvalidReason;
                        _log.Write("待受を開始しない: " + reason);
                        return false;

                    default:
                        StartUnderLock();
                        reason = null;
                        return true;
                }
            }
        }

        /// <summary>
        /// 現在の稼働世代の受付を止め、公開中のパイプインスタンスを閉じる。
        /// IPCサーバースレッドの終了は待たない。
        /// </summary>
        public void Stop()
        {
            lock (_gate)
            {
                HostGeneration generation = _current;
                if (generation == null)
                {
                    return;
                }

                generation.RequestStop();

                NamedPipeServerStream[] pipes = generation.Pipes.ToArray();
                generation.Pipes.Clear();
                foreach (NamedPipeServerStream pipe in pipes)
                {
                    // PipeOptions.Asynchronous で生成しているため、待受と読み取りのブロックがここで解ける。
                    try
                    {
                        pipe.Dispose();
                    }
                    catch (Exception exception)
                    {
                        _log.WriteException("待受中のパイプを閉じるときに例外が起きた。", exception);
                    }
                }

                _stopped = generation;
                _current = null;
                _log.Write("待受の受付を止めた: " + _pipeName);
            }
        }

        private HostStatus StatusUnderLock()
        {
            if (!_budget.IsValid)
            {
                return HostStatus.NotStartedInvalidBudget;
            }

            if (_current != null)
            {
                return HostStatus.Running;
            }

            if (_stopped != null && _stopped.HasLiveThreads)
            {
                return HostStatus.Stopping;
            }

            return HostStatus.Stopped;
        }

        private void StartUnderLock()
        {
            HostGeneration generation = new HostGeneration(_uiDispatcher);
            Thread thread = new Thread(() => ServerLoop(generation));
            thread.IsBackground = true;
            thread.Name = "pmx-editor-mcp-ipc";
            generation.ServerThread = thread;

            _current = generation;
            _stopped = null;
            _log.Write("待受を始める: " + _pipeName);
            thread.Start();
        }

        private void ServerLoop(HostGeneration generation)
        {
            int consecutivePrepareFailures = 0;

            while (true)
            {
                NamedPipeServerStream pipe;
                Exception prepareFailure;
                lock (_gate)
                {
                    if (generation.IsStopRequested)
                    {
                        break;
                    }

                    // 停止手順とこのロックを共有するため、生成したインスタンスが閉じられずに待受へ残ることはない。
                    pipe = TryCreatePipe(_pipeName, out prepareFailure);
                    if (pipe != null)
                    {
                        generation.Pipes.Add(pipe);
                    }
                }

                if (pipe == null)
                {
                    // 失敗が続く間ずっと同じ記録を積むと、ローテーションで有用な履歴が押し流される。
                    if (consecutivePrepareFailures == 0)
                    {
                        _log.WriteException("待受の準備に失敗した。解けるまで繰り返し試みる。", prepareFailure);
                    }

                    consecutivePrepareFailures++;

                    // 直ちに再試行すると失敗が続く間ずっと回り続けるため、間を置いてから次の反復へ戻る。
                    Thread.Sleep(PrepareRetryDelayMs);
                    continue;
                }

                if (consecutivePrepareFailures > 0)
                {
                    _log.Write("待受の準備に成功した: " + _pipeName
                        + "(" + consecutivePrepareFailures.ToString(CultureInfo.InvariantCulture) + "回失敗した後)");
                    consecutivePrepareFailures = 0;
                }

                try
                {
                    pipe.WaitForConnection();
                }
                catch (Exception exception)
                {
                    NoteLoopFailure(generation, exception);
                    Discard(generation, pipe);
                    continue;
                }

                generation.NoteConnected();
                _log.Write("接続を受けた: " + _pipeName);

                // この接続の処理は別のスレッドへ渡し、待受はすぐ次のインスタンスへ戻る。待たせると、
                // 2本目の接続が1本目の終わりまで開けない。
                NamedPipeServerStream accepted = pipe;
                Thread worker = null;
                worker = new Thread(() => Serve(generation, accepted, worker));
                worker.IsBackground = true;
                worker.Name = "pmx-editor-mcp-connection";

                // 覚えてから走らせる。走らせてから覚えると、覚える前に終わったスレッドを
                // 消し忘れて、止まり切ったあとも停止処理中に見える。
                generation.AddWorker(worker);
                worker.Start();
            }

            _log.Write("待受を終えた: " + _pipeName);
        }

        /// <summary>接続1本を処理して後始末する。待受のスレッドとは別のスレッドで走る。</summary>
        private void Serve(HostGeneration generation, NamedPipeServerStream pipe, Thread worker)
        {
            try
            {
                _connectionHandler(pipe, generation);
            }
            catch (Exception exception)
            {
                NoteLoopFailure(generation, exception);
            }
            finally
            {
                generation.NoteDisconnected();
                Discard(generation, pipe);
                generation.RemoveWorker(worker);
                _log.Write("切断した: " + _pipeName);
            }
        }

        private void NoteLoopFailure(HostGeneration generation, Exception exception)
        {
            if (generation.IsStopRequested)
            {
                // 停止手順がパイプを閉じたことによる解除で、異常ではない。
                // 待受中の解除と接続中の解除のどちらもここへ来る。
                _log.Write("停止により中断した: " + _pipeName);
                return;
            }

            _log.WriteException("待受で例外が起きた。", exception);
        }

        private void Discard(HostGeneration generation, NamedPipeServerStream pipe)
        {
            lock (_gate)
            {
                generation.Pipes.Remove(pipe);
            }

            try
            {
                pipe.Dispose();
            }
            catch (Exception)
            {
                // 閉じ済みのインスタンスを閉じても害はない。
            }
        }

        private static NamedPipeServerStream TryCreatePipe(string pipeName, out Exception failure)
        {
            try
            {
                PipeSecurity security = new PipeSecurity();
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    security.AddAccessRule(
                        new PipeAccessRule(identity.User, PipeAccessRights.FullControl, AccessControlType.Allow));
                }

                failure = null;
                return new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    // これでなければ、停止手順がパイプを閉じても待受・読み取りのブロックが解けない。
                    PipeOptions.Asynchronous,
                    PipeBufferSizeBytes,
                    PipeBufferSizeBytes,
                    security);
            }
            catch (Exception exception)
            {
                failure = exception;
                return null;
            }
        }
    }
}
