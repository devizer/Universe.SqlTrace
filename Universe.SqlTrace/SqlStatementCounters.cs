using System;
using System.Collections.Generic;

namespace Universe.SqlTrace
{
    public class SqlStatementCounters
    {
        public string SpName { get; set; }
        public string Sql { get; set; }
        public string Application { get; set; }
        public string Database { get; set; }
        public string ClientHost { get; set; }
        public int ClientProcess { get; set; }
        public string Login { get; set; }
        public int ServerProcess { get; set; }
        public int? SqlErrorCode { get; set; }
        public string SqlErrorText { get; set; }

        // Done: for TraceDetailsReport.ReadDetailsReport()
        public List<string> CompiledXmlPlan { get; set; }
        public List<string> ActualXmlPlan { get; set; }

        public SqlCounters Counters { get; set; }

        public override string ToString()
        {
            return string.Format("Sql: {0}, Application: {1}, Database: {2}, ClientHost: {3}, ClientProcess: {4}, Login: {5}, ServerProcess: {6}, {7}", Sql, Application, Database, ClientHost, ClientProcess, Login, ServerProcess, Counters)
                + (SqlErrorCode.HasValue ? $", Error {SqlErrorCode} '{SqlErrorText}'" : "")
                + XmlPlanToString("Compiled XML Plans", CompiledXmlPlan)
                + XmlPlanToString("Actual XML Plans", ActualXmlPlan);
        }

        string XmlPlanToString(string title, List<string> plans)
        {
            if (plans != null && plans.Count > 0)
            {
                var countString = plans.Count == 1 ? "(1 plan)" : $"({plans.Count} plans)";
                return $"{Environment.NewLine}{title} {countString}{Environment.NewLine}{string.Join(Environment.NewLine, plans.ToArray())}";
            }

            return null;
        }
    }
}