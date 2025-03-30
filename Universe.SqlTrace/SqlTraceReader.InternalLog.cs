using System;
using System.Collections.Generic;
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

        private void AddInternalTableRow(SqlDataReader rdr)
        {
            if (!EnableInternalLog) return;
            _InternalTable ??= new List<List<object>>();
            bool isEmpty = _InternalTable.Count == 0;
            if (isEmpty)
            {
                var header = new List<object>(rdr.FieldCount);
                for (int c = 0; c < rdr.FieldCount; c++) header.Add(rdr.GetName(c));
                _InternalTable.Add(header);
            }

            object[] row = new object[rdr.FieldCount];
            rdr.GetValues(row);
            _InternalTable.Add(row.ToList());
        }
    }
}
