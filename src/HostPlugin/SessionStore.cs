using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace PmxEditorMcp
{
    /// <summary>
    /// 接続元のプロセスに結び付く、接続をまたいで生き続ける入れ物。切断では失われず、
    /// 識別子を提示した繋ぎ直しで同じものへ戻る。
    /// </summary>
    public sealed class Session
    {
        internal Session(string id, HandleLedger handles, EventQueue events, ClientProcess client)
        {
            Id = id;
            Handles = handles;
            Events = events;
            Client = client;
        }

        /// <summary>見張りの登録と、終わったかどうかを守る錠。</summary>
        internal object WatchGate { get; } = new object();

        /// <summary>所有者の終了を見張っている登録。回収のときに解く。</summary>
        internal RegisteredWaitHandle Watch { get; set; }

        private volatile bool _ended;

        /// <summary>終わらせたあとなら真。一度真になったら戻らない。</summary>
        public bool IsEnded
        {
            get { return _ended; }

            internal set { _ended = value; }
        }

        /// <summary>ホストが発行した識別子。128ビットの乱数を16進で表した文字列。</summary>
        public string Id { get; }

        public HandleLedger Handles { get; }

        public EventQueue Events { get; }

        /// <summary>このセッションを所有する接続元のプロセス。</summary>
        public ClientProcess Client { get; }
    }

    /// <summary>
    /// ホストが持つセッションの集まり。識別子は推測できない乱数にする。
    /// 複数のスレッドから同時に呼んでよい。
    /// </summary>
    public sealed class SessionStore
    {
        /// <summary>識別子の長さ。128ビットを16進で表す。</summary>
        private const int IdBytes = 16;

        private readonly Dictionary<string, Session> _sessions =
            new Dictionary<string, Session>(StringComparer.Ordinal);

        private readonly object _gate = new object();

        private readonly RandomNumberGenerator _random = RandomNumberGenerator.Create();

        private readonly HostLog _log;

        private readonly HandleIdIssuer _handleIds;

        private readonly EventSequenceIssuer _eventSequence;

        /// <summary>要求の処理を直列化する錠。</summary>
        private readonly object _serialGate;

        private readonly Func<IUiInvoker> _currentUi;

        /// <summary>
        /// <paramref name="currentUi"/> は、終わらせるセッションのハンドルを手放す時点の稼働世代を返す。
        /// 稼働世代が無いときは断る窓口を返す。
        /// </summary>
        public SessionStore(
            HostLog log,
            HandleIdIssuer handleIds,
            EventSequenceIssuer eventSequence,
            object serialGate,
            Func<IUiInvoker> currentUi)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (handleIds == null)
            {
                throw new ArgumentNullException(nameof(handleIds));
            }

            if (eventSequence == null)
            {
                throw new ArgumentNullException(nameof(eventSequence));
            }

            if (serialGate == null)
            {
                throw new ArgumentNullException(nameof(serialGate));
            }

            if (currentUi == null)
            {
                throw new ArgumentNullException(nameof(currentUi));
            }

            _log = log;
            _handleIds = handleIds;
            _eventSequence = eventSequence;
            _serialGate = serialGate;
            _currentUi = currentUi;
        }

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _sessions.Count;
                }
            }
        }

        /// <summary>
        /// 接続に結び付けるセッションを決める。<paramref name="presented"/> が null か知らない識別子
        /// なら新しいセッションを作り、知っている識別子でその所有者が
        /// <paramref name="client"/> と同じプロセスなら、そのセッションへ戻す。別のプロセスからの
        /// 提示は偽を返して断る。戻したセッションは自分の所有者を保ち続け、そのときは
        /// <paramref name="client"/> の持ち主が閉じる。
        /// </summary>
        public bool TryResolve(string presented, ClientProcess client, out Session session)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            Session created;
            lock (_gate)
            {
                Session existing;
                if (presented != null && _sessions.TryGetValue(presented, out existing))
                {
                    if (existing.Client.HasExited || existing.Client.Id != client.Id)
                    {
                        session = null;
                        return false;
                    }

                    session = existing;
                    return true;
                }

                session = new Session(
                    NewId(), new HandleLedger(_log, _handleIds), new EventQueue(_eventSequence), client);
                _sessions.Add(session.Id, session);
                created = session;
            }

            // すでに終わっているプロセスの待機ハンドルは、登録した時点で合図される。
            RegisteredWaitHandle watch = ThreadPool.RegisterWaitForSingleObject(
                created.Client.Exited,
                (state, timedOut) => End(((Session)state).Id),
                created,
                Timeout.Infinite,
                true);

            lock (created.WatchGate)
            {
                created.Watch = watch;
                if (created.IsEnded)
                {
                    watch.Unregister(null);
                }
            }

            return true;
        }

        /// <summary>
        /// セッションを終わらせる。ハンドルを解放して台帳を閉じ、所有者の待機ハンドルと見張りを
        /// 解く。知らない識別子と、終わり済みの識別子では何もせず偽を返す。走っている要求が戻るまで
        /// 待つ。台帳と溜め場の錠を持ったまま呼ばない。
        /// </summary>
        public bool End(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            lock (_serialGate)
            {
                return EndUnderSerialGate(id);
            }
        }

        private bool EndUnderSerialGate(string id)
        {
            Session session;
            lock (_gate)
            {
                if (!_sessions.TryGetValue(id, out session))
                {
                    return false;
                }

                _sessions.Remove(id);
            }

            lock (session.WatchGate)
            {
                session.IsEnded = true;
                if (session.Watch != null)
                {
                    session.Watch.Unregister(null);
                }
            }

            try
            {
                session.Handles.ReleaseAll(_currentUi());
                session.Events.Close();
            }
            finally
            {
                session.Client.Dispose();
            }

            return true;
        }

        private string NewId()
        {
            byte[] bytes = new byte[IdBytes];
            _random.GetBytes(bytes);

            StringBuilder text = new StringBuilder(IdBytes * 2);
            foreach (byte value in bytes)
            {
                text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }
    }
}
