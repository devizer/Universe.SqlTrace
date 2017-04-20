using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading;

namespace Universe.SqlTrace.LocalInstances
{
    public class MSSqlServerInfo
    {

        public static System.Exception PingDatabase(string connectionString)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    using (SqlCommand cmd = new SqlCommand("select 1", con))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.ExecuteNonQuery();
                        return null;
                    }
                }
            }
            catch (System.Exception ex)
            {
                return ex;
            }
        }

        public static bool TryVersion(string connectionString, out Version version, out System.Exception exception)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    using (SqlCommand cmd = new SqlCommand("select @@microsoftversion", con))
                    {
                        cmd.CommandType = CommandType.Text;
                        long raw = Convert.ToInt64(cmd.ExecuteScalar());
                        int major = (int) (raw/0x1000000L);
                        int sp = (int) (raw & 0xFFFF);
                        version = new Version(major, 0, sp);
                        exception = null;
                        return true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Trace.WriteLine("Failed to get SQL version by " + connectionString + " connection. See Details Below" + Environment.NewLine + ex);
                exception = ex;
                version = null;
                return false;
            }
            
        }
        
        public class DatabaseInfo
        {
            public string Name;
            public int Size;


            public DatabaseInfo()
            {
            }


            public DatabaseInfo(string name, int size)
            {
                Name = name;
                Size = size;
            }
        }

        public static DatabaseInfo[] SelectDatabases(string connectionString)
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();
                return SelectDatabases(con);
            }
            
        }

        public class TableInfo
        {
            public string Name;
            public int Reserved;
            public int Used;


            public TableInfo()
            {
            }


            public TableInfo(string name, int reserved, int used)
            {
                Name = name;
                Reserved = reserved;
                Used = used;
            }
        }


        public static TableInfo[] SelectTables(string connectionString)
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();
                return SelectTables(con);
            }
        }

        public static TableInfo[] SelectTables(SqlConnection con)
        {
            using (SqlCommand cmd = new SqlCommand(SqlTables, con))
            using (SqlDataAdapter da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.Text;
                DataTable tbl = new DataTable();
                da.Fill(tbl);
                List<TableInfo> ret = new List<TableInfo>();
                foreach (DataRow r in tbl.Rows)
                {
                    ret.Add(new TableInfo(Convert.ToString(r[0]), Convert.ToInt32(r[1]), Convert.ToInt32(r[2])));
                }

                return ret.ToArray();
            }
            
        }

        public static DatabaseInfo[] SelectDatabases(SqlConnection con)
        {
            const string sql = "sp_databases";
            using(SqlCommand cmd = new SqlCommand(sql, con))
            using (SqlDataAdapter da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                DataTable tbl = new DataTable();
                da.Fill(tbl);
                List<DatabaseInfo> ret = new List<DatabaseInfo>();
                const string c = "DATABASE_NAME", z = "DATABASE_SIZE";
                foreach (DataRow r in tbl.Rows)
                {
                    if (tbl.Columns.Contains(c) && !r.IsNull(c))
                        ret.Add(new DatabaseInfo(r[c].ToString(), Convert.ToInt32(r[z])));
                }

                return ret.ToArray();
            }
        }

        public static bool IsDbExists(string connectionString, string dbName)
        {
            using(SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();
                DatabaseInfo[] all = SelectDatabases(con);
                foreach (DatabaseInfo s in all)
                {
                    if (StringComparer.InvariantCultureIgnoreCase.Compare(s.Name, dbName) == 0)
                        return true;
                }

                return false;
            }
        }

        public static bool HasAnotherConnections(SqlConnection connection, string dbName)
        {
            ICollection<ConnectionInfo> connections = GetAnotherConnections(connection, dbName);
            return connections.Count > 0;
        }

        public static void KillAnotherConnections(SqlConnection connection, string dbName)
        {
            ICollection<ConnectionInfo> connections = GetAnotherConnections(connection, dbName);

            foreach (ConnectionInfo info in connections)
                KillConnection(connection, info.Spid);
        }

        public static bool KillAnotherConnections(string connectionString, string dbName, TimeSpan timeout)
        {
            DateTime until = DateTime.Now + timeout;

            do
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    KillAnotherConnections(con, dbName);
                }
            } while (DateTime.Now < until);

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();
                return HasAnotherConnections(con, dbName);
            }


            // в другом потоке нельзя :(

