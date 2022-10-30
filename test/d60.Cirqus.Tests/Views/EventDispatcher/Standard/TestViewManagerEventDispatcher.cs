using System;
using System.Linq;
using System.Threading;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Config;
using d60.Cirqus.Events;
using d60.Cirqus.Logging;
using d60.Cirqus.Logging.Console;
using d60.Cirqus.MongoDb.Config;
using d60.Cirqus.MongoDb.Views;
using d60.Cirqus.Serialization;
using d60.Cirqus.Tests.MongoDb;
using d60.Cirqus.Tests.Views.NewViewManager.Commands;
using d60.Cirqus.Tests.Views.NewViewManager.Views;
using d60.Cirqus.Views;
using d60.Cirqus.Views.ViewManagers.Locators;
using MongoDB.Driver;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Views.NewViewManager
{
    [TestFixture, Category(TestCategories.MongoDb)]
    public class TestViewManagerEventDispatcher : FixtureBase
    {
        ViewManagerEventDispatcher _dispatcher;
        ICommandProcessor _commandProcessor;
        IMongoDatabase _mongoDatabase;

        protected override void DoSetUp()
        {
            CirqusLoggerFactory.Current = new ConsoleLoggerFactory(minLevel: Logger.Level.Debug);

            _mongoDatabase = MongoHelper.InitializeTestDatabase();
            
            _commandProcessor = CreateCommandProcessor(config => config
                .Logging(l => l.UseConsole(minLevel: Logger.Level.Warn))
                .EventStore(e => e.UseMongoDb(_mongoDatabase, "Events"))
                .EventDispatcher(e => e.Register<IEventDispatcher>(r => {
                    var repository = (IAggregateRootRepository)r.GetService(typeof(IAggregateRootRepository));
                    var eventStore = (IEventStore)r.GetService(typeof(IEventStore ));
                    var serializer = (IDomainEventSerializer)r.GetService(typeof(IDomainEventSerializer));
                    var typeMapper = (IDomainTypeNameMapper)r.GetService(typeof(IDomainTypeNameMapper));

                    _dispatcher = new ViewManagerEventDispatcher(repository, eventStore, serializer, typeMapper);

                    return _dispatcher;
                })
            ));

            RegisterForDisposal(_commandProcessor);
        }

        [TestCase(10)]
        [TestCase(100)]
        [TestCase(1000)]
        public void AutomaticallyReplaysEventsIfViewIsPurged(
	        int numberOfCommands)
        {
            var allPotatoesView = new MongoDbViewManager<AllPotatoesView>(_mongoDatabase);
            _dispatcher.AddViewManager(allPotatoesView);

            Console.WriteLine("Processing {0} commands....", numberOfCommands);
            Enumerable
	            .Range(0, numberOfCommands - 1)
                .ToList()
                .ForEach(_ => _commandProcessor.ProcessCommand(new BitePotato("someid1", .01m)));

            var lastResult = _commandProcessor.ProcessCommand(new BitePotato("someid2", .01m));

            Console.WriteLine("Waiting until {0} has been dispatched to the view...", lastResult.GetNewPosition());
            allPotatoesView.WaitUntilProcessed(lastResult, TimeSpan.FromSeconds(2)).Wait();

            var viewOnFirstLoad = allPotatoesView.Load(GlobalInstanceLocator.GetViewInstanceId());
            Assert.That(viewOnFirstLoad, Is.Not.Null);

            Console.WriteLine("Purging the view!");
            allPotatoesView.Purge();

            Console.WriteLine("Waiting until {0} has been dispatched to the view...", lastResult.GetNewPosition());
            allPotatoesView.WaitUntilProcessed(lastResult, TimeSpan.FromSeconds(30)).Wait();

            var viewOnNextLoad = allPotatoesView.Load(GlobalInstanceLocator.GetViewInstanceId());
            Assert.That(viewOnNextLoad, Is.Not.Null);

            Assert.That(viewOnNextLoad.LastGlobalSequenceNumber, Is.EqualTo(viewOnFirstLoad.LastGlobalSequenceNumber));
        }


        [TestCase(BlockOption.NoBlock)] 
        [TestCase(BlockOption.BlockOnViewManager)]
        [TestCase(BlockOption.BlockOnEventDispatcher)]
        public void CanBlockUntilViewIsUpdated(BlockOption blockOption)
        {
            // arrange
            var slowView = new MongoDbViewManager<SlowView>(_mongoDatabase);
            _dispatcher.AddViewManager(slowView);

            _commandProcessor.ProcessCommand(new BitePotato("potato1", .1m));
            _commandProcessor.ProcessCommand(new BitePotato("potato1", .1m));
            _commandProcessor.ProcessCommand(new BitePotato("potato1", .1m));
            _commandProcessor.ProcessCommand(new BitePotato("potato1", .1m));

            var result = _commandProcessor.ProcessCommand(new BitePotato("potato1", 1));

            // act
            switch (blockOption)
            {
                case BlockOption.BlockOnViewManager:
                    Console.WriteLine("Waiting for {0} on the view...", result.GetNewPosition());
                    slowView.WaitUntilProcessed(result, TimeSpan.FromSeconds(2)).Wait();
                    break;

                case BlockOption.BlockOnEventDispatcher:
                    Console.WriteLine("Waiting for {0} on the dispatcher...", result.GetNewPosition());
                    _dispatcher.WaitUntilProcessed<SlowView>(result, TimeSpan.FromSeconds(2)).Wait();
                    break;
            }

            // assert
            var instance = slowView.Load(InstancePerAggregateRootLocator.GetViewIdFromAggregateRootId("potato1"));

            if (blockOption == BlockOption.NoBlock)
            {
                Assert.That(instance, Is.Null);
                Console.WriteLine("View instance was null, just as expected");
            }
            else
            {
                Assert.That(instance, Is.Not.Null);
                Console.WriteLine("View instance was properly updated, just as expected");
            }
        }


        [Test]
        public void BasicDispatchOfSomeEvents()
        {
	        
	        
            var allPotatoesView = new MongoDbViewManager<AllPotatoesView>(_mongoDatabase);
            var potatoTimeToBeConsumedView = new MongoDbViewManager<PotatoTimeToBeConsumedView>(_mongoDatabase);

            _dispatcher.AddViewManager(allPotatoesView);
            _dispatcher.AddViewManager(potatoTimeToBeConsumedView);

            // act
            var firstPointInTime = new DateTime(1979, 3, 1, 12, 0, 0, DateTimeKind.Utc);
            TimeMachine.FixCurrentTimeTo(startTime: firstPointInTime);
            _commandProcessor.ProcessCommand(new BitePotato("potato1", 0.5m));
            _commandProcessor.ProcessCommand(new BitePotato("potato2", 0.3m));
            _commandProcessor.ProcessCommand(new BitePotato("potato2", 0.3m));
            _commandProcessor.ProcessCommand(new BitePotato("potato3", 0.3m));

            var nextPointInTime = new DateTime(1981, 6, 9, 12, 0, 0, DateTimeKind.Utc);
            TimeMachine.FixCurrentTimeTo(startTime: nextPointInTime);
            _commandProcessor.ProcessCommand(new BitePotato("potato1", 0.5m));
            _commandProcessor.ProcessCommand(new BitePotato("potato2", 0.5m));

            var lastPointInTime = new DateTime(1981, 6, 9, 12, 0, 0, DateTimeKind.Utc);
            var result = _commandProcessor.ProcessCommand(new BitePotato("potato3", 0.8m));

            //Flaky 1 (null) added wait handle
            _dispatcher.WaitUntilProcessed(result, TimeSpan.FromSeconds(10)).Wait();

            // assert
            var allPotatoes = allPotatoesView.Load(GlobalInstanceLocator.GetViewInstanceId());

            Assert.That(allPotatoes, Is.Not.Null);

            var potato1View = potatoTimeToBeConsumedView.Load(InstancePerAggregateRootLocator.GetViewIdFromAggregateRootId("potato1"));
            var potato2View = potatoTimeToBeConsumedView.Load(InstancePerAggregateRootLocator.GetViewIdFromAggregateRootId("potato2"));
            var potato3View = potatoTimeToBeConsumedView.Load(InstancePerAggregateRootLocator.GetViewIdFromAggregateRootId("potato3"));

            Assert.That(potato1View, Is.Not.Null);
            Assert.That(potato2View, Is.Not.Null);
            Assert.That(potato3View, Is.Not.Null);

            Assert.That(allPotatoes.NamesOfPotatoes.Count, Is.EqualTo(3));
            Assert.That(allPotatoes.NamesOfPotatoes["potato1"], Is.EqualTo("Jeff"));
            Assert.That(allPotatoes.NamesOfPotatoes["potato2"], Is.EqualTo("Bunny"));
            Assert.That(allPotatoes.NamesOfPotatoes["potato3"], Is.EqualTo("Walter"));

            Assert.That(potato1View.Name, Is.EqualTo("Jeff"));
            Assert.That(potato1View.TimeOfCreation.ToUniversalTime(), Is.EqualTo(firstPointInTime).Within(2).Milliseconds);
            Assert.That(potato1View.TimeToBeEaten, Is.EqualTo(nextPointInTime - firstPointInTime).Within(2).Milliseconds);

            Assert.That(potato2View.Name, Is.EqualTo("Bunny"));
            Assert.That(potato2View.TimeOfCreation.ToUniversalTime(), Is.EqualTo(firstPointInTime).Within(2).Milliseconds);
            Assert.That(potato2View.TimeToBeEaten, Is.EqualTo(nextPointInTime - firstPointInTime).Within(2).Milliseconds);

            Assert.That(potato3View.Name, Is.EqualTo("Walter"));
            Assert.That(potato3View.TimeOfCreation.ToUniversalTime(), Is.EqualTo(firstPointInTime).Within(2).Milliseconds);
            Assert.That(potato3View.TimeToBeEaten, Is.EqualTo(lastPointInTime - firstPointInTime).Within(2).Milliseconds);
        }
    }
}