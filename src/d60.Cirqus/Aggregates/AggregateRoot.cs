using System;
using System.Reflection;
using d60.Cirqus.Events;
using d60.Cirqus.Exceptions;
using d60.Cirqus.Extensions;
using d60.Cirqus.Numbers;

namespace d60.Cirqus.Aggregates;

/// <summary>
/// This is the base class of aggregate roots. Derive from this in order to create an event-driven
/// domain model root object that is capable of emitting its own events, building its state as a
/// projection off of previously emitted events.
/// </summary>
public abstract class AggregateRoot
{
	internal const int InitialAggregateRootSequenceNumber = -1;

	/// <summary>
	/// Gets the ID of the aggregate root. This one should be automatically set on the aggregate root
	/// instance provided by Cirqus
	/// </summary>
	public string Id { get; internal set; }

	internal IUnitOfWork UnitOfWork { get; set; }

	internal Metadata CurrentCommandMetadata { get; set; }

	internal void Initialize(string id)
	{
		Id = id;
	}

	public void InvokeCreated()
	{
		Created();
	}

	internal protected virtual void EventEmitted(DomainEvent e) { }

	internal long CurrentSequenceNumber = InitialAggregateRootSequenceNumber;

	internal long GlobalSequenceNumberCutoff = long.MaxValue;

	internal ReplayState ReplayState = ReplayState.None;

	/// <summary>
	/// Method that is called when the aggregate root is first created, allowing you to emit that
	/// famous created event if you absolutely need it ;)
	/// </summary>
	protected virtual void Created() { }

	/// <summary>
	/// Gets whether this aggregate root has emitted any events. I.e. it will already be false after
	/// having used the <see cref="Created"/> method to emit one
	/// single "MyRootCreated" event.
	/// </summary>
	protected bool IsNew => CurrentSequenceNumber == InitialAggregateRootSequenceNumber;
	
	/// <summary>
	/// Emits the given domain event, adding the aggregate root's <see cref="Id"/> and a sequence
	/// number to its metadata, along with some type information
	/// </summary>
	protected internal void Emit<TAggregateRoot>(
		DomainEvent<TAggregateRoot> e)
		where TAggregateRoot : AggregateRoot
	{
		ArgumentNullException.ThrowIfNull(e);

		if (string.IsNullOrWhiteSpace(Id))
		{
			throw new InvalidOperationException($"Attempted to emit event {e} from aggregate root {GetType()}, " +
			                                    $"but it has not yet been assigned an ID!");
		}

		var eventType = e.GetType();

		var emitterInterface = typeof(IEmit<>).MakeGenericType(eventType);
		if (!emitterInterface.IsAssignableFrom(GetType()))
		{
			throw new InvalidOperationException(
				$"Attempted to emit event {e} but the aggregate root {GetType().Name} does not " +
				$"implement IEmit<{e.GetType().Name}>"
			);
		}

		if (UnitOfWork == null)
		{
			throw new InvalidOperationException($"Attempted to emit event {e}, but the aggreate root does " +
			                                    $"not have a unit of work!");
		}

		if (ReplayState != ReplayState.None)
		{
			throw new InvalidOperationException($"Attempted to emit event {e}, but the aggreate root's replay " +
			                                    $"state is {ReplayState}! - events can only be emitted when the root " +
			                                    $"is not applying events");
		}

		if (!typeof(TAggregateRoot).IsAssignableFrom(GetType()))
		{
			throw new InvalidOperationException(
				$"Attempted to emit event {e} which is owned by {typeof(TAggregateRoot)} from aggregate root of " +
				$"type {GetType()}"
			);
		}

		// Use 1 tick resolution which is 100 ns. This corresponds to 5 zeros
		var now = TimeService.GetUtcNow();
		var globalSequenceNumber = GlobalSequenceNumberService.GetNewGlobalSequenceNumber();
		var sequenceNumber = CurrentSequenceNumber + 1;

		e.Meta.Merge(CurrentCommandMetadata ?? new Metadata());
		e.Meta[DomainEvent.MetadataKeys.AggregateRootId] = Id;
		e.Meta[DomainEvent.MetadataKeys.TimeUtc] = now.ToString(DomainEvent.DateTimeFormat);
		e.Meta[DomainEvent.MetadataKeys.GlobalSequenceNumber] = globalSequenceNumber.ToString(Metadata.NumberCulture);
		e.Meta[DomainEvent.MetadataKeys.SequenceNumber] = sequenceNumber.ToString(Metadata.NumberCulture);

		e.Meta.TakeFromAttributes(eventType);
		e.Meta.TakeFromAttributes(GetType());

		try
		{
			ApplyEvent(e, ReplayState.EmitApply);
		}
		catch (Exception exception)
		{
			throw new ApplicationException(
				$"Could not apply event {e} to {this} - please check the inner exception, " +
				$"and/or make sure that the aggregate root type is PUBLIC", 
				exception
			);
		}

		UnitOfWork.AddEmittedEvent(this, e);
		EventEmitted(e);
	}
	
