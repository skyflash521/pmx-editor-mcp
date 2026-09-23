using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>ハンドルを解放した結果。失効させた順と、そのうち解放が例外になったものを持つ。</summary>
    public sealed class HandleReleaseResult
    {
        public HandleReleaseResult(IList<int> invalidated, IList<int> failed)
        {
            Invalidated = new ReadOnlyCollection<int>(invalidated);
            Failed = new ReadOnlyCollection<int>(failed);
        }

        /// <summary>台帳から失効させたハンドル。子から依存元への順。</summary>
        public IList<int> Invalidated { get; }

        /// <summary>
        /// 解放が例外になったハンドル。<see cref="Invalidated"/> の部分集合で、失効はしている。
        /// </summary>
        public IList<int> Failed { get; }
    }

    /// <summary>
    /// セッションが保つ長寿命オブジェクトの台帳。ツールはハンドルIDで参照し、実体はホストが持つ。
    /// 解放の仕方は型ごとに違うので、発行するときに受け取って覚える。
    /// 複数のスレッドから同時に呼んでよい。解放の処理の中から台帳を呼んでもよい。
    /// </summary>
    public sealed class HandleLedger
    {
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

        /// <summary>依存元ごとの、それへ直に依存する有効なハンドル。発行した順に並ぶ。</summary>
        private readonly Dictionary<int, List<int>> _dependents = new Dictionary<int, List<int>>();

        private readonly object _gate = new object();

        private readonly HostLog _log;

        private readonly HandleIdIssuer _issuer;

        private int _lastId;

        private bool _closed;

        public HandleLedger(HostLog log, HandleIdIssuer issuer)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (issuer == null)
            {
                throw new ArgumentNullException(nameof(issuer));
            }

            _log = log;
            _issuer = issuer;
        }

        /// <summary>いま有効なハンドルの数。</summary>
        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _entries.Count;
                }
            }
        }

        /// <summary>台帳が閉じているか。まとめて解放したあとは真。</summary>
        public bool IsClosed
        {
            get
            {
                lock (_gate)
                {
                    return _closed;
                }
            }
        }

        /// <summary>この台帳が最後に発行したハンドルのID。まだ1件も発行していなければ0。</summary>
        public int LastIssuedId
        {
            get
            {
                lock (_gate)
                {
                    return _lastId;
                }
            }
        }

        /// <summary>
        /// ハンドルを発行する。<paramref name="dependencies"/> は生成に関与したハンドルで、
        /// この実体はそれらより先に解放される。有効でない依存元を渡すのは呼び出し側の誤り。
        /// 閉じた台帳では <see cref="InvalidOperationException"/>。
        /// </summary>
        public int Issue(
            string type, object target, Action release, IEnumerable<int> dependencies = null)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (release == null)
            {
                throw new ArgumentNullException(nameof(release));
            }

            if (type.Trim().Length == 0)
            {
                throw new ArgumentException("空にも空白だけにもできない。", nameof(type));
            }

            int[] listed = (dependencies ?? Enumerable.Empty<int>()).ToArray();
            lock (_gate)
            {
                if (_closed)
                {
                    throw new InvalidOperationException("台帳は閉じている。");
                }

                foreach (int dependency in listed)
                {
                    if (!_entries.ContainsKey(dependency))
                    {
                        throw new ArgumentException(
                            "有効でない依存元がある: " + dependency, nameof(dependencies));
                    }
                }

                _lastId = _issuer.Next();
                _entries.Add(_lastId, new Entry(type, target, release, listed));
                foreach (int dependency in listed.Distinct())
                {
                    List<int> direct;
                    if (!_dependents.TryGetValue(dependency, out direct))
                    {
                        direct = new List<int>();
                        _dependents.Add(dependency, direct);
                    }

                    direct.Add(_lastId);
                }

                return _lastId;
            }
        }

        /// <summary>
        /// そのハンドルが指す実体。型を問わずに取り出すので、受け取る側がその実体を扱えるかを
        /// 判ずる。解放済み・知らないハンドルでは偽。
        /// </summary>
        public bool TryGet(int id, out object target)
        {
            lock (_gate)
            {
                target = null;
                Entry entry;
                if (!_entries.TryGetValue(id, out entry))
                {
                    return false;
                }

                target = entry.Target;

                return true;
            }
        }

        /// <summary>
        /// ハンドルの実体を取り出す。知らない・解放済み・型が違うときは偽。
        /// </summary>
        public bool TryGet(int id, string type, out object target)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            lock (_gate)
            {
                target = null;
                Entry entry;
                if (!_entries.TryGetValue(id, out entry)
                    || !string.Equals(entry.Type, type, StringComparison.Ordinal))
                {
                    return false;
                }

                target = entry.Target;

                return true;
            }
        }

        /// <summary>そのハンドルが有効か。</summary>
        public bool IsValid(int id)
        {
            lock (_gate)
            {
                return _entries.ContainsKey(id);
            }
        }

        /// <summary>
        /// ハンドルを解放する。依存する子を先に解放してから自分を解放し、失効させる。知らない・
        /// 解放済みのハンドルでは偽。
        /// </summary>
        public bool TryRelease(int id, out HandleReleaseResult result)
        {
            result = null;
            List<Taken> taken;
            lock (_gate)
            {
                if (!_entries.ContainsKey(id))
                {
                    return false;
                }

                taken = Take(Ordered(new[] { id }));
            }

            result = ReleaseInOrder(taken);

            return true;
        }

        /// <summary>
        /// 指したハンドルをまとめて解放する。指したものとその依存子を合わせ、重なりを除いて
        /// それぞれをちょうど一度だけ解放する。どれか1つでも台帳に無ければ、何も解放せず偽。
        /// </summary>
        public bool TryReleaseAll(IEnumerable<int> ids, out HandleReleaseResult result)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            result = null;
            int[] listed = ids.ToArray();
            List<Taken> taken;
            lock (_gate)
            {
                if (listed.Any(id => !_entries.ContainsKey(id)))
                {
                    return false;
                }

                taken = Take(Ordered(listed));
            }

            result = ReleaseInOrder(taken);

            return true;
        }

        /// <summary>
        /// 指定したIDより後に発行したハンドルを解放し、失効させる。解放の順はまとめて解放するときと
        /// 同じく子から依存元へ。結果を破棄する呼び出しの後始末に使うもので、台帳は閉じない。
        /// </summary>
        public HandleReleaseResult ReleaseIssuedAfter(int id)
        {
            List<Taken> taken;
            lock (_gate)
            {
                taken = Take(Ordered(
                    _entries.Keys.Where(i => i > id).OrderByDescending(i => i).ToList()));
            }

            return ReleaseInOrder(taken);
        }

        /// <summary>
        /// すべてのハンドルを解放し、台帳を閉じる。セッションが終わるときに呼ぶ。解放の順は子から依存元へ。
        /// 1件も無くても記録を残す。閉じたあとは発行できない。二度呼んでもよい。
        /// </summary>
        public HandleReleaseResult ReleaseAll()
        {
            List<Taken> taken;
            lock (_gate)
            {
                _closed = true;
                taken = Take(Ordered(_entries.Keys.OrderByDescending(i => i).ToList()));
            }

            HandleReleaseResult result = ReleaseInOrder(taken);
            _log.Write(
                "全ハンドルの解放: 件数=" + result.Invalidated.Count
                    + " 失敗=" + result.Failed.Count);

            return result;
        }

        /// <summary>
        /// 並べたハンドルを、それぞれへ依存するハンドルを先に置いて解放する順。子が先に並び、直に
        /// 依存するものは後に発行したものから並ぶ。重なりは最初の1つだけを残す。
        /// </summary>
        private List<int> Ordered(IEnumerable<int> roots)
        {
            List<int> order = new List<int>();
            HashSet<int> seen = new HashSet<int>();
            Stack<KeyValuePair<int, int>> path = new Stack<KeyValuePair<int, int>>();
            foreach (int root in roots)
            {
                if (!seen.Add(root))
                {
                    continue;
                }

                path.Push(new KeyValuePair<int, int>(root, Direct(root).Count));
                while (path.Count != 0)
                {
                    KeyValuePair<int, int> top = path.Pop();
                    IList<int> direct = Direct(top.Key);
                    int next = top.Value - 1;
                    while (next >= 0 && seen.Contains(direct[next]))
                    {
                        next--;
                    }

                    if (next < 0)
                    {
                        order.Add(top.Key);

                        continue;
                    }

                    int child = direct[next];
                    seen.Add(child);
                    path.Push(new KeyValuePair<int, int>(top.Key, next));
                    path.Push(new KeyValuePair<int, int>(child, Direct(child).Count));
                }
            }

            return order;
        }

        /// <summary>そのハンドルへ直に依存する有効なハンドル。発行した順に並ぶ。</summary>
        private IList<int> Direct(int id)
        {
            List<int> direct;

            return _dependents.TryGetValue(id, out direct) ? direct : (IList<int>)new int[0];
        }

        /// <summary>
        /// 並べた順に台帳から外す。同じIDが二度並んでも一度しか外さない。
        /// </summary>
        private List<Taken> Take(IEnumerable<int> order)
        {
            List<Taken> taken = new List<Taken>();
            foreach (int id in order)
            {
                Entry entry;
                if (_entries.TryGetValue(id, out entry))
                {
                    _entries.Remove(id);
                    _dependents.Remove(id);
                    foreach (int dependency in entry.Dependencies)
                    {
                        List<int> direct;
                        if (_dependents.TryGetValue(dependency, out direct) && direct.Remove(id)
                            && direct.Count == 0)
                        {
                            _dependents.Remove(dependency);
                        }
                    }

                    taken.Add(new Taken(id, entry));
                }
            }

            return taken;
        }

        /// <summary>
        /// 外した順に解放する。解放が例外になっても失効はそのままで、後続の解放は続ける。
        /// </summary>
        private HandleReleaseResult ReleaseInOrder(IList<Taken> taken)
        {
            List<int> invalidated = new List<int>();
            List<int> failed = new List<int>();
            List<string> written = new List<string>();
            foreach (Taken item in taken)
            {
                invalidated.Add(item.Id);
                try
                {
                    item.Entry.Release();
                    written.Add("ハンドルの解放: id=" + item.Id + " type=" + item.Entry.Type);
                }
                catch (Exception exception)
                {
                    failed.Add(item.Id);
                    written.Add(
                        "ハンドルの解放で例外が起きた: id=" + item.Id + " type=" + item.Entry.Type
                            + Environment.NewLine + exception);
                }
            }

            _log.WriteAll(written);

            return new HandleReleaseResult(invalidated, failed);
        }

        /// <summary>台帳から外した1件。</summary>
        private sealed class Taken
        {
            public Taken(int id, Entry entry)
            {
                Id = id;
                Entry = entry;
            }

            public int Id { get; }

            public Entry Entry { get; }
        }

        private sealed class Entry
        {
            private readonly Action _release;

            public Entry(string type, object target, Action release, IList<int> dependencies)
            {
                Type = type;
                Target = target;
                Dependencies = dependencies;
                _release = release;
            }

            public string Type { get; }

            public object Target { get; }

            public IList<int> Dependencies { get; }

            public void Release()
            {
                _release();
            }
        }
    }
}
