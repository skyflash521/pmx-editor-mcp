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

        private const int RetryDelayMs = 500;

        private readonly object _gate = new object();
        private readonly string _pipeName;
        private readonly HostLog _log;
        private readonly ResponseBudget _budget;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly IModalWindowProbe _modals;
        private readonly ConnectionHandler _connectionHandler;

        private HostGeneration _current;
        private HostGeneration _stopped;

        public McpHost(
            string pipeName,
            HostLog log,
            ResponseBudget budget,
            IUiDispatcher uiDispatcher,
            ConnectionHandler connectionHandler,
            IModalWindowProbe modals = null)
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
            _modals = modals
                ?? new DesktopModalWindowProbe(TimeSpan.FromMilliseconds(RetryDelayMs));
        }

        public string PipeName => _pipeName;

        public string LogFilePath => _log.FilePath;

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

        /// <summary>いまの稼働世代。止まっている間は、常に断る窓口。</summary>
        public IUiInvoker CurrentUi
        {
            get
            {
                lock (_gate)
                {
                    return (IUiInvoker)_current ?? DeclinedUiInvoker.Instance;
                }
            }
        }

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
                        reason = "停止処理がまだ終わっていない。しばらく待ってからやり直す。";
                        return false;

                    case HostStatus.NotStartedInvalidBudget:
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
            HostGeneration generation = new HostGeneration(
                _uiDispatcher, _modals, TimeSpan.FromMilliseconds(RetryDelayMs));
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

                    pipe = TryCreatePipe(_pipeName, out prepareFailure);
                    if (pipe != null)
                    {
                        generation.Pipes.Add(pipe);
                    }
                }

                if (pipe == null)
                {
                    if (consecutivePrepareFailures == 0)
                    {
                        _log.WriteException("待受の準備に失敗した。解けるまで繰り返し試みる。", prepareFailure);
                    }

                    consecutivePrepareFailures++;

                    Thread.Sleep(RetryDelayMs);
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

                NamedPipeServerStream accepted = pipe;
                Thread worker = null;
                worker = new Thread(() => Serve(generation, accepted, worker));
                worker.IsBackground = true;
                worker.Name = "pmx-editor-mcp-connection";

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
