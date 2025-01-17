﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Config;
using d60.Cirqus.Events;
using d60.Cirqus.Extensions;
using d60.Cirqus.Logging;
using d60.Cirqus.Serialization;
using d60.Cirqus.Views.ViewManagers;
using Timer = System.Timers.Timer;

namespace d60.Cirqus.Views;

/// <summary>
/// Event dispatcher implementation that is capable of hosting any number of <see cref="IViewManager"/> implementations.
/// A dedicated thread will dispatch events to the views as they happen, periodically checking in the background whether
/// any of the views have got some catching up to do
/// </summary>
public class ViewManagerEventDispatcher : IDisposable, IAwaitableEventDispatcher
{
	private static Logger _logger;

	static ViewManagerEventDispatcher()
	{
		CirqusLoggerFactory.Changed += f => _logger = f.GetCurrentClassLogger();
	}

	private readonly BackoffHelper _backoffHelper = new BackoffHelper(new[]
	{
		TimeSpan.FromMilliseconds(100),
		TimeSpan.FromMilliseconds(100),
		TimeSpan.FromMilliseconds(100),
		TimeSpan.FromMilliseconds(100),
		TimeSpan.FromMilliseconds(100),
		TimeSpan.FromMilliseconds(100),
		TimeSpan.FromMilliseconds(100),
		TimeSpan.FromMilliseconds(100),
		TimeSpan.FromMilliseconds(100),
		TimeSpan.FromMilliseconds(100),
		TimeSpan.FromSeconds(1),
		TimeSpan.FromSeconds(1),
		TimeSpan.FromSeconds(1),
		TimeSpan.FromSeconds(1),
		TimeSpan.FromSeconds(1),
		TimeSpan.FromSeconds(5),
		TimeSpan.FromSeconds(5),
		TimeSpan.FromSeconds(5),
		TimeSpan.FromSeconds(5),
		TimeSpan.FromSeconds(5),
		TimeSpan.FromSeconds(5),
		TimeSpan.FromSeconds(30),
	});

	// Use a concurrent dict to store views so that it's safe to traverse in the background
	// even though new views may be added to it at runtime
	private readonly ConcurrentDictionary<IViewManager, object> _viewManagers = new();
	private readonly ConcurrentQueue<PieceOfWork> _work = new();
	private readonly IDictionary<string, object> _viewContextItems = new Dictionary<string, object>();
	private readonly IAggregateRootRepository _aggregateRootRepository;
	private readonly IEventStore _eventStore;
	private readonly IDomainEventSerializer _domainEventSerializer;
	private readonly IDomainTypeNameMapper _domainTypeNameMapper;
	private readonly Timer _automaticCatchUpTimer = new Timer();
	private readonly Thread _worker;
	private volatile bool _keepWorking = true;
	private int _maxDomainEventsPerBatch = 100;
	private long _sequenceNumberToCatchUpTo = -1;
	private bool _disposed;
	private IViewManagerProfiler _viewManagerProfiler = new NullProfiler();

	/// <summary>
	/// Constructs the event dispatcher
	/// </summary>
	public ViewManagerEventDispatcher(
		IAggregateRootRepository aggregateRootRepository,
		IEventStore eventStore,
		IDomainEventSerializer domainEventSerializer, 
		IDomainTypeNameMapper domainTypeNameMapper, 
		params IViewManager[] viewManagers)
	{
		if (viewManagers == null)
		{
			throw new ArgumentNullException(nameof(viewManagers));
		}

		_aggregateRootRepository = aggregateRootRepository ?? throw new ArgumentNullException(nameof(aggregateRootRepository));
		_eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
		_domainEventSerializer = domainEventSerializer ?? throw new ArgumentNullException(nameof(domainEventSerializer));
		_domainTypeNameMapper = domainTypeNameMapper ?? throw new ArgumentNullException(nameof(domainTypeNameMapper));

		viewManagers.ToList().ForEach(AddViewManager);

		_worker = new Thread(DoWork) { IsBackground = true };

		_automaticCatchUpTimer.Elapsed += delegate
		{
			_work.Enqueue(PieceOfWork.FullCatchUp(false));
		};

		AutomaticCatchUpInterval = TimeSpan.FromSeconds(1);
	}

	/// <summary>
	/// Sets the profiler that the event dispatcher should use to aggregate timing information
	/// </summary>
	public void SetProfiler(IViewManagerProfiler viewManagerProfiler)
	{
		if (viewManagerProfiler == null)
		{
			throw new ArgumentNullException(nameof(viewManagerProfiler));
		}

		_logger.Info("Setting profiler: {0}", viewManagerProfiler);
		_viewManagerProfiler = viewManagerProfiler;
	}

