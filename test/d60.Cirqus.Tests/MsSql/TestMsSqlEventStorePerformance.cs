﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using d60.Cirqus.Events;
using d60.Cirqus.Extensions;
using d60.Cirqus.Numbers;
using d60.Cirqus.Tests.Contracts.EventStore.Factories;
//using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Contracts.EventStore
{
    [TestFixture(typeof(MongoDbEventStoreFactory), Category = TestCategories.MongoDb)]
    [TestFixture(typeof(InMemoryEventStoreFactory))]
    //[TestFixture(typeof(MsSqlEventStoreFactory), Category = TestCategories.MsSql)]
    //[TestFixture(typeof(PostgreSqlEventStoreFactory), Category = TestCategories.PostgreSql)]
    //[TestFixture(typeof(NtfsEventStoreFactory))]
    //[TestFixture(typeof(SQLiteEventStoreFactory), Category = TestCategories.SQLite)]
    //[TestFixture(typeof(CachedEventStoreFactory), Category = TestCategories.MongoDb, Description = "Uses MongoDB behind the scenes")]
    public class TestEventStorePerformance<TEventStoreFactory>
        : FixtureBase where TEventStoreFactory
        : IEventStoreFactory, new()
    {
        readonly Random _random = new Random(DateTime.Now.GetHashCode());
        int _globalSequenceNumber;
        private IEventStore _eventStore;

        protected override void DoSetUp()
        {
            var eventStoreFactory = new TEventStoreFactory();

            _eventStore = eventStoreFactory.GetEventStore();

            if (_eventStore is IDisposable)
            {
                RegisterForDisposal((IDisposable)_eventStore);
            }
        }

        [TestCase(10000)]
        public void CheckReadPerformance(int numberOfEvents)
        {
            var stopwatch = Stopwatch.StartNew();
            WriteEvents(numberOfEvents);
            var seconds = stopwatch.Elapsed.TotalSeconds;

            Console.WriteLine("Writing {0} events took {1:0.0} s - that's {2:0.0} events/s", numberOfEvents, seconds, numberOfEvents / seconds);

            var measuredSeconds = new List<Tuple<double, int>>();

            100.Times(() =>
            {
                var readStopwatch = Stopwatch.StartNew();
                var randomAggregateRootId = GetRandomAggregateRootId();

                var events = _eventStore.Load(randomAggregateRootId).ToList();

                measuredSeconds.Add(Tuple.Create(readStopwatch.Elapsed.TotalSeconds, events.Count));
            });

            Console.WriteLine("Reading entire root event stream took {0:0.0} for {1:0.0} events - that's {2:0.0} events/s (AVG)",
                measuredSeconds.Average(a => a.Item1), measuredSeconds.Average(a => a.Item2), measuredSeconds.Average(a => a.Item2) / measuredSeconds.Average(a => a.Item1));

            var streamStopwatch = Stopwatch.StartNew();
            var counter = 0;
            foreach(var e in _eventStore.Stream())
            {
                counter++;
            }
            var totalSeconds = streamStopwatch.Elapsed.TotalSeconds;
            Console.WriteLine("Streaming all events took {0:0.0} s for {1} events - that's {2:0.0} events/s",
                totalSeconds, counter, counter / totalSeconds);
        }

        void WriteEvents(int numberOfEvents)
        {
            var sequenceNumbers = new Dictionary<string, int>();

            foreach(var batch in Enumerable.Range(0, numberOfEvents).Batch(1000))
            {
                _eventStore.Save(Guid.NewGuid(),
                    batch.Select(i => EventData.FromMetadata(GetMeta(sequenceNumbers), FakeData(1024))));
            }
        }

        Metadata GetMeta(Dictionary<string, int> sequenceNumbers)
        {
            var meta = new Metadata();

            var aggregateRootId = GetRandomAggregateRootId();

            meta[DomainEvent.MetadataKeys.AggregateRootId] = aggregateRootId;
            meta[DomainEvent.MetadataKeys.SequenceNumber] = GetNext(sequenceNumbers, aggregateRootId);
            meta[DomainEvent.MetadataKeys.GlobalSequenceNumber] = (_globalSequenceNumber++).ToString();

            return meta;
        }

        string GetRandomAggregateRootId()
        {
            return $"agg-{_random.Next(100)}";
        }

        string GetNext(Dictionary<string, int> sequenceNumbers, string aggregateRootId)
        {
            if(!sequenceNumbers.ContainsKey(aggregateRootId))
            {
                sequenceNumbers[aggregateRootId] = 0;
            }

            return (sequenceNumbers[aggregateRootId]++).ToString();
        }

        byte[] FakeData(int byteCount)
        {
            var buffer = new byte[byteCount];
            _random.NextBytes(buffer);
            return buffer;
        }
    }
}