using System;
using System.Collections.Generic;

namespace Universe.SqlTrace
{
    public class TraceGroupsReport<TKey> : Dictionary<TKey, SqlGroupCounters<TKey>>
    {
        private SqlCounters _Summary;

        public readonly TraceColumns GroupingField;

        public SqlCounters Summary
        {
            get
            {
                if (_Summary == null)
                {
                    SqlCounters ret = new SqlCounters();
                    foreach (SqlGroupCounters<TKey> value in Values)
                        ret = ret + value.Counters;

                    _Summary = ret;
                }

                return _Summary;
            }
        }

        public override string ToString()
        {
            return "Groups by " + GroupingField + ", Count: " + this.Count;
        }
    }
}
