﻿using d60.Cirqus.Commands;
using d60.Cirqus.Events;
using System.Collections.Generic;

namespace d60.Cirqus.Aggregates;

/// <summary>
/// Opens up <see cref="AggregateRoot"/> for getting sequence number information. Can be useful when working with caching
/// </summary>
public class AggregateRootInfo 
{
	public AggregateRoot Instance { get; private set; }

	public AggregateRootInfo(AggregateRoot instance)
	{
		Instance = instance;
	}

	/// <summary>
	/// Gets the ID of the instance (just for convenience)
	/// </summary>
	public string Id
	{
		get { return Instance.Id; }
	}

	/// <summary>
	/// Gets the current sequence number of the aggregate root - this number will be equal to the <see cref="DomainEvent.MetadataKeys.SequenceNumber"/> of
	/// the most recent event applied to the root, or <see cref="AggregateRoot.InitialAggregateRootSequenceNumber"/> if it has not yet had any events applied
	/// </summary>
	public long SequenceNumber
	{
		get { return Instance.CurrentSequenceNumber; }
	}



	/// <summary>
	/// Gets whether the instance is new, i.e. whether it has not yet had any events applied
	/// </summary>
	public bool IsNew
	{
		get { return Instance.CurrentSequenceNumber == AggregateRoot.InitialAggregateRootSequenceNumber; }
	}

	/// <summary>
	/// Applies the given event, performing any related lookups using the given unit of work
	/// </summary>
	public void Apply(DomainEvent domainEvent, IUnitOfWork unitOfWork)
	{
		var previousUnitOfWork = Instance.UnitOfWork;
		try
		{
			Instance.UnitOfWork = unitOfWork;
			Instance.ApplyEvent(domainEvent, ReplayState.ReplayApply);
		}
		finally
		{
			Instance.UnitOfWork = previousUnitOfWork;
		}
	}

	public void SetUnitOfWork(IUnitOfWork unitOfWork)
	{
		Instance.UnitOfWork = unitOfWork;
	}

        
	//public IUnitOfWork UnitOfWork => Instance.UnitOfWork;

	/// <summary>
	/// Brett : These are all the Committed Events which have been emitted for the for the Instance (AggregateRoot) and
	/// have been retrieved from the EventStore
	/// </summary>
	public IEnumerable<DomainEvent> GetEvents() {
		var uow = (Commands.RealUnitOfWork)Instance.UnitOfWork;
		return ((DefaultAggregateRootRepository)uow.Repository).GetEvents(Instance.Id);
	}

	//Brett    
	public void Emit<TAggregateRoot>(DomainEvent<TAggregateRoot> domainEvent) where TAggregateRoot : AggregateRoot {
		Instance.Emit<TAggregateRoot>(domainEvent);
	}

	/// <summary>
	/// Brett : These are all the Emitted Events for this Unit of Work. Multiple AggregateRoots may have emitted these events if a CompositeCommand is used which have 
	/// not yet been committed to the event store.
	/// </summary>
	public IEnumerable<DomainEvent> GetEmittedEvents() {
		return ((RealUnitOfWork)Instance.UnitOfWork).EmittedEvents;
	}



	// public IEnumerable<DomainEvent> Events => ((Commands.RealUnitOfWork)Instance.UnitOfWork).Repository.;
}