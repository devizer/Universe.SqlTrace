using System;

namespace Universe.SqlTrace
{
    public class SqlGroupCounters<TKey>
    {
        public TKey Group;
        public long Count;
        public SqlCounters Counters;

        public override string ToString()
        {
            return Group + ": " + Counters;
        }
    }
}