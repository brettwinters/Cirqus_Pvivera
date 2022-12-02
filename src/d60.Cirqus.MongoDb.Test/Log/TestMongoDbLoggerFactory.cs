using System;
using System.Linq;
using d60.Cirqus.MongoDb.Config;
using d60.Cirqus.MongoDb.Logging;
using d60.Cirqus.Testing;
using MongoDB.Driver;
using NUnit.Framework;

namespace d60.Cirqus.Tests.MongoDb
{
    public class TestMongoDbLoggerFactory : FixtureBase
    {
        IMongoDatabase _database;
        ICommandProcessor _commandProcessor;

        protected override void DoSetUp()
        {
            _database = MongoHelper.InitializeTestDatabase();

            _commandProcessor = CreateCommandProcessor(configure => configure
	            .Logging(l => l.UseMongoDb(_database, "lost"))
	            .EventStore(e => e.UseMongoDb(_database, "events"))
	            .EventDispatcher(ed => ed.UseSynchronousViewManagerEventDispatcher())
	        );

            RegisterForDisposal(_commandProcessor);
        }

        [Test]
        public void DoStuff()
        {
            var logStatements = _database.GetCollection<LogStatement>("logs").Find(_ => true).ToList();

            Console.WriteLine("---------------------------------------------------------------------------------------");
            //Console.WriteLine(string.Join(Environment.NewLine, logStatements.Select(s => s["text"])));
            Console.WriteLine(string.Join(Environment.NewLine, logStatements.Select(s => s.text)));
            Console.WriteLine("---------------------------------------------------------------------------------------");
        }
    }
}