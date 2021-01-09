using System;

namespace Universe.SqlTrace
{

    public class SqlGroupCounters<TKey>
    {
        public TKey Group { get; set; }
        // TODO: The same is stored in Counters.Requests 
        public long Count { get; set; }
        public SqlCounters Counters { get; set; }

        public override string ToString()
        {
            return Group + ": " + Counters;
        }
    }
}