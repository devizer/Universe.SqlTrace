﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Text;
using Universe.Utils;

namespace Universe.SqlTrace
{
    public class SqlTraceReader : IDisposable
    {
        private int _traceId;
        private string _traceFile;
        private string _connectionString;
        TraceColumns _columns = TraceColumns.None;
        public bool IsReady;
        private bool _NeedStop = false;

        private static string _PrevCreatedDirectory = null;
        
        public void Start(string connectionString, string tracePath, TraceColumns columns, params TraceRowFilter[] rowFilters)
        {

            _connectionString = connectionString;
            _traceFile = Path.Combine(tracePath, "trace_" + Guid.NewGuid().ToString("N"));
            _columns = columns;

            if (_PrevCreatedDirectory != tracePath)
            {
                if (!Directory.Exists(tracePath))
                    Directory.CreateDirectory(Path.GetDirectoryName(_traceFile));

                _PrevCreatedDirectory = tracePath;
            }

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();

                StringBuilder sqlSetFields = new StringBuilder();
                foreach (TraceColumns field in TraceFieldInfo.ToArray(columns))
                {
                    TraceFieldInfo info = TraceFieldInfo.Get(field);
                    sqlSetFields.AppendFormat(SQL_SET_TRACE_COLUMN, info.SqlId, field);
                }

                List<SqlParameter> parameters = new List<SqlParameter>();
                List<TraceColumns> rowFilterColumns = new List<TraceColumns>();
                TraceRowFilter[] distinctRowFilter = TraceRowFilter.GetDistinct(rowFilters);
                foreach (TraceRowFilter rowFilter in distinctRowFilter)
                {
                    TraceFieldInfo info = TraceFieldInfo.Get(rowFilter.Column);
                    string pName = "@filter_" + info.GroupExpression + "_" + parameters.Count;
                    sqlSetFields.AppendFormat("exec sp_trace_setfilter @TRACE, {0}, 0, 0, {1} -- {2}", info.SqlId, pName, info.GroupExpression);
                    sqlSetFields.AppendLine();
                    SqlParameter p = new SqlParameter(pName, rowFilter.Value);
                    parameters.Add(p);

                    if (rowFilterColumns.Contains(rowFilter.Column))
                        throw new ArgumentException("Duplicate row filter column " + rowFilter.Column, "rowFilters");

                    rowFilterColumns.Add(rowFilter.Column);
                }

                string sqlCmd = SQL_START1_TRACE + sqlSetFields + SQL_START2_TRACE;
                using (SqlCommand cmd = new SqlCommand(sqlCmd, con))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add("@file", SqlDbType.NVarChar).Value = _traceFile;
                    cmd.Parameters.AddRange(parameters.ToArray());
                    _traceId = (int)cmd.ExecuteScalar();
                    // PInvoke.DeleteFileOnReboot(_traceFile + ".trc");
                }
            }

