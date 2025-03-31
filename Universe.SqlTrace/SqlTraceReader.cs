using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Universe.Utils;

namespace Universe.SqlTrace
{
    // Event id="122": Showplan XML (Actual)
    // Event id="168": Showplan XML For Query Compile"
    // TextData is xml plan
    public partial class SqlTraceReader : IDisposable
    {
        // Kilo Bytes
        public int MaxFileSize { get; set; }

        // 122, after Start() can not be changed
        public bool NeedActualExecutionPlan
        {
            get => _NeedActualExecutionPlan;
            set => _NeedActualExecutionPlan = IsStarted ? throw new InvalidOperationException() : value;
        }

        // 168, after Start() can not be changed
        public bool NeedCompiledExecutionPlan
        {
            get => _NeedCompiledExecutionPlan;
            set => _NeedCompiledExecutionPlan = IsStarted ? throw new InvalidOperationException() : value;
        }

        private bool _NeedActualExecutionPlan; // 122
        private bool _NeedCompiledExecutionPlan; // 168

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

        public bool IsStarted => _traceFile != null;

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
                    if (EnableInternalLog)
                    {
                        Log($"Start Trace for TraceColumns=[{columns}]{Environment.NewLine}{DumpSqlCommandParameters(cmd)}{Environment.NewLine}{sqlCmd}{Environment.NewLine}");
                    }
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
                // Always Three Error Columns 
                var sqlErrorColumn = "Cast(CASE WHEN EventClass = 33 Then Error Else Null END As INT) Error, Cast(CASE WHEN EventClass = 33 Then Cast(TextData as nvarchar(max)) Else Null END As nvarchar(max)) ErrorText, SPID SPID";
                // Optional Compiled Execution Plan
                string sqlCompiledPlanColumns = null;
                if (NeedCompiledExecutionPlan) // 168
                {
                    sqlCompiledPlanColumns = ", SPID SPID_For_Compiled_Execution_Plan, Cast(CASE WHEN EventClass = 168 Then Cast(TextData as nvarchar(max)) Else Null End as nvarchar(max)) Compiled_Xml_Plan";
                }
                // Optional Actual Execution Plan
                string sqlActualPlanColumns = null;
                if (NeedActualExecutionPlan) // 122
                {
                    sqlActualPlanColumns = ", SPID SPID_For_Actual_Execution_Plan, Cast(CASE WHEN EventClass = 122 Then Cast(TextData as nvarchar(max)) Else Null End as nvarchar(max)) Actual_Xml_Plan";
                }
                var sqlColumns = sqlSelect == "" ? SQL_SELECT_COUNTERS : sqlSelect + ", " + SQL_SELECT_COUNTERS;
                // TODO: Always read SPID
                sqlColumns = sqlErrorColumn + sqlCompiledPlanColumns + sqlActualPlanColumns + ", " + sqlColumns;
                string sqlCmd = string.Format(SQL_SELECT_DETAILS, sqlColumns);

                if (EnableInternalLog) Log($"ReadDetailsReport Query{Environment.NewLine}Declare @file nvarchar(4000); set @file='{_traceFile}.trc';{Environment.NewLine}{sqlCmd}");
                using (SqlCommand cmd = new SqlCommand(sqlCmd, con))
                {
                    cmd.Parameters.Add("@file", SqlDbType.NVarChar).Value = _traceFile + ".trc";
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        ErrorsBySpid errors = new ErrorsBySpid();
                        PlansBySpidBuffer actualXmlPlansBuffer = new PlansBySpidBuffer();
                        PlansBySpidBuffer compiledXmlPlansBuffer = new PlansBySpidBuffer();
                        if (EnableInternalLog) _InternalTable = null;
                        while (rdr.Read())
                        {
                            if (EnableInternalLog) AddInternalTableRow(rdr);
                            // ALWAYS: 0: SqlErrorCode, 1: SqlErrorText, 2: SPID
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
                            
                            if (NeedCompiledExecutionPlan)
                            {
                                int? spidCompiledExecutionPlan = rdr.IsDBNull(colNum) ? (int?)null : rdr.GetInt32(colNum);
                                colNum++;
                                string compiledExecutionPlan = rdr.IsDBNull(colNum) ? null : rdr.GetString(colNum);
                                colNum++;
                                if (compiledExecutionPlan != null && spid.HasValue)
                                {
                                    compiledXmlPlansBuffer.AddPlanForSpid(spid.Value, compiledExecutionPlan);
                                    continue;
                                }
                            }

                            if (NeedActualExecutionPlan)
                            {
                                int? spidActualExecutionPlan = rdr.IsDBNull(colNum) ? (int?)null : rdr.GetInt32(colNum);
                                colNum++;
                                string actualExecutionPlan = rdr.IsDBNull(colNum) ? null : rdr.GetString(colNum);
                                colNum++;
                                if (actualExecutionPlan != null && spid.HasValue)
                                {
                                    actualXmlPlansBuffer.AddPlanForSpid(spid.Value, actualExecutionPlan);
                                    continue;
                                }
                            }

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
                                // Error for sql steatement
                                var errorBySpid = spid.HasValue ? errors.GetErrorBySpid(spid.Value) : null;
                                int? error = errorBySpid == null ? (int?) null : errorBySpid.Error; 
                                if (error.GetValueOrDefault() != 0)
                                {
                                    item.SqlErrorCode = error;
                                    item.SqlErrorText = errorBySpid?.ErrorText;
                                }
                                if (spid.HasValue) errors.SetErrorBySpid(spid.Value, 0, null);

                                if (NeedActualExecutionPlan)
                                {
                                    // Actual XML Plans for statement
                                    var actualPlans = spid.HasValue ? actualXmlPlansBuffer.GetPlansBySpid(spid.Value) : null;
                                    if (actualPlans != null)
                                    {
                                        item.ActualXmlPlan = actualPlans.SqlPlans;
                                        actualXmlPlansBuffer.ClearPlansForSpid(spid.Value);
                                    }
                                }

                                if (NeedCompiledExecutionPlan)
                                {
                                    // Compiled XML Plans for statement
                                    var compiledPlans = spid.HasValue ? compiledXmlPlansBuffer.GetPlansBySpid(spid.Value) : null;
                                    if (compiledPlans != null)
                                    {
                                        item.CompiledXmlPlan = compiledPlans.SqlPlans;
                                        compiledXmlPlansBuffer.ClearPlansForSpid(spid.Value);
                                    }
                                }

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
EXEC @ERROR = sp_trace_SetEvent @TRACE, {0}, 1  /* TextData */, @ON; -- {1}
EXEC @ERROR = sp_trace_SetEvent @TRACE, {0}, 12 /* SPID     */, @ON; -- SPID for {1}
";


        public string TraceFile
        {
            get { return _traceFile; }
        }

    }
}