/*
            Killer killer = new Killer(connectionString, dbName, until);

            KillDelegate kd = new KillDelegate(killer.Kill);
            AsyncCallback acb = new AsyncCallback(EndKill);
            IAsyncResult ar = kd.BeginInvoke(acb, kd);

            ar.AsyncWaitHandle.WaitOne(timeout, false);

            using(SqlConnection con = new SqlConnection(connectionString))
            return HasAnotherConnections(con, dbName);
*/
        }

        static void EndKill(IAsyncResult ar)
        {
            KillDelegate kd = (KillDelegate) ar.AsyncState;
            try
            {
                kd.EndInvoke(ar);
            }
            catch
            {
            }
        }

        private delegate void KillDelegate();

        class Killer
        {
            public string ConnectionString;
            public string Name;
            public DateTime Until;

            public Killer(string connectionString, string name, DateTime until)
            {
                ConnectionString = connectionString;
                Name = name;
                Until = until;
            }

            public void Kill()
            {
                while(true)
                {
                    if (DateTime.Now >= Until)
                        return;

                    using (SqlConnection con = new SqlConnection(ConnectionString))
                    {
                        con.Open();
                        KillAnotherConnections(con, Name);
                    }

                    if (DateTime.Now >= Until)
                        return;
                    
                    Thread.Sleep(0);

                    if (DateTime.Now >= Until)
                        return;

                    using (SqlConnection con = new SqlConnection(ConnectionString))
                    {
                        con.Open();
                        bool hasAnother = HasAnotherConnections(con, Name);
                        if (!hasAnother)
                            break;
                    }

                    if (DateTime.Now >= Until)
                        return;
                }
            }
        }




        static ICollection<ConnectionInfo> GetAnotherConnections(SqlConnection connection, string dbName)
        {
            List<ConnectionInfo> ret = new List<ConnectionInfo>();
            int? spid = GetCurrentSpid(connection);
            ICollection<ConnectionInfo> connections = GetConnections(connection);
            foreach (ConnectionInfo info in connections)
            {
                if (string.Equals(info.Database, dbName, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (spid.HasValue && spid.Value != info.Spid)
                        ret.Add(info);
                }
            }

            return ret;
        }

        static void KillConnection(SqlConnection connection, int spid)
        {
            using (SqlCommand cmd = new SqlCommand("kill " + spid, connection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.ExecuteScalar();
            }
        }


        static int? GetCurrentSpid(SqlConnection connection)
        {
            using (SqlCommand cmd = new SqlCommand("select @@spid", connection))
            {
                cmd.CommandType = CommandType.Text;
                object o = cmd.ExecuteScalar();
                if (o != null && DBNull.Value != o)
                    return Convert.ToInt32(o);
                else
                    return null;
            }
        }
        
        static ICollection<ConnectionInfo> GetConnections(SqlConnection connection)
        {
            
            using (SqlCommand cmd = new SqlCommand("sp_who", connection))
            {
                List<ConnectionInfo> ret = new List<ConnectionInfo>();
                cmd.CommandType = CommandType.StoredProcedure;
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                DataTable tbl = new DataTable();
                da.Fill(tbl);
                foreach (DataRow o in tbl.Rows)
                {
                    if (tbl.Columns.Contains("spid") && tbl.Columns.Contains("dbname"))
                    {
                        if (!o.IsNull("spid") && !o.IsNull("dbname"))
                        {
                            ConnectionInfo info = new ConnectionInfo(Convert.ToInt32(o["spid"]), o["dbname"].ToString());
                            ret.Add(info);
                        }
                    }
                }

                return ret;
            }
        }

        class ConnectionInfo
        {
            public readonly int Spid;
            public readonly string Database;


            public ConnectionInfo(int spid, string database)
            {
                Spid = spid;
                Database = database;
            }
        }

        private const string SqlTables =
            @"
select 
 o.name, 8*sum(i.reserved) reserved, 8*sum(i.used) used
From 
  sysobjects o join
  sysindexes i on o.id = i.id
where
  o.type='U'
group by o.name
order by sum(i.reserved) desc";

    }
}