	public void SetContextItems(
		IDictionary<string, object> contextItems)
	{
		if (contextItems == null)
		{
			throw new ArgumentNullException(nameof(contextItems));
		}

		foreach (var kvp in contextItems)
		{
			_viewContextItems[kvp.Key] = kvp.Value;
		}
	}

	/// <summary>
	/// Adds the given view manager
	/// </summary>
	public void AddViewManager(IViewManager viewManager)
	{
		if (viewManager == null)
		{
			throw new ArgumentNullException(nameof(viewManager));
		}

		_viewManagers.AddOrUpdate(
			key: viewManager,
			addValueFactory: v =>
			{
				_logger.Debug("Added view manager: {0}", viewManager);
				return new object();
			},
			updateValueFactory: (_, existing) => existing
		);
	}

	/// <summary>
	/// Removed the given view manager
	/// </summary>
	public void RemoveViewManager(IViewManager viewManager)
	{
		if (viewManager == null)
		{
			throw new ArgumentNullException(nameof(viewManager));
		}

		object _;
		if (_viewManagers.TryRemove(viewManager, out _))
		{
			_logger.Debug("Removed view manager: {0}", viewManager);
		}
	}

	public void Initialize(
		bool purgeExistingViews = false)
	{
		_logger.Info("Initializing event dispatcher with view managers: {0}", string.Join(", ", _viewManagers));

		_logger.Debug("Initiating immediate full catchup");
		_work.Enqueue(PieceOfWork.FullCatchUp(purgeExistingViews));

		_logger.Debug("Starting automatic catchup timer with {0} ms interval", _automaticCatchUpTimer.Interval);
		_automaticCatchUpTimer.Start();
		_worker.Start();
	}
	
	/// <summary>
	/// Streams then all events from the event store to the view managers then waits for
	/// them to catch up. 
	/// </summary>
	public async Task FullCatchUpAsync(
		TimeSpan timeout)
	{
		var position = _eventStore.GetLastGlobalSequenceNumber();
		_logger.Debug("Initiating immediate full catchup to Global Sequence Number {0}", position);

		foreach (var batch in _eventStore.Stream().Batch(1000))
		{
			if (_disposed)
			{
				_logger.Warn("Event processing stopped during FullCatchUpAsync since we're disposing");
				return;
			}

			_logger.Debug("Process batch of {0} events", batch.Count());
			
			DirectDispatch(batch.Select(e => _domainEventSerializer.Deserialize(e)));
		}

		await WaitUntilProcessed(
			result: CommandProcessingResult.WithNewPosition(position),
			timeout: timeout
		);

		#region

		void DirectDispatch(
			IEnumerable<DomainEvent> events)
		{
			var context = new DefaultViewContext(
				aggregateRootRepository: _aggregateRootRepository, 
				domainTypeNameMapper: _domainTypeNameMapper, 
				eventBatch: events
			);

			foreach (var kvp in _viewContextItems)
			{
				context.Items[kvp.Key] = kvp.Value;
			}

			var eventList = events.ToList();

			foreach (var viewManager in _viewManagers.Keys)
			{
				_logger.Debug("Dispatching batch of {0} events to {1}", eventList.Count, viewManager);
			
				// Doing this where multiple streams exist could result in the view manager position
				// bring out of sync with the instance positions. But this is not a problem since
				// we guarantee that the next global sequence number MUST be higher
				viewManager.Dispatch(context, eventList, _viewManagerProfiler);
			}
		}

		#endregion
	}
	
	public void Dispatch(
		IEnumerable<DomainEvent> events)
	{
		if (events == null)
		{
			throw new ArgumentNullException(nameof(events));
		}

		var list = events.ToList();
		if (!list.Any())
		{
			return;
		}

		var maxSequenceNumberInBatch = list.Max(e => e.GetGlobalSequenceNumber());

		Interlocked.Exchange(ref _sequenceNumberToCatchUpTo, maxSequenceNumberInBatch);

		_work.Enqueue(PieceOfWork.JustCatchUp(list));
	}

	/// <summary>
	/// Waits until the view(s) with the specified view instance type have successfully processed event at least up until those that were emitted
	/// as part of processing the command that yielded the given result
	/// </summary>
	public async Task WaitUntilProcessed<TViewInstance>(
		CommandProcessingResult result,
		TimeSpan timeout) 
		where TViewInstance : IViewInstance
	{
		if (result == null)
		{
			throw new ArgumentNullException(nameof(result));
		}
		await Task.WhenAll(
			_viewManagers.Keys
				.OfType<IViewManager<TViewInstance>>()
				.Select(v => v.WaitUntilProcessed(result, timeout))
				.ToArray()
		);
	}

