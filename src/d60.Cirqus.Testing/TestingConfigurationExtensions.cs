using System.Collections.Generic;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Config;
using d60.Cirqus.Config.Configurers;
using d60.Cirqus.Events;
using d60.Cirqus.Serialization;
using d60.Cirqus.Views;
using Microsoft.Extensions.DependencyInjection;

namespace d60.Cirqus.Testing;

public static class TestingConfigurationExtensions
{
	public static SynchronousViewManagerEventDispatcherConfigurationBuilder UseSynchronousViewManagerEventDispatcher(
		this EventDispatcherConfigurationBuilder builder, params IViewManager[] viewManagers)
	{
		var viewManagerConfigurationContainer = builder.Clone();

		builder.UseEventDispatcher(context =>
		{
			var scope = context.CreateScope();

			var eventDispatcher = new SynchronousViewManagerEventDispatcher(
				context.GetService<IEventStore>(),
				context.GetService<IAggregateRootRepository>(),
				context.GetService<IDomainEventSerializer>(),
				context.GetService<IDomainTypeNameMapper>(),
				viewManagers);

			var contextItems = scope.ServiceProvider.GetService<IDictionary<string, object>>();
			if (contextItems != null)
			{
				eventDispatcher.SetContextItems(contextItems);
			}

			return eventDispatcher;
		});

		return new SynchronousViewManagerEventDispatcherConfigurationBuilder(viewManagerConfigurationContainer);
	}
}