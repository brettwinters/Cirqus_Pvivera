﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Config;
using d60.Cirqus.Events;
using d60.Cirqus.Extensions;
using d60.Cirqus.Logging;
using d60.Cirqus.Logging.Console;
using d60.Cirqus.MongoDb.Config;
using d60.Cirqus.Serialization;
using d60.Cirqus.Tests.MongoDb;
using d60.Cirqus.Views;
using d60.Cirqus.Views.ViewManagers;
using MongoDB.Driver;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Views.ViewManager
{
    [TestFixture, Category(TestCategories.MongoDb), 
     Description("Verifies that the event store is not queried when the directly dispatched events fit " +
                 "perfectly for what the views expect")]
    public class TestViewManagerEventDispatcherWithOptimization : FixtureBase
    {
	    ViewManagerEventDispatcher _dispatcher;
	    ICommandProcessor _commandProcessor;
        IMongoDatabase _mongoDatabase;
        ThrowingEventStore _thisBadBoyEnsuresThatTheEventStoreIsNotUsed;
        
	    #region

        class LeCommand : Cirqus.Commands.Command<Root>
        {
	        public LeCommand(string aggregateRootId) : base(aggregateRootId)
	        {
	        }

	        public override void Execute(Root aggregateRoot)
	        {
		        aggregateRoot.EmitStuff();
	        }
        }

        class Root : AggregateRoot, IEmit<Event>
        {
	        public void EmitStuff()
	        {
		        Emit(new Event());
	        }

	        public void Apply(Event e)
	        {
	        }
        }

        class Event : DomainEvent<Root> { }

        class TestViewManager : IViewManager
        {
	        long _position = -1;

	        public string Id => $"testviewmanager-{GetHashCode()}";

	        public Task<long> GetPosition(bool canGetFromCache = true)
	        {
		        return Task.FromResult(_position);
	        }

	        public void Dispatch(IViewContext viewContext, IEnumerable<DomainEvent> batch, IViewManagerProfiler viewManagerProfiler)
	        {
		        foreach (var e in batch)
		        {
			        _position = e.GetGlobalSequenceNumber();
			        Console.WriteLine("TestViewManager: position = {0}", _position);
		        }
	        }

	        public async Task WaitUntilProcessed(CommandProcessingResult result, TimeSpan timeout)
	        {
		        if (!result.EventsWereEmitted) return;

		        var stopwatch = Stopwatch.StartNew();
                
		        while (_position < result.GetNewPosition())
		        {
			        await Task.Delay(100);

			        if (stopwatch.Elapsed > timeout)
			        {
				        throw new TimeoutException($"oh noes, the view did not catch up within {timeout} timeout!");
			        }
		        }
	        }

	        public void Purge()
	        {
		        _position = -1;
	        }
        }

        class ThrowingEventStore : IEventStore
        {
	        public bool Throw { get; set; }

	        public void Save(Guid batchId, IEnumerable<EventData> batch)
	        {

	        }

	        public IEnumerable<EventData> Load(string aggregateRootId, long firstSeq = 0)
	        {
		        if (!Throw) yield break;
		        
		        Assert.Fail("this event store prevents loading");

		        throw new InvalidOperationException("this event store prevents loading");
	        }

	        public IEnumerable<EventData> Stream(long globalSequenceNumber = 0)
	        {
		        if (!Throw) yield break;
		        
		        //Assert.Fail("this event store prevents streaming");

		        throw new InvalidOperationException("this event store prevents streaming");
	        }

	        // public long GetNextGlobalSequenceNumber()
	        // {
		       //  if (!Throw) return 0;
		       //  
		       //  Assert.Fail("this event store prevents the getting of the next sequence number");
		       //  
		       //  throw new InvalidOperationException("this event store prevents the getting of the next sequence number");
	        // }

	        public long GetLastGlobalSequenceNumber()
	        {
		        if (!Throw) return 0;
		        
		        Assert.Fail("this event store prevents the getting of the last sequence number");

		        throw new InvalidOperationException("this event store prevents the getting of the last sequence number");
	        }
        }

        #endregion

        protected override void DoSetUp()
        {
            CirqusLoggerFactory.Current = new ConsoleLoggerFactory(minLevel: Logger.Level.Debug);
            _mongoDatabase = MongoHelper.InitializeTestDatabase();
            
            _commandProcessor = CreateCommandProcessor(config => config
                .Logging(l => l.UseConsole(minLevel: Logger.Level.Warn))
                .EventStore(e => e.UseMongoDb(_mongoDatabase, "Events"))
                .EventDispatcher(e => e.Register<IEventDispatcher>(r => {
                    var repository = (IAggregateRootRepository)r.GetService(typeof(IAggregateRootRepository));
                    var serializer = (IDomainEventSerializer)r.GetService(typeof(IDomainEventSerializer));
                    var typeMapper = (IDomainTypeNameMapper)r.GetService(typeof(IDomainTypeNameMapper));

                    _thisBadBoyEnsuresThatTheEventStoreIsNotUsed = new ThrowingEventStore();

                    _dispatcher = new ViewManagerEventDispatcher(repository, _thisBadBoyEnsuresThatTheEventStoreIsNotUsed, serializer, typeMapper) {
                        AutomaticCatchUpInterval = TimeSpan.FromHours(24) //<effectively disable automatic catchup
                    };

                    return _dispatcher;
                }))
            );

            RegisterForDisposal(_commandProcessor);
        }

        [Test]
        [Description(@"This test throws a bunch of exceptions - the point is, though, that the event store's stream
                       method throws an exception, which means that the only way for the view manager to catch up is
                       if it can get events by direct dispatch")]
        public void CanDeliverDomainEventsDirectlyWhenEverythingAlignsPerfectly()
        {
            var testViewManager = new TestViewManager();
            _dispatcher.AddViewManager(testViewManager);
            _thisBadBoyEnsuresThatTheEventStoreIsNotUsed.Throw = true;

            CommandProcessingResult result = null;
            10.Times(() => result = _commandProcessor.ProcessCommand(new LeCommand("someId")));

            testViewManager.WaitUntilProcessed(result, TimeSpan.FromSeconds(30)).Wait();
            
            Assert.That(testViewManager.GetPosition().Result, Is.EqualTo(9));
        }
    }
}