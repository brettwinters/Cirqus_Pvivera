﻿using System;
using System.Linq;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Events;
using d60.Cirqus.MongoDb.Events;
using d60.Cirqus.Tests.MongoDb;
using d60.Cirqus.Tests.Stubs;
using d60.Cirqus.Views;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Bugs
{
    [TestFixture, Description("Verify bug in being able to correctly load active aggregate roots from the current unit of work")]
    public class VerifyThatDuplicateSequenceNumbersAreNotAssigned : FixtureBase
    {
        [Test, Category(TestCategories.MongoDb)]
        public void NoProblemoWithRealSetup()
        {
            // arrange
            var eventStore = new MongoDbEventStore(MongoHelper.InitializeTestDatabase(), "events");

            //Brett
            var commandProcessor = CreateCommandProcessor(config => config
                .EventStore(e => e.Register<IEventStore>(c => eventStore))
                .EventDispatcher(e => e.Register<IEventDispatcher>(c => new ConsoleOutEventDispatcher(eventStore)))
            );

            //Orig
            //var commandProcessor = CommandProcessor.With()
            //    .EventStore(e => e.Register<IEventStore>(c => eventStore))
            //    .EventDispatcher(e => e.Register<IEventDispatcher>(c => 
            //        new ConsoleOutEventDispatcher(eventStore)))
            //    .Create();

            RegisterForDisposal(commandProcessor);

            // make sure all roots exist
            Console.WriteLine("Processing initial two commands");
            commandProcessor.ProcessCommand(new DoSomethingToABunchOfRootsCommand("id1"));
            commandProcessor.ProcessCommand(new DoSomethingToABunchOfRootsCommand("id2"));

            // act
            Console.WriteLine("\r\n\r\nActing...");
            commandProcessor.ProcessCommand(new DoSomethingToABunchOfRootsCommand("id1", "id2"));

            // assert
        }

        [Test]
        public void NoProblemoWithTestContext()
        {
            // arrange
            var context = RegisterForDisposal( CreateTestContext());

            try
            {
                // make sure all roots exist
                Console.WriteLine("Processing initial two commands");
                context.ProcessCommand(new DoSomethingToABunchOfRootsCommand("id1"));
                context.ProcessCommand(new DoSomethingToABunchOfRootsCommand("id2"));

                // act
                Console.WriteLine("\r\n\r\nActing...");
                context.ProcessCommand(new DoSomethingToABunchOfRootsCommand("id1", "id2"));
            }
            finally
            {
                context.History.WriteTo(Console.Out);
            }
            // assert
        }

        public class DoSomethingToABunchOfRootsCommand : d60.Cirqus.Commands.Command<SomeRoot>
        {
            public string[] AdditionalRootIds { get; set; }

            public DoSomethingToABunchOfRootsCommand(string aggregateRootId, params string[] additionalRootIds) 
                : base(aggregateRootId)
            {
                AdditionalRootIds = additionalRootIds;
            }

            public override void Execute(SomeRoot aggregateRoot)
            {
                aggregateRoot.DoSomething(AdditionalRootIds);

                aggregateRoot.DoSomethingElse();
            }
        }

        public class SomeRoot : AggregateRoot, IEmit<SomethingHappened>, IEmit<SomethingElseHappened>
        {
            public void DoSomething(params string[] idsToDoSomethingTo)
            {
                Emit(new SomethingHappened());

                idsToDoSomethingTo.ToList().ForEach(id => Load<SomeRoot>(id).DoSomething());
                
                idsToDoSomethingTo.ToList().ForEach(id => Load<SomeRoot>(id).DoSomethingElse());
            }

            public void DoSomethingElse()
            {
                Emit(new SomethingElseHappened());
            }

            public void Apply(SomethingHappened e)
            {
            }

            public void Apply(SomethingElseHappened e)
            {
            }
        }

        public class SomethingHappened : DomainEvent<SomeRoot>
        {
        }

        public class SomethingElseHappened : DomainEvent<SomeRoot>
        {
        }
    }

}