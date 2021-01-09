using System;

namespace Universe.SqlTrace
{
#if !NETSTANDARD1_3
    [Serializable]
#endif
    public class SqlCounters
    {
        public long Duration { get; set; }
        public long CPU { get; set; }
        public long Reads { get; set; }
        public long Writes { get; set; }
        public long Requests { get; set; }

        public SqlCounters()
        {
            Requests = 1;
        }

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

        public static SqlCounters Zero
        {
            get { return new SqlCounters() {Requests = 0}; }
        }

        public override string ToString()
        {
            return string.Format("{{Duration: {0}, CPU: {1}, Reads: {2}, Writes: {3}, Requests: {4}}}", Duration, CPU, Reads, Writes, Requests);
        }
    }
}