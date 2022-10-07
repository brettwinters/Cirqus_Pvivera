using d60.Cirqus.Events;
using d60.Cirqus.PostgreSql.Events;
using d60.Cirqus.Tests.PostgreSql;

namespace d60.Cirqus.Tests.Contracts.EventStore.Factories
{
    public class PostgreSqlEventStoreFactory : IEventStoreFactory
    {
        readonly PostgreSqlEventStore _eventStore;

        public PostgreSqlEventStoreFactory()
        {
            var configuration = Configuration.Get();
            var connectionString = PostgreSqlTestHelper.PostgreSqlConnectionString;

            PostgreSqlTestHelper.DropTable("Events");
            _eventStore = new PostgreSqlEventStore(connectionString, "events");
            _eventStore.DropEvents();
        }

        public IEventStore GetEventStore()
        {
            return _eventStore;
        }
    }
}