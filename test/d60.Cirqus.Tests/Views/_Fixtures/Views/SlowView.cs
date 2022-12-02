using System.Threading;
using d60.Cirqus.Tests.Views.ViewManager.Events;
using d60.Cirqus.Views.ViewManagers;
using d60.Cirqus.Views.ViewManagers.Locators;

namespace d60.Cirqus.Tests.Views.ViewManager.Views
{
    public class SlowView : IViewInstance<InstancePerAggregateRootLocator>,
        ISubscribeTo<PotatoCreated>,
        ISubscribeTo<WasEaten>
    {
        public string Id { get; set; }
        
        public long LastGlobalSequenceNumber { get; set; }
        
        public void Handle(IViewContext context, PotatoCreated domainEvent)
        {
            Thread.Sleep(100);
        }

        public void Handle(IViewContext context, WasEaten domainEvent)
        {
            Thread.Sleep(100);
        }
    }
}