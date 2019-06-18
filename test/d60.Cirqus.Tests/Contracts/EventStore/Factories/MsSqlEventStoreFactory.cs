using d60.Cirqus.Config;
using d60.Cirqus.Events;
using d60.Cirqus.MsSql.Events;
using d60.Cirqus.Tests.MsSql;
using Microsoft.Extensions.Configuration;

namespace d60.Cirqus.Tests.Contracts.EventStore.Factories
{
    public class MsSqlEventStoreFactory : IEventStoreFactory
    {
        private readonly MsSqlEventStore _eventStore;

        public MsSqlEventStoreFactory()
        {
            var configuration = Configuration.Get();

            //var helper = new MsSqlTestHelper(configuration);

            MsSqlTestHelper.EnsureTestDatabaseExists();

            var connectionString = MsSqlTestHelper.ConnectionString;

            MsSqlTestHelper.DropTable("events");

            _eventStore = new MsSqlEventStore(configuration, MsSqlTestHelper.TestDbName, "events");
            
            _eventStore.DropEvents();
        }

        public IEventStore GetEventStore()
        {
            return _eventStore;
        }
    }
}