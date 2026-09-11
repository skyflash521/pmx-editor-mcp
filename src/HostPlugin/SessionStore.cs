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

        /// <summary>
        /// 終わらせたあとなら真。一度真になったら戻らない。別のスレッドが終わらせた結果を
        /// 要求の処理が読むので、書いたことが読む側へ必ず見える形で持つ。
        /// </summary>
        public bool IsEnded
        {
            get { return _ended; }

            internal set { _ended = value; }
        }

        /// <summary>ホストが発行した識別子。128ビットの乱数を16進で表した文字列。</summary>
        public string Id { get; }

        /// <summary>このセッションが保つハンドルの台帳。</summary>
        public HandleLedger Handles { get; }

        /// <summary>このセッションが溜めるイベント。</summary>
        public EventQueue Events { get; }

        /// <summary>このセッションを所有する接続元のプロセス。</summary>
        public ClientProcess Client { get; }
    }

    /// <summary>
    /// ホストが持つセッションの集まり。識別子は推測できない乱数にする——連番やプロセスIDだと、
    /// 別のクライアントが値を当てて他のセッションのハンドルとイベントを引き継げる。
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

        /// <summary>
        /// 要求の処理を直列化する錠。終わらせる前にこれを取るのは、要求が使っている最中の台帳と
        /// 溜め場を閉じないため。所有者の終了による回収も明示の終了と同じここを通す。
        /// </summary>
        private readonly object _serialGate;

        public SessionStore(
            HostLog log,
            HandleIdIssuer handleIds,
            EventSequenceIssuer eventSequence,
            object serialGate)
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

            _log = log;
            _handleIds = handleIds;
            _eventSequence = eventSequence;
            _serialGate = serialGate;
        }

        /// <summary>いま持っているセッションの数。</summary>
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
        /// 提示は偽を返して断る——認めると、所有者を接続元プロセスに置いた意味が失われる。
        /// 戻したセッションは自分の所有者を保ち続けるので、そのときは
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

            // 所有者の終了を見張る。すでに終わっていれば登録した時点で合図されるので、handshake の
            // 途中で終わった接続元のセッションも、作った直後に回収される。合図が先に走ると登録を
            // 覚える前に終わりうるので、覚えたところで終わり済みかを見て、そのときは自分で解く。
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
        /// 解く。知らない識別子と、終わり済みの識別子では何もせず偽を返す。
        /// </summary>
        public bool End(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            // 走っている要求が戻るまで待つ。取る順は直列化の錠が先で、持ち物の錠を持ったまま
            // こちらを取る経路は作らない。
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

            // 見張りの解除と後始末は、台帳の錠の外で行う。所有者の終了から呼ばれる経路と、明示の
            // 終了から呼ばれる経路が同じここへ来るので、取り出せた側だけが進む。
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
                session.Handles.ReleaseAll();
                session.Events.Close();
            }
            finally
            {
                session.Client.Dispose();
            }

            return true;
        }

        /// <summary>その識別子のセッション。知らなければ null。</summary>
        public Session Find(string id)
        {
            if (id == null)
            {
                return null;
            }

            lock (_gate)
            {
                Session session;

                return _sessions.TryGetValue(id, out session) ? session : null;
            }
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