	/// <summary>
	/// Waits until all view with the specified view instance type have successfully
	/// processed event at least up until those that were emitted
	/// as part of processing the command that yielded the given result
	/// </summary>
	public async Task WaitUntilProcessed(
		CommandProcessingResult result, 
		TimeSpan timeout)
	{
		if (result == null)
		{
			throw new ArgumentNullException(nameof(result));
		}
		
        await Task.WhenAll(
			_viewManagers.Keys
				.Select(v => v.WaitUntilProcessed(result, timeout))
				.ToArray()
		);
	}

	/// <summary>
	/// Gets/sets how many events to include at most in a batch between saving the state of view instances
	/// </summary>
	public int MaxDomainEventsPerBatch
	{
		get => _maxDomainEventsPerBatch;
		set
		{
			if (value < 1)
			{
				throw new ArgumentException($"Attempted to set MAX items per batch to {value}! " +
				                            $"Please set it to at least 1...");
			}
			_maxDomainEventsPerBatch = value;
		}
	}

	/// <summary>
	/// Gets/sets the interval between automatically checking whether any views have got catching up to do
	/// </summary>
	public TimeSpan AutomaticCatchUpInterval
	{
		get => TimeSpan.FromMilliseconds(_automaticCatchUpTimer.Interval);
		set
		{
			if (value < TimeSpan.FromMilliseconds(1))
			{
				throw new ArgumentException($"Attempted to set automatic catch-up interval to {value}! " +
				                            $"Please set it to at least 1 millisecond");
			}
			_automaticCatchUpTimer.Interval = value.TotalMilliseconds;

			_logger.Debug("Automatic catchup timer interval was set to {0} ms", _automaticCatchUpTimer.Interval);
		}
	}

	private void DoWork()
	{
		_logger.Info("View manager background thread started");

		while (_keepWorking)
		{
			if (!_work.TryDequeue(out var pieceOfWork))
			{
				Thread.Sleep(20);
				continue;
			}

			var sequenceNumberToCatchUpTo = pieceOfWork.CatchUpAsFarAsPossible
				? long.MaxValue
				: Interlocked.Read(ref _sequenceNumberToCatchUpTo);

			try
			{
				CatchUpTo(
					sequenceNumberToCatchUpTo: sequenceNumberToCatchUpTo,
					eventStore: _eventStore, 
					cachedInformationAllowed: pieceOfWork.CanUseCachedInformation, 
					purgeViewsFirst: pieceOfWork.PurgeViewsFirst, 
					viewManagers: _viewManagers.Keys.ToArray(), 
					events: pieceOfWork.Events
				);
				_backoffHelper.Reset();
			}
			catch (Exception exception)
			{
				var timeToWait = _backoffHelper.GetTimeToWait();

				if (sequenceNumberToCatchUpTo == long.MaxValue)
				{
					_logger.Warn(exception, "Could not catch up - waiting {0}", timeToWait);
				}
				else
				{
					_logger.Warn(exception, "Could not catch up to {0} - waiting {1}", sequenceNumberToCatchUpTo, timeToWait);
				}

				Thread.Sleep(timeToWait);
			}
		}

		_logger.Info("View manager background thread stopped!");
	}


	private void CatchUpTo(
		long sequenceNumberToCatchUpTo, 
		IEventStore eventStore, 
		bool cachedInformationAllowed, 
		bool purgeViewsFirst, 
		IViewManager[] viewManagers, 
		List<DomainEvent> events)
	{
		// bail out now if there isn't any actual work to do
		if (!viewManagers.Any())
		{
			return;
		}

		if (purgeViewsFirst)
		{
			foreach (var viewManager in viewManagers)
			{
				viewManager.Purge();
			}
		}

		// get the lowest position among all the view managers
		var positions = viewManagers
			.Select(viewManager => new Pos(viewManager, viewManager.GetPosition(canGetFromCache: cachedInformationAllowed).Result))
			.ToDictionary(a => a.ViewManager);

		var lowestSequenceNumberSuccessfullyProcessed = positions.Min(a => a.Value.Position);

		// if we've already been there, don't do anything
		if (lowestSequenceNumberSuccessfullyProcessed >= sequenceNumberToCatchUpTo)
		{
			return;
		}
		
		// First, if there are events (from the Command processing) we send them directly
		// to the ViewManager.
		if (events.Any() && events.First().GetGlobalSequenceNumber() > lowestSequenceNumberSuccessfullyProcessed)
		{
			DispatchBatchToViewManagers(viewManagers, events, positions);

			lowestSequenceNumberSuccessfullyProcessed = events.Last().GetGlobalSequenceNumber();
			
			// if we've done enough, quit now
			if (lowestSequenceNumberSuccessfullyProcessed >= sequenceNumberToCatchUpTo)
			{
				return;
			}
		}

		// Regular dispatch: We must replay - start from here:
        var sequenceNumberToReplayFrom = lowestSequenceNumberSuccessfullyProcessed + 1;
		foreach (var batch in eventStore.Stream(sequenceNumberToReplayFrom).Batch(MaxDomainEventsPerBatch))
		{
			DispatchBatchToViewManagers(viewManagers, batch, positions);
		}
	}

