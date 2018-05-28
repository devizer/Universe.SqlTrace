using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.ServiceProcess;
using Universe.SqlServerJam;
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
            if (MasterConnectionString != null) return;
            var servers = MyServers.GetSqlServers();
            foreach (var server in servers)
            {
                try
                {
                    using (SqlConnection con = new SqlConnection(server))
                    {
                        var man = con.Manage();
                        var isSysAdmin = (man.FixedServerRoles & FixedServerRoles.SysAdmin) != 0;
                        bool isLinux = man.HostPlatform == "Linux";
                        // if (!man.IsLocalDB && isSysAdmin)
                        if (isLinux || true)
                        {
                            MasterConnectionString = server;
                            Console.WriteLine("Discovered SQL Server: {0}, Ver is {1}", MasterConnectionString, man.ShortServerVersion);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    continue;
                }
            }

            throw new InvalidOperationException("Local Sql Express (or above) with SysAdmin authorization of the current user not found");
        }

        public static readonly string DB = "UNITEST_" + Guid.NewGuid().ToString("N");

        public static string TracePath =
            Environment.SystemDirectory.Substring(0, 2)
            + @"\\temp\\traces";

        public static readonly string WorkingApplicationName =
            "SqlTrace unit-test";

        public static string MasterConnectionString { get; private set; }

        public static string DbConnectionString
        {
            get
            {
                SqlConnectionStringBuilder b = new SqlConnectionStringBuilder(MasterConnectionString);
                b.InitialCatalog = DB;
                b.ApplicationName = "SqlTrace unit-tests";
                return b.ConnectionString;
            }
        }

        public static string AnySqlServer
        {
            get
            {
                return new SqlConnectionStringBuilder(MasterConnectionString).DataSource;
            }
        }

        public static void SetUp()
        {
            Trace.WriteLine(
                "Working SQL Server instance is " + MasterConnectionString);

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
            AgileDbKiller.Kill(DbConnectionString, throwOnError: false, retryCount: 7);
        }

    }
}
