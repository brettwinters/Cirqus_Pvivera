﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using d60.Cirqus.Events;
using d60.Cirqus.Extensions;
using d60.Cirqus.Logging;
using d60.Cirqus.Views.ViewManagers;
using d60.Cirqus.Views.ViewManagers.Locators;
using MongoDB.Driver;

namespace d60.Cirqus.MongoDb.Views;

public class SpecialMongoDbViewManager<TViewInstance> 
	: SpecialAbstractViewManager<TViewInstance> where TViewInstance : class, IViewInstance, ISubscribeTo, new()
{
	const string CurrentPositionPropertyName = "LastTimestamp";
	const long DefaultPosition = -1;
	readonly SpecialViewDispatcherHelper<TViewInstance> _dispatcherHelper = new();
	readonly IMongoCollection<TViewInstance> _viewCollection;
	readonly IMongoCollection<PositionDoc> _positionCollection;
	readonly ViewLocator _viewLocator = ViewLocator.GetLocatorFor<TViewInstance>();
	readonly Logger _logger = CirqusLoggerFactory.Current.GetCurrentClassLogger();
	readonly string _currentPositionDocId;
	long _cachedPosition;
	volatile bool _purging;


	/// <summary>
	/// Gets the server from the collection string. You must include the name of the collection
	/// in the connection string
	/// </summary>
	public SpecialMongoDbViewManager(
		string mongoDbConnectionString)
		: this(GetDatabaseFromConnectionString(mongoDbConnectionString))
	{
	}

	/// <summary>
	/// Gets the server from the collection string. 
	/// </summary>
	public SpecialMongoDbViewManager(
		string mongoDbConnectionString,
		string collectionName,
		string positionCollectionName = null)
		: this(GetDatabaseFromConnectionString(mongoDbConnectionString), collectionName, positionCollectionName)
	{
	}


	public SpecialMongoDbViewManager(
		IMongoDatabase database,
		string collectionName,
		string positionCollectionName = null)
	{
		positionCollectionName ??= collectionName + "Position";

		_viewCollection = database.GetCollection<TViewInstance>(collectionName);

		_logger.Info("Create index in '{0}': '{1}'", collectionName, CurrentPositionPropertyName);
		
		_viewCollection.Indexes.CreateOne(
			new CreateIndexModel<TViewInstance>(
				Builders<TViewInstance>.IndexKeys.Ascending(i => i.LastGlobalSequenceNumber),
				new CreateIndexOptions { Name = CurrentPositionPropertyName } 
			)
		);

		_positionCollection = database.GetCollection<PositionDoc>(positionCollectionName);
		_currentPositionDocId = $"__{collectionName}__position__";
	}


	/// <summary>
	/// The Name of the collection is the name of the ViewManager instance type
	/// </summary>
	public SpecialMongoDbViewManager(IMongoDatabase database)
		: this(database, typeof(TViewInstance).Name)
	{
	}
	
	// ReSharper disable once ClassNeverInstantiated.Local
	class PositionDoc
	{
		public string Id { get; set; }
		public long CurrentPosition { get; set; }
	}


	/// <summary>
	/// Can be set to true in order to enable batch dispatch
	/// </summary>
	public bool BatchDispatchEnabled { get; set; }

	public override TViewInstance Load(
		string viewId)
	{
		return _viewCollection
			.Find(x => x.Id == viewId)
			.SingleOrDefault();
	}

	public override void Delete(string viewId)
	{
		throw new NotImplementedException(nameof(Delete));
	}

	public override string Id => $"{typeof(TViewInstance).GetPrettyName()}/{_viewCollection}";

	public override async Task<long> GetPosition(
		bool canGetFromCache = true)
	{
		// if(canGetFromCache && false)
		// {
		// 	return GetPositionFromMemory()
		// 	       ?? GetPositionFromPersistentCache()
		// 	       ?? GetPositionFromViewInstances()
		// 	       ?? GetDefaultPosition();
		// }

		// return GetPositionFromPersistentCache()
		//        ?? GetPositionFromViewInstances()
		//        ?? GetDefaultPosition();

		

		var position = GetPositionFromMemory()
		               ?? GetPositionFromPersistentCache() 
		               ?? GetPositionFromViewInstances()
		               ?? GetDefaultPosition();

		return position;


		#region
		
		static long GetDefaultPosition()
		{
			return DefaultPosition;
		}
		
		long? GetPositionFromMemory()
		{
			var value = Interlocked.Read(ref _cachedPosition);
		
			if (value != DefaultPosition)
			{
				return value;
			}
		
			return null;
		}

		long? GetPositionFromPersistentCache()
		{
			var currentPositionDocument = 
				_positionCollection
				.Find(x => x.Id == _currentPositionDocId)
				.SingleOrDefault();

			return currentPositionDocument?.CurrentPosition;
		}

		long? GetPositionFromViewInstances()
		{
			// with MongoDB, we cannot know for sure how many events we've successfully processed of those that
			// have sequence numbers between the MIN and MAX sequence numbers currently stored in our views
			// - therefore, to be safe, we need to pick the MIN as our starting point....

			var viewWithTheLowestTimestamp = _viewCollection
				.Find(_ => true)
				.Limit(1)
				.SortBy(x => x.LastGlobalSequenceNumber)
				.FirstOrDefault();

			return viewWithTheLowestTimestamp?.LastGlobalSequenceNumber;
		}
		
		#endregion
	}

	

	public override void Purge()
	{
		try
		{
			_purging = true;

			_logger.Info("Purging '{0}'", _viewCollection.CollectionNamespace.CollectionName);
			
			_viewCollection.DeleteMany(_ => true);

			UpdatePersistentCache(DefaultPosition);

			Interlocked.Exchange(ref _cachedPosition, DefaultPosition);
		}
		finally
		{
			_purging = false;
		}
	}

	void UpdatePersistentCache(
		long newPosition)
	{
		_logger.Debug("Updating persistent position cache to {0}", newPosition);
		
		_positionCollection
			.UpdateOne(
				x => x.Id == _currentPositionDocId, 
				Builders<PositionDoc>.Update.Set(p => p.CurrentPosition, newPosition),
				new UpdateOptions(){ IsUpsert = true }
			);

		Interlocked.Exchange(ref _cachedPosition, newPosition);
	}

	public override void Dispatch(
		IViewContext viewContext, 
		IEnumerable<DomainEvent> batch, 
		IViewManagerProfiler viewManagerProfiler)
	{
		if(_purging)
		{
			return;
		}

		var cachedViewInstances = new Dictionary<string, TViewInstance>();

		var eventList = batch.ToList();

		if(!eventList.Any())
		{
			return;
		}

		if(BatchDispatchEnabled)
		{
			var domainEventBatch = new DomainEventBatch(eventList);
			eventList.Clear();
			eventList.Add(domainEventBatch);
		}

		foreach(var e in eventList)
		{
			if(!ViewLocator.IsRelevant<TViewInstance>(e))
			{
				continue;
			}

			var stopwatch = Stopwatch.StartNew();

			var viewIds = _viewLocator.GetAffectedViewIds(viewContext, e);

			foreach(var viewId in viewIds)
			{
				var viewInstance = cachedViewInstances[viewId] = GetOrCreateViewInstance(viewId, cachedViewInstances);

				_dispatcherHelper.DispatchToView(viewContext, e, viewInstance);
			}

			viewManagerProfiler.RegisterTimeSpent(this, e, stopwatch.Elapsed);
		}

		FlushCacheToDatabase(cachedViewInstances);

		RaiseUpdatedEventFor(cachedViewInstances.Values);

		UpdatePersistentCache(eventList.Max(e => e.GetTimeStamp()));
	}

	void FlushCacheToDatabase(
		Dictionary<string, TViewInstance> cachedViewInstances)
	{
		if(!cachedViewInstances.Any())
		{
			return;
		}

		_logger.Debug(
			"Flushing {0} view instances to '{1}'", 
			cachedViewInstances.Values.Count, 
			_viewCollection.CollectionNamespace.CollectionName
		);

		foreach(var viewInstance in cachedViewInstances.Values)
		{
			_viewCollection.ReplaceOne(
				filter: x => x.Id == viewInstance.Id,
				replacement: viewInstance,
				options: new ReplaceOptions() { IsUpsert = true }
			);
		}
	}

	TViewInstance GetOrCreateViewInstance(
		string viewId, 
		Dictionary<string, TViewInstance> cachedViewInstances)
	{
		if(cachedViewInstances.TryGetValue(viewId, out var instanceToReturn))
		{
			return instanceToReturn;
		}

		instanceToReturn = _viewCollection.Find(x => x.Id == viewId).SingleOrDefault() 
		                   ?? _dispatcherHelper.CreateNewInstance(viewId);

		return instanceToReturn;
	}


	static IMongoDatabase GetDatabaseFromConnectionString(
		string mongoDbConnectionString)
	{
		var mongoUrl = new MongoUrl(mongoDbConnectionString);

		if(string.IsNullOrWhiteSpace(mongoUrl.DatabaseName))
		{
			throw new MongoException($"MongoDB URL does not contain a database name!: {mongoDbConnectionString}");
		}

		return new MongoClient(mongoUrl).GetDatabase(mongoUrl.DatabaseName);
	}
}