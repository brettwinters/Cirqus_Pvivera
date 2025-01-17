﻿using System.Linq;
using System.Threading.Tasks;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Events;
using d60.Cirqus.InMemory.Events;
using d60.Cirqus.Tests.Extensions;
using d60.Cirqus.Tests.Stubs;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Commands
{
    [TestFixture]
    public class TestCommandTypeNames : FixtureBase
    {
        ICommandProcessor _commandProcessor;
        Task<InMemoryEventStore> _eventStore;

        protected override void DoSetUp()
        {
            _commandProcessor = CreateCommandProcessor(config => config
                .EventStore(e => _eventStore = e.UseInMemoryEventStore())
                .EventDispatcher(e => e.UseEventDispatcher(c => 
                    new ConsoleOutEventDispatcher(c.GetService<IEventStore>())))
                .Options(o =>
                {
                    o.AddCommandTypeNameToMetadata();
                }));

            RegisterForDisposal(_commandProcessor);
        }

        [Test]
        public async Task AddsCommandTypeNameToAllEmittedEventOnExecutableCommands()
        {
            var executableCommand = new Cwommand("bimse");

            //act
            _commandProcessor.ProcessCommand(executableCommand);

            //assert
            var events = (await _eventStore).ToList();

            const string expectedCommandTypeName = "d60.Cirqus.Tests.Commands.TestCommandTypeNames+Cwommand, d60.Cirqus.Tests";

            var commandTypeNamesPresent = events
                .Select(e =>
                {
                    if (!e.Meta.ContainsKey(DomainEvent.MetadataKeys.CommandTypeName))
                    {
                        throw new AssertionException($"Could not find the '{DomainEvent.MetadataKeys.CommandTypeName}' key in the command's metadata - had only {string.Join(", ", e.Meta.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                    }

                    return e.Meta[DomainEvent.MetadataKeys.CommandTypeName];
                })
                .Distinct()
                .ToList();

            Assert.That(commandTypeNamesPresent.Count, Is.EqualTo(1));

            var actualCommandTypeName = commandTypeNamesPresent.Single();

            Assert.That(actualCommandTypeName, Is.EqualTo(expectedCommandTypeName));
        }

        class Woot : AggregateRoot, IEmit<Ewent>
        {
            public void DoStuff()
            {
                Emit(new Ewent());
            }

            public void Apply(Ewent e)
            {
            }
        }

        class Ewent : DomainEvent<Woot> { }

        class Cwommand : d60.Cirqus.Commands.Command<Woot>
        {
            public Cwommand(string aggregateRootId)
                : base(aggregateRootId)
            {
            }

            public override void Execute(Woot aggregateRoot)
            {
                aggregateRoot.DoStuff();
            }
        }
    }
}