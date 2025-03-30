﻿using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using Dapper;
using NUnit.Framework;
using Universe.SqlServerJam;

namespace Universe.SqlTrace.Tests
{
    
    [TestFixture]
    public class Test_SqlCountersReader
    {
        private string Table1Name = null; // "##Temp_" + Guid.NewGuid().ToString("N");

        // Tricky hack - Table1Holder prevents deletion of Table1Name table until teardown.
        SqlConnection Table1Holder;


        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            TestEnvironment.Initialize();
            if (TestEnvironment.AnySqlServer == null)
                Assert.Fail("At least one instance of running SQL Server is required");

            TestEnvironment.SetUp();

            var dbConnectionString = TestEnvironment.DbConnectionString;
        }

        private void CreateTable1(string dbConnectionString)
        {
            Table1Name = "##Temp_" + Guid.NewGuid().ToString("N");
            Table1Holder = new SqlConnection(dbConnectionString);
            Table1Holder.Open();
            Table1Holder.Execute($"Create table {Table1Name}(id int)");
            Console.WriteLine($"Table Created: {Table1Name}");
        }

        [Test, TestCaseSource(typeof(SqlServerTestCase), nameof(SqlServerTestCase.GetSqlServers))]
        public void Test_Sandbox(SqlServerTestCase testCase)
        {
            CreateTable1(testCase.ConnectionString);

            using (SqlTraceReader reader = new SqlTraceReader())
            {
                var filterByProcess = TraceRowFilter.CreateByClientProcess(Process.GetCurrentProcess().Id);
                var filterLikeSqlTrace = TraceRowFilter.CreateLikeApplication("SqlTrace");
                reader.NeedActualExecutionPlan = testCase.NeedActualExecutionPlan;
                reader.NeedCompiledExecutionPlan = testCase.NeedCompiledExecutionPlan;
                reader.Start(testCase.ConnectionString, TestEnvironment.TracePath, TraceColumns.All, filterByProcess, filterLikeSqlTrace);

                using (SqlConnection con = new SqlConnection(testCase.ConnectionString))
                {
                    con.Open();

                    con.Execute(SqlBatch);

                    for (int i = 1; i < 10; i++)
                        con.Execute($"Insert {Table1Name}(id) Values(@i)", new { i = i });

                    con.Execute($"Select * From {Table1Name}");
                    con.Execute("sp_server_info", commandType: CommandType.StoredProcedure);
                }

                reader.Stop();
                var groupsByClientHost = reader.ReadGroupsReport<string>(TraceColumns.ClientHost);
                // Grouping By SQL? What for? Does not work for UTF8 Default Collation
                var collation = new SqlConnection(testCase.ConnectionString).Manage().Databases["master"].DefaultCollationName;
                Console.WriteLine($"Collation: {collation}");
                if (!collation.ToLower().EndsWith("utf8"))
                {
                    var groupsBySql = reader.ReadGroupsReport<string>(TraceColumns.Sql);
                }

                var rptSummary = reader.ReadSummaryReport();
                var rpt = reader.ReadDetailsReport();
                Console.WriteLine("Statements: " + rpt.Count);
                DumpCounters(rpt);

                Console.WriteLine("");
                Console.WriteLine("My Process: " + Process.GetCurrentProcess().Id);
                Console.WriteLine("Summary: " + rptSummary);
                Console.WriteLine("Details Summary " + rpt.Summary);
            }
        }

