using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using Universe.Utils;

namespace Universe.SqlTrace
{
    // Event id="122": Showplan XML (Actual)
    // Event id="168": Showplan XML For Query Compile"
    // TextData is xml plan
    public class SqlTraceReader : IDisposable
    {
        // Kilo Bytes
        public int MaxFileSize { get; set; }

        public bool NeedActualExecutionPlan { get; set; } // 122, after Start() can not be changed
        public bool NeedCompiledExecutionPlan { get; set; } // 168, after Start() can not be changed


        private int _traceId;
        private string _traceFile;
        private string _connectionString;
        TraceColumns _columns = TraceColumns.None;
        private bool _NeedStop = false;

        private static string _PrevCreatedDirectory = null;

        public SqlTraceReader()
        {
            MaxFileSize = 128 * 1024;
        }

        public void Start(string connectionString, string tracePath, TraceColumns columns, params TraceRowFilter[] rowFilters)
        {

            _connectionString = connectionString;
            _traceFile = Path.Combine(tracePath, "trace_" + Guid.NewGuid().ToString("N"));
            _columns = columns;

            // For non-local sql server it doesn't work
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

                if (NeedActualExecutionPlan)
                {
                    sqlSetFields.AppendFormat(SQL_SET_TRACE_XML_PLAN, 122, "Actual Execution Plan");
                }
                if (NeedCompiledExecutionPlan)
                {
                    sqlSetFields.AppendFormat(SQL_SET_TRACE_XML_PLAN, 168, "Compiled Execution Plan");
                }

                List<SqlParameter> parameters = new List<SqlParameter>();
                List<TraceColumns> rowFilterColumns = new List<TraceColumns>();
                TraceRowFilter[] distinctRowFilter = TraceRowFilter.GetDistinct(rowFilters);
                foreach (TraceRowFilter rowFilter in distinctRowFilter)
                {
                    TraceFieldInfo info = TraceFieldInfo.Get(rowFilter.Column);
                    string pName = "@filter_" + info.GroupExpression + "_" + parameters.Count;
                    string comparisonOperator = rowFilter.StrictEquality ? "0" : "6";
                    object comparisonValue = rowFilter.Value;
                    if (!rowFilter.StrictEquality && comparisonValue != null)
                        comparisonValue = "%" + Convert.ToString(comparisonValue).Replace("'", "''") + "%";

                    sqlSetFields.AppendFormat("exec sp_trace_setfilter @TRACE, {0}, 0, {1}, {2} -- {3}", 
                        info.SqlId, 
                        comparisonOperator,
                        pName, 
                        info.GroupExpression);

                    sqlSetFields.AppendLine();
                    SqlParameter p = new SqlParameter(pName, comparisonValue);
                    parameters.Add(p);

                    if (rowFilterColumns.Contains(rowFilter.Column))
                        throw new ArgumentException("Duplicate row filter column " + rowFilter.Column, "rowFilters");

                    rowFilterColumns.Add(rowFilter.Column);
                }

                string sqlCmd = SQL_START1_TRACE + sqlSetFields + SQL_START2_TRACE;
                // Console.WriteLine($"TRACE ON {connectionString}");
                using (SqlCommand cmd = new SqlCommand(sqlCmd, con))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add("@file", SqlDbType.NVarChar).Value = _traceFile;
                    var maxFileSize = Math.Max(8, MaxFileSize);
                    cmd.Parameters.Add("@MaxFileSize", SqlDbType.BigInt).Value = maxFileSize;
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
                }
            }
        }

        // TODO:
        // System.Data.SqlClient.SqlException : Cannot convert to text/ntext or collate to 'Latin1_General_100_CI_AS_SC_UTF8' because these legacy LOB types do not support UTF-8 or UTF-16 encodings. Use types varchar(max), nvarchar(max) or a collation which does not have the _SC or _UTF8 flags.
        public TraceDetailsReport ReadDetailsReport()
        {
            using (SqlConnection con = new SqlConnection(_connectionString))
            {

                if (con.State != ConnectionState.Open)
                    con.Open();

                List<SqlStatementCounters> ret = new List<SqlStatementCounters>();

                string sqlSelect = TraceFieldInfo.GetSqlSelect(_columns);
                var sqlColumns = sqlSelect == "" ? SQL_SELECT_COUNTERS : sqlSelect + ", " + SQL_SELECT_COUNTERS;
                var sqlErrorColumn = "Cast(CASE WHEN EventClass = 33 Then Error Else Null END As INT) Error, Cast(CASE WHEN EventClass = 33 Then Cast(TextData as nvarchar(max)) Else Null END As nvarchar(max)) ErrorText, SPID SPID_For_Error";
                // TODO: Always read SPID
                sqlColumns = sqlErrorColumn + ", " + sqlColumns;
                string sqlCmd = string.Format(SQL_SELECT_DETAILS, sqlColumns);

                using (SqlCommand cmd = new SqlCommand(sqlCmd, con))
                {
                    cmd.Parameters.Add("@file", SqlDbType.NVarChar).Value = _traceFile + ".trc";
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        ErrorsBySpid errors = new ErrorsBySpid();
                        while (rdr.Read())
                        {
                            int? spid = rdr.IsDBNull(2) ? (int?) null : rdr.GetInt32(2);
                            // if (spid == null) throw new InvalidOperationException("SPID column #1 cannot be null");

                            SqlStatementCounters item = new SqlStatementCounters();
                            
                            // Error of Exception event follows sql statement or sp row
                            int? tempSqlErrorNumber = rdr.IsDBNull(0) ? (int?) null : rdr.GetInt32(0);
                            string tempErrorText = rdr.IsDBNull(1) ? (string) null : rdr.GetString(1);
                            if (tempSqlErrorNumber.GetValueOrDefault() != 0)
                            {
                                if (spid != null)
                                {
                                    errors.SetErrorBySpid(spid.Value, tempSqlErrorNumber.Value, tempErrorText);
                                }
                                    
                                continue;
                            }

                            int colNum = 3;
                            if ((_columns & TraceColumns.Application) != 0)
                            {
                                item.Application = rdr.IsDBNull(colNum) ? null : rdr.GetString(colNum);
                                colNum++;
                            }

                            if ((_columns & TraceColumns.ClientHost) != 0)
                            {
                                item.ClientHost = rdr.IsDBNull(colNum) ? null : rdr.GetString(colNum);
                                colNum++;
                            }

                            if ((_columns & TraceColumns.ClientProcess) != 0)
                            {
                                item.ClientProcess = rdr.IsDBNull(colNum) ? 0 : rdr.GetInt32(colNum);
                                colNum++;
                            }

                            if ((_columns & TraceColumns.Database) != 0)
                            {
                                item.Database = rdr.IsDBNull(colNum) ? null : rdr.GetString(colNum);
                                colNum++;
                            }

                            if ((_columns & TraceColumns.Login) != 0)
                            {
                                item.Login = rdr.IsDBNull(colNum) ? null : rdr.GetString(colNum);
                                colNum++;
                            }

                            if ((_columns & TraceColumns.ServerProcess) != 0)
                            {
                                item.ServerProcess = rdr.IsDBNull(colNum) ? 0 : rdr.GetInt32(colNum);
                                colNum++;
                            }

                            if ((_columns & TraceColumns.Sql) != 0)
                            {
                                item.SpName = rdr.IsDBNull(colNum) ? null : rdr.GetString(colNum);
                                colNum++;

                                item.Sql = rdr.IsDBNull(colNum) ? null : rdr.GetString(colNum);
                                colNum++;
                            }

                            item.Counters = ReadCounters(rdr, colNum);
                            
                            if (item.Counters != null)
                            {
                                var errorBySpid = spid.HasValue ? errors.GetErrorBySpid(spid.Value) : null;
                                int? error = errorBySpid == null ? (int?) null : errorBySpid.Error; 
                                if (error.GetValueOrDefault() != 0)
                                {
                                    item.SqlErrorCode = error;
                                    item.SqlErrorText = errorBySpid?.ErrorText;
                                }
                                if (spid.HasValue) errors.SetErrorBySpid(spid.Value, 0, null);

                                ret.Add(item);
                            }

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
                                counters.Requests = count;
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
            bool isNull4 = rdr.IsDBNull(startIndex + 4); // RowCounts
            bool isNull5 = rdr.IsDBNull(startIndex + 5);
            if (isNull0 && isNull1 && isNull2 && isNull3 && isNull5)
                return null;

            ret.Duration = isNull0 ? 0 : rdr.GetInt64(startIndex + 0) / 1000;
            ret.CPU = isNull1 ? 0 : rdr.GetInt32(startIndex + 1);
            ret.Reads = isNull2 ? 0 : rdr.GetInt64(startIndex + 2);
            ret.Writes = isNull3 ? 0 : rdr.GetInt64(startIndex + 3);
            ret.RowCounts = isNull4 ? 0 : rdr.GetInt64(startIndex + 4);
            ret.Requests = isNull5 ? 0 : rdr.GetInt32(startIndex + 5);
            return ret;
        }

        private static SqlCounters ReadCounters(SqlDataReader rdr, int startIndex)
        {

            SqlCounters ret = new SqlCounters();
            bool isNull0 = rdr.IsDBNull(startIndex + 0);
            bool isNull1 = rdr.IsDBNull(startIndex + 1);
            bool isNull2 = rdr.IsDBNull(startIndex + 2);
            bool isNull3 = rdr.IsDBNull(startIndex + 3);
            bool isNull4 = rdr.IsDBNull(startIndex + 4);
            if (isNull0 && isNull1 && isNull2 && isNull3)
                return null;

            ret.Duration = isNull0 ? 0 : rdr.GetInt64(startIndex + 0) / 1000;
            ret.CPU = isNull1 ? 0 : rdr.GetInt32(startIndex + 1);
            ret.Reads = isNull2 ? 0 : rdr.GetInt64(startIndex + 2);
            ret.Writes = isNull3 ? 0 : rdr.GetInt64(startIndex + 3);
            ret.RowCounts = isNull4 ? 0 : rdr.GetInt64(startIndex + 4);
            return ret;
        }

        public void Dispose()
        {
            Stop();

            string file = _traceFile + ".trc";
            try
            {
                if (_traceFile != null /*&& File.Exists(file)*/) File.Delete(file);
            }
            catch(Exception ex)
            {
                if (!(ex is DirectoryNotFoundException || ex is FileNotFoundException))
                {
                    DebugConsole.WriteException(ex, "SqlTrace::OnDelete Skipping deletion of trace file {0}", file);
                    PInvoke.DeleteFileOnReboot(file);
                }
            }
        }

        private static readonly string
            SQL_START1_TRACE =
                @"DECLARE @TRACE int
SET NOCOUNT ON
DECLARE @ERROR int
DECLARE @ON bit
-- DECLARE @MaxFileSize bigint
-- SET @MaxFileSize = 16384
SET @ON = 1
EXEC @ERROR = sp_trace_create @TRACE OUTPUT, 0, @file, @maxfilesize
-- IF @ERROR <> 0 Begin RAISEERROR ('Failed to create trace', 16, 0) End
EXEC @ERROR = sp_trace_setfilter @TRACE, 1, 0, 7, N'%-- This magic comment as well as a batch are skipped by SqlTraceReader.Read call%'
-- ignore sp_reset_connection
EXEC @ERROR = sp_trace_setfilter @TRACE, 1, 0, 7, N'%sp_reset_connection%'
-- Statement below also works
-- EXEC @ERROR = sp_trace_setfilter @TRACE, 1, 0, 1, N'exec sp_reset_connection '
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 12, @ON -- SPID
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 13, @ON -- Duration
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 16, @ON -- Reads
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 17, @ON -- Writes
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 18, @ON -- CPU
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 48, @ON -- RowCounts
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 12, @ON -- SPID
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 13, @ON -- Duration
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 16, @ON -- Reads
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 17, @ON -- Writes
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 18, @ON -- CPU
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 48, @ON -- RowCounts

-- Error Code (follows command)
EXEC @ERROR = sp_trace_setevent @TRACE, 33, 12, @ON -- SPID
EXEC @ERROR = sp_trace_setevent @TRACE, 33, 31, @ON -- Error Code (int) of exception event
EXEC @ERROR = sp_trace_setevent @TRACE, 33, 1, @ON  -- TextData (error text) 

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
                "[Duration], [CPU], [Reads], [Writes], [RowCounts]",

            SQL_SELECT_DETAILS =
                @"SELECT {0} FROM ::fn_trace_gettable (@file, -1);
-- This magic comment as well as a batch are skipped by SqlTraceReader.Read call",

            SQL_SELECT_SUMMARY =
                @"SELECT Sum(Duration), Sum(CPU), Sum(Reads), Sum(Writes), Sum(RowCounts), Count(Duration) FROM ::fn_trace_gettable (@file, -1) Where EventClass <> 33;
-- This magic comment as well as a batch are skipped by SqlTraceReader.Read call",

            SQL_SELECT_GROUPS =
                @"SELECT {0}, Count(1), Sum([Duration]), Sum([CPU]), Sum([Reads]), Sum([Writes]), Sum(RowCounts) FROM ::fn_trace_gettable (@file, -1) Where EventClass <> 33 GROUP BY {0};
-- This magic comment as well as a batch are skipped by SqlTraceReader.Read call",
                

            SQL_SET_TRACE_COLUMN = @"
EXEC @ERROR = sp_trace_SetEvent @TRACE, 10, {0}, @ON; -- {1}
EXEC @ERROR = sp_trace_SetEvent @TRACE, 12, {0}, @ON; -- {1}
",

            SQL_SET_TRACE_XML_PLAN = @"
EXEC @ERROR = sp_trace_SetEvent @TRACE, {0}, 1 /* TextData */, @ON; -- {1}
EXEC @ERROR = sp_trace_SetEvent @TRACE, {0}, 12 /* TextData */, @ON; -- SPID for {1}
";


        public string TraceFile
        {
            get { return _traceFile; }
        }

        class ErrorBySpid
        {
            // Key
            public int Spid;
            
            // Value
            public int Error;
            public string ErrorText;
        }

        class ErrorsBySpid
        {
            private List<ErrorBySpid> Buffer = new List<ErrorBySpid>();

            public ErrorBySpid GetErrorBySpid(int spid)
            {
                foreach (var errorBySpid in Buffer)
                {
                    if (errorBySpid.Spid == spid) return errorBySpid;
                }

                return null;
            }

            public void SetErrorBySpid(int spid, int error, string errorText)
            {
                int index = -1, p = 0;
                foreach (var errorBySpid in Buffer)
                {
                    if (errorBySpid.Spid == spid)
                    {
                        index = p;
                        break;
                    }

                    p++; //wha?
                }

                if (error == 0)
                {
                    if (index >= 0)
                        Buffer.RemoveAt(index);

                    return;
                }

                if (index >= 0)
                {
                    Buffer[index].Error = error;
                    Buffer[index].ErrorText = errorText;
                }
                else
                {
                    Buffer.Add(new ErrorBySpid() { Spid = spid, Error = error, ErrorText = errorText});
                }
            }
        }
    }
}