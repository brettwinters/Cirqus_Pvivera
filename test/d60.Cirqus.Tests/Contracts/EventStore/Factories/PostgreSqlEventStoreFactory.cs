using d60.Cirqus.Events;
using d60.Cirqus.PostgreSql;
using d60.Cirqus.PostgreSql.Events;
using d60.Cirqus.Tests.MsSql;
using d60.Cirqus.Tests.PostgreSql;

namespace d60.Cirqus.Tests.Contracts.EventStore.Factories
{
    public class PostgreSqlEventStoreFactory : IEventStoreFactory
    {
        readonly PostgreSqlEventStore _eventStore;

        public PostgreSqlEventStoreFactory()
        {
            //Brett
            var configuration = Configuration.Get();
            //var helper = new PostgreSqlTestHelper(configuration); //PostgreSqlTestHelper
            //helper.EnsureTestDatabaseExists();
            ////var connectionString = helper.ConnectionString;
            //helper.DropTable("events");
            //_eventStore = new PostgreSqlEventStore(configuration, MsSqlTestHelper.TestDbName, "events");
            //_eventStore.DropEvents();

            //var helper = new PostgreSqlTestHelper(configuration);
            PostgreSqlTestHelper.DropTable("Events");

            //PostgreSqlTestHelper.DropTable("Events");
            //var connectionString = configuration[ PostgreSqlTestHelper.PostgreSqlConnectionString];
            _eventStore = new PostgreSqlEventStore(configuration, PostgreSqlTestHelper.TestDbName, "events");
            _eventStore.DropEvents();



            //orig
            //PostgreSqlTestHelper.DropTable("Events");
            //var connectionString = PostgreSqlTestHelper.PostgreSqlConnectionString;
            //_eventStore = new PostgreSqlEventStore(connectionString, "Events");
            //_eventStore.DropEvents();
        }

        public IEventStore GetEventStore()
        {
            return _eventStore;
        }
    }
}