using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace Universe.SqlTrace
{
    public partial class SqlTraceReader
    {
        public bool EnableInternalLog { get; set; }
        private List<string> _InternalLog;

        private List<List<object>> _InternalTable;

        public string InternalLog => EnableInternalLog && _InternalLog != null ? string.Join(Environment.NewLine, _InternalLog.ToArray()) : null;

        public List<List<object>> InternalTable => EnableInternalLog && _InternalTable != null ? _InternalTable : null;

        void Log(string message)
        {
            if (!EnableInternalLog) return;
            _InternalLog ??= new List<string>();
            _InternalLog.Add(message);
        }

        /*
        string DumpSqlCommandParameters(SqlCommand cmd)
        {
            StringBuilder ret = new StringBuilder();
            foreach (SqlParameter cmdParameter in cmd.Parameters)
            {
                object v = cmdParameter.Value;
                if (v is string) v = $"'{v.ToString().Replace(Environment.NewLine, " /* \\n #1# ")}'";
                var t = cmdParameter.SqlDbType.ToString();
                if (t?.ToLower() == "nvarchar") t = $"{t}(4000)";
                ret.AppendLine($"DECLARE {cmdParameter.ParameterName} {t}; SET {cmdParameter.ParameterName} = {v}");
            }

            return ret.ToString();
        }
        */

        string DumpSqlCommandParameters(DbCommand cmd)
        {
            StringBuilder ret = new StringBuilder();
            foreach (DbParameter cmdParameter in cmd.Parameters)
            {
                object v = cmdParameter.Value;
                if (v is string) v = $"'{v.ToString().Replace(Environment.NewLine, " /* \\n */ ")}'";
                var t = cmdParameter.DbType.ToString();
                if (t?.ToLower() == "nvarchar") t = $"{t}(4000)";
                ret.AppendLine($"DECLARE {cmdParameter.ParameterName} {t}; SET {cmdParameter.ParameterName} = {v}");
            }

            return ret.ToString();
        }

        // private void AddInternalTableRow(SqlDataReader rdr)
        private void AddInternalTableRow(DbDataReader rdr)
        {
            if (!EnableInternalLog) return;
            _InternalTable ??= new List<List<object>>();
            bool isEmpty = _InternalTable.Count == 0;
            if (isEmpty)
            {
                var header = new List<object>(rdr.FieldCount + 1);
                header.Add("#");
                for (int c = 0; c < rdr.FieldCount; c++) header.Add(rdr.GetName(c));
                _InternalTable.Add(header);
            }

            object[] sqlValues = new object[rdr.FieldCount];
            rdr.GetValues(sqlValues);
            var row = sqlValues.ToList();
            row.Insert(0, _InternalTable.Count - 1);
            _InternalTable.Add(row);
        }
    }
}
