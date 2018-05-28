using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using NUnit.Framework;
using Universe.SqlTrace.LocalInstances;

namespace Universe.SqlTrace.Tests
{
    
    [TestFixture]
    public class Test_SqlCountersReader
    {
        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            TestEnvironment.Initialize();
            if (TestEnvironment.AnySqlServer == null)
                Assert.Fail("At least one instance of running SQL Server is required");

            TestEnvironment.SetUp();

            using (SqlConnection con = new SqlConnection(TestEnvironment.DbConnectionString))
            {
                con.Open();
                
                using (SqlCommand cmd = new SqlCommand("Create table T1(id int)", con))
                {
                    cmd.ExecuteNonQuery();
                }

                using (SqlCommand cmd = new SqlCommand(SqlProc1, con))
                {
                    cmd.ExecuteNonQuery();
                }
            }

        }

        [Test]
        public void Test_Sandbox()
        {
            using (SqlTraceReader reader = new SqlTraceReader())
            {
                reader.Start(TestEnvironment.MasterConnectionString, TestEnvironment.TracePath, TraceColumns.All, TraceRowFilter.CreateByApplication(TestEnvironment.WorkingApplicationName), TraceRowFilter.CreateByClientProcess(Process.GetCurrentProcess().Id));

                using (SqlConnection con = new SqlConnection(TestEnvironment.DbConnectionString))
                {
                    con.Open();

                    for (int i = 0; i < 10; i++)
                    {
                        using (SqlCommand cmd = new SqlCommand("Insert T1(id) Values(" + i + ")", con))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand("Select * From T1", con))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    using (SqlCommand cmd = new SqlCommand("proc1", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.ExecuteNonQuery();
                    }

                }

                reader.Stop();
                var rptGroups = reader.ReadGroupsReport<string>(TraceColumns.ClientHost);
                var rptSummary = reader.ReadSummaryReport();
                var rpt = reader.ReadDetailsReport();
                Trace.WriteLine("Statements: " + rpt.Count);
                DumpCounters(rpt);

                Trace.WriteLine("");
                Trace.WriteLine("My Process: " + Process.GetCurrentProcess().Id);
                Trace.WriteLine("Summary: " + rptSummary);
                Trace.WriteLine("Details Summary " + rpt.Summary);
            }
        }

        private void DumpCounters(TraceDetailsReport rpt)
        {
            foreach (SqlStatementCounters statement in rpt)
            {
                Trace.WriteLine(
                    "{" + (statement.SpName == null ? statement.Sql : statement.SpName + ": " + statement.Sql) + "}: " 
                    + statement.Counters);
            }
        }

        [Test]
        public void Single_SqlBatch_Is_Captured()
        {
            string sql = "SELECT @@version, 'Hello, World!'; Exec Proc1;";
            sql = sql + sql + sql;
            using (SqlTraceReader reader = new SqlTraceReader())
            {
                Console.WriteLine("Master Connection: " + TestEnvironment.MasterConnectionString);
                reader.Start(TestEnvironment.MasterConnectionString, TestEnvironment.TracePath, TraceColumns.Sql | TraceColumns.ClientProcess);

                using (SqlConnection con = new SqlConnection(TestEnvironment.DbConnectionString))
                {
                    con.Open();
                    using (SqlCommand cmd = new SqlCommand(sql, con))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                reader.Stop();
                var rpt = reader.ReadDetailsReport();
                DumpCounters(rpt);
                int idProcess = Process.GetCurrentProcess().Id;
                foreach (SqlStatementCounters report in rpt)
                {
                    if (report.Sql == sql && report.ClientProcess == idProcess)
                        return;
                }

                Assert.Fail("Expected sql statement {0} by process {1}", sql, idProcess);
            }
        }

        [Test]
        public void Test_Empty_Session()
        {
            using (SqlTraceReader reader = new SqlTraceReader())
            {
                var dismatchFilter = TraceRowFilter.CreateByApplication(Guid.NewGuid().ToString());
                reader.Start(TestEnvironment.MasterConnectionString, TestEnvironment.TracePath, TraceColumns.All, dismatchFilter);
                
                // summary
                var summary = reader.ReadSummaryReport();
                Assert.Zero(summary.Requests);
                Console.WriteLine("Summary of empty session is " + summary);

                // details
                var details = reader.ReadDetailsReport();
                CollectionAssert.IsEmpty(details, "Details Collection");
                Assert.Zero(details.Summary.Requests);
                CollectionAssert.IsEmpty(details.GroupByApplication(), "Groups by application");

                reader.Stop();
                reader.Dispose();
            }
        }

        [Test]
        public void Single_StoredProcedure_Is_Captured()
        {
            string sql = "SELECT @@version, @parameter; declare @i int set @i=0 while @i<10 begin set @i=@i+1 select count(1) from dbo.sysobjects end";
            using (SqlTraceReader reader = new SqlTraceReader())
            {
                reader.Start(TestEnvironment.MasterConnectionString, TestEnvironment.TracePath, TraceColumns.All);

                using (SqlConnection con = new SqlConnection(TestEnvironment.DbConnectionString))
                {
                    con.Open();
                    using (SqlCommand cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.Add("@parameter", SqlDbType.Int).Value = 0;
                        cmd.ExecuteNonQuery();
                    }
                }

                var summary = reader.ReadSummaryReport();
                var groups = reader.ReadGroupsReport<int>(TraceColumns.ClientProcess);
                var details = reader.ReadDetailsReport();
                DumpCounters(details);
                int idProcess = Process.GetCurrentProcess().Id;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Trace summary is " + details.Summary);
                Console.ForegroundColor = ConsoleColor.White;

                bool isCaught = details.Any(x => 
                    x.SpName == "sp_executesql" 
                    && x.Sql.IndexOf(sql) >= 0 
                    && x.ClientProcess == idProcess);

                if (!isCaught)
                    Assert.Fail("Expected sql proc '{0}' call by process {1}", sql, idProcess);
            }
        }

        [Test]
        public void Test_Sp_Reset_Connection()
        {
            string app = "Test " + Guid.NewGuid();
            SqlConnectionStringBuilder b = new SqlConnectionStringBuilder(TestEnvironment.DbConnectionString);
            b.ApplicationName = app;
            // important
            b.Pooling = true;

            using (SqlTraceReader reader = new SqlTraceReader())
            {
                reader.Start(TestEnvironment.MasterConnectionString, TestEnvironment.TracePath, TraceColumns.All, TraceRowFilter.CreateByApplication(app));

                int nQueries = 42;
                for (int i = 0; i < nQueries; i++)
                {
                    using (SqlConnection con = new SqlConnection(b.ConnectionString))
                    {
                        con.Open();
                        using (SqlCommand cmd = new SqlCommand("Select @@version", con))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                // Details
                var details = reader.ReadDetailsReport();
                Console.WriteLine("Trace summary is " + details.Summary);

                Assert.AreEqual(details.Count, nQueries);
            }
        }





        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            TestEnvironment.TearDown();
        }

        private static readonly string SqlProc1 = @"
Create PROCEDURE [dbo].[proc1]
AS
BEGIN
	SET NOCOUNT ON;
	
	Declare @T2 TABLE(id int identity, title nchar(2000))
	
	insert @T2(title) Values('title1')
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

END
";

    }
}
