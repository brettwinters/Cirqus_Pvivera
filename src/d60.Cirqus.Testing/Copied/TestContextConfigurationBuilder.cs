using System;
using System.Linq;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Commands;
using d60.Cirqus.Config;
using d60.Cirqus.Config.Configurers;
using d60.Cirqus.Events;
using d60.Cirqus.InMemory.Events;
using d60.Cirqus.Logging;
using d60.Cirqus.Serialization;
using d60.Cirqus.Views;
using Microsoft.Extensions.DependencyInjection;

namespace d60.Cirqus.Testing;

public class TestContextConfigurationBuilder : IOptionalConfiguration<TestContext>
{
	private readonly IServiceCollection _services;
	static Logger _logger;

	readonly ConfigurationContainer _container = new ConfigurationContainer();

	private readonly NewConfigurationContainer _newContainer;

	public TestContextConfigurationBuilder(IServiceCollection services)
	{
		_services = services;
		_newContainer = new NewConfigurationContainer(services);

		FillInDefaults();

		CirqusLoggerFactory.Changed += f => _logger = f.GetCurrentClassLogger();
	}

	public IOptionalConfiguration<TestContext> AggregateRootRepository(
		Action<AggregateRootRepositoryConfigurationBuilder> configure)
	{
		configure(new AggregateRootRepositoryConfigurationBuilder(_newContainer));
		return this;
	}

	public IOptionalConfiguration<TestContext> EventDispatcher(Action<EventDispatcherConfigurationBuilder> configure)
	{
		configure(new EventDispatcherConfigurationBuilder(_services));
		return this;
	}

	public IOptionalConfiguration<TestContext> Options(Action<OptionsConfigurationBuilder> configure)
	{
		configure(new OptionsConfigurationBuilder(_newContainer));
		return this;
	}

	public TestContext Create()
	{
		FillInDefaults();

		var resolutionContext = _container.CreateContext();

		var eventStore = resolutionContext.Get<InMemoryEventStore>();
		var aggregateRootRepository = resolutionContext.Get<IAggregateRootRepository>();
		var eventDispatcher = resolutionContext.Get<IEventDispatcher>();
		var serializer = resolutionContext.Get<IDomainEventSerializer>();
		var commandMapper = resolutionContext.Get<ICommandMapper>();
		var domainTypeMapper = resolutionContext.Get<IDomainTypeNameMapper>();

		var testContext = new TestContext(eventStore, aggregateRootRepository, eventDispatcher, serializer, commandMapper, domainTypeMapper);

		testContext.Disposed += resolutionContext.Dispose;

		resolutionContext
			.GetAll<Action<TestContext>>().ToList()
			.ForEach(action => action(testContext)); 

		testContext.Initialize();

		return testContext;
	}

	void FillInDefaults()
	{
		_services.AddScoped(context =>
		{
			var eventStore = context.GetService<InMemoryEventStore>();
			var aggregateRootRepository = context.GetService<IAggregateRootRepository>();
			var eventDispatcher = context.GetService<IEventDispatcher>();
			var serializer = context.GetService<IDomainEventSerializer>();
			var commandMapper = context.GetService<ICommandMapper>();
			var domainTypeMapper = context.GetService<IDomainTypeNameMapper>();

			var testContext = new TestContext(eventStore, aggregateRootRepository, eventDispatcher, serializer, commandMapper, domainTypeMapper);

			context
				.GetServices<Action<TestContext>>().ToList()
				.ForEach(action => action(testContext));

			testContext.Initialize();

			return testContext;
		});

		var memoryEventStore = new InMemoryEventStore();

		_services.AddScoped(_ => memoryEventStore);
		_services.AddScoped<IEventStore>(x => memoryEventStore);

		_services.AddScoped<IEventDispatcher>(x =>
			new ViewManagerEventDispatcher(
				x.GetService<IAggregateRootRepository>(),
				x.GetService<InMemoryEventStore>(),
				x.GetService<IDomainEventSerializer>(),
				x.GetService<IDomainTypeNameMapper>()));

		_services.AddScoped<IAggregateRootRepository>(context =>
			new DefaultAggregateRootRepository(
				context.GetService<InMemoryEventStore>(),
				context.GetService<IDomainEventSerializer>(),
				context.GetService<IDomainTypeNameMapper>()));

		_services.AddScoped<IDomainEventSerializer>(_ => new JsonDomainEventSerializer("<events>"));
		_services.AddScoped<ICommandMapper>(_ => new DefaultCommandMapper());
		_services.AddScoped<IDomainTypeNameMapper>(_ => new DefaultDomainTypeNameMapper());
	}
}

public static class ServiceCollectionExtensions
{
	public static void AddTestContext(
		this IServiceCollection services,
		Action<IOptionalConfiguration<TestContext>> configure = null)
	{
		if (configure != null)
		{
			configure(new TestContextConfigurationBuilder(services));
		}
		else
		{
			new TestContextConfigurationBuilder(services);
		}
	}
}