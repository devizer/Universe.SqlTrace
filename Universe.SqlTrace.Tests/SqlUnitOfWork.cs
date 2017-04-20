using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace Universe.SqlTrace.Tests
{
    class SqlUnitOfWork
    {
        private const string SqlQuery = @"
SELECT @@version, @parameter;
Declare @T table(id uniqueidentifier default newid(), name nvarchar(3000));
declare @i int set @i=0 while @i<10 begin set @i=@i+1 insert @T(name) select name from dbo.sysobjects end";


        public static void UnitOfWork(SqlConnection connection, int count)
        {
            if (connection.State != ConnectionState.Open)
                connection.Open();

            for (int i = 0; i < count; i++)
            {
                using (SqlCommand cmd = new SqlCommand(SqlQuery, connection))
                {
                    cmd.Parameters.Add("@parameter", SqlDbType.Int).Value = 1;
                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    da.Fill(ds);
                }
            }
        }
    }
}
