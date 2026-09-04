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
    /// 接続が保つ長寿命オブジェクトの台帳。ツールはハンドルIDで参照し、実体はホストが持つ。
    /// 解放の仕方は型ごとに違うので、発行するときに受け取って覚える。
    /// 複数のスレッドから同時に呼んでよい。解放の処理の中から台帳を呼んでもよい。
    /// </summary>
    public sealed class HandleLedger
    {
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

        private readonly object _gate = new object();

        private readonly HostLog _log;

        private int _lastId;

        private bool _closed;

        public HandleLedger(HostLog log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            _log = log;
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

        /// <summary>最後に発行したハンドルのID。まだ1件も発行していなければ0。</summary>
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

                _lastId++;
                _entries.Add(_lastId, new Entry(type, target, release, listed));

                return _lastId;
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

                taken = Take(Dependents(id).Concat(new[] { id }));
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
                List<int> order = new List<int>();
                foreach (int issued in _entries.Keys.Where(i => i > id).OrderByDescending(i => i))
                {
                    foreach (int dependent in Dependents(issued).Concat(new[] { issued }))
                    {
                        if (!order.Contains(dependent))
                        {
                            order.Add(dependent);
                        }
                    }
                }

                taken = Take(order);
            }

            return ReleaseInOrder(taken);
        }

        /// <summary>
        /// すべてのハンドルを解放し、台帳を閉じる。接続が終わるときに呼ぶ。解放の順は子から依存元へ。
        /// 1件も無くても記録を残す。閉じたあとは発行できない。二度呼んでもよい。
        /// </summary>
        public HandleReleaseResult ReleaseAll()
        {
            List<Taken> taken;
            lock (_gate)
            {
                _closed = true;
                List<int> order = new List<int>();
                foreach (int id in _entries.Keys.OrderByDescending(i => i))
                {
                    foreach (int dependent in Dependents(id).Concat(new[] { id }))
                    {
                        if (!order.Contains(dependent))
                        {
                            order.Add(dependent);
                        }
                    }
                }

                taken = Take(order);
            }

            HandleReleaseResult result = ReleaseInOrder(taken);
            _log.Write(
                "全ハンドルの解放: 件数=" + result.Invalidated.Count
                    + " 失敗=" + result.Failed.Count);

            return result;
        }

        /// <summary>そのハンドルへ直に、または間に挟んで依存するハンドル。子が先に並ぶ。</summary>
        private IList<int> Dependents(int id)
        {
            List<int> found = new List<int>();
            foreach (KeyValuePair<int, Entry> pair in _entries.OrderByDescending(p => p.Key))
            {
                if (pair.Value.Dependencies.Contains(id))
                {
                    foreach (int dependent in Dependents(pair.Key).Concat(new[] { pair.Key }))
                    {
                        if (!found.Contains(dependent))
                        {
                            found.Add(dependent);
                        }
                    }
                }
            }

            return found;
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
            foreach (Taken item in taken)
            {
                invalidated.Add(item.Id);
                try
                {
                    item.Entry.Release();
                    _log.Write("ハンドルの解放: id=" + item.Id + " type=" + item.Entry.Type);
                }
                catch (Exception exception)
                {
                    failed.Add(item.Id);
                    _log.WriteException(
                        "ハンドルの解放で例外が起きた: id=" + item.Id + " type=" + item.Entry.Type,
                        exception);
                }
            }

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
