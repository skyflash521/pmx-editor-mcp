using System;
using System.Collections.Generic;
using PEPlugin.Form;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    public sealed class ModelStamp : IEquatable<ModelStamp>
    {
        private readonly int _updates;

        private readonly int _moves;

        private readonly int _undos;

        private readonly int _redos;

        private readonly string _identity;

        public ModelStamp(int updates, int moves, int undos, int redos, string identity)
        {
            _identity = identity;
            _updates = updates;
            _moves = moves;
            _undos = undos;
            _redos = redos;
        }

        public bool Equals(ModelStamp other)
        {
            return other != null
                && other._updates == _updates
                && other._moves == _moves
                && other._undos == _undos
                && other._redos == _redos
                && string.Equals(other._identity, _identity, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ModelStamp);
        }

        public override int GetHashCode()
        {
            return (((((_updates * 397) ^ _moves) * 397) ^ _undos) * 397) ^ _redos;
        }
    }

    public static class ModelStampSource
    {
        private static string FileState(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            if (OnNetwork(path))
            {
                throw new NotSupportedException("ネットワーク上のパスは照会しない。");
            }

            System.IO.FileInfo info = new System.IO.FileInfo(path);

            return info.Exists
                ? info.LastWriteTimeUtc.Ticks + "|" + info.Length
                : string.Empty;
        }

        private static bool OnNetwork(string path)
        {
            if (path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            {
                return true;
            }

            string root = System.IO.Path.GetPathRoot(path);

            return !string.IsNullOrEmpty(root)
                && new System.IO.DriveInfo(root).DriveType == System.IO.DriveType.Network;
        }

        public static ModelStamp Read(
            IModelUpdates updates, IPEFormConnector form, IPXPmxConnector pmx, Func<string, string> file)
        {
            return new ModelStamp(
                updates.Count,
                updates.HistoryMoves,
                form.UndoCount,
                form.RedoCount,
                string.Join(
                    "|",
                    pmx.CurrentPath,
                    (file ?? FileState)(pmx.CurrentPath),
                    form.VertexItemsCount,
                    form.FaceItemsCount,
                    form.MaterialItemsCount,
                    form.BoneItemsCount,
                    form.MorphItemsCount,
                    form.NodeItemsCount,
                    form.BodyItemsCount,
                    form.JointItemsCount));
        }
    }

    public sealed class CloneCache
    {
        private readonly Func<ModelStamp> _stamp;

        private readonly Dictionary<PmxFlow, Kept> _kept = new Dictionary<PmxFlow, Kept>();

        private bool _read;

        private bool _wrote;

        public CloneCache(Func<ModelStamp> stamp)
        {
            _stamp = stamp ?? throw new ArgumentNullException(nameof(stamp));
        }

        public bool TryGet(PmxFlow flow, out object clone)
        {
            Kept kept;
            if (_kept.TryGetValue(flow, out kept) && kept.Stamp.Equals(Stamp()))
            {
                clone = kept.Clone;

                return true;
            }

            _kept.Remove(flow);
            clone = null;

            return false;
        }

        public void Put(PmxFlow flow, object clone)
        {
            ModelStamp stamp = Stamp();
            if (stamp != null)
            {
                _kept[flow] = new Kept(clone, stamp);
            }
        }

        public void NoteRead()
        {
            _read = true;
        }

        public void NoteWrite()
        {
            _wrote = true;
        }

        public void Enter()
        {
            _read = false;
            _wrote = false;
        }

        public void Exit()
        {
            if (_wrote || !_read)
            {
                _kept.Clear();
            }
        }

        private ModelStamp Stamp()
        {
            try
            {
                return _stamp();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private sealed class Kept
        {
            public Kept(object clone, ModelStamp stamp)
            {
                Clone = clone;
                Stamp = stamp;
            }

            public object Clone { get; }

            public ModelStamp Stamp { get; }
        }
    }

    public sealed class CloneCacheScope : IUiDispatcher
    {
        private readonly IUiDispatcher _inner;

        private readonly CloneCache _cache;

        public CloneCacheScope(IUiDispatcher inner, CloneCache cache)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public IAsyncResult Begin(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            return _inner.Begin(() =>
            {
                _cache.Enter();
                try
                {
                    action();
                }
                finally
                {
                    _cache.Exit();
                }
            });
        }

        public bool Wait(IAsyncResult pending, TimeSpan limit)
        {
            return _inner.Wait(pending, limit);
        }

        public void End(IAsyncResult pending)
        {
            _inner.End(pending);
        }
    }
}
