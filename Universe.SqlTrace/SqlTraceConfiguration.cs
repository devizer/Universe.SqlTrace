using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text;

namespace Universe.SqlTrace
{
    public static class SqlTraceConfiguration
    {
        // TODO: Remove this type and move the property to instance property of SqlTraceReader
        public static DbProviderFactory DbProvider { get; set; } = SqlClientFactory.Instance;
    }

    public static class SqlTraceExtensions
    {
        // TODO: Remove this type and move ALL THE METHOD to private instance methods of SqlTraceReader
        public static DbParameter CreateCommandParameter(this DbConnection connection, string parameterName, object parameterValue)
        {
            DbParameter p = SqlTraceConfiguration.DbProvider.CreateParameter();
            p.ParameterName = parameterName;
            p.Value = parameterValue;
            return p;
        }
        public static void AddCommandParameter(this DbCommand command, string parameterName, DbType parameterType, object parameterValue)
        {
            var p = SqlTraceConfiguration.DbProvider.CreateParameter();
            p.DbType = parameterType;
            p.ParameterName = parameterName;
            p.Value = parameterValue;
            command.Parameters.Add(p);
        }
        public static DbConnection CreateConnection(string connectionString)
        {
            DbConnection ret = SqlTraceConfiguration.DbProvider.CreateConnection();
            ret.ConnectionString = connectionString;
            return ret;
        }

        public static DbCommand CreateDbCommand(this DbConnection connection)
        {
            var ret = SqlTraceConfiguration.DbProvider.CreateCommand();
            ret.Connection = connection;
            return ret;
        }
        public static DbCommand CreateDbCommand(this DbConnection connection, string commandText)
        {
            var ret = CreateDbCommand(connection);
            ret.CommandText = commandText;
            return ret;
        }
    }
}
