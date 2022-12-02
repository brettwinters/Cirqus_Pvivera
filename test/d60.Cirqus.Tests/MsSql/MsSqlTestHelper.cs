using System;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace d60.Cirqus.Tests.MsSql
{
    class MsSqlTestHelper : SqlTestHelperBase
    {
        
        static MsSqlTestHelper() {
            _configuration = Configuration.Get();
        }

        private static IConfigurationRoot _configuration;
        public static string TestDbName = "sqltestdb";

        //public MsSqlTestHelper(IConfigurationRoot configuration)
        //{
        //    _configuration = configuration;
        //}

        public static string ConnectionString => _configuration.GetConnectionString(TestDbName);

        public static void EnsureTestDatabaseExists()
        {
            var databaseName = GetDatabaseName(ConnectionString);
            var masterConnectionString = ConnectionString.Replace(databaseName, "master");

            try
            {
                using (var conn = new SqlConnection(masterConnectionString))
                {
                    conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $@"
BEGIN
    CREATE DATABASE [{databaseName}]
END

";
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException exception)
            {
                if (exception.Errors.Cast<SqlError>().Any(e => e.Number == 1801))
                {
                    Console.WriteLine("Test database '{0}' already existed", databaseName);
                    return;
                }
                throw;
            }
        }

        public static void DropTable(string tableName)
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $@"
BEGIN
    DROP TABLE [{tableName}]
END

";
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException exception)
            {
                if (exception.Number == 3701)
                {
                    Console.WriteLine("Table '{0}' was already gone", tableName);
                    return;
                }
                throw;
            }
        }


    }
}