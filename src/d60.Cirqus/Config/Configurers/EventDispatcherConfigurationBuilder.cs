using System;
using System.Collections.Generic;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Events;
using d60.Cirqus.Serialization;
using d60.Cirqus.Views;
using d60.Cirqus.Views.ViewManagers;
using Microsoft.Extensions.DependencyInjection;

namespace d60.Cirqus.Config.Configurers;

/// <summary>
/// Configuration builder for attaching event processors to the command processor
/// </summary>
public class EventDispatcherConfigurationBuilder : NewConfigurationBuilder<IEventDispatcher>
{
	private readonly IServiceCollection _services;
	private bool _hasService;

	/// <summary>
	/// Constructs the builder
	/// </summary>
	public EventDispatcherConfigurationBuilder(IServiceCollection services) : base(new NewConfigurationContainer(services))
	{
		_services = services;
	}

	/// <summary>
	/// Registers a <see cref="Views.ViewManagerEventDispatcher"/> to manage the given views.
	/// Can be called multiple times in order to register
	/// multiple "pools" of views (each will be managed by a dedicated worker thread).
	/// </summary>
	public ViewManagerEventDispatcherConfigurationBuilder UseViewManagerEventDispatcher(
		params IViewManager[] viewManagers)
	{
		var viewManagerConfigurationContainer = new NewConfigurationContainer(_services);

		UseEventDispatcher(context =>
		{
			var scope = context.CreateScope();

			var eventDispatcher = new ViewManagerEventDispatcher(
				scope.ServiceProvider.GetService<IAggregateRootRepository>(),
				scope.ServiceProvider.GetService<IEventStore>(),
				scope.ServiceProvider.GetService<IDomainEventSerializer>(),
				scope.ServiceProvider.GetService<IDomainTypeNameMapper>(),
				viewManagers);

			var waitHandle = scope.ServiceProvider.GetService<ViewManagerWaitHandle>();
			if (waitHandle != null)
			{
				waitHandle.Register(eventDispatcher);
			}

			var configuration = scope.ServiceProvider.GetService<ViewManagerEventDispatcherConfiguration>();
			if (configuration?.MaxDomainEventsPerBatch > 0)
			{
				eventDispatcher.MaxDomainEventsPerBatch = configuration.MaxDomainEventsPerBatch;
			}

			var viewManagerProfiler = scope.ServiceProvider.GetService<IViewManagerProfiler>();
			if (viewManagerProfiler != null)
			{
				eventDispatcher.SetProfiler(viewManagerProfiler);
			}

			var contextItems = scope.ServiceProvider.GetService<IDictionary<string, object>>();
			if (contextItems != null)
			{
				eventDispatcher.SetContextItems(contextItems);
			}

			var autoDistributionViewManagerEventDispatcher = scope.ServiceProvider.GetService<AutoDistributionViewManagerEventDispatcher>();
			if (autoDistributionViewManagerEventDispatcher != null)
			{
				autoDistributionViewManagerEventDispatcher.Register(eventDispatcher);
				return autoDistributionViewManagerEventDispatcher;
			}

			return eventDispatcher;
		});

		return new ViewManagerEventDispatcherConfigurationBuilder(viewManagerConfigurationContainer);
	}

	/// <summary>
	/// Configures a dependent view manager event dispatcher that tacks on to any number of dependent views, catching up from the
	/// event store when the dependent views have caught up.
	/// </summary>
	public DependentViewManagerEventDispatcherSettings UseDependentViewManagerEventDispatcher(
		params IViewManager[] viewManagers)
	{
		var settings = new DependentViewManagerEventDispatcherSettings();

		UseEventDispatcher(context =>
		{
			var eventDispatcher = new DependentViewManagerEventDispatcher(settings.DependentViewManagers,
				viewManagers,
				context.GetService<IEventStore>(),
				context.GetService<IDomainEventSerializer>(),
				context.GetService<IAggregateRootRepository>(),
				context.GetService<IDomainTypeNameMapper>(),
				settings.ViewContextItems)
			{
				MaxDomainEventsPerBatch = settings.MaxDomainEventsPerBatch
			};

			if (settings.ViewManagerProfiler != null)
			{
				eventDispatcher.SetProfiler(settings.ViewManagerProfiler);
			}

			foreach (var waitHandle in settings.WaitHandles)
			{
				waitHandle.Register(eventDispatcher);
			}

			return eventDispatcher;
		});

		return settings;
	}

	/// <summary>
	/// Registers the given event dispatcher. Can be called multiple times.
	/// </summary>
	public void UseEventDispatcher(IEventDispatcher eventDispatcher)
	{
		UseEventDispatcher(context => eventDispatcher);
	}

	/// <summary>
	/// Registers the given <see cref="IEventDispatcher"/> func, using a <see cref="CompositeEventDispatcher"/> to compose with
	/// previously registered event dispatchers.
	/// </summary>
	public void UseEventDispatcher(Func<IServiceProvider, IEventDispatcher> factory)
	{
		if (_hasService)
		{
			Decorate((inner, context) =>
				new CompositeEventDispatcher(
					inner,
					factory.Invoke(context)));
		}
		else
		{
			Register(factory);
			_hasService = true;
		}
	}

	public IRegistrar2 Clone()
	{
		return new NewConfigurationContainer(_services);
	}
}