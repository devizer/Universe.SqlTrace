using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using Universe.SqlTrace.LocalInstances;

namespace Universe.SqlTrace.Tests
{
    static class TestEnvironment
    {
        static TestEnvironment()
        {
            Initialize();
        }

        public static void Initialize()
        {
            if (AnySqlServer != null) return;
            var servers = LocalInstancesDiscovery.GetFull(TimeSpan.FromSeconds(9));
            LocalInstanceInfo.SqlInstance found = null;
            Debug.WriteLine("SQL Server Instances: " + servers);
            foreach (var sqlInstance in servers.Instances)
                if (sqlInstance.Status == ServiceControllerStatus.Running)
                    if (sqlInstance.Description != null)
                        // if (sqlInstance.Edition == SqlEdition.Express)
                            if (SqlServerUtils.IsAdmin(sqlInstance.FullLocalName))
                            {
                                AnySqlServer = sqlInstance.FullLocalName;
                                found = sqlInstance;
                                break;
                            }

            if (string.IsNullOrEmpty(AnySqlServer))
                Console.WriteLine("SQL Server Not Found. Tests should not work properly");
            else
                Console.WriteLine("Discovered SQL Server: {0}, Ver is {1}", AnySqlServer, found.FileVer);
        }

        public static string AnySqlServer;

        public static readonly string DB =
            "UNITEST_" + Guid.NewGuid().ToString("N");

        public static string TracePath =
            Environment.SystemDirectory.Substring(0, 2)
            + @"\\temp\\traces";

        public static readonly string WorkingAppicationName =
            "SqlTrace unit-test";

        public static string MasterConnectionString
        {
            get
            {
                return
                    "Application Name=SQL Unit Testing framework;"
                    + "Integrated Security=SSPI;"
                    + "Data Source=" + AnySqlServer + ";"
                    + "Pooling=false;";
            }
        }

        public static string DbConnectionString
        {
            get
            {
                return
                    "Application Name=" + WorkingAppicationName + ";" +
                    "Integrated Security=SSPI;"
                    + "Data Source=" + AnySqlServer + ";"
                    + "Pooling=true;"
                    + "Initial Catalog=" + DB + ";"
                    + "Max Pool Size=300";
            }
        }

        public static void SetUp()
        {
            Trace.WriteLine(
                "Working SQL Server instance is " + AnySqlServer);

            using (var con = new SqlConnection(MasterConnectionString))
            {
                con.Open();

                var sql = "Create Database [" + DB + "]";
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void TearDown()
        {
            try
            {
                List<SqlServerUtils.ConnectionInfo> connections;
                using (var con = new SqlConnection(MasterConnectionString))
                {
                    connections =
                        SqlServerUtils.GetConnections(con)
                            .FindAll(info => info.Database == DB && info.Spid > 50);
                }

                SqlServerUtils.KillConnections(MasterConnectionString, connections);

                using (var con = new SqlConnection(MasterConnectionString))
                using (var cmd = new SqlCommand("Drop Database " + DB, con))
                {
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(
                    "Failed to teardown unit test" + Environment.NewLine + ex);
            }
        }
    }
}
