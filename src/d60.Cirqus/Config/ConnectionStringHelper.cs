using Microsoft.Extensions.Configuration;

namespace d60.Cirqus.Config
{
    public class ConnectionStringHelper : IConnectionStringHelper
    {
        private readonly IConfigurationRoot _configuration;

        public ConnectionStringHelper(IConfigurationRoot configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Looks for a connection string in AppSettings with the specified name and returns that if possible - otherwise,
        /// it is assumed that the string is a connection string in itself
        /// </summary>
        public string GetConnectionString(string connectionStringName)
        {
            return _configuration.GetConnectionString(connectionStringName);
        }
    }
}