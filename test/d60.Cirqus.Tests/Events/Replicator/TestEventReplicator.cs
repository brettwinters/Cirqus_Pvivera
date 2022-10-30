using System;
using System.Linq;
using System.Threading;
using d60.Cirqus.Events;
using d60.Cirqus.InMemory.Events;
using d60.Cirqus.Numbers;
using d60.Cirqus.Serialization;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Events.Replicator
{
    [TestFixture]
    public class TestEventReplicator : FixtureBase
    {
	    #region

	    class RecognizableEvent : DomainEvent
	    {
		    public RecognizableEvent(string id)
		    {
			    Id = id;
		    }

		    public string Id { get; set; }
	    }

	    #endregion
	    
        [Test]
        public void DoesNotThrowWhenDisposingStoppedReplicator()
        {
            // arrange
            var eventReplicator = new EventReplicator(new InMemoryEventStore(), new InMemoryEventStore());

            // act / Assert
            eventReplicator.Dispose();
        }

        [Test]
        public void TryReplicating()
        {
            var serializer = new JsonDomainEventSerializer();
            var source = new InMemoryEventStore();
            var destination = new InMemoryEventStore();
            var seqNo = 0;

         //    Func<string, EventData> getRecognizableEvent = text => serializer.Serialize(
	        //     new RecognizableEvent(text)
	        //     {
	        //         Meta =
	        //         {
	        //             [DomainEvent.MetadataKeys.AggregateRootId] = "268DD0C0-529F-4242-9D53-601A88BB1813",
	        //             [DomainEvent.MetadataKeys.SequenceNumber] = (seqNo).ToString(Metadata.NumberCulture),
	        //             [DomainEvent.MetadataKeys.GlobalSequenceNumber] = (seqNo++).ToString(Metadata.NumberCulture),
	        //         }
	        //     }
	        // );

            Func<string, EventData> getRecognizableEvent = text => serializer.Serialize(
	            new RecognizableEvent(text)
	            {
	                Meta =
	                {
	                    [DomainEvent.MetadataKeys.AggregateRootId] = "268DD0C0-529F-4242-9D53-601A88BB1813",
	                    [DomainEvent.MetadataKeys.SequenceNumber] = (seqNo++).ToString(Metadata.NumberCulture),
	                    [DomainEvent.MetadataKeys.GlobalSequenceNumber] = GlobalSequenceNumberService.GetNewGlobalSequenceNumber().ToString(Metadata.NumberCulture),
	                }
	            }
	        );

            // arrange
            using (var eventReplicator = new EventReplicator(source, destination))
            {
                eventReplicator.Start();
                Thread.Sleep(TimeSpan.FromSeconds(2));

                // act
                source.Save(Guid.NewGuid(), new[] { getRecognizableEvent("hello") });
                source.Save(Guid.NewGuid(), new[] { getRecognizableEvent("there") });
                source.Save(Guid.NewGuid(), new[] { getRecognizableEvent("my") });
                source.Save(Guid.NewGuid(), new[] { getRecognizableEvent("friend") });

                while (destination.GetLastGlobalSequenceNumber() != 4 - 1)
                {
	                Thread.Sleep(TimeSpan.FromMilliseconds(50));
                }
            }

            // assert
            var greeting = string.Join(" ", destination
                .Select(x => serializer.Deserialize(x))
                .OfType<RecognizableEvent>()
                .Select(e => e.Id));

            Assert.That(greeting, Is.EqualTo("hello there my friend"));
        }
    }
}