	internal void ApplyEvent(
		DomainEvent e, 
		ReplayState replayState)
	{
		// tried caching here with a (aggRootType, eventType) lookup in two levels of concurrent dictionaries....
		// didn't provide significant perf boost

		var aggregateRootType = GetType();
		var domainEventType = e.GetType();

		var applyMethod = aggregateRootType.GetMethod("Apply", new[] { domainEventType });

		if (applyMethod == null)
		{
			throw new ApplicationException(
				$"Could not find appropriate Apply method on {aggregateRootType} - expects a method with a " +
				$"public void Apply({domainEventType.FullName}) signature"
			);
		}
		
		if (CurrentSequenceNumber + 1 != e.GetSequenceNumber())
		{
			throw new ApplicationException(
				$"Tried to apply event with sequence number {e.GetSequenceNumber()} to aggregate root of " +
				$"type {aggregateRootType} with ID {Id} with current sequence number {CurrentSequenceNumber}. " +
				$"Expected an event with sequence number {CurrentSequenceNumber + 1}."
			);
		}

		var previousReplayState = ReplayState;

		try
		{
			ReplayState = replayState;

			if (ReplayState == ReplayState.ReplayApply)
			{
				GlobalSequenceNumberCutoff = e.GetGlobalSequenceNumber();
			}

			applyMethod.Invoke(this, new object[] { e });

			GlobalSequenceNumberCutoff = long.MaxValue;
			ReplayState = previousReplayState;
		}
		catch (TargetInvocationException tae)
		{
			throw new ApplicationException($"Error when applying event {e} to aggregate root with ID {Id}", tae);
		}

		CurrentSequenceNumber = e.GetSequenceNumber();
	}

	public override string ToString()
	{
		return $"{GetType().Name} ({Id})";
	}

	/// <summary>
	/// Creates another aggregate root with the specified <paramref name="aggregateRootId"/>. Will throw an exception if a root already exists with the specified ID.
	/// </summary>
	protected TAggregateRoot Create<TAggregateRoot>(
		string aggregateRootId) 
		where TAggregateRoot : AggregateRoot, new()
	{
		if (UnitOfWork == null)
		{
			throw new InvalidOperationException(
				$"Attempted to Load {typeof(TAggregateRoot)} with ID {aggregateRootId} from {GetType()}, but it has not been initialized with a unit of work! The unit of work must be attached to the aggregate root in order to cache hydrated aggregate roots within the current unit of work."
			);
		}

		if (ReplayState != ReplayState.None)
		{
			throw new InvalidOperationException($"Attempted to create new aggregate root of type {typeof(TAggregateRoot)} with ID {aggregateRootId}, but cannot create anything when replay state is {ReplayState}");
		}

		if (UnitOfWork.Exists(aggregateRootId, long.MaxValue))
		{
			throw new InvalidOperationException($"Cannot create aggregate root {typeof(TAggregateRoot)} with ID {aggregateRootId} because an instance with that ID already exists!");
		}

		var aggregateRoot = (TAggregateRoot)UnitOfWork.Get<TAggregateRoot>(aggregateRootId, long.MaxValue, createIfNotExists: true);

		aggregateRoot.CurrentCommandMetadata = CurrentCommandMetadata;

		aggregateRoot.InvokeCreated();

		return aggregateRoot;
	}

