using System;
using System.Collections.Generic;
using System.Threading;

namespace PmxEditorMcp.Bridge.Tests
{
    internal static class DedicatedParallel
    {
        internal static void For(int from, int to, Action<int> body)
        {
            int next = from - 1;
            List<Exception> failures = new List<Exception>();
            Thread[] workers = new Thread[Math.Max(1, Math.Min(to - from, Environment.ProcessorCount))];
            for (int at = 0; at < workers.Length; at++)
            {
                workers[at] = new Thread(() =>
                {
                    try
                    {
                        int index;
                        while ((index = Interlocked.Increment(ref next)) < to)
                        {
                            body(index);
                        }
                    }
                    catch (Exception failure)
                    {
                        lock (failures)
                        {
                            failures.Add(failure);
                        }
                    }
                })
                {
                    IsBackground = true,
                    Priority = ThreadPriority.BelowNormal,
                };
                workers[at].Start();
            }

            foreach (Thread worker in workers)
            {
                worker.Join();
            }

            if (failures.Count > 0)
            {
                throw new AggregateException(failures);
            }
        }
    }
}
