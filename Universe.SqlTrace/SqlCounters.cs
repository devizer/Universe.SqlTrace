using System.Collections.Generic;

namespace Universe.SqlTrace
{
    public class SqlCounters
    {
        public long Duration;
        public long CPU;
        public long Reads;
        public long Writes;
        public long Requests = 1;

        public static SqlCounters operator +(SqlCounters one, SqlCounters two)
        {
            SqlCounters ret = new SqlCounters();
            ret.Duration = one.Duration + two.Duration;
            ret.CPU = one.CPU + two.CPU;
            ret.Reads = one.Reads + two.Reads;
            ret.Writes = one.Writes + two.Writes;
            ret.Requests = one.Requests + two.Requests;
            return ret;
        }


        public override string ToString()
        {
            return string.Format("Duration: {0}, CPU: {1}, Reads: {2}, Writes: {3}, Requests: {4}", Duration, CPU, Reads, Writes, Requests);
        }
    }
}