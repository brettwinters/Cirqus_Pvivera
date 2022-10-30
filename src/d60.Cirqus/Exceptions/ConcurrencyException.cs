using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using d60.Cirqus.Events;
using d60.Cirqus.Extensions;

namespace d60.Cirqus.Exceptions;

/// <summary>
/// Exception that must be raised when an attempt to commit a batch of events has failed because one or more of the involved event sequence numbers have been taken
/// </summary>
[Serializable]
public class ConcurrencyException : ApplicationException
{
	public ConcurrencyException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}

	public ConcurrencyException(Guid batchId, IEnumerable<EventData> involvedDomainEvents, Exception innerException)
		: base(FormatErrorMessage(batchId, involvedDomainEvents), innerException)
	{
            
	}

	static string FormatErrorMessage(Guid batchId, IEnumerable<EventData> involvedDomainEvents)
	{
		var sequenceNumbersText = string.Join(Environment.NewLine, involvedDomainEvents
			.Select(e => $"    {e.GetGlobalSequenceNumber()} - {e.GetAggregateRootId()} / {e.GetSequenceNumber()}"));

		return $@"Could not save batch {batchId} containing

{sequenceNumbersText}

to the event store because someone else beat us to it";
	}
}