        [Test, TestCaseSource(typeof(SqlServerTestCase), nameof(SqlServerTestCase.GetSqlServers))]
        public void RowCounts_Of_Insert(SqlServerTestCase testCase)
        {
            var masterConnectionString = testCase.ConnectionString;
            using (SqlConnection con = new SqlConnection(masterConnectionString))
            {
                if (con.Manage().IsAzure)
                {
                    Console.WriteLine("Tracing for Azure is not yet implemented");
                    return;
                }
            }

            using (SqlConnection con = new SqlConnection(masterConnectionString))
            {
                Console.WriteLine($"Version of [{masterConnectionString}]: {con.Manage().ShortServerVersion}");
            }


            var table = $"#T_{Guid.NewGuid().ToString("N")}";
            string[] sqlCommands = new[]
            {
                $"Create Table {table}(id int);",
                $"Insert {table} Values(42); Insert {table} Values(43); Insert {table} Values(44); Insert {table} Values(45);",
            };

            TraceTetsEnv env = new TraceTetsEnv(masterConnectionString);
            using (env)
            {
                using (SqlTraceReader reader = new SqlTraceReader())
                {
                    Console.WriteLine($@"
Master Connection: {env.MasterConnectionString}
TraceDir:          {env.TraceDirectory}
TableName:         {env.TableName}");

                    reader.NeedActualExecutionPlan = testCase.NeedActualExecutionPlan;
                    reader.NeedCompiledExecutionPlan = testCase.NeedCompiledExecutionPlan;
                    reader.Start(env.MasterConnectionString, env.TraceDirectory,
                        TraceColumns.Sql | TraceColumns.ClientProcess, 
                        TraceRowFilter.CreateByClientProcess(Process.GetCurrentProcess().Id));

                    using (SqlConnection con = new SqlConnection(masterConnectionString))
                    {
                        con.Open(); // keep temp table
                        foreach (var sql in sqlCommands)
                        {
                            con.Execute(sql);
                        }
                    }

                    Console.WriteLine($"Trace File: {reader.TraceFile}");
                    reader.Stop();
                    var detailsReport = reader.ReadDetailsReport();
                    DumpCounters(detailsReport);
                    Assert.Greater(detailsReport.Count, 0, "At least one sql command should be caught");
                    var rowCountsOfLastStatements = detailsReport.Last().Counters.RowCounts;
                    Assert.AreEqual(4, rowCountsOfLastStatements, "Insert 4x should result RowCounts==4");
                }
            }
        }

        [Test, TestCaseSource(typeof(SqlServerTestCase), nameof(SqlServerTestCase.GetSqlServers))]
        public void Single_SqlBatch_Is_Captured(SqlServerTestCase testCase)
        {
            string masterConnectionString = testCase.ConnectionString;
            using (SqlConnection con = new SqlConnection(masterConnectionString))
            {
                if (con.Manage().IsAzure)
                {
                    Console.WriteLine("Tracing for Azure is not yet implemented");
                    return;
                }
            }

            using (SqlConnection con = new SqlConnection(masterConnectionString))
            {
                var man = con.Manage();
                Console.WriteLine($"Version of [{masterConnectionString}]: {man.ShortServerVersion}{Environment.NewLine}" +
                                  $"{man.MediumServerVersion}");
            }


            TraceTetsEnv env = new TraceTetsEnv(masterConnectionString);
            using (env)
            {
                string sql = "Set NOCOUNT ON; SELECT @@version, 'Hello, World!'; Exec sp_server_info;";
                sql = sql + sql + sql;
                using (SqlTraceReader reader = new SqlTraceReader())
                {
                    Console.WriteLine($@"
Master Connection: {env.MasterConnectionString}
TraceDir:          {env.TraceDirectory}
TableName:         {env.TableName}");

                    reader.NeedActualExecutionPlan = testCase.NeedActualExecutionPlan;
                    reader.NeedCompiledExecutionPlan = testCase.NeedCompiledExecutionPlan;
                    reader.Start(env.MasterConnectionString, env.TraceDirectory,
                        TraceColumns.Sql | TraceColumns.ClientProcess);

                    using (SqlConnection con = new SqlConnection(masterConnectionString))
                    {
                        con.Open();
                        using (SqlCommand cmd = new SqlCommand(sql, con))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    reader.Stop();
                    var detailsReport = reader.ReadDetailsReport();
                    DumpCounters(detailsReport);
                    Assert.Greater(detailsReport.Count, 0, "At least one sql command should be caught");

                    int idProcess = Process.GetCurrentProcess().Id;
                    foreach (SqlStatementCounters report in detailsReport)
                    {
                        if (report.SqlErrorCode.HasValue)
                            Assert.Fail("All the statement are successful, but '{0}' produces error {1}", report.Sql,
                            report.SqlErrorCode);

                        if (report.Sql == sql && report.ClientProcess == idProcess)
                            return;
                    }

                    Assert.Fail("Expected sql statement {0} by process {1}", sql, idProcess);
                }
            }
        }

        [Test, TestCaseSource(typeof(SqlServerTestCase), nameof(SqlServerTestCase.GetSqlServers))]
        public void Error_Is_Captured(SqlServerTestCase testCase)
        {
            string masterConnectionString = testCase.ConnectionString;
            using (SqlConnection con = new SqlConnection(masterConnectionString))
            {
                if (con.Manage().IsAzure)
                {
                    Console.WriteLine("Tracing for Azure is not yet implemented");
                    return;
                }
            }

            using (SqlConnection con = new SqlConnection(masterConnectionString))
            {
                Console.WriteLine($"Version of [{masterConnectionString}]: {con.Manage().ShortServerVersion}");
            }


            TraceTetsEnv env = new TraceTetsEnv(masterConnectionString);
            using (env)
            {
                string sql = "Select 42 / 0;";
                sql = sql + sql + sql;
                using (SqlTraceReader reader = new SqlTraceReader())
                {
                    Console.WriteLine($@"
Master Connection: {env.MasterConnectionString}
TraceDir:          {env.TraceDirectory}
TableName:         {env.TableName}");

                    reader.NeedActualExecutionPlan = testCase.NeedActualExecutionPlan;
                    reader.NeedCompiledExecutionPlan = testCase.NeedCompiledExecutionPlan;
                    reader.Start(env.MasterConnectionString, env.TraceDirectory,
                        TraceColumns.Sql | TraceColumns.ClientProcess, TraceRowFilter.CreateByClientProcess(Process.GetCurrentProcess().Id));

                    Exception caught = null;
                    try
                    {
                        using (SqlConnection con = new SqlConnection(masterConnectionString))
                        {
                            con.Open();
                            using (SqlCommand cmd = new SqlCommand(sql, con))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Console.WriteLine("Divide by zero IS REQUIRED: " + ex);
                        caught = ex;
                    }

                    reader.Stop();
                    var detailsReport = reader.ReadDetailsReport();
                    DumpCounters(detailsReport);
                    Assert.AreEqual(1, detailsReport.Count, "Exactly one statement is expected");
                    Assert.Greater(detailsReport.Count, 0, "At least one sql command should be caught");

                    var expecedSqlErrorCode = 8134;
                    foreach (SqlStatementCounters report in detailsReport)
                    {
                        if (report.SqlErrorCode != expecedSqlErrorCode)
                            Assert.Fail("SQL ERROR 8134 expected. Caught Exception is " + caught);
                        else
                            Console.WriteLine($"{Environment.NewLine}{Environment.NewLine}SQL Error Code {expecedSqlErrorCode} found{Environment.NewLine}{report}");
                    }

                }
            }
        }


        [Test, TestCaseSource(typeof(SqlServerTestCase), nameof(SqlServerTestCase.GetSqlServers))]
        public void RaiseDeadLock1205(SqlServerTestCase testCase)
        {
            string connectionString = testCase.ConnectionString;
            var cmds = new[]
            {
                "Begin Tran",
                "CREATE TYPE dbo.GodType_42_31415926 AS TABLE(Value0 Int NOT NULL, Value1 Int NOT NULL)",
                "Declare @myPK dbo.GodType_42_31415926",
                "Rollback"
            };

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();

                if (con.Manage().ShortServerVersion.Major <= 9)
                {
                    Console.WriteLine("Current implementation of the test does not support very old SQL Server");
                    return;
                }

                try
                {
                    foreach (var cmd in cmds)
                        con.Execute(cmd);
                }
                catch (SqlException e)
                {
                    bool isDeadLock = e.Errors.OfType<SqlError>().Any(x => x.Number == 1205);
                    if (isDeadLock)
                    {
                        Console.WriteLine("Deadlock successfully caught. " + e.GetLegacyExceptionDigest());
                        return;
                    }
                    throw;
                }
            }

            Assert.Fail("Deadlock is expected");
        }

        [Test, TestCaseSource(typeof(SqlServerTestCase), nameof(SqlServerTestCase.GetSqlServers))]
        public void Test_Empty_Session(SqlServerTestCase testCase)
        {
            string connectionString = testCase.ConnectionString;
            using (SqlTraceReader reader = new SqlTraceReader())
            {
                reader.NeedActualExecutionPlan = testCase.NeedActualExecutionPlan;
                reader.NeedCompiledExecutionPlan = testCase.NeedCompiledExecutionPlan;
                reader.Start(connectionString, TestEnvironment.TracePath, TraceColumns.All);
                
                // summary
                SqlCounters summary = reader.ReadSummaryReport();
                Assert.Zero(summary.Requests);
                Console.WriteLine("Summary of empty session is " + summary);

                // details. Summary query above should not be present in details report below.
                var details = reader.ReadDetailsReport();
                CollectionAssert.IsEmpty(details, "Details Collection");
                Assert.Zero(details.Summary.Requests);
                CollectionAssert.IsEmpty(details.GroupByApplication(), "Groups by application");

                reader.Stop();
                reader.Dispose();
            }
        }

        [Test, TestCaseSource(typeof(SqlServerTestCase), nameof(SqlServerTestCase.GetSqlServers))]
        public void Single_StoredProcedure_Is_Captured(SqlServerTestCase testCase)
        {
            string connectionString = testCase.ConnectionString;
            string sql = @"SELECT @@version, @parameter;";

            using (SqlTraceReader reader = new SqlTraceReader())
            {
                reader.NeedActualExecutionPlan = testCase.NeedActualExecutionPlan;
                reader.NeedCompiledExecutionPlan = testCase.NeedCompiledExecutionPlan;
                reader.Start(connectionString, TestEnvironment.TracePath, TraceColumns.All);

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    using (SqlCommand cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.Add("@parameter", SqlDbType.Int).Value = 42;
                        cmd.ExecuteNonQuery();
                    }
                }

                var summary = reader.ReadSummaryReport();
                var groups = reader.ReadGroupsReport<int>(TraceColumns.ClientProcess);
                var details = reader.ReadDetailsReport();
                DumpCounters(details);
                int idProcess = Process.GetCurrentProcess().Id;
                Console.WriteLine("Trace summary is " + details.Summary);

                bool isCaught = details.Any(x => 
                    x.SpName == "sp_executesql" 
                    && x.Sql.IndexOf(sql) >= 0 
                    && x.ClientProcess == idProcess);

                if (!isCaught)
                    Assert.Fail("Expected sql proc '{0}' call by process {1}", "sp_executesql", idProcess);
            }
        }

        [Test, TestCaseSource(typeof(SqlServerTestCase), nameof(SqlServerTestCase.GetSqlServers))]
        public void Test_Sp_Reset_Connection(SqlServerTestCase testCase)
        {
            string connectionString = testCase.ConnectionString;
            string app = "Test " + Guid.NewGuid().ToString("N");
            SqlConnectionStringBuilder b = new SqlConnectionStringBuilder(connectionString);
            b.ApplicationName = app;
            // important
            b.Pooling = true;

            using (SqlTraceReader reader = new SqlTraceReader())
            {
                reader.NeedActualExecutionPlan = testCase.NeedActualExecutionPlan;
                reader.NeedCompiledExecutionPlan = testCase.NeedCompiledExecutionPlan;
                reader.Start(connectionString, TestEnvironment.TracePath, TraceColumns.All, TraceRowFilter.CreateByApplication(app));

                int nQueries = 42;
                for (int i = 0; i < nQueries; i++)
                {
                    using (SqlConnection con = new SqlConnection(b.ConnectionString))
                    {
                        con.Open();
                        int rowsCount = con.Query<dynamic>("Select @@version as Ver, 42 as Answer").Count();
                        /*
                        using (SqlCommand cmd = new SqlCommand("Select @@version as Ver, 42 as Answer", con))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    */
                    }
                }
                // Details
                var details = reader.ReadDetailsReport();
                Console.WriteLine("Trace summary is " + details.Summary);

                Assert.AreEqual(details.Count, nQueries);
            }
        }


        private void DumpCounters(TraceDetailsReport rpt)
        {
            Console.WriteLine("STATEMENTS");
            Console.WriteLine("~~~~~~~~~~");
            foreach (SqlStatementCounters statement in rpt)
            {
                Console.WriteLine(
                    "{" + (statement.SpName == null ? statement.Sql : statement.SpName + ": " + statement.Sql) + "}: "
                    + statement.Counters + ", Error: " + (statement.SqlErrorCode.HasValue ? statement.SqlErrorCode.ToString() : "<None>")
                    + ", ErrorText: " + (statement.SqlErrorText == null ? "<null>" : $"'{statement.SqlErrorText}'"));
            }
        }




        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            TestEnvironment.TearDown();
        }

        private static readonly string SqlBatch = @"
Declare @T2 TABLE(id int identity, title nchar(2000))
	
Insert @T2(title) Values('title1')
insert @T2(title) Values('title2')
insert @T2(title) Values('title3')
insert @T2(title) Values('title4')
insert @T2(title) Values('title5')
	
insert @T2(title) select title from @T2
insert @T2(title) select title from @T2
insert @T2(title) select title from @T2
insert @T2(title) select title from @T2
insert @T2(title) select title from @T2
insert @T2(title) select title from @T2
insert @T2(title) select title from @T2
insert @T2(title) select title from @T2
insert @T2(title) select title from @T2
	
Select GETDATE()
";

    }
}