            _NeedStop = true;
        }

        public void Stop()
        {
            if (_NeedStop)
            {
                _NeedStop = false;

                using (SqlConnection con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    using (SqlCommand cmd = new SqlCommand(SQL_STOP_TRACE, con))
                    {
                        cmd.Parameters.Add("@trace", SqlDbType.Int).Value = _traceId;
                        cmd.ExecuteNonQuery();
                    }

                    // OkStop++;
                    // Trace.WriteLine("OkStop: " + OkStop);
                }
            }
        }

        public TraceDetailsReport ReadDetailsReport()
        {
            using (SqlConnection con = new SqlConnection(_connectionString))
            {

                if (con.State != ConnectionState.Open)
                    con.Open();

                List<SqlStatementCounters> ret = new List<SqlStatementCounters>();

                string sqlSelect = TraceFieldInfo.GetSqlSelect(_columns);
                string sqlCmd =
                    string.Format(
                        SQL_SELECT_DETAILS,
                        sqlSelect == "" ? SQL_SELECT_COUNTERS : sqlSelect + ", " + SQL_SELECT_COUNTERS
                        );

                using (SqlCommand cmd = new SqlCommand(sqlCmd, con))
                {
                    cmd.Parameters.Add("@file", SqlDbType.NVarChar).Value = _traceFile + ".trc";
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            SqlStatementCounters item = new SqlStatementCounters();
                            int num = 0;
                            if ((_columns & TraceColumns.Application) != 0)
                            {
                                item.Application = rdr.IsDBNull(num) ? null : rdr.GetString(num);
                                num++;
                            }

                            if ((_columns & TraceColumns.ClientHost) != 0)
                            {
                                item.ClientHost = rdr.IsDBNull(num) ? null : rdr.GetString(num);
                                num++;
                            }

                            if ((_columns & TraceColumns.ClientProcess) != 0)
                            {
                                item.ClientProcess = rdr.IsDBNull(num) ? 0 : rdr.GetInt32(num);
                                num++;
                            }

                            if ((_columns & TraceColumns.Database) != 0)
                            {
                                item.Database = rdr.IsDBNull(num) ? null : rdr.GetString(num);
                                num++;
                            }

                            if ((_columns & TraceColumns.Login) != 0)
                            {
                                item.Login = rdr.IsDBNull(num) ? null : rdr.GetString(num);
                                num++;
                            }

                            if ((_columns & TraceColumns.ServerProcess) != 0)
                            {
                                item.ServerProcess = rdr.IsDBNull(num) ? 0 : rdr.GetInt32(num);
                                num++;
                            }

                            if ((_columns & TraceColumns.Sql) != 0)
                            {
                                item.SpName = rdr.IsDBNull(num) ? null : rdr.GetString(num);
                                num++;

                                item.Sql = rdr.IsDBNull(num) ? null : rdr.GetString(num);
                                num++;
                            }

                            item.Counters = ReadCounters(rdr, num);
                            if (item.Counters != null)
                                ret.Add(item);

                        }
                    }
                }

                return new TraceDetailsReport(_columns, ret);
            }
        }

        public TraceGroupsReport<TKey> ReadGroupsReport<TKey>(TraceColumns groupingField)
        {
            if ((_columns & groupingField) == 0)
                throw new ArgumentException("Axis field " + groupingField + " is not included in trace", "groupingField");

            TraceFieldInfo info = TraceFieldInfo.Get(groupingField);

            string sql = string.Format(
                SQL_SELECT_GROUPS,
                info.GroupExpression);
                

            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                if (con.State != ConnectionState.Open)
                    con.Open();

                using (SqlCommand cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.Add("@file", SqlDbType.NVarChar).Value = _traceFile + ".trc";
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        TraceGroupsReport<TKey> ret = new TraceGroupsReport<TKey>();
                        while (rdr.Read())
                        {
                            object rawKey = rdr.IsDBNull(0) ? null : rdr.GetValue(0);
                            int count = rdr.GetInt32(1);
                            SqlCounters counters = ReadCounters(rdr, 2);
                            if (counters != null)
                            {
                                TKey key = (TKey)rawKey;
                                SqlGroupCounters<TKey> group =
                                    new SqlGroupCounters<TKey>() { Group = key, Count = count, Counters = counters };

                                ret.Add(key, group);
                            }
                        }

                        return ret;
                    }
                }
            }

        }

        public SqlCounters ReadSummaryReport()
        {
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                if (con.State != ConnectionState.Open)
                    con.Open();

                using (SqlCommand cmd = new SqlCommand(SQL_SELECT_SUMMARY, con))
                {
                    cmd.Parameters.Add("@file", SqlDbType.NVarChar).Value = _traceFile + ".trc";
                    using (SqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        SqlCounters summary;
                        if (rdr.Read())
                            summary = ReadCounters_WithRequestsCount(rdr, 0);
                        else
                            summary = SqlCounters.Zero;

                        return summary;
                    }

                }
            }
        }


        private static SqlCounters ReadCounters_WithRequestsCount(SqlDataReader rdr, int startIndex)
        {

            SqlCounters ret = new SqlCounters();
            bool isNull0 = rdr.IsDBNull(startIndex + 0);
            bool isNull1 = rdr.IsDBNull(startIndex + 1);
            bool isNull2 = rdr.IsDBNull(startIndex + 2);
            bool isNull3 = rdr.IsDBNull(startIndex + 3);
            bool isNull4 = rdr.IsDBNull(startIndex + 4);
            if (isNull0 && isNull1 && isNull2 && isNull3 && isNull4)
                return null;

            ret.Duration = isNull0 ? 0 : rdr.GetInt64(startIndex + 0) / 1000;
            ret.CPU = isNull1 ? 0 : rdr.GetInt32(startIndex + 1);
            ret.Reads = isNull2 ? 0 : rdr.GetInt64(startIndex + 2);
            ret.Writes = isNull3 ? 0 : rdr.GetInt64(startIndex + 3);
            ret.Requests = isNull4 ? 0 : rdr.GetInt32(startIndex + 4);
            return ret;
        }

        private static SqlCounters ReadCounters(SqlDataReader rdr, int startIndex)
        {

            SqlCounters ret = new SqlCounters();
            bool isNull0 = rdr.IsDBNull(startIndex + 0);
            bool isNull1 = rdr.IsDBNull(startIndex + 1);
            bool isNull2 = rdr.IsDBNull(startIndex + 2);
            bool isNull3 = rdr.IsDBNull(startIndex + 3);
            if (isNull0 && isNull1 && isNull2 && isNull3)
                return null;

            ret.Duration = isNull0 ? 0 : rdr.GetInt64(startIndex + 0) / 1000;
            ret.CPU = isNull1 ? 0 : rdr.GetInt32(startIndex + 1);
            ret.Reads = isNull2 ? 0 : rdr.GetInt64(startIndex + 2);
            ret.Writes = isNull3 ? 0 : rdr.GetInt64(startIndex + 3);
            return ret;
        }

        public void Dispose()
        {
            Stop();

            string file = _traceFile + ".trc";
            try
            {
                if (_traceFile != null /* && File.Exists(file) */)
                    File.Delete(file);
            }
            catch(Exception ex)
            {
                DebugConsole.WriteException(ex, "SKIP trace file {0}", file);
                PInvoke.DeleteFileOnReboot(file);
            }
        }

        private static readonly string
            SQL_START1_TRACE =
                @"
