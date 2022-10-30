using Microsoft.Extensions.Configuration;

namespace d60.Cirqus.Tests
{
    public static class Configuration
    {
        public static IConfigurationRoot Get()
        {
            return new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
        }
    }
}