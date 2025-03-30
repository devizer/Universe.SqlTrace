using System;

namespace Universe.SqlTrace
{
    [Flags]
    public enum TraceColumns
    {
        Sql = 1, /* Group By SQL is nonsense */
        Application = 2,
        Database = 4,
        ClientHost = 8,
        ClientProcess = 16,
        Login = 32,
        ServerProcess = 64,

        None = 0,
        All = 
            Sql
            | Application
            | Database
            | ClientHost
            | ClientProcess
            | Login
            | ServerProcess
    }
}