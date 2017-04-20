namespace Universe.SqlTrace
{
    public class SqlStatementCounters
    {
        public string SpName;
        public string Sql;
        public string Application;
        public string Database;
        public string ClientHost;
        public int ClientProcess;
        public string Login;
        public int ServerProcess;

        public SqlCounters Counters;

        public override string ToString()
        {
            return string.Format("Sql: {0}, Application: {1}, Database: {2}, ClientHost: {3}, ClientProcess: {4}, Login: {5}, ServerProcess: {6}, {7}", Sql, Application, Database, ClientHost, ClientProcess, Login, ServerProcess, Counters);
        }
    }
}