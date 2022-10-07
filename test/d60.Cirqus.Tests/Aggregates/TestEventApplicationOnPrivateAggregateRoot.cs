using d60.Cirqus.Aggregates;
using d60.Cirqus.Events;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Aggregates
{
    [TestFixture]
    [Description("Relying on dynamic dispatch in order to dispatch events to aggregate roots is silly because of the limitations")]
    public class TestEventApplicationOnPrivateAggregateRoot : FixtureBase
    {
        [Test]
        public void CanDoItPrivately()
        {
            using (var context = CreateTestContext())
            {
                context.ProcessCommand(new Commando("hej"));
                context.ProcessCommand(new Commando("hej"));
                context.ProcessCommand(new Commando("hej"));
            }
        }

        class Root : AggregateRoot, IEmit<Event>
        {
            public void DoItPrivately()
            {
                Emit(new Event());
            }

            public void Apply(Event e)
            {
                
            }
        }

        class Event : DomainEvent<Root> { }

        class Commando : d60.Cirqus.Commands.Command<Root>
        {
            public Commando(string aggregateRootId) : base(aggregateRootId)
            {
            }

            public override void Execute(Root aggregateRoot)
            {
                aggregateRoot.DoItPrivately();
            }
        }
    }
}