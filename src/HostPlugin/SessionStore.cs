using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

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

        public SessionStore(HostLog log, HandleIdIssuer handleIds, EventSequenceIssuer eventSequence)
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

            _log = log;
            _handleIds = handleIds;
            _eventSequence = eventSequence;
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

                return true;
            }
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
                text.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }
    }
}
