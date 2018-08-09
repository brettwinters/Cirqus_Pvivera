using System.Configuration;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace d60.Cirqus.MsSql
{
    class SqlHelper
    {
        /// <summary>
        /// Looks for a connection string in AppSettings with the specified name and returns that if possible - otherwise,
        /// it is assumed that the string is a connection string in itself
        /// </summary>
        public static string GetConnectionString(string connectionStringOrConnectionStringName)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            var configuration = builder.Build();

            var connectionStringSettings = configuration.GetConnectionString(connectionStringOrConnectionStringName);

            var connectionString = connectionStringSettings ?? connectionStringOrConnectionStringName;

            return connectionString;
        }
    }
}