using System;
using System.Data.SqlClient;
using Universe.SqlServerJam;

namespace Universe.SqlTrace.Tests
{
    public static class SqlServerTestCaseExtensions
    {
        public static string GetMediumVersion(this SqlServerTestCase testCase)
        {
            string masterConnectionString = testCase.ConnectionString;
            using (SqlConnection con = new SqlConnection(masterConnectionString))
            {
                var man = con.Manage();
                return man.MediumServerVersion;
            }
        }

        public static bool IsAzure(this SqlServerTestCase testCase)
        {
            string masterConnectionString = testCase.ConnectionString;
            using (SqlConnection con = new SqlConnection(masterConnectionString))
            {
                if (con.Manage().IsAzure)
                {
                    Console.WriteLine("Tracing for Azure is not yet implemented");
                    return true;
                }
            }

            return false;
        }
    }
}