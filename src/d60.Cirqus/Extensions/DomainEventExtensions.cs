﻿using System;
using System.Globalization;
using System.Linq;
using d60.Cirqus.Events;

namespace d60.Cirqus.Extensions;

public static class DomainEventExtensions
{
	/// <summary>
	/// Gets the aggregate root ID from the domain event
	/// </summary>
	public static string GetAggregateRootId(this IDomainEvent domainEvent, bool throwIfNotFound = true)
	{
		return GetMetadataField(domainEvent, DomainEvent.MetadataKeys.AggregateRootId, value => value, throwIfNotFound);
	}

	/// <summary>
	/// Gets the batch ID from the domain event
	/// </summary>
	public static Guid GetBatchId(this IDomainEvent domainEvent, bool throwIfNotFound = true)
	{
		return GetMetadataField(domainEvent, DomainEvent.MetadataKeys.BatchId, value => new Guid(Convert.ToString(value)), throwIfNotFound);
	}

	/// <summary>
	/// Gets the (root-local) sequence number from the domain event
	/// </summary>
	public static long GetSequenceNumber(this IDomainEvent domainEvent, bool throwIfNotFound = true)
	{
		return GetMetadataField(domainEvent, DomainEvent.MetadataKeys.SequenceNumber, Convert.ToInt64, throwIfNotFound);
	}

	/// <summary>
	/// Gets the global sequence number from the domain event
	/// </summary>
	public static long GetGlobalSequenceNumber(this IDomainEvent domainEvent, bool throwIfNotFound = true)
	{
		return GetMetadataField(domainEvent, DomainEvent.MetadataKeys.GlobalSequenceNumber, Convert.ToInt64, throwIfNotFound);
	}

	//TODO Write Tests
	/// <summary>
	/// Gets the timestamp in <see cref="DateTime.Ticks"/> from the domain event
	/// </summary>
	public static long GetTimeStamp(
		this IDomainEvent domainEvent, 
		bool throwIfNotFound = true)
	{
		return GetMetadataField(
			domainEvent: domainEvent, 
			key: DomainEvent.MetadataKeys.TimeUtc, 
			converter: v => DateTime.Parse(v).Ticks, 
			throwIfNotFound: throwIfNotFound
		);
	}

	/// <summary>
	/// Gets the UTC time of when the event was emitted from the <seealso cref="DomainEvent.MetadataKeys.TimeUtc"/>
	/// header on the event. If <seealso cref="throwIfNotFound"/> is false and the header is not present,
	/// <seealso cref="DateTime.MinValue"/> is returned
	/// </summary>
	public static DateTime GetUtcTime(
		this IDomainEvent domainEvent, 
		bool throwIfNotFound = true)
	{
		var timeAsString = GetMetadataField(domainEvent, DomainEvent.MetadataKeys.TimeUtc, Convert.ToString, throwIfNotFound);

		if (string.IsNullOrWhiteSpace(timeAsString))
		{
			return DateTime.MinValue;
		}

		var dateTime = DateTime.ParseExact(timeAsString, DomainEvent.DateTimeFormat, CultureInfo.CurrentCulture);

		return new DateTime(dateTime.Ticks, DateTimeKind.Utc);
	}


	static TValue GetMetadataField<TValue>(
		IDomainEvent domainEvent, 
		string key, 
		Func<string, TValue> converter, 
		bool throwIfNotFound)
	{
		var metadata = domainEvent.Meta;

		if (metadata.ContainsKey(key))
		{
			return converter(metadata[key]);
		}

		if (!throwIfNotFound)
		{
			return converter(null);
		}

		var metadataString = string.Join(", ", metadata.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
		var message = $"Attempted to get value of key '{key}' from event {domainEvent}, but only the following" + $" metadata were available: {metadataString}";

		throw new InvalidOperationException(message);
	}
}