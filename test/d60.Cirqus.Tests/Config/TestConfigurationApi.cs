using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Commands;
using d60.Cirqus.Config.Configurers;
using d60.Cirqus.Events;
using d60.Cirqus.Extensions;
using d60.Cirqus.Logging;
using d60.Cirqus.MongoDb.Config;
using d60.Cirqus.MongoDb.Views;
using d60.Cirqus.Tests.MongoDb;
using d60.Cirqus.Views.ViewManagers;
using d60.Cirqus.Views.ViewManagers.Locators;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver.Builders;
using NUnit.Framework;
using TestContext = d60.Cirqus.Testing.TestContext;

namespace d60.Cirqus.Tests.Config
{
    [TestFixture]
    public class TestConfigurationApi : FixtureBase
    {
        [Test, Category(TestCategories.MongoDb)]
        public async Task CanInstallMultipleEventDispatchers() {
            var database = MongoHelper.InitializeTestDatabase();

            var waiter = new ViewManagerWaitHandle();


            //Brett new
            var commandProcessor = base.CreateCommandProcessor(config => config
                 .EventStore(es => { es.UseMongoDb(database, "Events"); es.EnableCaching(10); })
                 .EventDispatcher(ed => {
                     ed.UseViewManagerEventDispatcher(new MongoDbViewManager<ConfigTestView>(database, "view1")).WithWaitHandle(waiter).WithConfiguration(new ViewManagerEventDispatcherConfiguration(200));
                     ed.UseViewManagerEventDispatcher(new MongoDbViewManager<ConfigTestView>(database, "view2")).WithWaitHandle(waiter);
                     ed.UseViewManagerEventDispatcher(new MongoDbViewManager<ConfigTestView>(database, "view3")).WithWaitHandle(waiter);
                     ed.UseViewManagerEventDispatcher(new MongoDbViewManager<ConfigTestView>(database, "view4")).WithWaitHandle(waiter);
                 }));

            //orig
            //var commandProcessor = CommandProcessor.With()
            //    .Logging(l => l.UseConsole(minLevel: Logger.Level.Warn))
            //    .EventStore(e =>
            //    {
            //        e.UseMongoDb(database, "Events");
            //        e.EnableCaching(10);
            //    })
            //    .EventDispatcher(d =>
            //    {
            //        d.UseViewManagerEventDispatcher(new MongoDbViewManager<ConfigTestView>(database, "view1"))
            //            .WithWaitHandle(waiter)
            //            .WithMaxDomainEventsPerBatch(200);

            //        d.UseViewManagerEventDispatcher(new MongoDbViewManager<ConfigTestView>(database, "view2"))
            //            .WithWaitHandle(waiter);

            //        d.UseViewManagerEventDispatcher(new MongoDbViewManager<ConfigTestView>(database, "view3"))
            //            .WithWaitHandle(waiter);

            //        d.UseViewManagerEventDispatcher(new MongoDbViewManager<ConfigTestView>(database, "view4"))
            //            .WithWaitHandle(waiter);
            //    })
            //    .Create();

            RegisterForDisposal(commandProcessor);

            Console.WriteLine("Processing 100 commands...");
            99.Times(() => {
                commandProcessor.ProcessCommand(new ConfigTestCommand("id1"));
                commandProcessor.ProcessCommand(new ConfigTestCommand("id2"));
            });
            commandProcessor.ProcessCommand(new ConfigTestCommand("id1"));
            var lastResult = commandProcessor.ProcessCommand(new ConfigTestCommand("id2"));

            Console.WriteLine("Waiting until views have been updated");
            await waiter.WaitForAll(lastResult, TimeSpan.FromSeconds(5));

            Console.WriteLine("Done - checking collections");
            var expectedViewCollectionNames = new[] { "view1", "view2", "view3", "view4" };

            var viewCollectionNames = database.GetCollectionNames()
                .OrderBy(n => n)
                .Where(c => c.StartsWith("view") && !c.EndsWith("Position"))
                .ToArray();

            Assert.That(viewCollectionNames, Is.EqualTo(expectedViewCollectionNames));

            expectedViewCollectionNames.ToList()
                .ForEach(name => {
                    var doc = database.GetCollection<ConfigTestView>(name)
                        .FindOne(Query.EQ("_id", GlobalInstanceLocator.GetViewInstanceId()));

                    Assert.That(doc.ProcessedEventNumbers, Is.EqualTo(Enumerable.Range(0, 200).ToArray()));
                    Assert.That(doc.CountsByRootId["id1"], Is.EqualTo(100));
                    Assert.That(doc.CountsByRootId["id2"], Is.EqualTo(100));
                });
        }

