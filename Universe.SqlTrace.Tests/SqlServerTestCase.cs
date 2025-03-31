﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Dapper;
using Universe.SqlServerJam;

namespace Universe.SqlTrace.Tests
{
    public class SqlServerTestCase
    {
        public string ConnectionString { get; set; }
        public bool NeedActualExecutionPlan { get; set; }
        public bool NeedCompiledExecutionPlan { get; set; }

        public override string ToString()
        {
            List<string> ret = new List<string>();
            SqlConnectionStringBuilder b = new SqlConnectionStringBuilder(ConnectionString);
            var dataSource = b.DataSource;
            ret.Add(dataSource);
            if (NeedCompiledExecutionPlan && NeedActualExecutionPlan) ret.Add("Compiled+Actual XML Plan");
            if (NeedCompiledExecutionPlan && !NeedActualExecutionPlan) ret.Add("Compiled XML Plan");
            if (!NeedCompiledExecutionPlan && NeedActualExecutionPlan) ret.Add("Actual XML Plan");

            return string.Join(". ", ret);
        }


        public static List<SqlServerTestCase> GetSqlServers()
        {
            List<SqlServerTestCase> ret = new List<SqlServerTestCase>();

            List<SqlServerRef> all = SqlDiscovery.GetLocalDbAndServerList();
            int prevWorkers, prevPorts;
            ThreadPool.GetMinThreads(out prevWorkers, out prevPorts);
            var workers = Math.Max(prevWorkers, all.Count + 2);
            ThreadPool.SetMinThreads(workers, prevPorts);

            var aliveConnectionStrings = all
                .Select(x => PatchConnectionString(x.ConnectionString))
                .AsParallel().WithDegreeOfParallelism(Math.Max(all.Count, 2))
                .Where(IsAlive)
                .ToList();

            foreach (var aliveConnectionString in aliveConnectionStrings)
            {
                ret.Add(new SqlServerTestCase() { ConnectionString = aliveConnectionString });
            }

            return ret;
        }

        public static List<SqlServerTestCase> GetSqlServersVariesByPlans()
        {
            var withoutPlans = GetSqlServers();
            List<SqlServerTestCase> ret = new List<SqlServerTestCase>();

            foreach (var sqlServerTestCase in withoutPlans)
            {
                var aliveConnectionString = sqlServerTestCase.ConnectionString;
                ret.Add(new SqlServerTestCase() { ConnectionString = aliveConnectionString });
                ret.Add(new SqlServerTestCase() { ConnectionString = aliveConnectionString, NeedActualExecutionPlan = true });
                ret.Add(new SqlServerTestCase() { ConnectionString = aliveConnectionString, NeedCompiledExecutionPlan = true });
                ret.Add(new SqlServerTestCase() { ConnectionString = aliveConnectionString, NeedActualExecutionPlan = true, NeedCompiledExecutionPlan = true });
            }

            return ret;
        }

        static string PatchConnectionString(string cs)
        {
            SqlConnectionStringBuilder b = new SqlConnectionStringBuilder(cs);
            b.ApplicationName = "SqlTrace unit-tests";
            b.Encrypt = false;
            b.Pooling = true;
            return b.ConnectionString;
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