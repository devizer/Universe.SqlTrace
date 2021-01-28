using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Universe.SqlServerJam;

namespace Universe.SqlTrace.Tests
{
    [TestFixture]
    public class Test_Local_Instances_Discovery
    {

        [Test]
        public void Local_Instances_Get()
        {
            List<SqlServerRef> servers = SqlDiscovery.GetLocalDbAndServerList();
        }

        [Test]
        public void Local_Instances_GetFull()
        {
            List<SqlServerRef> servers = SqlDiscovery.GetLocalDbAndServerList();
            Parallel.ForEach(servers, sqlRef =>
            {
                string ver = null;
                SqlConnectionStringBuilder b = new SqlConnectionStringBuilder(sqlRef.ConnectionString);
                try
                {
                    using (SqlConnection con = new SqlConnection(sqlRef.ConnectionString))
                    {
                        ver = con.Manage().ShortServerVersion.ToString();
                    }
                }
                catch(Exception ex)
                {
                    ver = ex.GetType().Name;
                }

                Console.WriteLine($"{b.DataSource} --> {ver}");
                List<SqlServerRef> transportList = sqlRef.ProbeTransports(9000);
                
            });
        }

        [Test]
        public void Local_Instances_Contains_Data()
        {
            // LocalInstancesDiscovery.Get() - does not return LocalDB instances, nut includes version using exe file version
            // LocalInstanceInfo servers = LocalInstancesDiscovery.Get();
            List<SqlServerRef> servers = SqlDiscovery.GetLocalDbAndServerList();
            // StringBuilder dump = new StringBuilder();
            // var stringWriter = new StringWriter(dump);
            // servers.WriteToXml(stringWriter);
            Console.WriteLine("FOUND SQL Instances:" + string.Join(",", servers.Select(x  => x.Data)));

            Assert.IsTrue(servers.Count > 0, "SQL Server is required. Either running or stopped");
            foreach (var i in servers)
            {
                // TODO: Assert.IsNotNull(i.Version, "File version property of instance {0} is required", i);
                // TODO: Assert.IsTrue(i.Version.Major != 0, "Major file version of instance {0} should be not zero", i);
                Assert.IsNotNull(i.DataSource, "Instance should have name");
            }
        }

        [SetUp]
        public void SetUp()
        {
            Console.WriteLine("Setup");
        }

        [TearDown]
        public void TearDown()
        {
            Console.WriteLine("TearDown");
        }

        [Test, TestCaseSource(typeof(MyServers), nameof(MyServers.GetSqlServers))]
        public void Test_Discovery(string master)
        {
            using (SqlConnection con = new SqlConnection(master))
            {
                var server = new SqlConnectionStringBuilder(master).DataSource;
                Console.WriteLine($"Ver: {con.Manage().ShortServerVersion} {con.Manage().ProductLevel} {con.Manage().ProductUpdateLevel} ({server})");
                // Console.WriteLine($"{master}");
            }
        }

        [Test]
        public void Test_Explicit_Discovery()
        {
            var servers = MyServers.GetSqlServers();
            foreach (var server in servers)
            {
                Console.WriteLine($" * {server}");
            }
        }
        
    }
}
