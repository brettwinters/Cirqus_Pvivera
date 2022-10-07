using System;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Events;
using d60.Cirqus.Serialization;
using d60.Cirqus.Snapshotting;
using Microsoft.Extensions.DependencyInjection;

namespace d60.Cirqus.Config.Configurers;

public class AggregateRootRepositoryConfigurationBuilder : NewConfigurationBuilder<IAggregateRootRepository>
{
	public AggregateRootRepositoryConfigurationBuilder(IRegistrar2 registrar) : base(registrar) { }

	/// <summary>
	/// Registers a <see cref="FactoryBasedAggregateRootRepository"/> as the <see cref="IAggregateRootRepository"/> implementation. 
	/// </summary>
	public void UseFactoryMethod(Func<Type, AggregateRoot> factoryMethod)
	{
		Register(context =>
			new FactoryBasedAggregateRootRepository(
				context.GetService<IEventStore>(),
				context.GetService<IDomainEventSerializer>(),
				context.GetService<IDomainTypeNameMapper>(),
				factoryMethod));
	}

	/// <summary>
	/// Registers a <see cref="IAggregateRootRepository"/> as a decorator in front of the existing <see cref="InMemorySnapshotCache"/>
	/// which will use an <see cref="CachingAggregateRootRepositoryDecorator"/> to cache aggregate roots.
	/// </summary>
	public void EnableInMemorySnapshotCaching(int approximateMaxNumberOfCacheEntries)
	{
		Decorate((inner, context) =>
			new CachingAggregateRootRepositoryDecorator(
				inner,
				new InMemorySnapshotCache
				{
					ApproximateMaxNumberOfCacheEntries = approximateMaxNumberOfCacheEntries
				},
				context.GetService<IEventStore>(),
				context.GetService<IDomainEventSerializer>()));
	}
}