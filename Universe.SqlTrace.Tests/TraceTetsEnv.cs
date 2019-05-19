using System;
using System.Data.SqlClient;
using System.Threading;
using Universe.SqlServerJam;

namespace Universe.SqlTrace.Tests
{
    class TraceTetsEnv : IDisposable
    {
        public readonly string MasterConnectionString;
        private string _TableName;
        private object Sync = new object();

        private Lazy<string> _TraceDirectory;
        SqlConnection TableHolder;

        public TraceTetsEnv(string masterConnectionString) : this()
        {
            MasterConnectionString = masterConnectionString;
        }

        protected TraceTetsEnv()
        {
            _TraceDirectory = new Lazy<string>(GetTraceDirectory, LazyThreadSafetyMode.ExecutionAndPublication);

        }

        public string TableName
        {
            get
            {
                LazyInit();
                return _TableName;
            }
        }

        public string TraceDirectory => _TraceDirectory.Value;

        void LazyInit()
        {
            lock (Sync)
            {
                if (_TableName == null)
                {
                    _TableName = "##Temp_" + Guid.NewGuid().ToString("N");

                    var tableHolder = new SqlConnection(TestEnvironment.DbConnectionString);
                    tableHolder.Open();
                    using (SqlCommand cmd = new SqlCommand($"Create table {_TableName}(id int)", tableHolder))
                    {
                        cmd.ExecuteNonQuery();
                        Console.WriteLine($"Table Created: {_TableName}");
                    }

                    TableHolder = tableHolder;
                }
            }
        }

        private string GetTraceDirectory()
        {
            using (SqlConnection con = new SqlConnection(MasterConnectionString))
            {
                // return "C:\\Temp";
                return con.Manage().DefaultPaths.DefaultLog ?? "C:\\Temp";
            }
        }


        public void Dispose()
        {
            if (TableHolder != null)
            {
                TableHolder.Close();
                TableHolder = null;
            }
        }
    }
}
