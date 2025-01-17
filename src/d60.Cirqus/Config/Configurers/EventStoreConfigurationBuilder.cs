using d60.Cirqus.Events;

namespace d60.Cirqus.Config.Configurers;

/// <summary>
/// Configuration builder to help with configuring the <see cref="IEventStore"/> implementation
/// </summary>
public class EventStoreConfigurationBuilder : NewConfigurationBuilder<IEventStore>
{
	/// <summary>
	/// Constructs the builder
	/// </summary>
	public EventStoreConfigurationBuilder(IRegistrar2 registrar) : base(registrar) { }

	/// <summary>
	/// Enables an in-memory event cache that caches the most recently used events. <see cref="maxCacheEntries"/> specifies
	/// the approximate number of events to be held in the cache
	/// </summary>
	public void EnableCaching(int maxCacheEntries)
	{
		Decorate((inner, context) => new CachingEventStoreDecorator(inner)
		{
			MaxCacheEntries = maxCacheEntries
		});
	}
}