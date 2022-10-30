using d60.Cirqus.Aggregates;
using d60.Cirqus.Commands;
using d60.Cirqus.Events;
using d60.Cirqus.Extensions;
using d60.Cirqus.InMemory.Events;
using d60.Cirqus.Tests.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Views
{
    [TestFixture]
    public class TestDefaultEventDispatcherConfiguration
    {
	    #region

	    public class Root : AggregateRoot, IEmit<Event>
	    {
		    public void Boom()
		    {
			    Emit(new Event());
		    }

		    public void Apply(Event e)
		    {
		    }
	    }

	    public class Event : DomainEvent<Root> { }

	    private class Commando : Command<Root>
	    {
		    public Commando(string aggregateRootId) 
			    : base(aggregateRootId)
		    {
		    }

		    public override void Execute(Root aggregateRoot)
		    {
			    aggregateRoot.Boom();
		    }
	    }

	    #endregion
	    
        [Test]
        public void WorkWhenSpecifyingMinimalConfiguration()
        {
            var services = new ServiceCollection();
            services.AddCirqus(config =>
	            config.EventStore(e => e.Register<IEventStore>(c => new InMemoryEventStore()))
	        );
            
            var provider = services.BuildServiceProvider();

            using var commandProcessor = provider.GetRequiredService<ICommandProcessor>();
            commandProcessor.ProcessCommand(new Commando("id"));
        }

        [Test]
        public void WorkWhenSpecifyingAggregateRootRepository()
        {
            var services = new ServiceCollection();
            services.AddCirqus(config => config.EventStore(e => e.Register<IEventStore>(c => new InMemoryEventStore())));

            var provider = services.BuildServiceProvider();

            using var commandProcessor = provider.GetRequiredService<ICommandProcessor>();
            commandProcessor.ProcessCommand(new Commando("id"));
        }
    }
}