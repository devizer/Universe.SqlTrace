using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Threading;
using Universe.SqlTrace.LocalInstances;

namespace Universe.SqlTrace.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            TestEnvironment.SetUp();

            if (TestEnvironment.AnySqlServer == null)
                Console.WriteLine("Valid SQL Server Not Found :(");
            else
            {
                Console.WriteLine("Sql Server: {0}", TestEnvironment.AnySqlServer);

                try
                {
                    Stress.RunStress();
                }
                finally
                {
                    TestEnvironment.TearDown();
                }
            }
        }


    }
}
