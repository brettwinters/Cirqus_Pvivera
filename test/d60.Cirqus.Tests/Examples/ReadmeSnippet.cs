using System;
using System.Collections.Generic;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Commands;
using d60.Cirqus.Config;
using d60.Cirqus.Events;
using d60.Cirqus.Extensions;
using d60.Cirqus.MsSql.Config;
using d60.Cirqus.MsSql.Views;
using d60.Cirqus.Tests.Contracts.EventStore.Factories;
using d60.Cirqus.Tests.Contracts.Views.Factories;
using d60.Cirqus.Tests.Extensions;
using d60.Cirqus.Tests.MsSql;
using d60.Cirqus.Views.ViewManagers;
using d60.Cirqus.Views.ViewManagers.Locators;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Examples
{    
    [TestFixture(typeof(MongoDbViewManagerFactory), Category = TestCategories.MongoDb)]
    //[TestFixture(typeof(PostgreSqlViewManagerFactory), Category = TestCategories.PostgreSql)]
    //[TestFixture(typeof(MsSqlViewManagerFactory), Category = TestCategories.MsSql)]
    //[TestFixture(typeof(EntityFrameworkViewManagerFactory), Category = TestCategories.MsSql)]
    [TestFixture(typeof(InMemoryViewManagerFactory))]
    //[TestFixture(typeof(HybridDbViewManagerFactory), Category = TestCategories.MsSql)]
    //[TestFixture(typeof(NtfsEventStoreFactory))]
    public class ViewProfiling<TFactory> : FixtureBase where TFactory : AbstractViewManagerFactory, new()
    {
        TFactory _factory;

        protected override void DoSetUp()
        {
            _factory = new TFactory();
        }

        [Test]
        public void TheSnippet()
        {

            var viewManager = _factory.GetViewManager<CounterView>();

            var processor = CreateCommandProcessor(config => config
                           .EventStore(e => e.UseInMemoryEventStore())
                           .EventDispatcher(e => e.UseViewManagerEventDispatcher(viewManager)));

            RegisterForDisposal(processor);

            processor.ProcessCommand(new IncrementCounter("id", 1));
            processor.ProcessCommand(new IncrementCounter("id", 2));
            processor.ProcessCommand(new IncrementCounter("id", 3));
            processor.ProcessCommand(new IncrementCounter("id", 5));
            processor.ProcessCommand(new IncrementCounter("id", 8));
        }

        public class IncrementCounter : Command<Counter>
        {
            public IncrementCounter(string aggregateRootId, int delta)
                : base(aggregateRootId)
            {
                Delta = delta;
            }

            public int Delta { get; private set; }

            public override void Execute(Counter aggregateRoot)
            {
                aggregateRoot.Increment(Delta);
            }
        }

        public class CounterIncremented : DomainEvent<Counter>
        {
            public CounterIncremented(int delta)
            {
                Delta = delta;
            }

            public int Delta { get; private set; }
        }

        public class Counter : AggregateRoot, IEmit<CounterIncremented>
        {
            int _currentValue;

            public void Increment(int delta)
            {
                Emit(new CounterIncremented(delta));
            }

            public void Apply(CounterIncremented e)
            {
                _currentValue += e.Delta;
            }

            public int CurrentValue
            {
                get { return _currentValue; }
            }

            public double GetSecretBizValue()
            {
                return CurrentValue % 2 == 0
                    ? Math.PI
                    : CurrentValue;
            }
        }

        public class CounterView : IViewInstance<InstancePerAggregateRootLocator>, ISubscribeTo<CounterIncremented>
        {
            public CounterView()
            {
                SomeRecentBizValues = new List<double>();
            }

            public string Id { get; set; }

            public long LastGlobalSequenceNumber { get; set; }

            public int CurrentValue { get; set; }

            public double SecretBizValue { get; set; }

            public List<double> SomeRecentBizValues { get; set; }

            public void Handle(IViewContext context, CounterIncremented domainEvent)
            {
                CurrentValue += domainEvent.Delta;

                var counter = context.Load<Counter>(domainEvent.GetAggregateRootId(), domainEvent.GetGlobalSequenceNumber());

                SecretBizValue = counter.GetSecretBizValue();

                SomeRecentBizValues.Add(SecretBizValue);

                // trim to 10 most recent biz values
                while (SomeRecentBizValues.Count > 10)
                    SomeRecentBizValues.RemoveAt(0);
            }
        }
    }
}