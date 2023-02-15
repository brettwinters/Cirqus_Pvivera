using System;
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

public class MongoDbViewManager<TViewInstance> 
	: AbstractViewManager<TViewInstance> 
	where TViewInstance : class, IViewInstance, ISubscribeTo, new()
{
	private const string CurrentPositionPropertyName = "LastGlobalSequenceNumber";
	private const long DefaultPosition = -1;
	private readonly ViewDispatcherHelper<TViewInstance> _dispatcherHelper = new();
	private readonly IMongoCollection<TViewInstance> _viewCollection;
	private readonly IMongoCollection<PositionDoc> _positionCollection;
	private readonly ViewLocator _viewLocator = ViewLocator.GetLocatorFor<TViewInstance>();
	private readonly Logger _logger = CirqusLoggerFactory.Current.GetCurrentClassLogger();
	private readonly string _currentPositionDocId;
	private long _cachedPosition = -1;
	private volatile bool _purging;
	
	/// <summary>
	/// Gets the server from the collection string. You must include the name of the collection
	/// in the connection string
	/// </summary>
	public MongoDbViewManager(string mongoDbConnectionString) 
		: this(GetDatabaseFromConnectionString(mongoDbConnectionString))
	{
	}

	/// <summary>
	/// Gets the server from the collection string. 
	/// </summary>
	public MongoDbViewManager(
		string mongoDbConnectionString,
		string collectionName,
		string positionCollectionName = null)
		: this(GetDatabaseFromConnectionString(mongoDbConnectionString), collectionName, positionCollectionName)
	{
	}


	public MongoDbViewManager(
		IMongoDatabase database,
		string collectionName,
		string positionCollectionName = null)
	{
		positionCollectionName = positionCollectionName ?? collectionName + "Position";

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
	public MongoDbViewManager(IMongoDatabase database): this(database, typeof(TViewInstance).Name)
	{
	}

	private class PositionDoc
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
		throw new NotImplementedException("Use soft delete");
	}

	public override string Id => $"{typeof(TViewInstance).GetPrettyName()}/{_viewCollection}";

	public override async Task<long> GetPosition(
		bool canGetFromCache = true)
	{
		// Original author used "if(canGetFromCache && false)" but this would always
		// be false and therefore never used. Also found a bug in the Purge() method 
		// where the _cachedPosition was updated after UpdatePersistentCache leading to 
		// the issue of -1 / 0 once purged. I think...
		var cachePosition = canGetFromCache ? GetPositionFromMemory() : null;
		
		return cachePosition
		       ?? await GetPositionFromPersistentCacheAsync() 
		       ?? await GetPositionFromViewInstancesAsync() 
		       ?? DefaultPosition;

        #region
        
        long? GetPositionFromMemory()
		{
			var value = Interlocked.Read(ref _cachedPosition);
			if (value != DefaultPosition)
			{
				return value;
			}
			return null;
		}

        // I think this is used is because sometimes there is a mismatch between when
        // the instances are updated and when the position is fetched.
        async Task<long?> GetPositionFromPersistentCacheAsync()
        {
	        var currentPositionDocument = await _positionCollection
			        .Find(x => x.Id == _currentPositionDocId)
			        .SingleOrDefaultAsync();

	        return currentPositionDocument?.CurrentPosition;
        }

        // The only reason this is here is so that if the Persistent cache is 
        // deleted then it can still find it's position
        async Task<long?> GetPositionFromViewInstancesAsync()
		{
			var viewWithTheLowestGlobalSequenceNumber = await _viewCollection
				.Find(_ => true)
				.Limit(1)
				.SortBy(x => x.LastGlobalSequenceNumber)
				.FirstOrDefaultAsync();

			return viewWithTheLowestGlobalSequenceNumber?.LastGlobalSequenceNumber;
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
		}
		finally
		{
			_purging = false;
		}
	}

	private void UpdatePersistentCache(
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
		if (_purging)
		{
			return;
		}
		
		var eventList = batch.ToList();
		if (!eventList.Any())
		{
			return;
		}

		if(BatchDispatchEnabled)
		{
			var domainEventBatch = new DomainEventBatch(eventList);
			eventList.Clear();
			eventList.Add(domainEventBatch);
		}
		
		var cachedViewInstances = new Dictionary<string, TViewInstance>();

		foreach(var e in eventList)
		{
			if (!ViewLocator.IsRelevant<TViewInstance>(e))
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

		UpdatePersistentCache(eventList.Max(e => e.GetGlobalSequenceNumber()));
	}
	
	private TViewInstance GetOrCreateViewInstance(
		string viewId, 
		Dictionary<string, TViewInstance> cachedViewInstances)
	{
		if(cachedViewInstances.TryGetValue(viewId, out var instanceToReturn))
		{
			return instanceToReturn;
		}

		instanceToReturn = _viewCollection
			.Find(x => x.Id == viewId)
			.SingleOrDefault();

		return instanceToReturn ?? _dispatcherHelper.CreateNewInstance(viewId);
	}

	private void FlushCacheToDatabase(
		Dictionary<string, TViewInstance> cachedViewInstances)
	{
		if (!cachedViewInstances.Any())
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
	
	private static IMongoDatabase GetDatabaseFromConnectionString(
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