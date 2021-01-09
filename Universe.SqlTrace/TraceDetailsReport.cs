using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Universe.SqlTrace
{
    public class TraceDetailsReport : ReadOnlyCollection<SqlStatementCounters>
    {
        public readonly TraceColumns IncludedColumns = TraceColumns.None;
        public delegate bool Predicate(SqlStatementCounters filter);

        private SqlCounters _Summary;


        public TraceDetailsReport()
            : base(new SqlStatementCounters[] { })
        {
        }

        public TraceDetailsReport(TraceColumns includedColumns, IList<SqlStatementCounters> list) : base(list)
        {
            IncludedColumns = includedColumns;
        }

        public void Filter(Predicate filter)
        {
            List<SqlStatementCounters> found = new List<SqlStatementCounters>();
            foreach (SqlStatementCounters detailsReport in this)
                if (filter(detailsReport))
                    found.Add(detailsReport);

            base.Items.Clear();
            foreach (SqlStatementCounters statementCounter in found)
                base.Items.Add(statementCounter);

            _Summary = null;
        }

        public SqlCounters Summary
        {
            get
            {
                if (_Summary == null)
                {
                    SqlCounters ret = SqlCounters.Zero;
                    foreach (SqlStatementCounters value in this)
                        ret = ret + value.Counters;

                    _Summary = ret;
                }

                return _Summary;
            }
        }

        public TraceGroupsReport<int> GroupByClientProcess()
        {
            if ((IncludedColumns & TraceColumns.ClientProcess) == 0)
                throw new InvalidOperationException();

            return CreateDictionary(delegate(SqlStatementCounters counters) { return counters.ClientProcess; });
        }

        public TraceGroupsReport<string> GroupByClientHost()
        {
            if ((IncludedColumns & TraceColumns.ClientHost) == 0)
                throw new InvalidOperationException();

            return CreateDictionary(delegate(SqlStatementCounters counters) { return counters.ClientHost; });
        }

        public TraceGroupsReport<string> GroupByLogin()
        {
            if ((IncludedColumns & TraceColumns.Login) == 0)
                throw new InvalidOperationException();

            return CreateDictionary(delegate(SqlStatementCounters counters) { return counters.Login; });
        }

        public TraceGroupsReport<string> GroupByDatabase()
        {
            if ((IncludedColumns & TraceColumns.Database) == 0)
                throw new InvalidOperationException();

            return CreateDictionary(delegate(SqlStatementCounters counters) { return counters.Database; });
        }

        public TraceGroupsReport<string> GroupByApplication()
        {
            if ((IncludedColumns & TraceColumns.Application) == 0)
                throw new InvalidOperationException();

            return CreateDictionary(delegate(SqlStatementCounters counters) { return counters.Application; });
        }

        public TraceGroupsReport<int> GroupByServerProcess()
        {
            if ((IncludedColumns & TraceColumns.ServerProcess) == 0)
                throw new InvalidOperationException();

            return CreateDictionary(delegate(SqlStatementCounters counters) { return counters.ServerProcess; });
        }

        private delegate T GetKey<T>(SqlStatementCounters arg);

        private TraceGroupsReport<T> CreateDictionary<T>(GetKey<T> getKey)
        {
            TraceGroupsReport<T> ret = new TraceGroupsReport<T>();
            foreach (SqlStatementCounters counter in this)
            {
                T key = getKey(counter);
                SqlGroupCounters<T> value;
                if (!ret.TryGetValue(key, out value))
                {
                    value = new SqlGroupCounters<T>();
                    value.Count = 1;
                    value.Group = key;
                    value.Counters = counter.Counters;
                    ret[key] = value;
                }
                else
                {
                    value.Count++;
                    value.Counters = value.Counters + counter.Counters;
                }
            }

            return ret;
            
        }
    }
}
