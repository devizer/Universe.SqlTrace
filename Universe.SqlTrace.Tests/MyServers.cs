using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using Dapper;
using Universe.SqlServerJam;

namespace Universe.SqlTrace.Tests
{
    public class MyServers
    {
        public static List<string> GetSqlServers()
        {

            List<SqlServerRef> all = SqlDiscovery.GetLocalDbAndServerList();
            ThreadPool.GetMinThreads(out var prevWorkers, out var prevPorts);
            var workers = Math.Max(prevWorkers, all.Count + 1);
            ThreadPool.SetMinThreads(workers, prevPorts);

            return all.Select(x => x.ConnectionString)
                .AsParallel().WithDegreeOfParallelism(all.Count)
                .Where(IsAlive)
                .ToList();
        }

        static bool IsAlive(string cs)
        {
            try
            {
                using (var con = new SqlConnection(cs + "; Connection Timeout=9"))
                    con.Execute("Select 'Pong'", commandTimeout: 9);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

    }
}