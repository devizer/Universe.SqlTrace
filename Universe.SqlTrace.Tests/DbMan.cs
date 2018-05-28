using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Universe.SqlServerJam;

namespace Universe.SqlTrace.Tests
{
    class DbMan
    {
        public string MasterConnection { get; }
        private string DbName;

        public DbMan(string masterConnection)
        {
            MasterConnection = masterConnection;
        }

        public string ConnectionString
        {
            get
            {
                if (DbName == null)
                {
                    DbName = "Unit Tests " + Guid.NewGuid().ToString("N");
                    CreateDb(MasterConnection, DbName);
                }

                SqlConnectionStringBuilder b = new SqlConnectionStringBuilder(MasterConnection);
                b.InitialCatalog = DbName;
                return b.ConnectionString;
            }
        }

        public void Dispose()
        {
            if (DbName != null)
            {
            }
        }

        static void CreateDb(string master, string db)
        {
            using (SqlConnection con = new SqlConnection(master))
            {
                con.Execute($"Create Database [{db}]");
            }
        }
    }
}
