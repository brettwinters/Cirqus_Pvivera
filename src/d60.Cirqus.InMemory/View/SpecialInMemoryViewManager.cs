using System.Collections.Concurrent;
using System.Diagnostics;
using d60.Cirqus.Events;
using d60.Cirqus.Extensions;
using d60.Cirqus.Views.ViewManagers;
using d60.Cirqus.Views.ViewManagers.Locators;

namespace d60.Cirqus.InMemory.Views;

/// <summary>
/// In-memory catch-up view manager that can be used when your command processing happens on multiple machines
/// or if you want your in-mem views to be residing on another machine than the one that does the command processing.
/// </summary>
public class SpecialInMemoryViewManager<TViewInstance> : SpecialAbstractViewManager<TViewInstance> where TViewInstance : class, IViewInstance, ISubscribeTo, new()
{
	readonly ConcurrentDictionary<string, TViewInstance> _views;
	readonly SpecialViewDispatcherHelper<TViewInstance> _dispatcher = new();
	readonly ViewLocator _viewLocator = ViewLocator.GetLocatorFor<TViewInstance>();
	long _position = -1;
	
	public SpecialInMemoryViewManager() {
		_views = new ConcurrentDictionary<string, TViewInstance>();
	}

	public override TViewInstance? Load(
		string viewId)
	{
		return _views.TryGetValue(viewId, out var instance) ? instance : null;
	}

	public ConcurrentDictionary<string, TViewInstance> LoadAll() => _views;

	public override void Delete(
		string viewId)
	{
		Console.WriteLine("Deleting view {0}", viewId);
		_views.TryRemove(viewId, out _);
	}

	public override string Id => $"{typeof(TViewInstance).GetPrettyName()}/{GetHashCode()}";

	public bool BatchDispatchEnabled { get; set; } = false;

	public override Task<long> GetPosition(
		bool canGetFromCache = true)
	{
		return Task.FromResult(Interlocked.Read(ref _position));
	}

	public override void Dispatch(
		IViewContext viewContext,
		IEnumerable<DomainEvent> batch, 
		IViewManagerProfiler viewManagerProfiler)
	{
		var updatedViews = new HashSet<TViewInstance>();
		var eventList = batch.ToList();

		if (BatchDispatchEnabled)
		{
			var domainEventBatch = new DomainEventBatch(eventList);
			eventList.Clear();
			eventList.Add(domainEventBatch);
		}

		foreach (var e in eventList)
		{
			if (ViewLocator.IsRelevant<TViewInstance>(e))
			{
				var stopwatch = Stopwatch.StartNew();
				var affectedViewIds = _viewLocator.GetAffectedViewIds(viewContext, e);
				foreach (var viewId in affectedViewIds)
				{
					var viewInstance = _views.GetOrAdd(viewId, id => _dispatcher.CreateNewInstance(id));

					_dispatcher.DispatchToView(viewContext, e, viewInstance);

					updatedViews.Add(viewInstance);
				}

				viewManagerProfiler.RegisterTimeSpent(this, e, stopwatch.Elapsed);
			}
			
			//TODO Uncomment
			Interlocked.Exchange(ref _position, e.GetTimeStamp());
		}

		RaiseUpdatedEventFor(updatedViews);
	}

	public override void Purge()
	{
		_views.Clear();
		Interlocked.Exchange(ref _position, -1);
	}
}