using d60.Cirqus.Events;
using d60.Cirqus.Tests.Views.ViewManager.AggregateRoots;

namespace d60.Cirqus.Tests.Views.ViewManager.Events
{
    public class WasBitten : DomainEvent<Potato>
    {
        public decimal FractionBittenOff { get; set; }
    }
}