using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
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

            var all = SqlDiscovery.GetLocalDbAndServerList();
            ThreadPool.GetMinThreads(out _, out var prevPorts);
            ThreadPool.SetMinThreads(all.Count+1, prevPorts);

            return all.Select(x => x.ConnectionString)
                .AsParallel().WithDegreeOfParallelism(all.Count)
                .Where(IsAlive)
                .ToList();
        }

        static bool IsAlive(string cs)
        {
            try
            {
                using (var con = new SqlConnection(cs + "; Connection Timeout=3"))
                    con.Execute("-- ping", commandTimeout: 3);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

    }
}