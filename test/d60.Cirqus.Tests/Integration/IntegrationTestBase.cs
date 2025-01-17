﻿using System;
using d60.Cirqus.Events;
using d60.Cirqus.InMemory.Events;
using d60.Cirqus.MongoDb.Events;
using d60.Cirqus.MsSql.Events;
using d60.Cirqus.PostgreSql.Events;
using d60.Cirqus.Tests.Extensions;
using d60.Cirqus.Tests.MongoDb;
using d60.Cirqus.Tests.MsSql;
using d60.Cirqus.Tests.PostgreSql;
using MongoDB.Driver;

namespace d60.Cirqus.Tests.Integration
{
    public abstract class IntegrationTestBase : FixtureBase
    {
        IMongoDatabase _mongoDatabase;

        protected override void DoSetUp() {
        }

        protected ICommandProcessor GetCommandProcessor(EventStoreOption eventStoreOption) {
            var eventStore = GetEventStore(eventStoreOption);

            //Brett
            var commandProcessor = base.CreateCommandProcessor(config => config
                 .EventStore(e => e.RegisterInstance(eventStore))
                 .EventDispatcher(e => e.UseConsoleOutEventDispatcher()));

            //Orig
            //var commandProcessor = CommandProcessor.With()
            //    .EventStore(e => e.RegisterInstance(eventStore))
            //    .EventDispatcher(e => e.UseConsoleOutEventDispatcher())
            //    .Create();

            RegisterForDisposal(commandProcessor);

            return commandProcessor;
        }

        IEventStore GetEventStore(EventStoreOption eventStoreOption) {
            switch (eventStoreOption) {
                case EventStoreOption.InMemory:
                    return new InMemoryEventStore();

                case EventStoreOption.MongoDb:
                    return new MongoDbEventStore(GetMongoDb(), "Events");

                case EventStoreOption.SqlServer:
                    MsSqlTestHelper.EnsureTestDatabaseExists();
                    MsSqlTestHelper.DropTable("Events");
                    return new MsSqlEventStore(
                        //Configuration.Get(), 
                        MsSqlTestHelper.ConnectionString, 
                        "Events");

                case EventStoreOption.Postgres:
                    PostgreSqlTestHelper.DropTable("Events");
                    return new PostgreSqlEventStore(
                        //Configuration.Get(), 
                        PostgreSqlTestHelper.PostgreSqlConnectionString, 
                        "Events");

                default:
                    throw new ArgumentOutOfRangeException("eventStoreOption", "Unknown event store option");
            }
        }

        IMongoDatabase GetMongoDb() {
            if (_mongoDatabase != null) {
                return _mongoDatabase;
            }
            _mongoDatabase = MongoHelper.InitializeTestDatabase();
            return _mongoDatabase;
        }

        public enum EventStoreOption
        {
            InMemory,
            MongoDb,
            SqlServer,
            Postgres
        }
    }
}