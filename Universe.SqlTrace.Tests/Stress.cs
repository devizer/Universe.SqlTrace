using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Universe.SqlTrace.LocalInstances;

namespace Universe.SqlTrace.Tests
{
    class Stress
    {
        private static int ScenarioDuration = 20000;

        public static void RunStress()
        {
            // TestEnvironment.TracePath = "R:\\Temp\\";

            Func<SqlConnection> connectionFactory =
                delegate
                    {
                        SqlConnection connection = new SqlConnection(TestEnvironment.DbConnectionString);
                        return connection;
                    };

            using (SqlConnection con = new SqlConnection(TestEnvironment.DbConnectionString))
            using (SqlTraceReader rdr = new SqlTraceReader())
            {
                rdr.Start(TestEnvironment.DbConnectionString, TestEnvironment.TracePath, TraceColumns.All);
                SqlUnitOfWork.UnitOfWork(con, 1);
            }

            long clientDuration0;
            long clientCpu0;
            StressController.Run(1, delegate { }, out clientDuration0, out clientCpu0);


            foreach (int threads in StressController.ThreadsCount)
            {
                var resultFormat = "{0,-4} {1,-41} Commands {4,6} CPU {2,5} Duration {3,5}";
                Stopwatch watch = new Stopwatch();
                
                // Сценарий 1. Одиночный запрос без трассировки
                {
                    long clientDuration;
                    long clientCpu;
                    long count = 0;
                    StressController.Run(
                        threads,
                        delegate
                            {
                                lock(watch)
                                    if (!watch.IsRunning)
                                        watch.Start();


                                while(watch.ElapsedMilliseconds < ScenarioDuration)
                                {
                                    using (var connection = connectionFactory())
                                    {
                                        connection.Open();
                                        int? spid = SqlServerUtils.GetCurrentSpid(connection);
                                        const string sql = "Select NULL";
                                        using (SqlCommand cmd = new SqlCommand(sql, connection))
                                        {
                                            object scalar = cmd.ExecuteScalar();
                                        }
                                    }

                                    Interlocked.Increment(ref count);
                                }
                            },
                        out clientDuration,
                        out clientCpu);

                    Console.WriteLine(resultFormat, threads, "No Trace:", clientCpu, clientDuration, count);
                }

                // Сценарий 2. Сеанс трассировки на каждый запрос
                {
                    var systemDrive = Environment.SystemDirectory.Substring(0, 3);
                    var scenarios =
                        new[]
                            {
                                new { TraceDrive = systemDrive, Title = "Trace Session Per Request via Sys Drive" },
                                new { TraceDrive = RamDriveInfo.RamDrivePath, Title = "Trace Session Per Request via Ram Drive" },

                            };

                    foreach(var scenario in scenarios)
                        if (scenario.TraceDrive != null)
                        {
                            watch = new Stopwatch();
                            long clientDuration;
                            long clientCpu;
                            long count = 0;
                            StressController.Run(
                                threads,
                                delegate
                                    {
                                        lock (watch)
                                            if (!watch.IsRunning)
                                                watch.Start();

                                        var traceConnection =
                                            new SqlConnectionStringBuilder(TestEnvironment.MasterConnectionString);

                                        traceConnection.Pooling = true;

                                        while (watch.ElapsedMilliseconds < ScenarioDuration)
                                        {
                                            using (var connection = connectionFactory())
                                            {
                                                connection.Open();
                                                int? spid = SqlServerUtils.GetCurrentSpid(connection);
                                                SqlTraceReader rdr = new SqlTraceReader();
                                                rdr.Start(
                                                    traceConnection.ConnectionString,
                                                    Path.Combine(scenario.TraceDrive, "temp\\traces"),
                                                    TraceColumns.None,
                                                    TraceRowFilter.CreateByServerProcess(spid.Value)
                                                    );

                                                using (rdr)
                                                {
                                                    const string sql = "Select NULL";
                                                    using (SqlCommand cmd = new SqlCommand(sql, connection))
                                                    {
                                                        object scalar = cmd.ExecuteScalar();
                                                    }

                                                    var sqlSummary = rdr.ReadSummaryReport();
                                                    
                                                }

                                                rdr.Stop();
                                            }

                                            Interlocked.Increment(ref count);
                                        }
                                    },
                                out clientDuration,
                                out clientCpu
                                );

                            Console.WriteLine(
                                resultFormat,
                                threads,
                                scenario.Title + ":",
                                clientCpu,
                                clientDuration,
                                count);
                        }
                }


                // Сценарий 3. Сеанс трассировки на весь Unit of work
                {
                    watch = new Stopwatch();
                    long clientDuration;
                    long clientCpu;
                    long count = 0;
                    StressController.Run(
                        threads,
                        delegate
                            {
                                lock (watch)
                                    if (!watch.IsRunning)
                                        watch.Start();

                                
                                using (SqlTraceReader rdr = new SqlTraceReader())
                                {
                                    const string name = "Trace Session per Scenario";
                                    var v1 = new SqlConnectionStringBuilder(TestEnvironment.MasterConnectionString);
                                    v1.Pooling = true;
                                    v1.ApplicationName = name;


                                    rdr.Start(
                                        v1.ConnectionString,
                                        TestEnvironment.TracePath,
                                        TraceColumns.None,
                                        TraceRowFilter.CreateByApplication(name)
                                        );

                                    while (watch.ElapsedMilliseconds < ScenarioDuration)
                                    {
                                        using (var connection = connectionFactory())
                                        {
                                            connection.Open();
                                            int? spid = SqlServerUtils.GetCurrentSpid(connection);

                                            const string sql = "Select NULL";
                                            using (SqlCommand cmd = new SqlCommand(sql, connection))
                                            {
                                                object scalar = cmd.ExecuteScalar();
                                            }

                                        }

                                        Interlocked.Increment(ref count);
                                    }

                                    var sqlSummary = rdr.ReadSummaryReport();
                                    rdr.Stop();
                                }
                            },
                        out clientDuration,
                        out clientCpu
                        );

                    Console.WriteLine(resultFormat, threads, "Trace Per Session:", clientCpu, clientDuration, count);
                }
            }
        }
    }
}