	/// <summary>
	/// Tries to load another aggregate root with the specified <paramref name="aggregateRootId"/>. Returns null if no root exists with that ID.
	/// Throws an <see cref="InvalidCastException"/> if a root was found, but its type was not compatible with the specified <typeparamref name="TAggregateRoot"/> type.
	/// </summary>
	protected TAggregateRoot TryLoad<TAggregateRoot>(
		string aggregateRootId) 
		where TAggregateRoot : AggregateRoot, new()
	{
		if (UnitOfWork == null)
		{
			throw new InvalidOperationException(
				$"Attempted to Load {typeof(TAggregateRoot)} with ID {aggregateRootId} from {GetType()}, but it has not been initialized with a unit of work! The unit of work must be attached to the aggregate root in order to cache hydrated aggregate roots within the current unit of work."
			);
		}

		var globalSequenceNumberCutoffToLookFor = ReplayState == ReplayState.ReplayApply
			? GlobalSequenceNumberCutoff
			: long.MaxValue;

		AggregateRoot aggregateRoot;
		try
		{
			aggregateRoot = UnitOfWork
				.Get<TAggregateRoot>(aggregateRootId, globalSequenceNumberCutoffToLookFor, createIfNotExists: false);
		}
		catch(AggregateRootNotFoundException)
		{
			return null;
		}

		try
		{
			aggregateRoot.CurrentCommandMetadata = CurrentCommandMetadata;
             
			return (TAggregateRoot) aggregateRoot;
		}
		catch (Exception exception)
		{
			throw new InvalidCastException($"Found aggregate root with ID {aggregateRoot} and type {aggregateRoot.GetType()}, but the type is not compatible with the desired {typeof(TAggregateRoot)} type!", exception);
		}
	}

	/// <summary>
	/// Loads another aggregate root with the specified <paramref name="aggregateRootId"/>. Throws an exception if an aggregate root with that ID could not be found.
	/// Also throws an exception if an aggregate root instance was found, but the type was not compatible with the desired <typeparamref name="TAggregateRoot"/>
	/// </summary>
	protected TAggregateRoot Load<TAggregateRoot>(string aggregateRootId) where TAggregateRoot : AggregateRoot, new()
	{
		if (UnitOfWork == null)
		{
			throw new InvalidOperationException(
				$"Attempted to Load {typeof(TAggregateRoot)} with ID {aggregateRootId} from {GetType()}, but it has not been initialized with a unit of work! The unit of work must be attached to the aggregate root in order to cache hydrated aggregate roots within the current unit of work."
			);
		}

		var globalSequenceNumberCutoffToLookFor = ReplayState == ReplayState.ReplayApply
			? GlobalSequenceNumberCutoff
			: long.MaxValue;

		var aggregateRoot = UnitOfWork.Get<TAggregateRoot>(aggregateRootId, globalSequenceNumberCutoffToLookFor, createIfNotExists: false);

		if (!(aggregateRoot is TAggregateRoot))
		{
			throw new InvalidOperationException($"Attempted to load aggregate root with ID {aggregateRootId} as a {typeof(TAggregateRoot)}, but the concrete type is {aggregateRootId.GetType()} which is not compatible");
		}

		aggregateRoot.CurrentCommandMetadata = CurrentCommandMetadata;

		return (TAggregateRoot)aggregateRoot;
	}
}