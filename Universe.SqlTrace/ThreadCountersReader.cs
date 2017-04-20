using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Universe.SqlTrace
{
    public class ThreadCountersReader
    {
        public readonly int ThreadId;
        public readonly TimeSpan UserTime;
        public readonly TimeSpan TotalTime;


        public static ThreadCountersReader operator -(ThreadCountersReader first, ThreadCountersReader second)
        {
            return
                new ThreadCountersReader(
                    second.ThreadId, 
                    - second.UserTime + first.UserTime, 
                    - second.UserTime + first.UserTime
                    );
            
        }

        public ThreadCountersReader(int threadId, TimeSpan userTime, TimeSpan totalTime)
        {
            ThreadId = threadId;
            UserTime = userTime;
            TotalTime = totalTime;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThreadId();

        public static ThreadCountersReader CreateCurrent()
        {
            IntPtr id = GetCurrentThreadId();
            Process p = Process.GetCurrentProcess();
            ProcessThreadCollection threads = p.Threads;
            ProcessThread found = null;
            foreach (ProcessThread thread in threads)
            {
                if (thread.Id == id.ToInt32())
                {
                    found = thread;
                    break;
                }
            }

            ThreadCountersReader ret = new ThreadCountersReader(id.ToInt32(), found.UserProcessorTime, found.TotalProcessorTime);
            return ret;
        }
    }
}