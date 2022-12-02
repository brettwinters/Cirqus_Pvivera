using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Config;
using d60.Cirqus.Events;
using d60.Cirqus.InMemory.Events;
using d60.Cirqus.Numbers;
using d60.Cirqus.Serialization;
using d60.Cirqus.Testing;
using d60.Cirqus.Tests.Stubs;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using TestContext = d60.Cirqus.Testing.TestContext;

namespace d60.Cirqus.Tests.Aggregates
{
    [TestFixture]
    public class TestEventApplication
    {
        readonly JsonDomainEventSerializer _domainEventSerializer = new JsonDomainEventSerializer();
        readonly DefaultDomainTypeNameMapper _defaultDomainTypeNameMapper = new DefaultDomainTypeNameMapper();
        private DefaultAggregateRootRepository _aggregateRootRepository;

        #region

        class SomeEvent : DomainEvent<SomeAggregate>
        {
	        public readonly string What;

	        public SomeEvent(string what)
	        {
		        What = what;
	        }
        }

        class SomeAggregate : AggregateRoot, IEmit<SomeEvent>
        {
	        public readonly List<string> StuffThatWasDone = new List<string>();
        
	        public void DoSomething()
	        {
		        Emit(new SomeEvent("emitted an event"));
	        }

	        public void Apply(SomeEvent e)
	        {
		        StuffThatWasDone.Add(e.What);
	        }
        }

        #endregion
        
        [SetUp]
        public void Setup()
        {
	        //TODO Remove once sure it works
	        FakeGlobalSequenceNumberService.Reset();
	        
	        _aggregateRootRepository = new DefaultAggregateRootRepository(
		        eventStore: new InMemoryEventStore(), 
		        domainEventSerializer: _domainEventSerializer,
		        domainTypeNameMapper: _defaultDomainTypeNameMapper
		    );
        }
        
        /// <summary>
        /// Without caching: Elapsed total: 00:00:03.0647447, hydrations/s: 32,6
        /// </summary>
        [TestCase(2000, 100)]
        public void TestRawApplicationPerformance(int numberOfEvents, int numberOfHydrations)
        {
            const string aggregateRootId = "bim";

            var service = new ServiceCollection();
            service.AddTestContext(config => config.Options(x => x.Asynchronous()));
            var provider = service.BuildServiceProvider();

            using var context = provider.GetRequiredService<TestContext>();
            Console.WriteLine("Saving {0} to history of '{1}'", numberOfEvents, aggregateRootId);

            using (var printer = new Timer(2000))
            {
	            var inserts = 0;
	            printer.Elapsed += delegate { Console.WriteLine("{0} events saved...", inserts); };
	            printer.Start();

	            foreach (var e in Enumerable.Range(0, numberOfEvents).Select(i => new SomeEvent($"Event {i}")))
	            {
		            context.Save(aggregateRootId, e);
		            inserts++;
	            }
            }

            Console.WriteLine("Hydrating {0} times", numberOfHydrations);
            var stopwatch = Stopwatch.StartNew();
            numberOfHydrations.Times(() =>
            {
	            using var uow = context.BeginUnitOfWork();
	            var _ = uow.Load<SomeAggregate>(aggregateRootId);
            });
                
            var elapsed = stopwatch.Elapsed;
            Console.WriteLine("Elapsed total: {0}, hydrations/s: {1:0.0}", elapsed, numberOfHydrations/elapsed.TotalSeconds);
        }

        [Test]
        public void AppliesEmittedEvents()
        {
            var someAggregate = new SomeAggregate
            {
                UnitOfWork = new ConsoleOutUnitOfWork(_aggregateRootRepository),
            };
            someAggregate.Initialize("root_id");

            someAggregate.DoSomething();

            Assert.That(someAggregate.StuffThatWasDone.Count, Is.EqualTo(1));
            Assert.That(someAggregate.StuffThatWasDone.First(), Is.EqualTo("emitted an event"));
        }

        [Test]
        public void ProvidesSuitableMetadataOnEvents()
        {
            var timeForFirstEvent = new DateTime(1979, 3, 19, 19, 0, 0, DateTimeKind.Utc);
            var timeForNextEvent = timeForFirstEvent.AddMilliseconds(2);
            
            var eventCollector = new InMemoryUnitOfWork(_aggregateRootRepository, _defaultDomainTypeNameMapper);

            var someAggregate = new SomeAggregate
            {
                UnitOfWork = eventCollector,
            };
            someAggregate.Initialize("root_id");

            TimeMachine.FixCurrentTimeTo(startTime: timeForFirstEvent);

            someAggregate.DoSomething();

            TimeMachine.FixCurrentTimeTo(startTime: timeForNextEvent);

            someAggregate.DoSomething();

            var events = eventCollector.Cast<SomeEvent>().ToList();
            var firstEvent = events[0];

            Assert.That(DateTime.Parse(firstEvent.Meta[DomainEvent.MetadataKeys.TimeUtc]), Is.EqualTo(timeForFirstEvent).Within(1).Milliseconds);
            Assert.That(firstEvent.Meta[DomainEvent.MetadataKeys.Owner], Is.EqualTo("d60.Cirqus.Tests.Aggregates.TestEventApplication+SomeAggregate, d60.Cirqus.Tests"));
            Assert.That(firstEvent.Meta[DomainEvent.MetadataKeys.Type], Is.EqualTo("d60.Cirqus.Tests.Aggregates.TestEventApplication+SomeEvent, d60.Cirqus.Tests"));
            Assert.That(firstEvent.Meta[DomainEvent.MetadataKeys.SequenceNumber], Is.EqualTo("0"));
            Assert.That(firstEvent.Meta[DomainEvent.MetadataKeys.AggregateRootId], Is.EqualTo("root_id"));

            var nextEvent = events[1];

            Assert.That(DateTime.Parse(nextEvent.Meta[DomainEvent.MetadataKeys.TimeUtc]), Is.EqualTo(timeForNextEvent).Within(1).Milliseconds);
            Assert.That(nextEvent.Meta[DomainEvent.MetadataKeys.Owner], Is.EqualTo("d60.Cirqus.Tests.Aggregates.TestEventApplication+SomeAggregate, d60.Cirqus.Tests"));
            Assert.That(nextEvent.Meta[DomainEvent.MetadataKeys.Type], Is.EqualTo("d60.Cirqus.Tests.Aggregates.TestEventApplication+SomeEvent, d60.Cirqus.Tests"));
            Assert.That(nextEvent.Meta[DomainEvent.MetadataKeys.SequenceNumber], Is.EqualTo("1"));
            Assert.That(nextEvent.Meta[DomainEvent.MetadataKeys.AggregateRootId], Is.EqualTo("root_id"));
        }

        [Test]
        public void FailsOnSequenceMismatch()
        {
            var someAggregate = new SomeAggregate();

            var @eventWithTooLateSeqNumber = new SomeEvent("something");

            // some global seq - not important
            @eventWithTooLateSeqNumber.Meta[DomainEvent.MetadataKeys.GlobalSequenceNumber] = 10.ToString();
            
            // local seq that are too far ahead
            @eventWithTooLateSeqNumber.Meta[DomainEvent.MetadataKeys.SequenceNumber] = 1.ToString();

            Assert.Throws<ApplicationException>(() => someAggregate.ApplyEvent(@eventWithTooLateSeqNumber, ReplayState.ReplayApply));
        }

        
    }

}