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
public class SpecialViewManagerEventDispatcher : IDisposable, IAwaitableEventDispatcher
{
	static Logger _logger;

	static SpecialViewManagerEventDispatcher()
	{
		CirqusLoggerFactory.Changed += f => _logger = f.GetCurrentClassLogger();
	}

	readonly BackoffHelper _backoffHelper = new BackoffHelper(new[]
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

	/// <summary>
	/// Use a concurrent queue to store views so that it's safe to traverse in the background even though new views may 
	/// be added to it at runtime
	/// </summary>
	readonly ConcurrentDictionary<IViewManager, object> _viewManagers = new ConcurrentDictionary<IViewManager, object>();
	readonly ConcurrentQueue<PieceOfWork> _work = new ConcurrentQueue<PieceOfWork>();
	readonly IDictionary<string, object> _viewContextItems = new Dictionary<string, object>();
	readonly IAggregateRootRepository _aggregateRootRepository;
	readonly IEventStore _eventStore;
	readonly IDomainEventSerializer _domainEventSerializer;
	readonly IDomainTypeNameMapper _domainTypeNameMapper;

	readonly Timer _automaticCatchUpTimer = new Timer();
	readonly Thread _worker;

	volatile bool _keepWorking = true;

	int _maxDomainEventsPerBatch = 100;

	long _sequenceNumberToCatchUpTo = -1;

	IViewManagerProfiler _viewManagerProfiler = new NullProfiler();

	/// <summary>
	/// Constructs the event dispatcher
	/// </summary>
	public SpecialViewManagerEventDispatcher(
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
	public void AddViewManager(
		IViewManager viewManager)
	{
		if (viewManager == null)
		{
			throw new ArgumentNullException(nameof(viewManager));
		}

		_viewManagers.AddOrUpdate(
			key: viewManager,
			addValueFactory: _ =>
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
	public void RemoveViewManager(
		IViewManager viewManager)
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
		
		var maxSequenceNumberInBatch = list.Max(e => e.GetTimeStamp());

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
	/// Waits until all view with the specified view instance type have successfully processed event at least up until those that were emitted
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

	void DoWork()
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
					timeStampToCatchUpTo: sequenceNumberToCatchUpTo,
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

	void CatchUpTo(
		long timeStampToCatchUpTo, 
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

		// get the lowest position among all the view managers. The position used by our view
		// managers is the timestamp
		var positions = viewManagers
			.Select(viewManager => new Pos(viewManager, viewManager.GetPosition(canGetFromCache: cachedInformationAllowed).Result))
			.ToDictionary(a => a.ViewManager);

		var earliestTimeStampSuccessfullyProcessed = positions.Min(a => a.Value.Position);

		// if we've already been there, don't do anything
		if (earliestTimeStampSuccessfullyProcessed >= timeStampToCatchUpTo)
		{
			return;
		}

		// If we have events from this catchup (such as when a command was processed) then dispatch them
		// to the view managers.
		// It's possible that our events are behind the timestamp on the view managers. In this case we
		// ignore the events and continue on to the next step which streams
		if (events.Any() && events.First().GetTimeStamp() > earliestTimeStampSuccessfullyProcessed)
		{
			DispatchBatchToViewManagers(viewManagers, events, positions);
			
			earliestTimeStampSuccessfullyProcessed = events.Last().GetTimeStamp();
		
			// if we've done enough, quit now
			if (earliestTimeStampSuccessfullyProcessed >= timeStampToCatchUpTo)
			{
				return;
			}
		}

		// Regular dispatch: We must replay - start from here:
		var timeStampToReplayFrom = earliestTimeStampSuccessfullyProcessed + 1;
		
		//TODO stream from Timestamp
		// It's expensive to stream everything, but I'm not sure of the direction. Either we could (1) replace
		// the global sequence number with the timestamp, (2) Put the timestamp in the event outside the meta
		// or write a Stream(Datetime/long timestamp) method. But for the last idea we'd need to convert to 
		// DateTime or just use the timestamp natively. I'm not even sure if the timestamp is reliable - does 
		// the server produce an accurate time?
		var eventsToStream = eventStore.Stream().Where(e => e.GetTimeStamp() > timeStampToReplayFrom).ToList();
		foreach (var batch in eventsToStream.Batch(MaxDomainEventsPerBatch))
		{
			DispatchBatchToViewManagers(viewManagers, batch, positions);
		}
	}

	class Pos
	{
		public Pos(
			IViewManager viewManager, 
			long position)
		{
			ViewManager = viewManager;
			Position = position;
		}

		public IViewManager ViewManager { get; private set; }
		public long Position { get; private set; }
	}

	void DispatchBatchToViewManagers(
		IEnumerable<IViewManager> viewManagers, 
		IEnumerable<EventData> batch, 
		Dictionary<IViewManager, Pos> positions)
	{
		var eventList = batch
			.Select(e => _domainEventSerializer.Deserialize(e))
			.ToList();

		DispatchBatchToViewManagers(viewManagers, eventList, positions);
	}

	void DispatchBatchToViewManagers(
		IEnumerable<IViewManager> viewManagers,
		List<DomainEvent> eventList, 
		Dictionary<IViewManager, Pos> positions)
	{
		foreach (var viewManager in viewManagers)
		{
			var thisParticularPosition = positions[viewManager].Position;
			
			if (thisParticularPosition >= eventList.Max(e => e.GetTimeStamp()))
			{
				continue;
			}

			var context = new DefaultViewContext(_aggregateRootRepository, _domainTypeNameMapper, eventList);
			_viewContextItems.InsertInto(context.Items);

			_logger.Debug("Dispatching batch of {0} events to {1}", eventList.Count, viewManager);

			viewManager.Dispatch(context, eventList, _viewManagerProfiler);
		}
	}

	class PieceOfWork
	{
		PieceOfWork()
		{
			Events = new List<DomainEvent>();
		}

		public static PieceOfWork FullCatchUp(bool purgeExistingViews)
		{
			return new PieceOfWork
			{
				CatchUpAsFarAsPossible = true,
				CanUseCachedInformation = false,
				PurgeViewsFirst = purgeExistingViews
			};
		}

		public static PieceOfWork JustCatchUp(List<DomainEvent> recentlyEmittedEvents)
		{
			return new PieceOfWork
			{
				CatchUpAsFarAsPossible = false,
				CanUseCachedInformation = true,
				PurgeViewsFirst = false,
				Events = recentlyEmittedEvents
			};
		}

		public List<DomainEvent> Events { get; private set; }

		public bool CatchUpAsFarAsPossible { get; private set; }

		public bool CanUseCachedInformation { get; private set; }

		public bool PurgeViewsFirst { get; private set; }

		public override string ToString()
		{
			return $"Catch up {(CatchUpAsFarAsPossible ? "to MAX" : "to latest")} (allow cache: {CanUseCachedInformation}, purge: {PurgeViewsFirst})";
		}
	}

	private bool _disposed;

	~SpecialViewManagerEventDispatcher()
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
	protected virtual void Dispose(bool disposing)
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
				//swallow
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
				//swallow
			}
		}

		_disposed = true;
	}

	public IEnumerable<IViewManager> GetViewManagers()
	{
		return _viewManagers.Keys;
	}
}