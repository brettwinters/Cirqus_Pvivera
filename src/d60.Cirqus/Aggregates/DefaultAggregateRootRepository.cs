﻿using d60.Cirqus.Config;
using d60.Cirqus.Events;
using d60.Cirqus.Exceptions;
using d60.Cirqus.Extensions;
using d60.Cirqus.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace d60.Cirqus.Aggregates;

/// <summary>
/// Standard replaying aggregate root repository that will return an aggregate root and always replay all events in order to bring it up-to-date
/// </summary>
public class DefaultAggregateRootRepository : IAggregateRootRepository
{
	private readonly IEventStore _eventStore;
	private readonly IDomainEventSerializer _domainEventSerializer;
	private readonly IDomainTypeNameMapper _domainTypeNameMapper;

	public DefaultAggregateRootRepository(IEventStore eventStore, IDomainEventSerializer domainEventSerializer, IDomainTypeNameMapper domainTypeNameMapper) {
		_eventStore = eventStore;
		_domainEventSerializer = domainEventSerializer;
		_domainTypeNameMapper = domainTypeNameMapper;
	}

	/// <summary>
	/// Checks whether one or more events exist for an aggregate root with the specified ID. If <seealso cref="maxGlobalSequenceNumber"/> is specified,
	/// it will check whether the root instance existed at that point in time
	/// </summary>
	public bool Exists(string aggregateRootId, long maxGlobalSequenceNumber = long.MaxValue) {
		var firstEvent = _eventStore.Load(aggregateRootId).FirstOrDefault();

		return firstEvent != null && firstEvent.GetGlobalSequenceNumber() <= maxGlobalSequenceNumber;
	}

	/// <summary>
	/// Gets the aggregate root of the specified type with the specified ID by hydrating it with events from the event store. The
	/// root will have events replayed until the specified <seealso cref="maxGlobalSequenceNumber"/> ceiling. If the root has
	/// no events (i.e. it doesn't exist yet), a newly initialized instance is returned.
	/// </summary>
	public AggregateRoot Get<TAggregateRoot>(string aggregateRootId, IUnitOfWork unitOfWork, long maxGlobalSequenceNumber = long.MaxValue, bool createIfNotExists = false) {
		var domainEventsForThisAggregate = _eventStore.Load(aggregateRootId);

		var eventsToApply = domainEventsForThisAggregate
			.Where(e => e.GetGlobalSequenceNumber() <= maxGlobalSequenceNumber)
			.Select(e => _domainEventSerializer.Deserialize(e));

		AggregateRoot aggregateRoot = null;

		foreach (var e in eventsToApply) {
			if (aggregateRoot == null) {
				if (!e.Meta.ContainsKey(DomainEvent.MetadataKeys.Owner)) {
					throw new InvalidOperationException($"Attempted to load aggregate root with ID {aggregateRootId} but the first event {e} did not contain metadata with the aggregate root type name!");
				}

				var aggregateRootTypeName = e.Meta[DomainEvent.MetadataKeys.Owner];
				var aggregateRootType = _domainTypeNameMapper.GetType(aggregateRootTypeName);
				aggregateRoot = CreateNewAggregateRootInstance(aggregateRootType, aggregateRootId, unitOfWork);
			}

			aggregateRoot.ApplyEvent(e, ReplayState.ReplayApply);
		}

		if (aggregateRoot == null) {
			if (!createIfNotExists) {
				throw new AggregateRootNotFoundException(typeof(TAggregateRoot), aggregateRootId);
			}

			aggregateRoot = CreateNewAggregateRootInstance(typeof(TAggregateRoot), aggregateRootId, unitOfWork);
		}

		return aggregateRoot;
	}

	/// <summary>
	/// Inheritors should override this to create instances of the specified type.
	/// </summary>
	/// <param name="aggregateRootType">The type of the aggregate root to create - already validated to ensure it is a sub-type of <see cref="AggregateRoot"/>.</param>
	/// <returns>An instance of <paramref name="aggregateRootType"/>.</returns>
	protected virtual AggregateRoot CreateAggregateRootInstance(Type aggregateRootType) => (AggregateRoot)Activator.CreateInstance(aggregateRootType);

	private AggregateRoot CreateNewAggregateRootInstance(Type aggregateRootType, string aggregateRootId, IUnitOfWork unitOfWork) {
		if (!typeof(AggregateRoot).IsAssignableFrom(aggregateRootType)) {
			throw new ArgumentException($"Cannot create new aggregate root with ID {aggregateRootId} of type {aggregateRootType} because it is not derived from AggregateRoot!");
		}

		var aggregateRoot = CreateAggregateRootInstance(aggregateRootType);

		aggregateRoot.Initialize(aggregateRootId);
		aggregateRoot.UnitOfWork = unitOfWork;

		return aggregateRoot;
	}

	//Brett
	public IEnumerable<DomainEvent> GetEvents(string aggregateRootId) { 
		var eventData = _eventStore.Load(aggregateRootId);
		var events = eventData.Select(ed => _domainEventSerializer.Deserialize(ed));
		return events;

	}
}