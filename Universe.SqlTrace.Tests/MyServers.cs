﻿using System;
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
            int prevWorkers, prevPorts;
            ThreadPool.GetMinThreads(out prevWorkers, out prevPorts);
            var workers = Math.Max(prevWorkers, all.Count + 2);
            ThreadPool.SetMinThreads(workers, prevPorts);

            return all.Select(x => x.ConnectionString)
                .AsParallel().WithDegreeOfParallelism(Math.Max(all.Count, 2))
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