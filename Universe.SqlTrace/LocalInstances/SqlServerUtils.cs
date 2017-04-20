using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace Universe.SqlTrace.LocalInstances
{
    public class SqlServerUtils
    {
        public static void KillConnections(string connectionString, ICollection<ConnectionInfo> connections)
        {
            foreach (ConnectionInfo connectionInfo in connections)
            {
                using (var con = new SqlConnection(connectionString))
                {
                    con.Open();
                    string sql = "kill " + connectionInfo.Spid;
                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public static List<ConnectionInfo> GetConnections(SqlConnection connection)
        {
            if (connection.State != ConnectionState.Open)
                connection.Open();

            using (var cmd = new SqlCommand("sp_who", connection))
            {
                var ret = new List<ConnectionInfo>();
                cmd.CommandType = CommandType.StoredProcedure;
                var da = new SqlDataAdapter(cmd);
                var tbl = new DataTable();
                da.Fill(tbl);
                foreach (DataRow o in tbl.Rows)
                {
                    if (tbl.Columns.Contains("spid") && tbl.Columns.Contains("dbname"))
                    {
                        if (!o.IsNull("spid") && !o.IsNull("dbname"))
                        {
                            var info = new ConnectionInfo(Convert.ToInt32(o["spid"]), o["dbname"].ToString());
                            ret.Add(info);
                        }
                    }
                }

                return ret;
            }
        }

        public static int? GetCurrentSpid(SqlConnection connection)
        {
            if (connection.State != ConnectionState.Open)
                connection.Open();

            using (var cmd = new SqlCommand("select @@spid", connection))
            {
                cmd.CommandType = CommandType.Text;
                object o = cmd.ExecuteScalar();
                if (o != null && DBNull.Value != o)
                    return Convert.ToInt32(o);
                else
                    return null;
            }

        }

        public static SqlServerRole GetCurrentUserRoles(SqlConnection connection)
        {
            if (connection.State != ConnectionState.Open)
                connection.Open();

            object[] roles =
                new object[]
                    {
                        "sysadmin", SqlServerRole.sysadmin , 
                        "dbcreator", SqlServerRole.dbcreator , 
                        "bulkadmin", SqlServerRole.bulkadmin , 
                        "diskadmin", SqlServerRole.diskadmin , 
                        "processadmin", SqlServerRole.processadmin , 
                        "serveradmin", SqlServerRole.serveradmin , 
                        "setupadmin", SqlServerRole.setupadmin , 
                        "securityadmin", SqlServerRole.securityadmin , 
                    };

            StringBuilder sqlColumns = new StringBuilder();
            for(int i=0; i<roles.Length; i+=2)
            {
                sqlColumns
                    .Append(sqlColumns.Length == 0 ? "" : ", ")
                    .AppendFormat("IS_SRVROLEMEMBER('{0}')", roles[i]);
            }

            string sql = "SELECT " + sqlColumns;
            using(SqlCommand cmd = new SqlCommand(sql, connection))
            {
                using(SqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    int column = 0;
                    SqlServerRole ret = 0;
                    if (rdr.Read())
                    {
                        for (int i = 1; i < roles.Length; i += 2)
                            if (!rdr.IsDBNull(column) && rdr.GetInt32(column) == 1)
                                ret |= (SqlServerRole)roles[i];

                    }

                    return ret;
                }
            }
        }

        public static bool IsAdmin(string localDataSource)
        {
            string cs = "Data Source=" + localDataSource + "; Integrated Security=SSPI;";
            using (SqlConnection con = new SqlConnection(cs))
            {
                return (GetCurrentUserRoles(con) & SqlServerRole.sysadmin) != 0;
            }
        }

        #region Nested type: ConnectionInfo

        [Serializable]
        public class ConnectionInfo
        {
            public readonly string Database;
            public readonly int Spid;

            public ConnectionInfo(int spid, string database)
            {
                Spid = spid;
                Database = database;
            }
        }

        #endregion
    }

    [Flags]
    public enum SqlServerRole
    {
        sysadmin      = 1,
        dbcreator     = 2,
        bulkadmin     = 4,
        diskadmin     = 8,
        processadmin  = 16,
        serveradmin   = 32,
        setupadmin    = 64,
        securityadmin = 128,
    }
}