using System.Threading.Tasks;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Config;
using d60.Cirqus.Config.Configurers;
using d60.Cirqus.Events;
using d60.Cirqus.InMemory.Events;
using d60.Cirqus.Tests.Stubs;
using Microsoft.Extensions.DependencyInjection;

namespace d60.Cirqus.Tests.Extensions
{
    public static class Helpful
    {
        public static TAggregateRoot Get<TAggregateRoot>(
            this IAggregateRootRepository repo, 
            string aggregateRootId) 
            where TAggregateRoot : AggregateRoot, new()
        {
            var aggregateRoot = repo.Get<TAggregateRoot>(aggregateRootId, new InMemoryUnitOfWork(repo, new DefaultDomainTypeNameMapper()), createIfNotExists: true);
            return (TAggregateRoot)aggregateRoot;
        }

        internal static Task<InMemoryEventStore> UseInMemoryEventStore(this EventStoreConfigurationBuilder builder)
        {
            var inMemoryEventStore = new InMemoryEventStore();

            builder.RegisterInstance<IEventStore>(inMemoryEventStore);

            return Task.FromResult(inMemoryEventStore);
        }

        internal static void UseConsoleOutEventDispatcher(this EventDispatcherConfigurationBuilder builder)
        {
            builder.UseEventDispatcher(c => new ConsoleOutEventDispatcher(c.GetService<IEventStore>()));
        }
    }
}