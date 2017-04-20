using System;
using System.Data.SqlClient;
using Universe.SqlTrace.LocalInstances;

namespace Universe.SqlTrace.Tests
{
    class TraceDecorator
    {
        public bool UseTrace = false;
        public Action<SqlConnection> Work;
        public Func<SqlConnection> ConnectionFactory;
        
        public void Exec1(StressCounters counters)
        {
            SqlTraceReader rdr = new SqlTraceReader();

            try
            {

                var connection = ConnectionFactory();
                connection.Open();

                if (UseTrace)
                {
                    int? spid = SqlServerUtils.GetCurrentSpid(connection);
                    rdr.Start(
                        TestEnvironment.MasterConnectionString, 
                        TestEnvironment.TracePath, 
                        TraceColumns.None,
                        TraceRowFilter.CreateByServerProcess(spid.Value)
                        );
                }

                Work(connection);
                connection.Close();

                if (UseTrace)
                {
                    rdr.Stop();
                    var summary = rdr.ReadSummaryReport();
                    counters.Server_CPU += summary.CPU;
                    counters.Server_Duration += summary.Duration;
                    counters.Server_Reads += summary.Reads;
                    counters.Server_Writes += summary.Writes;
                }
            }
            finally
            {
                rdr.Dispose();
            }

        }

        public static void Run()
        {
        }
    }
}