	private class Pos
	{
		public Pos(IViewManager viewManager, long position)
		{
			ViewManager = viewManager;
			Position = position;
		}

		public IViewManager ViewManager { get; private set; }
		
		public long Position { get; private set; }
	}

	private void DispatchBatchToViewManagers(
		IEnumerable<IViewManager> viewManagers, 
		IEnumerable<EventData> batch, 
		Dictionary<IViewManager, Pos> positions)
	{
		var eventList = batch
			.Select(e => _domainEventSerializer.Deserialize(e))
			.ToList();

		DispatchBatchToViewManagers(viewManagers, eventList, positions);
	}

	private void DispatchBatchToViewManagers(
		IEnumerable<IViewManager> viewManagers,
		List<DomainEvent> eventList, 
		Dictionary<IViewManager, Pos> positions)
	{
		foreach (var viewManager in viewManagers)
		{
			var thisParticularPosition = positions[viewManager].Position;
			
			if (thisParticularPosition >= eventList.Max(e => e.GetGlobalSequenceNumber()))
			{
				continue;
			}

			var context = new DefaultViewContext(
				aggregateRootRepository: _aggregateRootRepository, 
				domainTypeNameMapper: _domainTypeNameMapper, 
				eventBatch: eventList
			);
			_viewContextItems.InsertInto(context.Items);

			_logger.Debug("Dispatching batch of {0} events to {1}", eventList.Count, viewManager);

			viewManager.Dispatch(context, eventList, _viewManagerProfiler);
		}
	}

	private class PieceOfWork
	{
		private PieceOfWork()
		{
			Events = new List<DomainEvent>();
		}

		public static PieceOfWork FullCatchUp(
			List<DomainEvent> recentlyEmittedEvents)
		{
			return new PieceOfWork
			{
				CatchUpAsFarAsPossible = true,
				CanUseCachedInformation = false,
				PurgeViewsFirst = false,
				Events = recentlyEmittedEvents
			};
		}

		public static PieceOfWork FullCatchUp(
			bool purgeExistingViews)
		{
			return new PieceOfWork
			{
				CatchUpAsFarAsPossible = true,
				CanUseCachedInformation = false,
				PurgeViewsFirst = purgeExistingViews
			};
		}

		public static PieceOfWork JustCatchUp(
			List<DomainEvent> recentlyEmittedEvents)
		{
			return new PieceOfWork
			{
				CatchUpAsFarAsPossible = false,
				CanUseCachedInformation = true,
				PurgeViewsFirst = false,
				Events = recentlyEmittedEvents
			};
		}

		public List<DomainEvent> Events { get; private init; }

		public bool CatchUpAsFarAsPossible { get; private init; }

		public bool CanUseCachedInformation { get; private init; }

		public bool PurgeViewsFirst { get; private init; }

		public override string ToString()
		{
			return $"Catch up {(CatchUpAsFarAsPossible ? "to MAX" : "to latest")} (allow cache: {CanUseCachedInformation}, purge: {PurgeViewsFirst})";
		}
	}
	
	~ViewManagerEventDispatcher()
	{
		Dispose(false);
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Stops the background timer and shuts down the worker thread
	/// </summary>
	/// <param name="disposing"></param>
	protected virtual void Dispose(
		bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			_keepWorking = false;

			try
			{
				_automaticCatchUpTimer.Stop();
				_automaticCatchUpTimer.Dispose();
			}
			catch
			{
			}

			try
			{
				if (!_worker.Join(TimeSpan.FromSeconds(4)))
				{
					_logger.Warn("Worker thread did not stop within 4 second timeout!");
				}
			}
			catch
			{
			}
		}

		_disposed = true;
	}

	public IEnumerable<IViewManager> GetViewManagers()
	{
		return _viewManagers.Keys;
	}
}