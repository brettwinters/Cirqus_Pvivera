using System;

namespace d60.Cirqus.Config.Configurers
{
    public interface IAggregateRootRepositoryConfigurationBuilderApi
    {
        IEventDispatcherConfigurationBuilderApi AggregateRootRepository(Action<AggregateRootRepositoryConfigurationBuilder> configure);
    }
}