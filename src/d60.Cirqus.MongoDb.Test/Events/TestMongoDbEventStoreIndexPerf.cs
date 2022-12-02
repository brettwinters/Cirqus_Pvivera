using System;
using System.Collections.Generic;
using System.Linq;
using d60.Cirqus.Events;
using d60.Cirqus.MongoDb.Events;
using d60.Cirqus.Numbers;
using d60.Cirqus.Serialization;
using NUnit.Framework;
using d60.Cirqus.Tests;

namespace d60.Cirqus.Tests.MongoDb
{
    [TestFixture]
    [Category(TestCategories.MongoDb)]
    public class TestMongoDbEventStoreIndexPerf : FixtureBase
    {
        [TestCase(false, 100, 10, Description = "Indexes")]
        [TestCase(true, 100, 1000, Description = "Indexes")]
        [TestCase(false, 100, 1000, Description = "NO indexes")]
        [LongRunningTestCase(true, 100, 10*1000, Description = "Indexes")]
        [LongRunningTestCase(false, 100, 10 * 1000, Description = "NO indexes")]
        public void IndexSpeedTest(
	        bool useIndexes, 
	        int numberOfQueries, 
	        int numberOfEvents)
        {
            var sequenceNumbers = new Dictionary<string, long>();
            var serializer = new JsonDomainEventSerializer();

            try
            {
                var database = MongoHelper.InitializeTestDatabase();
                var eventStore = new MongoDbEventStore(database, "events", automaticallyCreateIndexes: useIndexes);

                var random = new Random(DateTime.Now.GetHashCode());
                var aggregateRootIds = Enumerable.Range(0, 1000).Select(i => i.ToString()).ToArray();

                Func<string, long> getNextSequenceNumber = id => !sequenceNumbers.ContainsKey(id) ? (sequenceNumbers[id] = 0) : ++sequenceNumbers[id];
                Func<string> randomAggregateRootId = () => aggregateRootIds[random.Next(aggregateRootIds.Length)];

                var events = Enumerable.Range(1, numberOfEvents)
                    .Select(i => Event(getNextSequenceNumber, randomAggregateRootId()))
                    .ToList();

                TakeTime("Insert " + events.Count + " events", () =>
                {
                    foreach (var e in events)
                    {
                        eventStore.Save(Guid.NewGuid(), new[] { serializer.Serialize(e) });
                    }
                });

                TakeTime("Execute " + numberOfQueries + " queries", () => numberOfQueries.Times(() => eventStore.Load(randomAggregateRootId()).ToList()));
            }
            finally
            {
                Console.WriteLine("This is how far we got:{0}", string.Join(Environment.NewLine, sequenceNumbers.Select(kvp => $"    {kvp.Key}: {kvp.Value}")));
            }
        }

        static DomainEvent Event(
	        Func<string, long> getNextSeqNo, 
	        string aggregateRootId)
        {
            var nextSeqNo = getNextSeqNo(aggregateRootId);

            Console.WriteLine("Generating event {0} / {1}", aggregateRootId, nextSeqNo);

            return new SomeEvent
            {
                SomeValue = "hej",
                Meta =
                {
	                [DomainEvent.MetadataKeys.GlobalSequenceNumber] = GlobalSequenceNumberService.GetNewGlobalSequenceNumber().ToString(),
	                [DomainEvent.MetadataKeys.AggregateRootId] = aggregateRootId,
	                [DomainEvent.MetadataKeys.SequenceNumber] = nextSeqNo.ToString(Metadata.NumberCulture),
                }
            };
        }

        class SomeEvent : DomainEvent
        {
            public string SomeValue { get; set; }
        }
    }
}