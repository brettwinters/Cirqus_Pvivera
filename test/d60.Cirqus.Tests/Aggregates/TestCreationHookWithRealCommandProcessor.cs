using System.Linq;
using System.Threading.Tasks;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Commands;
using d60.Cirqus.Events;
using d60.Cirqus.Extensions;
using d60.Cirqus.Serialization;
using d60.Cirqus.Testing.Internals;
using d60.Cirqus.Tests.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Aggregates
{
    [TestFixture]
    public class TestCreationHookWithRealCommandProcessor : FixtureBase
    {
        ICommandProcessor _commandProcessor;
        Task<InMemoryEventStore> _eventStoreTask;

        protected override void DoSetUp()
        {
            var services = new ServiceCollection();
            services.AddCirqus(c =>
                c.EventStore(e => _eventStoreTask = e.UseInMemoryEventStore()));

            var provider = services.BuildServiceProvider();

            _commandProcessor = provider.GetService<ICommandProcessor>();

            RegisterForDisposal(_commandProcessor);
        }

        [Test]
        public void InvokesCreatedHookWhenAggregateRootIsFirstCreated()
        {
            var domainEventSerializer = new JsonDomainEventSerializer();

            _commandProcessor.ProcessCommand(new MakeRootDoSomething("id1"));

            var expectedSequenceOfEvents = new[] { typeof(RootCreated), typeof(RootDidSomething) };
            var actualSequenceOfEvents = _eventStoreTask.Result.Select(e => domainEventSerializer.Deserialize(e).GetType()).ToArray();

            Assert.That(actualSequenceOfEvents, Is.EqualTo(expectedSequenceOfEvents));
        }

        [Test]
        public void InvokesCreatedHookWhenAggregateRootIsFirstCreatedAndNeverAgain()
        {
            // arrange
            var domainEventSerializer = new JsonDomainEventSerializer();

            // act
            _commandProcessor.ProcessCommand(new MakeRootDoSomething("rootid"));
            _commandProcessor.ProcessCommand(new MakeRootDoSomething("rootid"));
            _commandProcessor.ProcessCommand(new MakeRootDoSomething("rootid"));

            // assert
            var expectedSequenceOfEvents = new[]
            {
                typeof(RootCreated), 
                typeof(RootDidSomething), 
                typeof(RootDidSomething), 
                typeof(RootDidSomething)
            };
            var actualSequenceOfEvents = _eventStoreTask.Result.Select(e => domainEventSerializer.Deserialize(e).GetType()).ToArray();

            Assert.That(actualSequenceOfEvents, Is.EqualTo(expectedSequenceOfEvents));
        }

        public class MakeRootDoSomething : Command<Root>
        {
            public MakeRootDoSomething(string aggregateRootId)
                : base(aggregateRootId)
            {
            }

            public override void Execute(Root aggregateRoot)
            {
                aggregateRoot.DoSomething();
            }
        }

        public class Root : AggregateRoot,
            IEmit<RootCreated>,
            IEmit<RootDidSomething>
        {
            protected override void Created()
            {
                Emit(new RootCreated());
            }

            public void DoSomething()
            {
                Emit(new RootDidSomething());
            }

            public void Apply(RootCreated e)
            {

            }

            public void Apply(RootDidSomething e)
            {

            }
        }

        public class RootCreated : DomainEvent<Root> { }
        public class RootDidSomething : DomainEvent<Root> { }
    }
}