SET NOCOUNT ON
DECLARE @ERROR int
DECLARE @TRACE int
DECLARE @ON bit
DECLARE @maxfilesize bigint
set @maxfilesize = 16384
SET @ON = 1
EXEC @ERROR = sp_trace_create @TRACE OUTPUT, 0, @file, @maxfilesize
-- IF @ERROR <> 0 Begin RAISEERROR ('Failed to create trace', 16, 0) End
EXEC @ERROR = sp_trace_setfilter @TRACE, 1, 0, 7, N'%PlAcE hoLDER to ignoRE tHIS crEATIon stateMENT by Universe Trace Library%'
-- ignore sp_reset_connection
EXEC @ERROR = sp_trace_setfilter @TRACE, 1, 0, 7, N'%sp_reset_connection%'
-- Statement below also works
-- EXEC @ERROR = sp_trace_setfilter @TRACE, 1, 0, 1, N'exec sp_reset_connection '
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 13, @ON -- Duration
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 16, @ON -- Reads
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 17, @ON -- Writes
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 18, @ON -- CPU
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 13, @ON -- Duration
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 16, @ON -- Reads
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 17, @ON -- Writes
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 18, @ON -- CPU
-- SP Completed Only 
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 27, @ON -- EventClass
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 34, @ON -- ObjectName
",

            SQL_START2_TRACE = @"
EXEC @ERROR = sp_trace_setstatus @TRACE, 1
SELECT @TRACE
",

            SQL_STOP_TRACE =
                @"
EXEC sp_trace_setstatus @trace, 0
EXEC sp_trace_setstatus @trace, 2",

            SQL_SELECT_COUNTERS =
                "[Duration], [CPU], [Reads], [Writes]",

            SQL_SELECT_DETAILS =
                @"SELECT {0} FROM ::fn_trace_gettable (@file, -1)",

            SQL_SELECT_SUMMARY =
                @"SELECT Sum(Duration), Sum(CPU), Sum(Reads), Sum(Writes), Count(Duration) FROM ::fn_trace_gettable(@file, -1)",

            SQL_SELECT_GROUPS =
                "SELECT {0}, Count(1), Sum([Duration]), Sum([CPU]), Sum([Reads]), Sum([Writes]) FROM ::fn_trace_gettable (@file, -1) GROUP BY {0}",

            SQL_SET_TRACE_COLUMN = @"
EXEC @ERROR = sp_trace_SetEvent @TRACE, 10, {0}, @ON; -- {1}
EXEC @ERROR = sp_trace_SetEvent @TRACE, 12, {0}, @ON; -- {1}
";
    }
}