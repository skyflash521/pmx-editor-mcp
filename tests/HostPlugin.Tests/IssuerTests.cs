using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// ハンドルIDとイベントの連番をホストが1つの発行元から採ることを、台帳とキューを複数作って
    /// 確かめる。発行元が接続ごとに残っている実装は、ここで番号の重複として落ちる。
    /// </summary>
    public sealed class IssuerTests : IDisposable
    {
        private const string UiModel = "uiModel";

        private const string Type = "pmx_view.mouse_down";

        private readonly string _root;

        private readonly HostLog _log;

        public IssuerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-issuers-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _log = new HostLog(Path.Combine(_root, "host.log"));
        }

        [Fact]
        public void TheReservedIdIsNeverIssued()
        {
            HandleIdIssuer issuer = new HandleIdIssuer();

            Assert.NotEqual(HandleIdIssuer.Reserved, issuer.Next());
        }

        [Fact]
        public void TheIssuerStopsForGoodOnceItRunsOut()
        {
            HandleIdIssuer issuer = Exhausted();

            Assert.Throws<InvalidOperationException>(() => issuer.Next());
            Assert.Throws<InvalidOperationException>(() => issuer.Next());
        }

        [Fact]
        public void TheLastIdBeforeTheReservedOneIsStillIssued()
        {
            Assert.Equal(HandleIdIssuer.Reserved - 1, Exhausted().Last);
        }

        /// <summary>予約の1つ手前まで配った発行器。</summary>
        private static HandleIdIssuer Exhausted()
        {
            HandleIdIssuer issuer = new HandleIdIssuer();
            issuer.SkipTo(HandleIdIssuer.Reserved - 1);

            return issuer;
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
        public void TheHandleIdGrowsByOneFromOne()
        {
            HandleIdIssuer issuer = new HandleIdIssuer();

            Assert.Equal(1, issuer.Next());
            Assert.Equal(2, issuer.Next());
        }

        [Fact]
        public void TheEventSequenceGrowsByOneFromOne()
        {
            EventSequenceIssuer issuer = new EventSequenceIssuer();

            Assert.Equal(1L, issuer.Next());
            Assert.Equal(2L, issuer.Next());
        }

        [Fact]
        public void HandleIdsDoNotRepeatAcrossLedgersOfTheSameIssuer()
        {
            HandleIdIssuer issuer = new HandleIdIssuer();
            HandleLedger first = new HandleLedger(_log, issuer);
            HandleLedger second = new HandleLedger(_log, issuer);

            int[] issued =
            {
                Issue(first),
                Issue(second),
                Issue(first),
                Issue(second),
            };

            Assert.Equal(issued.Length, issued.Distinct().Count());
        }

        /// <summary>
        /// 台帳を捨てて作り直すのが繋ぎ直しに当たる。作り直した先が1へ戻れば、前の台帳が出した
        /// IDと重なる。
        /// </summary>
        [Fact]
        public void HandleIdsDoNotRepeatAfterTheLedgerIsMadeAgain()
        {
            HandleIdIssuer issuer = new HandleIdIssuer();
            int before = Issue(new HandleLedger(_log, issuer));
            int after = Issue(new HandleLedger(_log, issuer));

            Assert.True(after > before);
        }

        [Fact]
        public void EventSequencesDoNotRepeatAcrossQueuesOfTheSameIssuer()
        {
            EventSequenceIssuer issuer = new EventSequenceIssuer();
            EventQueue first = new EventQueue(issuer);
            EventQueue second = new EventQueue(issuer);

            long[] issued =
            {
                first.Enqueue(Type, 1, null).Seq,
                second.Enqueue(Type, 1, null).Seq,
                first.Enqueue(Type, 1, null).Seq,
                second.Enqueue(Type, 1, null).Seq,
            };

            Assert.Equal(issued.Length, issued.Distinct().Count());
        }

        [Fact]
        public void NumbersDoNotRepeatWhenSeveralLedgersAndQueuesIssueAtOnce()
        {
            const int Ledgers = 8;
            const int PerLedger = 200;

            HandleIdIssuer handleIds = new HandleIdIssuer();
            EventSequenceIssuer eventSequence = new EventSequenceIssuer();
            List<HandleLedger> ledgers = new List<HandleLedger>();
            List<EventQueue> queues = new List<EventQueue>();
            for (int i = 0; i < Ledgers; i++)
            {
                ledgers.Add(new HandleLedger(_log, handleIds));
                queues.Add(new EventQueue(eventSequence));
            }

            List<int> ids = new List<int>();
            List<long> sequences = new List<long>();
            Parallel.For(0, Ledgers, index =>
            {
                int[] localIds = new int[PerLedger];
                long[] localSequences = new long[PerLedger];
                for (int i = 0; i < PerLedger; i++)
                {
                    localIds[i] = Issue(ledgers[index]);
                    localSequences[i] = queues[index].Enqueue(Type, 1, null).Seq;
                }

                lock (ids)
                {
                    ids.AddRange(localIds);
                    sequences.AddRange(localSequences);
                }
            });

            Assert.Equal(Ledgers * PerLedger, ids.Distinct().Count());
            Assert.Equal(Ledgers * PerLedger, sequences.Distinct().Count());
        }

        [Fact]
        public void TheIssuerIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => new HandleLedger(_log, null));
            Assert.Throws<ArgumentNullException>(() => new EventQueue(null));
        }

        private static int Issue(HandleLedger ledger)
        {
            return ledger.Issue(UiModel, new object(), () => { });
        }
    }
}
