using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using Universe.SqlServerJam;

namespace Universe.SqlTrace.Tests
{
    static class TestEnvironment
    {
        private static volatile bool Initialized = false, IsDbCreated = false;
        
        static TestEnvironment()
        {
            Initialize();
        }
        
        

        public static void Initialize()
        {
            if (Initialized) return;
            Initialized = true;
            if (MasterConnectionString != null) return;
            var servers = SqlServerTestCase.GetSqlServers();
            foreach (var server in servers)
            {
                try
                {
                    using (SqlConnection con = new SqlConnection(server.ConnectionString))
                    {
                        var man = con.Manage();
                        var isSysAdmin = (man.FixedServerRoles & FixedServerRoles.SysAdmin) != 0;
                        bool isLinux = man.HostPlatform == "Linux";
                        // if (!man.IsLocalDB && isSysAdmin)
                        if (isLinux || true)
                        {
                            MasterConnectionString = server.ConnectionString;
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

            throw new InvalidOperationException("Local Sql Express or LocalDB 2014+ (or above) with SysAdmin permission for the current user not found");
        }

        public static readonly string DB = "SQLTRACE_UNITEST_" + Guid.NewGuid().ToString("N");

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
            if (IsDbCreated) return;
            IsDbCreated = true;
            
            Trace.WriteLine(
                "Working SQL Server instance is [" + MasterConnectionString + "]");

            using (var con = new SqlConnection(MasterConnectionString))
            {
                con.Open();

                var sql = "Create Database [" + DB + "]";
                bool isLocalDb = con.Manage().IsLocalDB;
                if (isLocalDb)
                {
                    // Bug on appveyor for newly installed LocalDB:
                    // CREATE FILE encountered operating system error 5(Access is denied.) while attempting to open or create the physical file 'C:\Users\appveyorUNITEST_9cca1fb18d654287be3d5662ed5800ac.mdf'.
                    string fileName = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), $"{DB}.mdf");
                    sql = $"Create Database [{DB}] On (Name = {DB}_dat, FileName = '{fileName}')";
                }

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
