using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Universe.SqlTrace.Tests
{
    static class MyWaitHandle
    {
        public static void MyWaitAll(WaitHandle[] handles)
        {
            if (handles.LongLength <= 64)
                WaitHandle.WaitAll(handles);
            else
            {
                List<WaitHandle> list = new List<WaitHandle>();
                foreach (ManualResetEvent doneEvent in handles)
                {
                    list.Add(doneEvent);

                    if (list.Count == 64)
                    {
                        WaitHandle.WaitAll(list.ToArray());
                        list.Clear();
                    }
                }

                if (list.Count > 0)
                    WaitHandle.WaitAll(list.ToArray());
            }
        }
    }
}
