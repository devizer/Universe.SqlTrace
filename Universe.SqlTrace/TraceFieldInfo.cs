using System;
using System.Collections.Generic;
using System.Text;

namespace Universe.SqlTrace
{
    public class TraceFieldInfo
    {
        public readonly int SqlId;
        public readonly string Select;
        public readonly string GroupExpression;

        private static TraceColumns[] allColumns =
        {
            TraceColumns.Application,
            TraceColumns.ClientHost,
            TraceColumns.ClientProcess,
            TraceColumns.Database,
            TraceColumns.Login,
            TraceColumns.ServerProcess,
            TraceColumns.Sql,
        };

        public TraceFieldInfo(int sqlId, string select)
            : this(sqlId, @select, @select)
        {
        }

        public TraceFieldInfo(int sqlId, string @select, string groupExpression)
        {
            SqlId = sqlId;
            Select = @select;
            GroupExpression = groupExpression;
        }


        public static TraceFieldInfo Get(TraceColumns field)
        {
            if (field == TraceColumns.Application)
                return new TraceFieldInfo(10, "ApplicationName");

            if (field == TraceColumns.ClientHost)
                return new TraceFieldInfo(8, "HostName");

            if (field == TraceColumns.ClientProcess)
                return new TraceFieldInfo(9, "ClientProcessID");

            if (field == TraceColumns.Database)
                return new TraceFieldInfo(35, "DatabaseName");

            if (field == TraceColumns.Login)
                return new TraceFieldInfo(11, "LoginName");

            if (field == TraceColumns.ServerProcess)
                return new TraceFieldInfo(12, "SPID");

            if (field == TraceColumns.Sql)
                return new TraceFieldInfo(
                    1,
                    "Cast((CASE WHEN EventClass = 10 THEN ObjectName ELSE NULL END) as NVARCHAR(MAX)) SP, Cast(CASE WHEN EventClass = 10 Or EventClass = 12 Then TextData ELSE null END as NVARCHAR(MAX)) SqlText",
                    "Cast((CASE WHEN EventClass = 10 THEN Cast(ObjectName as NVARCHAR(MAX)) ELSE Cast(TextData as NVARCHAR(MAX)) END) as NVARCHAR(MAX))");

            throw new ArgumentException(
                "Unknown TraceColumn " + field,
                "field");
        }

        public static List<TraceColumns> ToArray(TraceColumns columns)
        {
            List<TraceColumns> ret = new List<TraceColumns>();
            foreach (TraceColumns item in allColumns)
                if (0 != (item & columns))
                    ret.Add(item);

            return ret;
        }

        public static string GetSqlSelect(TraceColumns columns)
        {
            List<TraceColumns> list = ToArray(columns);
            StringBuilder ret = new StringBuilder();
            foreach (TraceColumns field in list)
            {
                TraceFieldInfo info = Get(field);
                ret.Append(ret.Length > 0 ? ", " : "").Append(info.Select);
            }

            return ret.ToString();
        }
    }
}