﻿using System;
using System.Linq;
using System.Threading.Tasks;
using d60.Cirqus.Extensions;
using d60.Cirqus.Tests.Extensions;
using d60.Cirqus.Tests.Views.TestAggregateRootView.Model;
using d60.Cirqus.Views.ViewManagers;
using d60.Cirqus.Views.ViewManagers.Locators;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Views.TestAggregateRootView
{
    [TestFixture]
    public class AggregateRootViewWorks : FixtureBase
    {
        ICommandProcessor _commandProcessor;
        InMemoryViewManager<AggregateRootView> _viewManager;
        ViewManagerWaitHandle _waitHandler;

        protected override void DoSetUp()
        {
            _viewManager = new InMemoryViewManager<AggregateRootView>();

            _waitHandler = new ViewManagerWaitHandle();

            var services = new ServiceCollection();
            services.AddCirqus(c =>                c
                .EventStore(e => e.UseInMemoryEventStore())
                .EventDispatcher(e => e.UseViewManagerEventDispatcher(_viewManager).WithWaitHandle(_waitHandler))
            );
            var provider = services.BuildServiceProvider();

            _commandProcessor = provider.GetService<ICommandProcessor>();

            RegisterForDisposal(_commandProcessor);
        }

        [Test]
        public async Task YeahItWorks()
        {
            CommandProcessingResult lastResult = null;

            foreach (var i in Enumerable.Range(0, 40))
            {
                lastResult = _commandProcessor.ProcessCommand(new IncrementNumberCommand("id1"));
            }

            await _waitHandler.WaitForAll(lastResult, TimeSpan.FromSeconds(10));

            var view = _viewManager.Load("id1");

            Assert.That(view.Counter, Is.EqualTo(40));
        }

        class AggregateRootView : IViewInstance<InstancePerAggregateRootLocator>, ISubscribeTo<NumberEvent>
        {
            public string Id { get; set; }
            public long LastGlobalSequenceNumber { get; set; }

            public int Counter { get; set; }

            public void Handle(IViewContext context, NumberEvent domainEvent)
            {
                var aggregateRootId = domainEvent.GetAggregateRootId();
                var instance = context.Load<AggregateRootWithLogic>(aggregateRootId);
                Counter = instance.Counter;
            }
        }
    }
}