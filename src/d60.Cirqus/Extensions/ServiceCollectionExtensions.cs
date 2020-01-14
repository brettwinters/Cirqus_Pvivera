using System;
using d60.Cirqus.Config.Configurers;
using Microsoft.Extensions.DependencyInjection;

namespace d60.Cirqus.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static void AddCirqus(
            this IServiceCollection services, 
            Action<ILoggingAndEventStoreConfiguration> configure)
        {
            configure(new CommandProcessorConfigurationBuilder(services));
        }
    }
}