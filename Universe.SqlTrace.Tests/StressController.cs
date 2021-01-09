using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Universe.SqlTrace.Tests
{
    class StressController
    {
        public static int[] ThreadsCount = new[] {1,128, 16, 4, 2};

        static readonly object SyncStart = new object();

        public static void Run(int threads, Action scenario, out long clientDuration, out long clientCpu)
        {
            
            long sumCpu = 0;
            long totalCounter = 0;
            List<ManualResetEvent> doneEvents = new List<ManualResetEvent>();
            List<Thread> threadList = new List<Thread>();
            ManualResetEvent doneStart = new ManualResetEvent(false);
            int counter = 0;
            Stopwatch swAll = null;
            for (int i = 0; i < threads; i++)
            {
                ManualResetEvent evs = new ManualResetEvent(false);
                doneEvents.Add(evs);
                Thread t = new Thread(delegate(object state)
                {
                    Interlocked.Increment(ref counter);
                    if (counter >= threads)
                        doneStart.Set();

                    doneStart.WaitOne();

                    lock (SyncStart)
                    {
                        if (swAll == null)
                        {
                            swAll = new Stopwatch();
                            swAll.Start();
                        }
                    }

                    try
                    {
                        var start = CpuUsage.CpuUsage.GetByThread();
                        // ThreadCountersReader start = ThreadCountersReader.CreateCurrent();
                        Stopwatch sw = new Stopwatch();
                        sw.Start();

                        scenario();

                        CpuUsage.CpuUsage? delta = CpuUsage.CpuUsage.GetByThread() - start;
                        Interlocked.Add(ref sumCpu, (long) delta.GetValueOrDefault().TotalMicroSeconds / 1000);
                    }
                    finally
                    {
                        evs.Set();
                    }
                });

                threadList.Add(t);
                t.Name = "Sql Profiler Stress " + i;
                t.IsBackground = true;
                t.Start();
            }



            MyWaitHandle.MyWaitAll(doneEvents.ToArray());

            clientDuration = swAll.ElapsedMilliseconds;
            clientCpu = sumCpu;

            foreach (Thread thread in threadList)
                thread.Join();
        }

    }
}