        public class ConfigTestView : IViewInstance<GlobalInstanceLocator>, ISubscribeTo<ConfigTestEvent>
        {
            public ConfigTestView() {
                CountsByRootId = new Dictionary<string, int>();
                ProcessedEventNumbers = new List<long>();
            }
            public string Id { get; set; }
            public long LastGlobalSequenceNumber { get; set; }
            public List<long> ProcessedEventNumbers { get; set; }
            public Dictionary<string, int> CountsByRootId { get; set; }
            public void Handle(IViewContext context, ConfigTestEvent domainEvent) {
                ProcessedEventNumbers.Add(domainEvent.GetGlobalSequenceNumber());

                var id = domainEvent.GetAggregateRootId();

                if (!CountsByRootId.ContainsKey(id))
                    CountsByRootId[id] = 0;

                CountsByRootId[id]++;

                Thread.Sleep(10);
            }
        }

        public class ConfigTestRoot : AggregateRoot, IEmit<ConfigTestEvent>
        {
            public int EmittedEvents { get; set; }

            public void EmitStuff() {
                Emit(new ConfigTestEvent());
            }

            public void Apply(ConfigTestEvent e) {
                EmittedEvents++;
            }
        }

        public class ConfigTestEvent : DomainEvent<ConfigTestRoot> { }

        public class ConfigTestCommand : d60.Cirqus.Commands.Command<ConfigTestRoot>
        {
            public ConfigTestCommand(string aggregateRootId) : base(aggregateRootId) { }

            public override void Execute(ConfigTestRoot aggregateRoot) {
                aggregateRoot.EmitStuff();
            }
        }

        [Test, Category(TestCategories.MongoDb)]
        public void CanDoTheConfigThing() {
            var database = MongoHelper.InitializeTestDatabase();


            //Brett new
            var processor = CreateCommandProcessor(config => config
                .Logging(l => l.UseConsole())
                .EventStore(es => es.UseMongoDb(database, "Events"))
                .AggregateRootRepository(r => r.EnableInMemorySnapshotCaching(10000))
                .EventDispatcher(ed => ed.UseViewManagerEventDispatcher())
                .Options(o => {
                    o.PurgeExistingViews(true);
                    o.AddDomainExceptionType<ApplicationException>();
                    o.SetMaxRetries(10);
                }));

            //orig
            //var fullConfiguration = CommandProcessor.With()
            //    .Logging(l => l.UseConsole())
            //    .EventStore(e => e.UseMongoDb(database, "Events"))
            //    .AggregateRootRepository(r => r.EnableInMemorySnapshotCaching(10000))
            //    .EventDispatcher(d => d.UseViewManagerEventDispatcher())
            //    .Options(o => {
            //        o.PurgeExistingViews(true);
            //        o.AddDomainExceptionType<ApplicationException>();
            //        o.SetMaxRetries(10);
            //    });
            //var processor = fullConfiguration;

            RegisterForDisposal(processor);

            var someCommand = new SomeCommand();
            processor.ProcessCommand(someCommand);

            Assert.That(someCommand.WasProcessed, Is.EqualTo(true));
        }

        [Test]
        public void CanDecorateAggregateRootRepositoryForTestContext() {
            var decorated = false;

            //Brett
            CreateTestContext(config => config
                .AggregateRootRepository(rcb => rcb
                    .Decorate<IAggregateRootRepository>((repo, services) => {
                        decorated = true;
                        return repo;
                    })
                )
            );

            //orig
            //TestContext.With()
            //    .AggregateRootRepository(x => x.Decorate(c => {
            //        decorated = true;
            //        return c.Get<IAggregateRootRepository>();
            //    }))
            //    .Create();

            Assert.True(decorated);
        }

        public class SomeCommand : ExecutableCommand
        {
            public bool WasProcessed { get; set; }

            public override void Execute(ICommandContext context) {
                WasProcessed = true;
            }
        }
    }
}