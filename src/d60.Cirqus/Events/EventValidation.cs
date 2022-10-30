using System;
using System.Collections.Generic;
using System.Linq;
using d60.Cirqus.Extensions;

namespace d60.Cirqus.Events;

/// <summary>
/// Provides a bunch of static methods that make it easy to perform some simple integrity tests on an event batch that is to be committed
/// </summary>
public class EventValidation
{
	/// <summary>
	/// Validates the integrity of the given event batch with respect to sequence numbers etc.
	/// </summary>
	public static void ValidateBatchIntegrity(
		Guid batchId, 
		List<EventData> events)
	{
		EnsureAllEventsHaveSequenceNumbers(events);

		EnsureAllEventsHaveAggregateRootId(events);

		//TODO uncomment
		//EnsureSeq(batchId, events);
		EnsureSequenceNumbers(batchId, events);
		EnsureGlobalSequenceNumbers(batchId, events);
	}

	static void EnsureAllEventsHaveAggregateRootId(
		List<EventData> events)
	{
		if (events.Any(e => !e.Meta.ContainsKey(DomainEvent.MetadataKeys.AggregateRootId)))
		{
			throw new InvalidOperationException("Can't save batch with event without an aggregate root id");
		}
	}

	static void EnsureAllEventsHaveSequenceNumbers(
		List<EventData> events)
	{
		if (events.Any(e => !e.Meta.ContainsKey(DomainEvent.MetadataKeys.SequenceNumber)))
		{
			throw new InvalidOperationException("Can't save batch with event without a sequence number");
		}

		if (events.Any(e => !e.Meta.ContainsKey(DomainEvent.MetadataKeys.GlobalSequenceNumber)))
		{
			throw new InvalidOperationException("Can't save batch with event without a global sequence number");
		}
	}

	static void EnsureGlobalSequenceNumbers(
		Guid batchId,
		List<EventData> events)
	{
		var globalSequenceNumbers = events.Select(e => e.GetGlobalSequenceNumber());
		
		if (!IsUnique(globalSequenceNumbers))
		{
			throw new InvalidOperationException(
				$"Attempted to save batch {batchId} which contained events with non-unique " +
				$"global sequence numbers!{string.Join(", ", events.Select(ev => ev.GetGlobalSequenceNumber()))}"
			);
		}
		
		if (!IsOrdered(globalSequenceNumbers))
		{
			throw new InvalidOperationException(
				$"Attempted to save batch {batchId} which contained events with non-ordered " +
				$"global sequence numbers!{string.Join(", ", events.Select(ev => ev.GetGlobalSequenceNumber()))}"
			);
		}

		#region 

		bool IsOrdered(
			IEnumerable<long> numbers)
		{
			return numbers.OrderBy(a => a).SequenceEqual(numbers);
		}
		
		bool IsUnique(IEnumerable<long> numbers)
		{
			return numbers.Distinct().Count() == numbers.Count();
		}

		#endregion
	}
	static void EnsureSequenceNumbers(
		Guid batchId,
		List<EventData> events)
	{
		var groupedSequenceNumbers = events
			.GroupBy(
				e => e.GetAggregateRootId(), 
				(id, es) => (id, eIds: es.Select(e => e.GetSequenceNumber())) 
			);
		
		// SequenceNumbers
		foreach (var group in groupedSequenceNumbers)
		{
			if (!IsSequential(group.eIds))
			{
				throw new InvalidOperationException(
					$"Attempted to save batch {batchId} which contained events with non-sequential sequence numbers!" +
					$"{string.Join(Environment.NewLine, events.Select(ev => $"{ev.GetAggregateRootId()} / {ev.GetSequenceNumber()}"))}"
				);
			}
		}

		#region

		bool IsSequential(
			IEnumerable<long> numbers)
		{
			return numbers.Zip(numbers.Skip(1), (a, b) => (a + 1) == b).All(x => x);
		}

		#endregion
	}

	// static void EnsureSeq(Guid batchId, List<EventData> events)
	// {
	// 	var aggregateRootSeqs = events
	// 		.GroupBy(e => e.GetAggregateRootId())
	// 		.ToDictionary(g => g.Key, g => g.Min(e => e.GetSequenceNumber()));
	//
	// 	var expectedNextGlobalSeq = events.First().GetGlobalSequenceNumber();
	//
	// 	foreach (var e in events)
	// 	{
	// 		var sequenceNumberOfThisEvent = e.GetSequenceNumber();
	// 		var globalSequenceNumberOfThisEvent = e.GetGlobalSequenceNumber();
	// 		var aggregateRootId = e.GetAggregateRootId();
	// 		var expectedSequenceNumber = aggregateRootSeqs[aggregateRootId];
	//
	// 		if (globalSequenceNumberOfThisEvent != expectedNextGlobalSeq)
	// 		{
	// 			throw new InvalidOperationException(
	// 				$"Attempted to save batch {batchId} which contained events with non-sequential " +
	// 				$"global sequence numbers!{string.Join(", ", events.Select(ev => ev.GetGlobalSequenceNumber()))}"
	// 			);
	// 		}
	//
	// 		if (sequenceNumberOfThisEvent != expectedSequenceNumber)
	// 		{
	// 			throw new InvalidOperationException(
	// 				$"Attempted to save batch {batchId} which contained events with non-sequential sequence numbers!" +
	// 				$"{string.Join(Environment.NewLine, events.Select(ev => $"{ev.GetAggregateRootId()} / {ev.GetSequenceNumber()}"))}"
	// 			);
	// 		}
	//
	// 		aggregateRootSeqs[aggregateRootId]++;
	// 		expectedNextGlobalSeq++;
	// 	}
	// }
}