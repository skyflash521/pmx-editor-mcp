using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class HandleLedgerTests : IDisposable
    {
        private const string UiModel = "uiModel";

        private const string Listener = "eventListener";

        private readonly string _root;

        private readonly HostLog _log;

        private readonly HandleLedger _ledger;

        private readonly List<int> _releases = new List<int>();

        public HandleLedgerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-handles-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _log = new HostLog(Path.Combine(_root, "host.log"));
            _ledger = new HandleLedger(_log);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void HandlesArePositiveAndDistinct()
        {
            int first = Issue(UiModel);
            int second = Issue(UiModel);

            Assert.Equal(1, first);
            Assert.Equal(2, second);
        }

        [Fact]
        public void TheTargetIsTakenBackByItsTypeAndId()
        {
            object target = new object();
            int id = _ledger.Issue(UiModel, target, () => { });

            object found;

            Assert.True(_ledger.TryGet(id, UiModel, out found));
            Assert.Same(target, found);
        }

        [Fact]
        public void AHandleOfAnotherTypeIsNotTakenBack()
        {
            int id = Issue(UiModel);

            object found;

            Assert.False(_ledger.TryGet(id, Listener, out found));
            Assert.Null(found);
        }

        [Fact]
        public void AnUnknownHandleIsNotTakenBack()
        {
            object found;

            Assert.False(_ledger.TryGet(7, UiModel, out found));
        }

        [Fact]
        public void AReleasedHandleIsNotTakenBack()
        {
            int id = Issue(UiModel);
            HandleReleaseResult result;
            _ledger.TryRelease(id, out result);

            object found;

            Assert.False(_ledger.TryGet(id, UiModel, out found));
            Assert.False(_ledger.IsValid(id));
        }

        [Fact]
        public void ReleasingRunsTheReleaseOfTheType()
        {
            int id = Issue(UiModel);

            HandleReleaseResult result;

            Assert.True(_ledger.TryRelease(id, out result));
            Assert.Equal(new[] { id }, result.Invalidated);
            Assert.Empty(result.Failed);
            Assert.Equal(new[] { id }, _releases);
        }

        [Fact]
        public void ReleasingTwiceStopsAtTheSecond()
        {
            int id = Issue(UiModel);
            HandleReleaseResult result;
            _ledger.TryRelease(id, out result);

            Assert.False(_ledger.TryRelease(id, out result));
            Assert.Null(result);
            Assert.Equal(new[] { id }, _releases);
        }

        [Fact]
        public void DependentsAreReleasedBeforeTheHandleTheyDependOn()
        {
            int model = Issue(UiModel);
            int listener = Issue(Listener, model);

            HandleReleaseResult result;
            _ledger.TryRelease(model, out result);

            Assert.Equal(new[] { listener, model }, result.Invalidated);
            Assert.Equal(new[] { listener, model }, _releases);
        }

        [Fact]
        public void DependentsOfDependentsAreReleasedFirst()
        {
            int model = Issue(UiModel);
            int listener = Issue(Listener, model);
            int leaf = Issue(Listener, listener);

            HandleReleaseResult result;
            _ledger.TryRelease(model, out result);

            Assert.Equal(new[] { leaf, listener, model }, result.Invalidated);
        }

        [Fact]
        public void AHandleWithTwoDependenciesIsReleasedWithEitherOfThem()
        {
            int model = Issue(UiModel);
            int connector = Issue(UiModel);
            int listener = _ledger.Issue(
                Listener, new object(), () => _releases.Add(3), new[] { model, connector });

            HandleReleaseResult result;
            _ledger.TryRelease(connector, out result);

            Assert.Equal(new[] { listener, connector }, result.Invalidated);
            Assert.True(_ledger.IsValid(model));
        }

        [Fact]
        public void ReleasingOnlyTheDependentKeepsTheHandleItDependsOn()
        {
            int model = Issue(UiModel);
            int listener = Issue(Listener, model);

            HandleReleaseResult result;
            _ledger.TryRelease(listener, out result);

            Assert.Equal(new[] { listener }, result.Invalidated);
            Assert.True(_ledger.IsValid(model));
        }

        [Fact]
        public void AFailedReleaseIsRecordedAndTheRestGoesOn()
        {
            int model = _ledger.Issue(
                UiModel, new object(), () => { throw new InvalidOperationException("解放に失敗。"); });
            int listener = Issue(Listener, model);

            HandleReleaseResult result;
            _ledger.TryRelease(model, out result);

            Assert.Equal(new[] { listener, model }, result.Invalidated);
            Assert.Equal(new[] { model }, result.Failed);
            Assert.Subset(result.Invalidated.ToHashSet(), result.Failed.ToHashSet());
            Assert.False(_ledger.IsValid(model));
            Assert.Equal(new[] { listener }, _releases);
        }

        [Fact]
        public void AHandleReachedByTwoPathsIsReleasedOnce()
        {
            int root = Issue(UiModel);
            int left = Issue(Listener, root);
            int right = Issue(Listener, root);
            int leaf = _ledger.Issue(
                Listener, new object(), () => _releases.Add(4), new[] { left, right });

            HandleReleaseResult result;
            _ledger.TryRelease(root, out result);

            Assert.Equal(new[] { leaf, right, left, root }, result.Invalidated);
            Assert.Equal(new[] { 4 }, _releases.Where(r => r == 4));
            Assert.Equal(0, _ledger.Count);
        }

        [Fact]
        public void EveryHandleIsReleasedAtOnceInDependencyOrder()
        {
            int model = Issue(UiModel);
            int listener = Issue(Listener, model);
            int other = Issue(UiModel);

            HandleReleaseResult result = _ledger.ReleaseAll();

            Assert.Equal(0, _ledger.Count);
            Assert.Equal(3, result.Invalidated.Count);
            Assert.True(
                result.Invalidated.IndexOf(listener) < result.Invalidated.IndexOf(model),
                "子が依存元より先に並んでいない。");
            Assert.Contains(other, result.Invalidated);
        }

        [Fact]
        public void TheLedgerIsClosedAfterReleasingEveryHandle()
        {
            Issue(UiModel);

            Assert.False(_ledger.IsClosed);
            _ledger.ReleaseAll();

            Assert.True(_ledger.IsClosed);
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => _ledger.Issue(UiModel, new object(), () => { }));
            Assert.Contains("閉じている", error.Message, StringComparison.Ordinal);
            Assert.Equal(0, _ledger.Count);
        }

        [Fact]
        public void ReleasingEveryHandleTwiceIsAllowed()
        {
            Issue(UiModel);
            _ledger.ReleaseAll();

            HandleReleaseResult result = _ledger.ReleaseAll();

            Assert.Empty(result.Invalidated);
            Assert.Equal(2, Lines().Count(l => l.Contains("全ハンドルの解放")));
        }

        [Fact]
        public void ReleasingEveryHandleOfAnEmptyLedgerReleasesNothingButIsRecorded()
        {
            HandleReleaseResult result = _ledger.ReleaseAll();

            Assert.Empty(result.Invalidated);
            Assert.Empty(result.Failed);
            Assert.Single(Lines(), l => l.Contains("全ハンドルの解放: 件数=0 失敗=0"));
        }

        [Fact]
        public void ReleasingEveryHandleIsRecordedWithTheCounts()
        {
            Issue(UiModel);
            _ledger.Issue(
                UiModel, new object(), () => { throw new InvalidOperationException("解放に失敗。"); });

            _ledger.ReleaseAll();

            Assert.Single(Lines(), l => l.Contains("全ハンドルの解放: 件数=2 失敗=1"));
        }

        [Fact]
        public void ReleasingIsRecorded()
        {
            int id = Issue(UiModel);
            HandleReleaseResult result;

            _ledger.TryRelease(id, out result);

            Assert.Single(Lines(), l => l.Contains("ハンドルの解放: id=" + id + " type=" + UiModel));
        }

        [Fact]
        public void AFailedReleaseIsRecordedToo()
        {
            int id = _ledger.Issue(
                UiModel, new object(), () => { throw new InvalidOperationException("解放に失敗。"); });
            HandleReleaseResult result;

            _ledger.TryRelease(id, out result);

            Assert.Single(Lines(), l => l.Contains("ハンドルの解放で例外が起きた: id=" + id));
        }

        [Fact]
        public void AnInvalidDependencyStops()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(
                () => _ledger.Issue(Listener, new object(), () => { }, new[] { 7 }));

            Assert.Contains("依存元", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => new HandleLedger(null));
            Assert.Throws<ArgumentNullException>(() => _ledger.Issue(null, new object(), () => { }));
            Assert.Throws<ArgumentNullException>(() => _ledger.Issue(UiModel, null, () => { }));
            Assert.Throws<ArgumentNullException>(() => _ledger.Issue(UiModel, new object(), null));
            Assert.Throws<ArgumentException>(() => _ledger.Issue("  ", new object(), () => { }));

            object found;
            Assert.Throws<ArgumentNullException>(() => _ledger.TryGet(1, null, out found));
        }

        private int Issue(string type, int? dependency = null)
        {
            int id = 0;
            id = _ledger.Issue(
                type,
                new object(),
                () => _releases.Add(id),
                dependency.HasValue ? new[] { dependency.Value } : null);

            return id;
        }

        private string[] Lines()
        {
            return File.Exists(_log.FilePath)
                ? File.ReadAllLines(_log.FilePath).Where(l => l.Length != 0).ToArray()
                : new string[0];
        }
    }
}
