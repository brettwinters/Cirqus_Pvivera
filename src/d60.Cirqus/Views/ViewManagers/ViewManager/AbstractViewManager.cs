using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using d60.Cirqus.Events;
using d60.Cirqus.Views.ViewManagers.Locators;

namespace d60.Cirqus.Views.ViewManagers;

/// <summary>
/// Abstract base class with common functionality that has proven to be useful when you want
/// to implement <see cref="IViewManager{TViewInstance}"/>
/// </summary>
public abstract class AbstractViewManager<TViewInstance> 
	: IViewManager<TViewInstance> 
	where TViewInstance : class, IViewInstance, ISubscribeTo, new()
{
	public async Task WaitUntilProcessed(
		CommandProcessingResult result, 
		TimeSpan timeout)
	{
		if (!result.EventsWereEmitted)
		{
			return;
		}
		
		/*
		 * Lets say there are 3 view instances at these positions. 
		 * A = 5 -> 7, 8, 9, 10
		 * B = 3
		 * C = 6
		 *
		 * And lets say the event store for A is at 10 (i.e its ahead), then WaitForCatchup the
		 * GetCurrentPositions will return:
		 *
		 * GetPositionFromPositionCache -> 6* 
		 * GetPositionFromViewInstances -> 3 (lowest)
		 *
		 * So when the WaitUntilProcessed in AbstractViewManager gets the new position it
		 * returns 6
		 *
		 * The second time it runs, lets say B is at 4
		 *
		 * GetPositionFromPositionCache -> 10* 
		 * GetPositionFromViewInstances -> 3 (lowest)
		 *
		 * since it's already at 10, this view won't be updated!
		 *
		 * solutions:
		 * (1) Get the position only for the AR Id
		 * (2) 
		 * 
		 * The starting current position is from the last event stream ..7318 this could be more
		 * than the current event stream 
		 */

		var mostRecentGlobalSequenceNumber = result.GetNewPosition();
		
		var stopwatch = Stopwatch.StartNew();

		var currentPosition = await GetPosition(canGetFromCache: true);
		
		while (currentPosition < mostRecentGlobalSequenceNumber)
		{
			if (stopwatch.Elapsed > timeout)
			{
				throw new TimeoutException($"View for {typeof(TViewInstance)} did not catch up " +
				                           $"to {mostRecentGlobalSequenceNumber} within {timeout} timeout! " +
				                           $"The closest we got was {currentPosition}");
			}

			await Task.Delay(TimeSpan.FromMilliseconds(20));

			currentPosition = await GetPosition(canGetFromCache: false);
		}
	}

	public abstract string Id { get; }

	// public abstract Task<long> GetPositionAsync(Func<ViewLocator, Guid[]>? resolver = null);

	public abstract Task<long> GetPosition(bool canGetFromCache = true);

	public abstract void Dispatch(
		IViewContext viewContext, 
		IEnumerable<DomainEvent> batch, 
		IViewManagerProfiler viewManagerProfiler
	);

	public abstract void Purge();

	public abstract TViewInstance? Load(string viewId);

	public abstract void Delete(string viewId);

	public event ViewInstanceUpdatedHandler<TViewInstance> Updated;

	protected void RaiseUpdatedEventFor(
		IEnumerable<TViewInstance> viewInstances)
	{
		var updatedEvent = Updated;
		if (updatedEvent == null)
		{
			return;
		}

		foreach (var instance in viewInstances)
		{
			updatedEvent(instance);
		}
	}
}