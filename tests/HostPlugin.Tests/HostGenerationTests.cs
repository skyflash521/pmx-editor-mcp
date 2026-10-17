using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>稼働世代がUIスレッドへ委譲するときの、進めないときの答え方。</summary>
    public sealed class HostGenerationTests
    {
        private const string Shown = "確認: 未保存の編集項目があります";

        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

        [Fact]
        public void ADelegateThatFinishesIsRun()
        {
            bool ran = false;
            UiInvocation invocation = Generation(new StepDispatcher()).TryInvokeOnUi(() => ran = true);

            Assert.True(invocation.DidRun);
            Assert.Null(invocation.Unavailable);
            Assert.True(ran);
        }

        [Fact]
        public void ADelegateHeldByAModalIsAnsweredWithWhatIsShown()
        {
            StepDispatcher dispatcher = new StepDispatcher { Finishes = false };
            UiInvocation invocation =
                Generation(dispatcher, Shown).TryInvokeOnUi(() => { });

            Assert.False(invocation.DidRun);
            Assert.Contains(Shown, invocation.Unavailable);
            Assert.Contains("確かめられない", invocation.Unavailable);
            Assert.Contains("読み直し", invocation.Unavailable);
            Assert.Contains(EditorPrompt.ToolName, invocation.Unavailable);
        }

        [Fact]
        public void ADelegateThatIsSlowWithoutAModalIsWaitedFor()
        {
            StepDispatcher dispatcher = new StepDispatcher { WaitsBeforeFinishing = 2 };
            bool ran = false;

            Assert.True(Generation(dispatcher).TryInvokeOnUi(() => ran = true).DidRun);
            Assert.True(ran);
            Assert.Equal(3, dispatcher.Waits);
        }

        [Fact]
        public void NoFurtherDelegateIsStartedWhileTheHeldOneIsUnfinished()
        {
            StepDispatcher dispatcher = new StepDispatcher { Finishes = false };
            HostGeneration generation = Generation(dispatcher, Shown);
            generation.TryInvokeOnUi(() => { });

            bool ran = false;
            UiInvocation invocation = generation.TryInvokeOnUi(() => ran = true);

            Assert.False(invocation.DidRun);
            Assert.Contains(Shown, invocation.Unavailable);
            Assert.False(ran);
            Assert.Equal(1, dispatcher.Started);
        }

        [Fact]
        public async Task TwoDelegatesThatArriveTogetherStartOnlyOne()
        {
            using (ManualResetEventSlim registering = new ManualResetEventSlim())
            using (ManualResetEventSlim asked = new ManualResetEventSlim())
            {
                StepDispatcher dispatcher = new StepDispatcher { Finishes = false };
                dispatcher.WhileBeginning = () =>
                {
                    registering.Set();
                    asked.Wait(Limit);
                };
                HostGeneration generation = Generation(dispatcher, Shown);
                Task<UiInvocation> first = Task.Run(() => generation.TryInvokeOnUi(() => { }));
                Assert.True(registering.Wait(Limit));

                bool ran = false;
                Task<UiInvocation> other = Task.Run(() => generation.TryInvokeOnUi(() => ran = true));
                asked.Set();

                Assert.False((await first).DidRun);
                Assert.False((await other).DidRun);
                Assert.False(ran);
                Assert.Equal(1, dispatcher.Started);
            }
        }

        [Fact]
        public void ADelegateHeldBehindAnotherOneIsSaidNotToHaveStarted()
        {
            StepDispatcher dispatcher = new StepDispatcher { Finishes = false };
            HostGeneration generation = Generation(dispatcher, Shown);
            generation.TryInvokeOnUi(() => { });

            UiInvocation invocation = generation.TryInvokeOnUi(() => { });

            Assert.False(invocation.DidRun);
            Assert.False(invocation.DidStart);
        }

        [Fact]
        public void ADelegateHeldAfterItStartedIsNotSaidToHaveBeenLeftUnstarted()
        {
            StepDispatcher dispatcher = new StepDispatcher { Finishes = false };
            HostGeneration generation = Generation(dispatcher, Shown);

            UiInvocation invocation = generation.TryInvokeOnUi(() => { });

            Assert.False(invocation.DidRun);
            Assert.True(invocation.DidStart);
        }

        [Fact]
        public void TheHeldDelegateIsSaidToBeUnfinishedWhenNothingIsShownAnyMore()
        {
            StepDispatcher dispatcher = new StepDispatcher { Finishes = false };
            StepProbe probe = new StepProbe(Shown);
            HostGeneration generation = Generation(dispatcher, probe);
            generation.TryInvokeOnUi(() => { });

            probe.Shows = null;
            UiInvocation invocation = generation.TryInvokeOnUi(() => { });

            Assert.False(invocation.DidRun);
            Assert.Contains("前の呼び出し", invocation.Unavailable);
            Assert.DoesNotContain("表示", invocation.Unavailable);
        }

        [Fact]
        public void ADelegateIsStartedAgainOnceTheHeldOneFinishes()
        {
            StepDispatcher dispatcher = new StepDispatcher { Finishes = false };
            HostGeneration generation = Generation(dispatcher, Shown);
            generation.TryInvokeOnUi(() => { });
            dispatcher.FinishStarted();
            dispatcher.Finishes = true;

            bool ran = false;

            Assert.True(generation.TryInvokeOnUi(() => ran = true).DidRun);
            Assert.True(ran);
            Assert.Equal(2, dispatcher.Started);
        }

        [Fact]
        public void TheHeldDelegateIsTidiedUpOnceItFinishes()
        {
            StepDispatcher dispatcher = new StepDispatcher { Finishes = false };
            HostGeneration generation = Generation(dispatcher, Shown);
            generation.TryInvokeOnUi(() => { });
            dispatcher.FinishStarted();
            dispatcher.Finishes = true;
            Assert.Equal(0, dispatcher.Ended);

            generation.TryInvokeOnUi(() => { });
            generation.TryInvokeOnUi(() => { });

            Assert.Equal(3, dispatcher.Ended);
        }

        [Fact]
        public void ADelegateIsTidiedUpOnlyOnceWhenTheNextCallArrivesAsItFinishes()
        {
            StepDispatcher dispatcher = new StepDispatcher();
            HostGeneration generation = Generation(dispatcher);
            dispatcher.WhenWaitSucceeds = () =>
            {
                dispatcher.WhenWaitSucceeds = null;
                Task.Run(() => generation.TryInvokeOnUi(() => { })).Wait(Limit);
            };

            generation.TryInvokeOnUi(() => { });

            Assert.Equal(2, dispatcher.Started);
            Assert.Equal(2, dispatcher.Ended);
        }

        [Fact]
        public void TidyingUpTheHeldDelegateDoesNotCarryItsFailureToTheNextOne()
        {
            StepDispatcher dispatcher = new StepDispatcher { Finishes = false, EndThrows = true };
            HostGeneration generation = Generation(dispatcher, Shown);
            generation.TryInvokeOnUi(() => { });
            dispatcher.FinishStarted();
            dispatcher.Finishes = true;

            bool ran = false;

            Assert.True(generation.TryInvokeOnUi(() => ran = true).DidRun);
            Assert.True(ran);
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            StepDispatcher dispatcher = new StepDispatcher();

            Assert.Throws<ArgumentNullException>(
                () => new HostGeneration(null, new StepProbe(null), TimeSpan.FromTicks(1)));
            Assert.Throws<ArgumentNullException>(
                () => new HostGeneration(dispatcher, null, TimeSpan.FromTicks(1)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new HostGeneration(dispatcher, new StepProbe(null), TimeSpan.Zero));
            Assert.Throws<ArgumentNullException>(
                () => Generation(dispatcher).TryInvokeOnUi(null));
        }

        private static HostGeneration Generation(StepDispatcher dispatcher, string shown = null)
        {
            return Generation(dispatcher, new StepProbe(shown));
        }

        private static HostGeneration Generation(StepDispatcher dispatcher, StepProbe probe)
        {
            return new HostGeneration(dispatcher, probe, TimeSpan.FromTicks(1));
        }

        /// <summary>出ている表示を差し替えられる見張り。</summary>
        private sealed class StepProbe : IModalWindowProbe
        {
            public StepProbe(string shows)
            {
                Shows = shows;
            }

            /// <summary>出ていることにする表示。出ていないことにするなら null。</summary>
            public string Shows { get; set; }

            public string TryDescribe()
            {
                return Shows;
            }
        }

        /// <summary>
        /// 待ち合わせの回数を数え、終わるかどうかを題材の側で決められる委譲先。UIスレッドは
        /// 持たないので、始めた処理はその場で実行する。
        /// </summary>
        private sealed class StepDispatcher : IUiDispatcher
        {
            private readonly List<UnfinishedPending> _unfinished = new List<UnfinishedPending>();

            /// <summary>始めた実行が終わるかどうか。</summary>
            public bool Finishes { get; set; } = true;

            /// <summary>終わるまでに待ち合わせを空振りさせる回数。</summary>
            public int WaitsBeforeFinishing { get; set; }

            /// <summary>始めた実行の数。</summary>
            public int Started { get; private set; }

            /// <summary>実行を始めている間に行うこと。同時に入ってくる並びを作るのに使う。</summary>
            public Action WhileBeginning { get; set; }

            /// <summary>
            /// 待ち合わせが成ったところで行うこと。終わった委譲の後始末の前に次が入る並びを作る
            /// のに使う。
            /// </summary>
            public Action WhenWaitSucceeds { get; set; }

            /// <summary>待ち合わせた数。</summary>
            public int Waits { get; private set; }

            /// <summary>後始末した数。</summary>
            public int Ended { get; private set; }

            /// <summary>後始末が落ちるかどうか。</summary>
            public bool EndThrows { get; set; }

            public IAsyncResult Begin(Action action)
            {
                Started++;
                if (WhileBeginning != null)
                {
                    WhileBeginning();
                }

                action();
                if (Finishes && WaitsBeforeFinishing == 0)
                {
                    return new FinishedPending();
                }

                UnfinishedPending pending = new UnfinishedPending();
                _unfinished.Add(pending);

                return pending;
            }

            public bool Wait(IAsyncResult pending, TimeSpan limit)
            {
                if (limit > TimeSpan.Zero)
                {
                    Waits++;
                    if (WaitsBeforeFinishing > 0 && Waits > WaitsBeforeFinishing)
                    {
                        FinishStarted();
                    }
                }

                bool finished = pending.IsCompleted || pending.AsyncWaitHandle.WaitOne(TimeSpan.Zero);
                if (finished && limit > TimeSpan.Zero && WhenWaitSucceeds != null)
                {
                    WhenWaitSucceeds();
                }

                return finished;
            }

            public void End(IAsyncResult pending)
            {
                Ended++;
                if (EndThrows)
                {
                    EndThrows = false;

                    throw new InvalidOperationException("後始末で落ちた。");
                }
            }

            /// <summary>始めた実行が終わったことにする。</summary>
            public void FinishStarted()
            {
                foreach (UnfinishedPending pending in _unfinished)
                {
                    pending.Finish();
                }
            }
        }
    }
}
