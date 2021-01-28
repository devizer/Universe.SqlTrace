using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using NUnit.Framework;
using Universe.SqlServerJam;

namespace Universe.SqlTrace.Tests
{
    public class Test_ExtendedEvents
    {
        
        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            TestEnvironment.Initialize();
            if (TestEnvironment.AnySqlServer == null)
                Assert.Fail("At least one instance of running SQL Server is required");

            TestEnvironment.SetUp();
        }

        [Test, TestCaseSource(typeof(MyServers), nameof(MyServers.GetSqlServers))]
        public void Show_Extended_Events_Types(string masterConnectionString)
        {
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

                try
                {
                    IEnumerable<dynamic> data = con.Query(SqlQueryEventKindList);
                    var asStrings = data
                        .Select(x => $" * {x.PackageName}::‹{x.ObjectName}› «{x.ObjectDescr}»")
                        .ToArray();

                    Console.WriteLine(
                        $"TOTAL {asStrings.Length} EVENTS:{Environment.NewLine}{string.Join(Environment.NewLine, asStrings)}"
                    );
                }
                catch (Exception ex)
                {
                    int? sqlError = (ex as SqlException)?.Number;
                    if (sqlError.GetValueOrDefault() == 208)
                        Console.WriteLine($"SqlException: Object not found{Environment.NewLine}{ex}");
                    else
                        throw;
                }
            }
        }



        private static readonly string SqlQueryEventKindList = @"
SELECT   -- Find an event you want.
        p.name         AS [PackageName],
        o.object_type  AS [ObjectType],
        o.name         AS [ObjectName],
        o.description  AS [ObjectDescr],
        p.guid         AS [PackageGuid]
    FROM
              sys.dm_xe_packages  AS p
        JOIN  sys.dm_xe_objects   AS o
                ON  p.guid = o.package_guid
    WHERE
        o.object_type = 'event'   --'action'  --'target'
        AND
        p.name LIKE '%'
        AND
        o.name LIKE '%'
    ORDER BY
        p.name, o.object_type, o.name;
";
    }
}